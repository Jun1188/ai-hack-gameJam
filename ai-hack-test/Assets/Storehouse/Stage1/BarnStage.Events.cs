using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spoken-puzzle events woven between assembly steps. Numbers are first-pass values to tune in playtests.
public sealed partial class BarnStage
{
    [Header("Event B: gust")]
    [SerializeField, Min(1f)] private float gustSeconds = 8f;
    [SerializeField] private float gustRise = 15f, gustCaneRise = 5f, gustHoldFall = 30f;

    [Header("Event A: hammering")]
    [SerializeField, Min(.2f)] private float strikeInterval = .7f;
    [SerializeField] private float[] nailGain = { 2f, 6f, 12f, 16f, 20f };   // per strike, force 1..5
    [SerializeField] private float crackPerLevel = 10f;                         // per strike for each level above 3

    // B: gust
    private readonly List<BarnItem> shaking = new();
    private readonly Dictionary<BarnItem, float> tilt = new();
    private bool gustActive, gustDone, holdArrived;
    private int fallenWalls;
    private float gustTime;
    private BarnItem holding, pushing;

    // A: hammering
    private bool hammering;
    private int force = 4, nailIndex, nailsDone;
    private float crack, nailProgress;
    private Transform crackMark;
    private float crackShownUntil;

    // C: hidden item
    private bool hiddenActive, hiddenDone, hiddenStarted;

    // D: broken joist
    private bool brokenAnnounced;

    private void OnStepEntered(int step)
    {
        if (step <= 8)
            Log("할아버지", StepLines[step].Replace("할아버지   ", ""));
        switch (step)
        {
            case 3 when !brokenAnnounced:
                brokenAnnounced = true;
                RobotSay("어, 할아버지! 장선 하나가 두 동강 났어요… 이건 못 쓰겠어요. 대신 쓸 만한 게 있을까요?");
                Log("시스템", "부서진 장선 대신 쓸 물건을 로봇에게 제안해 보세요. 주변을 둘러보세요.");
                break;
            case 5:
                Log("시스템", "벽은 말로 돌릴 수 있어요. 예: \"창문 벽 돌려서 앞쪽에 세워\"");
                break;
            case 7:
                Log("시스템", "로봇은 힘 조절을 몰라요. 망치질 중에 \"살살\" / \"세게\" / \"멈춰\"로 조절하세요.");
                break;
        }
    }

    private void OnInstalled(BarnItem item, BarnSlot slot)
    {
        if (item.Kind == BarnKind.Wall)
        {
            wallOrder.Remove(item);
            wallOrder.Add(item);
            int standing = 0;
            foreach (BarnItem wall in wallOrder)
                if (wall.Installed)
                    standing++;
            if (standing == 3 && !gustDone && !gustActive)
                StartGust(item);
        }
        if (slot.Step == 3 && AllFilled(3) && !hiddenStarted)
            StartHidden();
        if (item.Kind == BarnKind.Roof)
            foreach (BarnItem nail in Nails())
            {
                nail.Model.gameObject.SetActive(true);
                nail.Hidden = false;
            }
    }

    private void UpdateEvents(float deltaTime)
    {
        if (gustActive)
            UpdateGust(deltaTime);
        if (crackMark != null && crackMark.gameObject.activeSelf && Time.time > crackShownUntil)
            crackMark.gameObject.SetActive(false);
    }

    // ----- B. Gust: two standing walls wobble while the third goes up -----

    private void StartGust(BarnItem third)
    {
        shaking.Clear();
        tilt.Clear();
        foreach (BarnItem wall in wallOrder)
            if (wall.Installed && wall != third && shaking.Count < 2)
            {
                shaking.Add(wall);
                tilt[wall] = 0f;
            }
        if (shaking.Count == 0)
            return;
        gustActive = true;
        gustTime = 0f;
        fallenWalls = 0;
        holding = null;
        holdArrived = false;
        queue.Clear();
        RobotSay("으악, 돌풍이에요! 벽이 흔들려요! 어느 벽을 잡을까요?");
        Log("시스템", $"돌풍! {gustSeconds:0}초 동안 버티세요. 벽을 가리키고 \"잡아\", 또는 \"왼쪽 벽 잡아\".");
    }

