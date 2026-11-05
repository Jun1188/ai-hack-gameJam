using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

// IMGUI overlay in the same visual language as the rest of the storehouse stage (StorehouseHud).
public sealed partial class BarnStage
{
    private const float HeaderHeight = 92f;
    private const float FooterHeight = 172f;
    private const string ChatControl = "BarnChat";

    private string chatText = "";
    private bool chatFocused;
    private Rect eventPanel;
    private GUIStyle inputStyle, wrapStyle;

    private bool PointerOverHud(Vector2 guiPoint)
        => guiPoint.y < HeaderHeight || guiPoint.y > Screen.height - FooterHeight || eventPanel.Contains(guiPoint);

    private void OnGUI()
    {
        if (items.Count == 0 || view == null || StorehouseTransition.Busy)
            return;
        HandleChatKeys();
        float width = Screen.width;
        float height = Screen.height;
        DrawWorldMarkers();
        DrawHeader(width);
        DrawEventPanel(width);
        DrawFooter(width, height);
    }

    private void HandleChatKeys()
    {
        Event input = Event.current;
        if (input.type != EventType.KeyDown)
            return;
        bool enter = input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter;
        if (enter && chatFocused)
        {
            string text = chatText;
            chatText = "";
            GUI.FocusControl(null);
            chatFocused = false;
            input.Use();
            Submit(text);
        }
        else if (enter)
        {
            GUI.FocusControl(ChatControl);
            input.Use();
        }
        else if (input.keyCode == KeyCode.Escape && chatFocused)
        {
            GUI.FocusControl(null);
            chatFocused = false;
            input.Use();
        }
    }

    private void DrawHeader(float width)
    {
        StorehouseHud.Fill(new Rect(0f, 0f, width, HeaderHeight), StorehouseHud.Ink);
        StorehouseHud.Fill(new Rect(0f, HeaderHeight - 1f, width, 1f), StorehouseHud.Gold);
        string phase = completed ? "완성" : $"{Step} / 8";
        GUI.Label(new Rect(22f, 6f, width - 44f, 22f), $"STAGE 01  /  곳간 (튜토리얼)      단계 {phase}",
            StorehouseHud.Type(14, StorehouseHud.Gold));
        GUI.Label(new Rect(22f, 27f, width - 44f, 34f), completed ? "새 곳간이 완성됐습니다" : StepTasks[Step],
            StorehouseHud.Type(23, StorehouseHud.Paper));
        if (!completed)
            GUI.Label(new Rect(22f, 62f, width - 44f, 24f), StepLines[Step], StorehouseHud.Type(14, StorehouseHud.Mint));

        int filled = 0;
        foreach (BarnSlot slot in slots)
            if (slot.Filled != null)
                filled++;
        float bar = Mathf.Min(170f, width * .22f);
        GUI.Label(new Rect(width - bar - 22f, 6f, bar, 22f), $"부품 {filled:00} / {slots.Count}   못 {nailsDone}/3",
            StorehouseHud.Type(13, StorehouseHud.Mint, TextAnchor.MiddleRight));
        StorehouseHud.Fill(new Rect(width - bar - 22f, 31f, bar, 3f), new Color(.21f, .29f, .25f));
        StorehouseHud.Fill(new Rect(width - bar - 22f, 31f, bar * (filled + nailsDone) / (slots.Count + 3f), 3f),
            StorehouseHud.Mint);
        GUI.Label(new Rect(width - bar - 22f, 38f, bar, 20f), recallVisible ? "H  회상 끄기" : "H  회상 보기",
            StorehouseHud.Type(12, StorehouseHud.Paper, TextAnchor.MiddleRight));
    }

