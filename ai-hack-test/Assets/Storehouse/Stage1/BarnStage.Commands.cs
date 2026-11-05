using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Command pipeline:
//   input (chat / speech-to-text)
//     ├─ fast path: short keywords run immediately, no LLM (멈춰 / 살살 / 세게 / 잡아)
//     └─ normal path: Claude → RobotPlan JSON → validation → robot queue
//        (falls back to a keyword parser when no API key is set or the request fails)
public sealed partial class BarnStage
{
    private static readonly HashSet<string> ActionTypes = new()
        { "move_to", "push", "pick_up", "place", "rotate", "hold", "hammer", "say" };
    private static readonly string[] StopWords = { "멈춰", "그만", "스톱", "정지", "stop" };
    private static readonly string[] SoftWords = { "살살", "약하게", "부드럽게" };
    private static readonly string[] HardWords = { "세게", "힘껏", "강하게" };
    private static readonly string[] DirectionWords = { "왼", "오른", "앞", "뒤", "창", "문", "긴", "짧은" };

    private const string SystemPrompt =
        "너는 3D 퍼즐 게임 「할아버지와 로봇」의 아이 로봇이다. 태풍에 무너진 곳간을 할아버지와 함께 다시 짓는다.\n" +
        "할아버지(플레이어)의 말을 듣고 로봇 행동 계획을 JSON으로 답한다.\n" +
        "규칙:\n" +
        "- target과 slot에는 <scene>의 objects/slots에 있는 id만 쓴다. 해당 없으면 빈 문자열.\n" +
        "- 쓸 수 있는 행동은 allowed_actions에 있는 것뿐이다. 순서대로 실행된다. place는 집기를 포함하니 pick_up을 따로 넣지 않아도 된다.\n" +
        "- '이거', '저거', '그거'는 pointed(할아버지가 지팡이로 가리킨 물건)를 뜻한다.\n" +
        "- 왼쪽/오른쪽은 할아버지 화면 기준이다. screen_x가 작을수록 왼쪽, 클수록 오른쪽.\n" +
        "- 놓을 수 있는지, 순서가 맞는지는 게임이 최종 판정한다. 너는 할아버지 뜻에 가장 가까운 행동을 고르면 된다.\n" +
        "- 대체 부품을 제안받으면 그 물건의 tags를 근거로 쓸 만한지 say로 말한다(장선 대용은 long, sturdy, straight가 모두 필요). 쓸 만하면 place도 넣는다.\n" +
        "- 말이 모호하거나 대상이 둘 이상이면 actions를 비우고 say로 되묻는다.\n" +
        "- hidden_item이 있으면 그 속성 안에서만 묘사한다. 속성에 없는 건 지어내지 말고 '잘 모르겠어요'라고 한다. 물건 이름이나 정체는 절대 먼저 말하지 않는다. '꺼내 와'라고 하면 pick_up item_hidden.\n" +
        "- say는 호기심 많은 아이 말투의 존댓말로 1~2문장. 할아버지를 '할아버지'라고 부른다.\n" +
        "- 행동과 무관한 질문이면 actions는 비우고 say로만 답한다.";

    private readonly List<(string speaker, string text)> chatLog = new();
    private int requestSerial;
    private bool thinking;

    public void Submit(string raw)
    {
        string text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
            return;
        Log("할아버지", text);
        if (completed)
        {
            RobotSay("곳간 다 지었어요! F를 누르면 마당으로 돌아가요.");
            return;
        }
        if (TryGuessHidden(text) || TryFastPath(text))
            return;
        if (claude.Available)
            StartCoroutine(AskClaude(text));
        else
            ExecutePlan(FallbackParse(text), "키워드");
    }

    // ----- Fast path -----

    private bool TryFastPath(string text)
    {
        string compact = BarnText.Compact(text).ToLowerInvariant();
        if (compact.Length == 0 || compact.Length > 6 || BarnText.ContainsAny(compact, DirectionWords))
            return false;

        if (BarnText.ContainsAny(compact, StopWords))
        {
            StopRobot(true);
            return true;
        }
        if (BarnText.ContainsAny(compact, SoftWords))
        {
            AdjustForce(-1);
            return true;
        }
        if (BarnText.ContainsAny(compact, HardWords))
        {
            AdjustForce(+1);
            return true;
        }
        if (compact.Contains("잡아") && gustActive)
        {
            BarnItem target = pointed != null && shaking.Contains(pointed) ? pointed : null;
            if (target == null)
            {
                int unheld = 0;
                foreach (BarnItem wall in shaking)
                    if (wall != holding)
                    {
                        unheld++;
                        target = wall;
                    }
                if (unheld != 1)
                    return false;   // two candidates and nothing pointed at: let the LLM resolve "which one"
            }
            StopRobot(false);
            queue.Enqueue(RobotAction.Make("hold", target.Id));
            return true;
        }
        return false;
    }