    private void UpdateGust(float deltaTime)
    {
        gustTime += deltaTime;
        foreach (BarnItem wall in shaking.ToArray())
        {
            bool held = holding == wall && holdArrived;
            bool caned = holding != null && holding != wall && holdArrived;   // grandfather props the other with his cane
            float rate = held ? -gustHoldFall : caned ? gustCaneRise : gustRise;
            tilt[wall] = Mathf.Clamp(tilt[wall] + rate * deltaTime, 0f, 100f);
            float wobble = Mathf.Sin(Time.time * (held ? 4f : 9f) + wall.Id.Length) * (held ? .6f : 1.5f + tilt[wall] * .04f);
            PoseTilted(wall, tilt[wall] * .11f + wobble);
            if (tilt[wall] >= 100f)
                StartCoroutine(FallOver(wall));
        }
        if (gustTime >= gustSeconds || shaking.Count == 0)
            EndGust();
    }

    // Tips a standing wall outward around its bottom edge.
    private void PoseTilted(BarnItem wall, float degrees)
    {
        BarnSlot slot = wall.Slot;
        if (slot == null)
            return;
        Vector3 outward = Flat(slot.Position - assemblyRoot.position).normalized;
        Vector3 axis = Vector3.Cross(Vector3.up, outward);
        Vector3 pivot = slot.Position + Vector3.down * 1.0f;
        Quaternion lean = Quaternion.AngleAxis(degrees, axis);
        wall.Model.SetPositionAndRotation(pivot + lean * (slot.Position - pivot), lean * slot.Rotation);
    }

    private IEnumerator FallOver(BarnItem wall)
    {
        shaking.Remove(wall);
        fallenWalls++;
        if (holding == wall)
        {
            holding = null;
            robot.SetBracing(false);
        }
        RobotSay($"앗! {BarnText.Josa(wall.Name, "이", "가")} 넘어가요!");
        for (float angle = 12f; angle < 88f; angle += Time.deltaTime * 160f)
        {
            PoseTilted(wall, angle);
            yield return null;
        }
        BarnSlot slot = wall.Slot;
        Vector3 outward = Flat(slot.Position - assemblyRoot.position).normalized;
        Uninstall(wall);
        wall.Turns = 0;
        wall.LoosePosition = Flat(slot.Position) + outward * 1.15f + Vector3.up * .07f;
        PoseTiltedLoose(wall, slot, outward);
    }