    private void DrawEventPanel(float width)
    {
        eventPanel = Rect.zero;
        bool hammerPanel = SlotFilled("slot_roof") && nailsDone < 3;
        if (!gustActive && !hammerPanel && !hiddenActive)
            return;
        float panelHeight = gustActive ? 54f + shaking.Count * 40f : hammerPanel ? 150f : 92f;
        eventPanel = new Rect(width - 292f, HeaderHeight + 12f, 270f, panelHeight);
        StorehouseHud.Fill(eventPanel, StorehouseHud.Ink);
        StorehouseHud.Fill(new Rect(eventPanel.x, eventPanel.y, eventPanel.width, 2f), StorehouseHud.Gold);
        float x = eventPanel.x + 14f;
        float y = eventPanel.y + 8f;
        float w = eventPanel.width - 28f;

        if (gustActive)
        {
            GUI.Label(new Rect(x, y, w, 22f), $"B  돌풍!   {Mathf.Max(0f, gustSeconds - gustTime):0.0}초 버티기",
                StorehouseHud.Type(15, StorehouseHud.Gold));
            y += 30f;
            foreach (BarnItem wall in shaking)
            {
                string who = holding == wall ? (holdArrived ? "  로봇이 잡음" : "  로봇 가는 중") : holding != null && holdArrived ? "  지팡이로 받침" : "";
                GUI.Label(new Rect(x, y, w, 18f), wall.Name + who, StorehouseHud.Type(12, StorehouseHud.Paper));
                Gauge(new Rect(x, y + 20f, w, 8f), tilt[wall] / 100f, tilt[wall] > 70f ? new Color(.92f, .38f, .3f) : StorehouseHud.Gold);
                y += 40f;
            }
            return;
        }
        if (hammerPanel)
        {
            GUI.Label(new Rect(x, y, w, 22f), hammering ? "A  망치질 중" : "A  망치질 대기  (\"못 박아\")",
                StorehouseHud.Type(15, StorehouseHud.Gold));
            y += 28f;
            GUI.Label(new Rect(x, y, w, 18f), $"힘  {new string('■', force)}{new string('□', 5 - force)}   ({force}/5)",
                StorehouseHud.Type(13, force >= 4 ? new Color(.95f, .55f, .4f) : StorehouseHud.Mint));
            y += 24f;
            GUI.Label(new Rect(x, y, w, 16f), "판재 갈라짐", StorehouseHud.Type(12, StorehouseHud.Paper));
            Gauge(new Rect(x, y + 18f, w, 8f), crack / 100f, new Color(.92f, .38f, .3f));
            y += 34f;
            GUI.Label(new Rect(x, y, w, 16f), $"못 {Mathf.Min(nailsDone + 1, 3)} 박힘", StorehouseHud.Type(12, StorehouseHud.Paper));
            Gauge(new Rect(x, y + 18f, w, 8f), nailProgress / 100f, StorehouseHud.Mint);
            return;
        }
        GUI.Label(new Rect(x, y, w, 22f), "C  바닥 밑의 물건", StorehouseHud.Type(15, StorehouseHud.Gold));
        GUI.Label(new Rect(x, y + 26f, w, 52f), "로봇에게 생김새를 물어보고 정체를 맞혀 보세요.",
            Wrap(13, StorehouseHud.Paper));
    }

    private static void Gauge(Rect rect, float value, Color color)
    {
        StorehouseHud.Fill(rect, new Color(.21f, .29f, .25f));
        StorehouseHud.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
    }