    // ----- Normal path (LLM) -----

    private System.Collections.IEnumerator AskClaude(string text)
    {
        int serial = ++requestSerial;
        thinking = true;
        RobotPlan plan = null;
        string error = null;
        yield return claude.Request(SystemPrompt, BuildContext(), text, result => plan = result, message => error = message);
        if (serial != requestSerial)
            yield break;   // a newer command superseded this one
        thinking = false;
        if (plan == null)
        {
            Debug.LogWarning("[Barn] Claude 요청 실패, 키워드 파서로 대체: " + error);
            plan = FallbackParse(text);
            ExecutePlan(plan, "키워드(대체)");
            yield break;
        }
        ExecutePlan(plan, "LLM");
    }

    // Validation: unknown action types, ids and disallowed actions are dropped before anything runs.
    private void ExecutePlan(RobotPlan plan, string source)
    {
        var allowed = new HashSet<string>(AllowedActions());
        var accepted = new List<RobotAction>();
        foreach (RobotAction action in plan.actions ?? new RobotAction[0])
        {
            if (action == null || !ActionTypes.Contains(action.type))
                continue;
            if (!allowed.Contains(action.type))
            {
                Debug.Log($"[Barn] 지금은 허용되지 않은 행동 무시: {action}");
                continue;
            }
            bool needsTarget = action.type is "push" or "pick_up" or "move_to";
            bool knownTarget = string.IsNullOrEmpty(action.target) || itemById.ContainsKey(action.target)
                || slotById.ContainsKey(action.target) || action.target == "grandfather";
            if (!knownTarget || (needsTarget && string.IsNullOrEmpty(action.target)))
            {
                Debug.Log($"[Barn] 알 수 없는 대상 무시: {action}");
                continue;
            }
            if (!string.IsNullOrEmpty(action.slot) && !slotById.ContainsKey(action.slot))
                action.slot = "";
            accepted.Add(action);
        }
        Debug.Log($"[Barn] {source} 계획: say=\"{plan.say}\" actions=[{string.Join(", ", accepted)}]");

        if (!string.IsNullOrWhiteSpace(plan.say))
            RobotSay(plan.say);
        else if (accepted.Count == 0)
            RobotSay("음… 무슨 말인지 잘 모르겠어요. 다시 말해 줄래요?");
        if (accepted.Count == 0)
            return;

        // A new instruction replaces whatever was still waiting; the action in progress finishes first,
        // except for the endless ones (holding, hammering) which the new instruction interrupts.
        queue.Clear();
        if (hammering && !accepted.Exists(a => a.type == "hammer"))
            StopRobot(false);
        foreach (RobotAction action in accepted)
            queue.Enqueue(action);
    }

    private IEnumerable<string> AllowedActions()
    {
        yield return "say";
        yield return "move_to";
        if (gustActive)
        {
            yield return "hold";
            yield break;
        }
        yield return "push";
        yield return "pick_up";
        yield return "place";
        yield return "rotate";
        if (SlotFilled("slot_roof") && nailsDone < 3)
            yield return "hammer";
    }

