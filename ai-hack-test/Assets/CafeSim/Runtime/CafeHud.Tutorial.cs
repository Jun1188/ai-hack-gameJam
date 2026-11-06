using MapleCafe;
using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        internal static readonly Color32 TutorialHighlightColor = new(255, 200, 105, 255);
        [SerializeField] CafeBrewStation brewStation;
        [SerializeField] CafeMenuBook tutorialMenu;
        enum TutorialStage { WaitingForCustomer, OpenMenu, ChooseCard, FindBlender, MakeDrink, Done }
        TutorialStage tutorialStage;
        CafeVisit tutorialCustomer;
        Bounds tutorialBounds;
        bool TutorialOpen => tutorialStage == TutorialStage.MakeDrink;
        bool TutorialTabRequired => tutorialStage == TutorialStage.OpenMenu;

        void UpdateTutorial()
        {
            if (tutorialMenu == null || !tutorialMenu.isActiveAndEnabled || !tutorialMenu.HasCards ||
                brewStation == null || businessDay == null || CafeBusinessDay.Day != 1 ||
                tutorialStage == TutorialStage.Done || Halted || promptOpen || session.ChatBusy) return;

            // Keep completed lessons when the current customer leaves without a drink.
            if (tutorialCustomer == null || tutorialCustomer.State == VisitState.TimedOut)
                tutorialCustomer = ArrivedTutorialCustomer();

            switch (tutorialStage)
            {
                case TutorialStage.WaitingForCustomer:
                    if (tutorialCustomer == null) break;
                    if (tutorialMenu.HasSelectedCard)
                        tutorialStage = TutorialStage.FindBlender;
                    else if (tutorialMenu.IsListOpen)
                        tutorialStage = TutorialStage.ChooseCard;
                    else
                        BeginTutorialTabPrompt();
                    break;
                case TutorialStage.ChooseCard:
                    if (tutorialCustomer?.State == VisitState.Received)
                        tutorialStage = TutorialStage.Done;
                    else if (tutorialMenu.HasSelectedCard)
                        tutorialStage = TutorialStage.FindBlender;
                    break;
                case TutorialStage.FindBlender:
                    if (tutorialCustomer?.State == VisitState.Received)
                        tutorialStage = TutorialStage.Done;
                    else
                        WatchForBlender();
                    break;
            }
        }

        CafeVisit ArrivedTutorialCustomer()
        {
            CafeVisit customer = session.Selected;
            Transform body = session.BodyOf(customer);
            return customer?.State == VisitState.Active && !string.IsNullOrWhiteSpace(customer.Order?.speech) &&
                body != null && body.gameObject.activeInHierarchy &&
                (body.position - session.servicePoint.position).sqrMagnitude <= .0001f
                ? customer : null;
        }

        void BeginTutorialTabPrompt()
        {
            tutorialStage = TutorialStage.OpenMenu;
            tutorialMenu.TutorialTabRequired = true;
            businessDay.SetPaused(true);
            ApplyOverlayControls();
        }

        void AdvanceTutorialTabPrompt()
        {
            tutorialStage = TutorialStage.ChooseCard;
            EndTutorialTabPrompt();
            tutorialMenu.ToggleList();
        }

        void EndTutorialTabPrompt()
        {
            if (tutorialMenu != null) tutorialMenu.TutorialTabRequired = false;
            if (businessDay != null) businessDay.SetPaused(false);
        }

        void WatchForBlender()
        {
            if (!tutorialMenu.HasPinnedCard || barista == null || !barista.CanInteract || eyes == null) return;
            Vector3 towardCounter = session.servicePoint.position - eyes.transform.position;
            Vector3 look = eyes.transform.forward;
            towardCounter.y = look.y = 0f;
            if (Vector3.Dot(look.normalized, towardCounter.normalized) > .05f ||
                !brewStation.TryGetVisibleBlender(out tutorialBounds)) return;

            tutorialStage = TutorialStage.MakeDrink;
            businessDay.SetPaused(true);
            ApplyOverlayControls();
        }

        void CloseTutorial(bool resumeInput)
        {
            if (!TutorialOpen) return;
            tutorialStage = TutorialStage.Done;
            if (businessDay != null) businessDay.SetPaused(false);
            if (!resumeInput) return;
            ApplyOverlayControls();
            if (Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        void DismissTutorialForConversation()
        {
            if(TutorialTabRequired)
            {
                tutorialStage=TutorialStage.ChooseCard;
                EndTutorialTabPrompt();
            }
            else if(TutorialOpen)CloseTutorial(true);
            ApplyOverlayControls();
        }

        void DrawTutorialHint()
        {
            if (Halted || promptOpen || session.ChatBusy || tutorialMenu == null) return;
            string message = tutorialStage switch
            {
                TutorialStage.ChooseCard => "주문에 맞는 메뉴 카드를 클릭하면 왼쪽 위에 확대돼요.",
                TutorialStage.FindBlender => "확대된 카드를 참고해 뒤돌아서 블렌더를 보세요.",
                _ => null
            };
            if (message == null) return;

            const float width = 520f;
            float height = artText.CalcHeight(new GUIContent(message), width - 40f);
            var panel = new Rect((1280f - width) * .5f, 624f - height, width, height + 36f);
            Paper(panel);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 8f, width - 40f, 22f), "첫 근무 안내", artLabel);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 27f, width - 40f, height), message, artText);
        }

        void DrawTutorialTabPrompt(float scale, Vector2 offset)
        {
            const string message = "Tab을 눌러 메뉴 카드를 여세요.";
            var screen = new Rect(-offset / scale, new Vector2(Screen.width, Screen.height) / scale);
            var panel = new Rect(380f, 486f, 520f, 100f);
            DrawTutorialSpotlight(screen, true, new Rect(panel.x - 5f, panel.y - 5f, panel.width + 10f, panel.height + 10f));
            Paper(panel, shadow: false);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, 460f, 22f), "첫 근무 안내", artLabel);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 43f, 68f, 36f), "Tab", artCap);
            GUI.Label(new Rect(panel.x + 102f, panel.y + 50f, 398f, 30f), message, artText);
        }

        void DrawTutorialSpotlight(Rect screen, bool hasFocus, Rect focus)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var previous = GUI.color;
                GUI.color = new Color(.12f, .07f, .04f, .72f);
                Texture2D white = Texture2D.whiteTexture;
                if (!hasFocus)
                    GUI.DrawTexture(screen, white);
                else
                {
                    GUI.DrawTexture(Rect.MinMaxRect(screen.xMin, screen.yMin, screen.xMax, focus.yMin), white);
                    GUI.DrawTexture(Rect.MinMaxRect(screen.xMin, focus.yMax, screen.xMax, screen.yMax), white);
                    GUI.DrawTexture(Rect.MinMaxRect(screen.xMin, focus.yMin, focus.xMin, focus.yMax), white);
                    GUI.DrawTexture(Rect.MinMaxRect(focus.xMax, focus.yMin, screen.xMax, focus.yMax), white);
                    GUI.color = TutorialHighlightColor;
                    const float line = 3f;
                    GUI.DrawTexture(new Rect(focus.x, focus.y, focus.width, line), white);
                    GUI.DrawTexture(new Rect(focus.x, focus.yMax - line, focus.width, line), white);
                    GUI.DrawTexture(new Rect(focus.x, focus.y, line, focus.height), white);
                    GUI.DrawTexture(new Rect(focus.xMax - line, focus.y, line, focus.height), white);
                }
                GUI.color = previous;
            }
        }

        void DrawTutorial(float scale, Vector2 offset)
        {
            var screen = new Rect(-offset / scale, new Vector2(Screen.width, Screen.height) / scale);
            bool hasFocus = TryGetTutorialScreenBounds(scale, offset, screen, out Rect focus);
            DrawTutorialSpotlight(screen, hasFocus, focus);
            const string title = "카드를 보고 음료 만들기";
            const string body = "확대된 카드에 적힌 재료를 F로 집어 제조 컵에 넣으세요. 재료를 모두 넣은 뒤 빈손으로 제조 컵에 F를 눌러 블렌딩하고, 머그잔에 옮기세요.";
            const float width = 550f, margin = 24f;
            float bodyHeight = artText.CalcHeight(new GUIContent(body), width - 44f);
            float height = Mathf.Max(170f, 126f + bodyHeight);
            var panel = new Rect((1280f - width) * .5f, 720f - margin - height, width, height);
            if (hasFocus && panel.Overlaps(focus))
            {
                panel.y = margin;
                if (panel.Overlaps(focus))
                {
                    panel.y = (720f - height) * .5f;
                    panel.x = focus.center.x < 640f ? 1280f - margin - width : margin;
                }
            }

            Paper(panel);
            GUI.Label(new Rect(panel.x + 22, panel.y + 14, width - 44, 22), "첫 근무 안내", artLabel);
            GUI.Label(new Rect(panel.x + 22, panel.y + 40, width - 44, 27), title, artText);
            GUI.Label(new Rect(panel.x + 22, panel.y + 70, width - 44, bodyHeight), body, artText);
            if (GUI.Button(new Rect(panel.xMax - 184, panel.yMax - 45, 162, 30), "안내 끝내기  Enter", artButton))
                CloseTutorial(true);
        }

        bool TryGetTutorialScreenBounds(float scale, Vector2 offset, Rect screen, out Rect focus)
        {
            Bounds bounds = tutorialBounds;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = eyes.WorldToScreenPoint(corner);
                if (point.z <= eyes.nearClipPlane) continue;
                Vector2 ui = (new Vector2(point.x, Screen.height - point.y) - offset) / scale;
                min = Vector2.Min(min, ui);
                max = Vector2.Max(max, ui);
            }
            focus = default;
            if (min.x >= max.x || min.y >= max.y || min.x >= screen.xMax || max.x <= screen.xMin ||
                min.y >= screen.yMax || max.y <= screen.yMin) return false;

            focus = Rect.MinMaxRect(Mathf.Max(min.x - 14f, screen.xMin), Mathf.Max(min.y - 14f, screen.yMin),
                Mathf.Min(max.x + 14f, screen.xMax), Mathf.Min(max.y + 14f, screen.yMax));
            return true;
        }
    }
}