    private void DrawFooter(float width, float height)
    {
        float y = height - FooterHeight;
        StorehouseHud.Fill(new Rect(0f, y, width, FooterHeight), StorehouseHud.Ink);
        StorehouseHud.Fill(new Rect(0f, y, width, 1f), StorehouseHud.Gold);

        int lines = Mathf.Min(4, chatLog.Count);
        for (int i = 0; i < lines; i++)
        {
            var (speaker, text) = chatLog[chatLog.Count - lines + i];
            Color color = speaker == "로봇" ? StorehouseHud.Mint : speaker == "시스템" ? new Color(.75f, .75f, .7f) : StorehouseHud.Gold;
            GUI.Label(new Rect(22f, y + 6f + i * 22f, width - 44f, 22f), $"{speaker}   {text}", LogStyle(color));
        }

        float inputY = y + 100f;
        float buttons = 250f;
        if (completed)
        {
            if (StorehouseHud.Button(new Rect(22f, inputY, 200f, 34f), "F  마당으로 돌아가기", true))
                StorehouseTransition.ReturnToCottage();
        }
        else
        {
            inputStyle ??= MakeInputStyle();
            GUI.SetNextControlName(ChatControl);
            chatText = GUI.TextField(new Rect(22f, inputY, width - 44f - buttons, 34f), chatText ?? "", 120, inputStyle);
            chatFocused = GUI.GetNameOfFocusedControl() == ChatControl;
            if (!chatFocused && string.IsNullOrEmpty(chatText))
                GUI.Label(new Rect(32f, inputY, width - 64f - buttons, 34f),
                    VoiceListening ? $"듣는 중…  {voiceHypothesis}" : "Enter를 눌러 로봇에게 말하기   예) \"돌 제자리로 밀어\"",
                    StorehouseHud.Type(14, new Color(.6f, .62f, .58f)));
            float bx = width - 22f - buttons + 8f;
            if (StorehouseHud.Button(new Rect(bx, inputY, 92f, 34f), "보내기") && !string.IsNullOrWhiteSpace(chatText))
            {
                string text = chatText;
                chatText = "";
                Submit(text);
            }
            if (StorehouseHud.Button(new Rect(bx + 100f, inputY, buttons - 108f, 34f), VoiceListening ? "■  음성 끄기 (V)" : "●  음성 (V)",
                VoiceListening))
                ToggleVoice();
        }
        string mode = claude.Available ? $"LLM {claude.model}" : "키워드 파서 (API 키 없음)";
        GUI.Label(new Rect(22f, y + 140f, width - 44f, 24f),
            $"WASD 걷기    왼쪽 클릭 가리키기(지팡이)    오른쪽 드래그 회전    휠 확대    H 회상    Esc 나가기        {mode}",
            StorehouseHud.Type(12, StorehouseHud.Paper));
    }

    private GUIStyle MakeInputStyle()
    {
        var style = new GUIStyle(GUI.skin.textField)
        {
            font = StorehouseHud.Type(16, StorehouseHud.Paper).font,
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(10, 10, 4, 4)
        };
        style.normal.textColor = style.focused.textColor = style.hover.textColor = StorehouseHud.Paper;
        return style;
    }

    private static GUIStyle LogStyle(Color color)
    {
        GUIStyle style = StorehouseHud.Type(14, color);
        style.wordWrap = false;
        return style;
    }

    private GUIStyle Wrap(int size, Color color)
    {
        wrapStyle ??= new GUIStyle(StorehouseHud.Type(size, color)) { wordWrap = true, alignment = TextAnchor.UpperLeft };
        wrapStyle.fontSize = size;
        wrapStyle.normal.textColor = color;
        return wrapStyle;
    }