    private string BuildContext()
    {
        var json = new StringBuilder(6000);
        json.Append("{\"step\":{\"index\":").Append(Step).Append(",\"task\":")
            .Append(BarnText.Quote(Step <= 8 ? StepTasks[Step] : "완성")).Append(",\"grandfather_knowledge\":")
            .Append(BarnText.Quote(Step <= 8 ? StepLines[Step].Replace("할아버지   ", "") : "")).Append('}');
        json.Append(",\"event\":").Append(BarnText.Quote(CurrentEventText()));
        json.Append(",\"pointed\":").Append(BarnText.Quote(pointed != null ? pointed.Id : ""));
        json.Append(",\"camera\":").Append(BarnText.Quote(CameraText()));
        json.Append(",\"robot\":{\"carrying\":").Append(BarnText.Quote(robot.Carrying?.Id ?? ""))
            .Append(",\"holding\":").Append(BarnText.Quote(holding?.Id ?? "")).Append('}');
        json.Append(",\"allowed_actions\":[");
        bool first = true;
        foreach (string action in AllowedActions())
        {
            if (!first) json.Append(',');
            json.Append(BarnText.Quote(action));
            first = false;
        }
        json.Append(']');

        json.Append(",\"objects\":[");
        first = true;
        foreach (BarnItem item in items)
        {
            if (!Visible(item) && item.Kind != BarnKind.Nail)
                continue;
            if (item.Kind == BarnKind.Nail && !item.Model.gameObject.activeInHierarchy)
                continue;
            if (!first) json.Append(',');
            first = false;
            json.Append("{\"id\":").Append(BarnText.Quote(item.Id))
                .Append(",\"name\":").Append(BarnText.Quote(item.Kind == BarnKind.Hidden && !hiddenDone ? "장선 사이의 물건(로봇만 보임)" : item.Name))
                .Append(",\"state\":").Append(BarnText.Quote(ItemState(item)))
                .Append(",\"tags\":[");
            bool firstTag = true;
            foreach (string tag in item.Tags)
            {
                if (!firstTag) json.Append(',');
                json.Append(BarnText.Quote(tag));
                firstTag = false;
            }
            json.Append("],\"screen_x\":").Append(ScreenX(item.Model.position).ToString("0.00",
                System.Globalization.CultureInfo.InvariantCulture)).Append('}');
        }
        json.Append(']');

        json.Append(",\"slots\":[");
        first = true;
        foreach (BarnSlot slot in slots)
        {
            if (!first) json.Append(',');
            first = false;
            string blocked = slot.Filled == null ? slot.Prerequisite() : null;
            json.Append("{\"id\":").Append(BarnText.Quote(slot.Id))
                .Append(",\"name\":").Append(BarnText.Quote(slot.Name))
                .Append(",\"step\":").Append(slot.Step)
                .Append(",\"filled_by\":").Append(BarnText.Quote(slot.Filled?.Id ?? ""))
                .Append(",\"blocked_reason\":").Append(BarnText.Quote(blocked ?? ""))
                .Append(",\"screen_x\":").Append(ScreenX(slot.Position).ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture)).Append('}');
        }
        json.Append(']');