    // A fallen wall lies flat just outside its slot, already facing the right way for a quick re-raise.
    private void PoseTiltedLoose(BarnItem wall, BarnSlot slot, Vector3 outward)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, outward);
        wall.LooseRotation = Quaternion.AngleAxis(90f, axis) * slot.Rotation;
        ApplyLoosePose(wall);
    }

    private void EndGust()
    {
        gustActive = false;
        gustDone = true;
        foreach (BarnItem wall in shaking)
            if (wall.Slot != null)
                PlaceModel(wall, wall.Slot);
        shaking.Clear();
        holding = null;
        robot.SetBracing(false);
        if (fallenWalls == 0)
        {
            RobotSay("휴우… 버텼어요! 할아버지 지팡이 덕분이에요!");
            Log("할아버지", "허허, 잘 붙잡았다. 바람이 잦아들었구나.");
        }
        else
        {
            RobotSay("넘어진 벽은 다시 세우면 돼요! 괜찮아요!");
            Log("할아버지", "괜찮다, 다시 세우면 되지.");
        }
    }

    private IEnumerator DoHold(RobotAction action, Outcome outcome)
    {
        if (!gustActive || shaking.Count == 0)
        {
            Fail(outcome, "지금은 붙잡을 게 없어요.");
            yield break;
        }
        BarnItem target = Find(action.target);
        if (target == null || !shaking.Contains(target))
            target = pointed != null && shaking.Contains(pointed) ? pointed : MostTilted();
        if (target == null)
        {
            Fail(outcome, "어느 벽을 잡을지 모르겠어요!");
            yield break;
        }
        robot.SetBracing(false);
        holding = target;
        holdArrived = false;
        RobotSay($"{target.Name} 잡으러 가요!");
        yield return Walk(Approach(target.WorldBounds()));
        if (holding != target || !gustActive)
            yield break;
        yield return robot.FaceTowards(target.Slot != null ? target.Slot.Position : target.Model.position);
        holdArrived = true;
        robot.SetBracing(true);
        RobotSay("꽉 잡았어요! 할아버지는 그쪽을 받쳐 주세요!");
    }

    private BarnItem MostTilted()
    {
        BarnItem best = null;
        foreach (BarnItem wall in shaking)
            if (wall != holding && (best == null || tilt[wall] > tilt[best]))
                best = wall;
        return best;
    }

    // ----- A. Hammering: the robot strikes on its own; the player tunes force by voice -----

    private IEnumerator DoHammer(RobotAction action, Outcome outcome)
    {
        if (!SlotFilled("slot_roof"))
        {
            Fail(outcome, "지붕이 올라가야 못을 박을 수 있어요.");
            yield break;
        }
        if (nailsDone >= 3)
        {
            Fail(outcome, "못은 세 개 다 박았어요!");
            yield break;
        }
        if (gustActive)
        {
            Fail(outcome, "바람 때문에 지금은 못 박아요!");
            yield break;
        }
        if (robot.Carrying != null)
            DropCarried();
        BarnItem chosen = Find(action.target);
        List<BarnItem> nails = new(Nails());
        nailIndex = chosen != null && chosen.Kind == BarnKind.Nail && !chosen.Has("done") ? nails.IndexOf(chosen) : NextNail(nails);

        yield return Walk(new Vector3(assemblyRoot.position.x, 0f, assemblyRoot.position.z - 2.2f));
        yield return robot.Hop(RoofStand(nails[nailIndex]), 2.4f, .7f);
        robot.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        robot.ShowHammer(true);
        hammering = true;
        RobotSay(force >= 4 ? "망치질 시작! 쾅쾅 세게 박을게요!" : "망치질 시작할게요!");
        Log("할아버지", "내가 판재를 붙잡고 있으마.");

        bool weakNoted = false;
        while (hammering && nailsDone < 3)
        {
            yield return new WaitForSeconds(strikeInterval);
            if (!hammering)
                break;
            robot.Strike();
            nailProgress = Mathf.Min(100f, nailProgress + nailGain[force - 1]);
            if (force >= 4)
                crack = Mathf.Min(100f, crack + (force - 3) * crackPerLevel);
            SinkNail(nails[nailIndex], nailProgress / 100f);

            if (crack >= 100f)
            {
                ShowCrack(nails[nailIndex]);
                RobotSay("쩌억! 판재가 갈라졌어요… 너무 세게 했나 봐요. 이 못부터 다시 해야 해요.");
                Log("시스템", "판재가 갈라졌어요. 새 판재로 갈았습니다. \"살살\"로 힘을 낮추고 다시 \"못 박아\".");
                crack = 0f;
                nailProgress = 0f;
                SinkNail(nails[nailIndex], 0f);
                break;
            }
            if (force == 1 && !weakNoted)
            {
                weakNoted = true;
                RobotSay("톡… 톡… 너무 약해서 잘 안 들어가요.");
            }
            if (nailProgress >= 100f)
            {
                nails[nailIndex].Tags.Add("done");
                nailsDone++;
                nailProgress = 0f;
                if (nailsDone >= 3)
                {
                    RobotSay("못 세 개 다 박았어요! 지붕이 꽉 붙었어요!");
                    break;
                }
                RobotSay($"{nailsDone}개 박았어요! 다음 못!");
                nailIndex = NextNail(nails);
                yield return robot.Hop(RoofStand(nails[nailIndex]), .4f, .45f);
            }
        }
        EndHammer();
        yield return robot.Hop(new Vector3(robot.transform.position.x, 0f, assemblyRoot.position.z - 2.2f), 1.2f, .6f);
        RefreshStep();
    }

    private static int NextNail(List<BarnItem> nails)
    {
        for (int i = 0; i < nails.Count; i++)
            if (!nails[i].Has("done"))
                return i;
        return 0;
    }

    private static Vector3 RoofStand(BarnItem nail) => nail.Model.position + new Vector3(0f, -.08f, -.42f);

    private static void SinkNail(BarnItem nail, float progress)
    {
        foreach (Transform part in nail.Model)
        {
            float rest = part.name == "Nail head" ? .2f : .1f;
            part.localPosition = new Vector3(0f, rest - .18f * progress, 0f);
        }
    }

    private void ShowCrack(BarnItem nail)
    {
        if (crackMark == null)
        {
            crackMark = StorehouseParts.Box(nail.Model.parent, "Split board", Vector3.zero, new Vector3(1.1f, .02f, .03f), stone);
            crackMark.GetComponent<Renderer>().sharedMaterial = metal;
        }
        crackMark.SetParent(nail.Model.parent, false);
        crackMark.localPosition = nail.Model.localPosition + new Vector3(.35f, .05f, -.05f);
        crackMark.localRotation = nail.Model.localRotation * Quaternion.Euler(0f, 18f, 0f);
        crackMark.gameObject.SetActive(true);
        crackShownUntil = Time.time + 2.5f;
    }

    private void EndHammer()
    {
        hammering = false;
        robot.ShowHammer(false);
    }

    private void AdjustForce(int delta)
    {
        int before = force;
        force = Mathf.Clamp(force + delta, 1, 5);
        string[] feel = { "", "아주 살살", "살살", "적당히", "세게", "온 힘으로" };
        if (force == before)
            RobotSay(delta < 0 ? "이게 제일 약한 거예요!" : "이게 제일 센 거예요!");
        else if (hammering)
            RobotSay($"{feel[force]} 칠게요! (힘 {force})");
        else
            RobotSay($"알겠어요, 다음엔 {feel[force]} 할게요. (힘 {force})");
    }

    // ----- C. The thing under the floor: only the robot can see it -----

    private void StartHidden()
    {
        hiddenStarted = true;
        hiddenActive = true;
        queue.Clear();
        RobotSay("어? 할아버지! 장선 사이에 뭔가 있어요! 허리 굽히기 힘드시죠? 제가 봐 드릴게요.");
        Log("시스템", "바닥 밑 물건: \"어떻게 생겼어?\" \"무슨 색이야?\"처럼 물어보고 정체를 맞혀 보세요. \"꺼내 와\"라고 해도 됩니다.");
        queue.Enqueue(RobotAction.Make("move_to", "item_hidden"));
    }

    private bool TryGuessHidden(string text)
    {
        if (!hiddenActive || hiddenDone)
            return false;
        foreach (string answer in hiddenItem.answers)
            if (!string.IsNullOrWhiteSpace(answer) && text.Contains(answer))
            {
                RobotSay("맞아요! 딱 그거예요! 제가 꺼내 올게요!");
                StopRobot(false);
                queue.Enqueue(RobotAction.Make("pick_up", "item_hidden"));
                return true;
            }
        return false;
    }

    private IEnumerator DeliverHiddenItem()
    {
        BarnItem item = itemById["item_hidden"];
        item.Name = hiddenItem.identity;
        if (grandfather != null)
        {
            yield return Walk(Approach(new Bounds(grandfather.position + Vector3.up * .8f, new Vector3(.6f, 1.6f, .6f))));
            yield return robot.FaceTowards(grandfather.position);
        }
        RobotSay($"할아버지, 이거예요! {hiddenItem.identity}!");
        yield return new WaitForSeconds(1.2f);
        robot.Release(looseRoot);
        item.LoosePosition = Flat(robot.transform.position + robot.transform.forward * .45f) + Vector3.up * .06f;
        item.LooseRotation = Quaternion.Euler(0f, 30f, 0f);
        ApplyLoosePose(item);
        hiddenActive = false;
        hiddenDone = true;
        Log("할아버지", hiddenItem.story);
        RefreshStep();
    }
}