    private void DrawWorldMarkers()
    {
        if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
            return;

        if (pointed != null && pointed.Model.gameObject.activeInHierarchy)
        {
            Bounds bounds = pointed.WorldBounds();
            Vector3 top = StorehouseHud.Project(view, bounds.center + Vector3.up * (bounds.extents.y + .15f));
            if (top.z > 0f)
                Tag(new Vector2(top.x, top.y), $"▼ 가리킴: {pointed.Name}", StorehouseHud.Gold);
        }
        if (hiddenActive)
        {
            Vector3 spot = StorehouseHud.Project(view, itemById["item_hidden"].Model.position + Vector3.up * .4f);
            if (spot.z > 0f)
                Tag(new Vector2(spot.x, spot.y), "?", StorehouseHud.Gold, 22);
        }
        if (gustActive)
            foreach (BarnItem wall in shaking)
            {
                Vector3 mark = StorehouseHud.Project(view, wall.Model.position + Vector3.up * 1.3f);
                if (mark.z > 0f)
                    Tag(new Vector2(mark.x, mark.y), $"흔들림 {tilt[wall]:0}", tilt[wall] > 70f ? new Color(.95f, .45f, .35f) : StorehouseHud.Paper);
            }

        Vector3 head = StorehouseHud.Project(view, robot.HeadTop);
        if (head.z <= 0f)
            return;
        string bubble = robot.Speaking ? robot.Speech : thinking ? "음… (생각 중)" : null;
        if (bubble == null)
            return;
        GUIStyle style = Wrap(15, StorehouseHud.Ink);
        float bubbleWidth = Mathf.Min(340f, style.CalcSize(new GUIContent(bubble)).x + 24f);
        float bubbleHeight = style.CalcHeight(new GUIContent(bubble), bubbleWidth - 20f) + 14f;
        var rect = new Rect(Mathf.Clamp(head.x - bubbleWidth * .5f, 8f, Screen.width - bubbleWidth - 8f),
            Mathf.Clamp(head.y - bubbleHeight - 14f, HeaderHeight + 6f, Screen.height - FooterHeight - bubbleHeight - 6f),
            bubbleWidth, bubbleHeight);
        StorehouseHud.Fill(rect, StorehouseHud.Paper);
        StorehouseHud.Fill(new Rect(rect.x, rect.yMax, rect.width, 2f), StorehouseHud.Mint);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, rect.height - 14f), bubble, style);
    }

    private static void Tag(Vector2 at, string text, Color color, int size = 13)
    {
        GUIStyle style = StorehouseHud.Type(size, color, TextAnchor.MiddleCenter);
        float width = style.CalcSize(new GUIContent(text)).x + 16f;
        var rect = new Rect(at.x - width * .5f, at.y - 26f, width, 24f);
        StorehouseHud.Fill(rect, StorehouseHud.Ink);
        GUI.Label(rect, text, style);
    }

    // ----- Voice input (Windows dictation → same Submit() as chat) -----

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private DictationRecognizer dictation;
    private string voiceHypothesis = "";
    private bool VoiceListening => dictation != null && dictation.Status == SpeechSystemStatus.Running;

    private void ToggleVoice()
    {
        try
        {
            if (dictation == null)
            {
                dictation = new DictationRecognizer();
                dictation.DictationHypothesis += text => voiceHypothesis = text;
                dictation.DictationResult += (text, confidence) =>
                {
                    voiceHypothesis = "";
                    Submit(text);
                };
                dictation.DictationComplete += cause =>
                {
                    if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.TimeoutExceeded)
                        Log("시스템", "음성 인식이 멈췄어요: " + cause);
                };
                dictation.DictationError += (error, result) => Log("시스템", "음성 인식 오류: " + error);
            }
            if (VoiceListening)
                dictation.Stop();
            else
                dictation.Start();
        }
        catch (System.Exception error)
        {
            Log("시스템", "음성 인식을 쓸 수 없어요 (Windows 설정 > 개인 정보 > 음성 인식 확인): " + error.Message);
            dictation = null;
        }
    }

    private void OnDestroy()
    {
        if (dictation == null)
            return;
        if (dictation.Status == SpeechSystemStatus.Running)
            dictation.Stop();
        dictation.Dispose();
    }
#else
    private string voiceHypothesis = "";
    private bool VoiceListening => false;
    private void ToggleVoice() => Log("시스템", "이 플랫폼에서는 음성 입력이 아직 지원되지 않아요. 채팅을 써 주세요.");
#endif

    private void UpdateVoiceKey(Keyboard keys)
    {
        if (keys.vKey.wasPressedThisFrame)
            ToggleVoice();
    }
}