        if (SlotFilled("slot_roof") && nailsDone < 3)
            json.Append(",\"hammer\":{\"force\":").Append(force).Append(",\"crack\":").Append(Mathf.RoundToInt(crack))
                .Append(",\"nail_progress\":").Append(Mathf.RoundToInt(nailProgress)).Append(",\"nails_done\":")
                .Append(nailsDone).Append('}');
        if (gustActive)
        {
            json.Append(",\"gust\":{\"seconds_left\":").Append(Mathf.CeilToInt(gustSeconds - gustTime)).Append(",\"shaking\":[");
            for (int i = 0; i < shaking.Count; i++)
                json.Append(i > 0 ? "," : "").Append("{\"id\":").Append(BarnText.Quote(shaking[i].Id))
                    .Append(",\"tilt\":").Append(Mathf.RoundToInt(tilt[shaking[i]])).Append('}');
            json.Append("]}");
        }
        if (hiddenActive)
        {
            // Only the attributes go to the model, never the identity, so it cannot be blurted out.
            json.Append(",\"hidden_item\":{\"material\":").Append(BarnText.Quote(hiddenItem.material))
                .Append(",\"color\":").Append(BarnText.Quote(hiddenItem.color))
                .Append(",\"size\":").Append(BarnText.Quote(hiddenItem.size))
                .Append(",\"shape\":").Append(BarnText.Quote(hiddenItem.shape))
                .Append(",\"feature\":").Append(BarnText.Quote(hiddenItem.feature)).Append('}');
        }
        json.Append('}');
        return json.ToString();
    }

    private string CurrentEventText()
    {
        if (gustActive) return "B 돌풍: 세워 둔 벽 두 장이 흔들림. 로봇은 한 번에 하나만 hold 가능. 할아버지가 나머지를 지팡이로 받침";
        if (hammering) return "A 망치질: 로봇이 자동으로 못을 박는 중. 힘은 할아버지가 살살/세게로 조절";
        if (hiddenActive) return "C 바닥 밑의 물건: 장선 사이에서 로봇이 무언가 발견. 할아버지는 볼 수 없음";
        if (Step == 3) return "D 부서진 부품: joist_3이 부러짐. 대신 쓸 물건을 찾아야 함";
        if (Step == 7) return "A 망치질 대기: hammer로 못 박기 시작";
        return "";
    }

    private string ItemState(BarnItem item)
    {
        if (item.Kind == BarnKind.Nail)
            return item.Has("done") ? "박힘" : "안 박힘";
        if (item.Installed)
            return holding == item ? "세워짐, 로봇이 붙잡는 중" : shaking.Contains(item) ? "세워짐, 흔들림" : $"{item.Slot.Id}에 설치됨";
        string state = robot.Carrying == item ? "로봇이 들고 있음" : "바닥/작업대에 놓여 있음";
        if (item.TurnPeriod > 1 && !item.FacingOk)
            state += item.Has("window") ? ", 창이 안쪽을 향함(돌려야 함)" : ", 문 구멍이 거꾸로(돌려야 함)";
        if (item.Heavy)
            state += ", 무거워서 밀어야 함";
        if (!string.IsNullOrEmpty(item.Note))
            state += ", " + item.Note;
        return state;
    }

    private float ScreenX(Vector3 world)
    {
        if (view == null)
            return 0f;
        Vector3 viewport = view.WorldToViewportPoint(world);
        return Mathf.Clamp(viewport.x * 2f - 1f, -1.5f, 1.5f);
    }

    private string CameraText()
    {
        if (view == null)
            return "";
        Vector3 forward = Flat(view.transform.forward).normalized;
        return $"화면 오른쪽 = 월드 ({view.transform.right.x:0.0},{view.transform.right.z:0.0}), 화면 안쪽 = ({forward.x:0.0},{forward.z:0.0}). 곳간 기준 앞쪽은 -z(바람 드는 쪽), 오른쪽은 +x(마당 쪽)";
    }

    // ----- Demo fallback parser: keyword matching for the basic commands -----

    private RobotPlan FallbackParse(string text)
    {
        var plan = new RobotPlan();
        var actions = new List<RobotAction>();
        string t = BarnText.Compact(text);
        string pointedId = pointed?.Id ?? "";
        bool all = BarnText.ContainsAny(t, "다", "모두", "전부");

        void Say(string line) => plan.say = line;

        if (hiddenActive && BarnText.ContainsAny(t, "꺼내", "가져와", "줘봐"))
        {
            actions.Add(RobotAction.Make("pick_up", "item_hidden"));
            Say("꺼내 올게요!");
        }
        else if (hiddenActive && BarnText.ContainsAny(t, "생겼", "모양", "색", "크기", "뭐야", "뭐니", "재질", "어때", "특징", "소리"))
            Say(DescribeHidden(t));
        else if (gustActive && t.Contains("잡"))
        {
            BarnItem wall = PickByDirection(shaking, t) ?? (pointed != null && shaking.Contains(pointed) ? pointed : null);
            if (wall != null)
                actions.Add(RobotAction.Make("hold", wall.Id));
            else
                Say("어느 벽이요? 왼쪽이요, 오른쪽이요?");
        }
        else if (BarnText.ContainsAny(t, "못", "망치", "박아"))
            actions.Add(RobotAction.Make("hammer"));
        else if (BarnText.ContainsAny(t, "돌려", "회전", "뒤집"))
        {
            string target = WallFromText(t) ?? pointedId;
            string direction = t.Contains("왼") ? "left" : t.Contains("오른") ? "right" : "";
            actions.Add(RobotAction.Make("rotate", target, direction: direction));
            if (BarnText.ContainsAny(t, "세워", "놓아", "붙여"))
                actions.Add(RobotAction.Make("place", target));
        }
        else if (BarnText.ContainsAny(t, "블록", "돌", "기초"))
        {
            if (pointed != null && pointed.Kind == BarnKind.Block && !pointed.Installed && !all)
                actions.Add(RobotAction.Make("push", pointedId));
            else
                foreach (BarnItem block in items)
                    if (block.Kind == BarnKind.Block && !block.Installed)
                        actions.Add(RobotAction.Make("push", block.Id));
            Say(actions.Count > 0 ? "돌을 제자리로 밀게요!" : "돌은 다 제자리에 있어요.");
        }
        else if (BarnText.ContainsAny(t, "바닥틀", "틀"))
            actions.Add(RobotAction.Make("place", "floor_frame", "slot_floor_frame"));
        else if (BarnText.ContainsAny(t, "장대", "빨랫대", "빨래"))
        {
            Say("빨랫대 장대요? 길고 단단하고 곧아요. 이거면 될 것 같아요!");
            actions.Add(RobotAction.Make("place", "prop_pole"));
        }
        else if (BarnText.ContainsAny(t, "빗자루", "빗자리"))
        {
            Say("빗자루는 길긴 한데… 너무 약해 보여요. 한번 해 볼까요?");
            actions.Add(RobotAction.Make("place", "prop_broom"));
        }
        else if (t.Contains("사다리"))
        {
            Say("사다리는 튼튼한데 가로대가 튀어나와 있어요…");
            actions.Add(RobotAction.Make("place", "prop_ladder"));
        }
        else if (t.Contains("장선"))
        {
            // Good joists first, so the snapped one is discovered last instead of halting the batch.
            foreach (BarnItem joist in items)
                if (joist.Kind == BarnKind.Joist && !joist.Installed && !joist.Has("broken"))
                    actions.Add(RobotAction.Make("place", joist.Id));
            foreach (BarnItem joist in items)
                if (joist.Kind == BarnKind.Joist && !joist.Installed && joist.Has("broken"))
                    actions.Add(RobotAction.Make("place", joist.Id));
            Say("장선을 두 뼘 간격으로 깔게요!");
        }
        else if (BarnText.ContainsAny(t, "바닥판", "바닥덮", "덮어"))
            actions.Add(RobotAction.Make("place", "floor_deck", "slot_floor_deck"));
        else if (t.Contains("벽"))
        {
            string wall = WallFromText(t);
            if (wall != null)
                actions.Add(RobotAction.Make("place", wall));
            else
                foreach (string id in new[] { "wall_back", "wall_window", "wall_side", "wall_door" })
                    if (!itemById[id].Installed)
                    {
                        actions.Add(RobotAction.Make("place", id));
                        if (!all)
                            break;
                    }
        }
        else if (t.Contains("지붕"))
            actions.Add(RobotAction.Make("place", "roof", "slot_roof"));
        else if (BarnText.ContainsAny(t, "문짝", "문달", "문을달"))
            actions.Add(RobotAction.Make("place", "door", "slot_door"));
        else if (pointed != null && BarnText.ContainsAny(t, "이거", "저거", "그거", "놓아", "끼워", "가져"))
        {
            actions.Add(pointed.Kind == BarnKind.Block ? RobotAction.Make("push", pointedId) : RobotAction.Make("place", pointedId));
        }
        else if (BarnText.ContainsAny(t, "이리", "와", "따라"))
            actions.Add(RobotAction.Make("move_to", "grandfather"));
        else
            Say("음… 잘 모르겠어요. \"장선 깔아\"처럼 말해 줄래요?");

        plan.actions = actions.ToArray();
        return plan;
    }

    private string WallFromText(string t)
    {
        if (t.Contains("창")) return "wall_window";
        if (BarnText.ContainsAny(t, "문있", "문구멍", "문벽", "문달린")) return "wall_door";
        if (BarnText.ContainsAny(t, "뒷벽", "긴벽", "뒤쪽")) return "wall_back";
        if (BarnText.ContainsAny(t, "옆벽", "짧은벽", "왼쪽벽")) return "wall_side";
        return null;
    }

    private BarnItem PickByDirection(List<BarnItem> candidates, string t)
    {
        bool left = t.Contains("왼");
        bool right = t.Contains("오른");
        if (!left && !right)
            return null;
        BarnItem best = null;
        float bestX = 0f;
        foreach (BarnItem item in candidates)
        {
            float x = ScreenX(item.Model.position);
            if (best == null || (left ? x < bestX : x > bestX))
            {
                best = item;
                bestX = x;
            }
        }
        return best;
    }

    private string DescribeHidden(string t)
    {
        // The attributes are written as designer notes, so the demo parser quotes them as what the robot sees.
        if (t.Contains("색")) return $"색깔이요? 음… {hiddenItem.color}. 그렇게 보여요!";
        if (BarnText.ContainsAny(t, "크기", "커", "작")) return $"크기는요, {hiddenItem.size}.";
        if (BarnText.ContainsAny(t, "재질", "뭘로", "무엇으로")) return $"만져 보니까… {hiddenItem.material}.";
        if (BarnText.ContainsAny(t, "특징", "소리")) return $"아, 그리고요! {hiddenItem.feature}.";
        return $"생긴 건요, {hiddenItem.shape}.";
    }

    // ----- Speech + log -----

    private void RobotSay(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        if (robot != null)
            robot.Say(line);
        Log("로봇", line);
    }

    private void Log(string speaker, string text)
    {
        chatLog.Add((speaker, text));
        if (chatLog.Count > 60)
            chatLog.RemoveAt(0);
    }
}
