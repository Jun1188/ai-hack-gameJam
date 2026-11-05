using MapleCafe;
using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        internal static readonly Color32 TutorialHighlightColor = new(255, 200, 105, 255);
        [SerializeField] CafeBrewStation brewStation;
        [SerializeField] CafeMenuBook tutorialMenu;
        enum TutorialStage { WaitingForCustomer, ChooseCard, MakeDrink, WaitingForDeparture, ReopenMenu, CloseMenu, Done }
        TutorialStage tutorialStage;
        CafeVisit tutorialCustomer;
        Bounds tutorialBounds;
        bool TutorialOpen => tutorialStage == TutorialStage.MakeDrink;

        void UpdateTutorial()
        {
            if (tutorialMenu == null || brewStation == null || businessDay == null || CafeBusinessDay.Day != 1 ||
                tutorialStage == TutorialStage.Done || Halted || promptOpen || session.ChatBusy) return;

            switch (tutorialStage)
            {
                case TutorialStage.WaitingForCustomer:
                    CafeVisit customer = session.Selected;
                    Transform body = session.BodyOf(customer);
                    if (customer?.State == VisitState.Active && !string.IsNullOrWhiteSpace(customer.Order?.speech) &&
                        body != null && body.gameObject.activeInHierarchy &&
                        (body.position - session.servicePoint.position).sqrMagnitude <= .0001f)
                    {
                        tutorialCustomer = customer;
                        tutorialStage = TutorialStage.ChooseCard;
                    }
                    break;
                case TutorialStage.ChooseCard:
                    if (tutorialCustomer.State == VisitState.TimedOut)
                        tutorialStage = TutorialStage.WaitingForCustomer;
                    else if (tutorialCustomer.State == VisitState.Received)
                        tutorialStage = TutorialStage.WaitingForDeparture;
                    else
                        WatchForBlender();
                    break;
                case TutorialStage.WaitingForDeparture:
                    if (tutorialCustomer.State == VisitState.TimedOut)
                        tutorialStage = TutorialStage.WaitingForCustomer;
                    else if (session.HasStartedDeparture(tutorialCustomer))
                        tutorialStage = tutorialMenu.IsListOpen ? TutorialStage.CloseMenu : TutorialStage.ReopenMenu;
                    break;
                case TutorialStage.ReopenMenu:
                    if (tutorialMenu.IsListOpen) tutorialStage = TutorialStage.CloseMenu;
                    break;
                case TutorialStage.CloseMenu:
                    if (!tutorialMenu.IsListOpen)
                        tutorialStage = tutorialMenu.HasPinnedCard ? TutorialStage.ReopenMenu : TutorialStage.Done;
                    break;
            }
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

        void CloseTutorial(bool resumeInput, bool skip = false)
        {
            if (!TutorialOpen) return;
            tutorialStage = skip ? TutorialStage.Done : TutorialStage.WaitingForDeparture;
            if (businessDay != null) businessDay.SetPaused(false);
            if (!resumeInput) return;
            ApplyOverlayControls();
            if (Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void DrawTutorialHint()
        {
            if (Halted || promptOpen || session.ChatBusy || tutorialMenu == null) return;
            string message = tutorialStage switch
            {
                TutorialStage.ChooseCard when tutorialMenu.IsListOpen => "주문에 맞는 메뉴 카드를 클릭하면 왼쪽 위에 확대돼요.",
                TutorialStage.ChooseCard when tutorialMenu.HasPinnedCard => "카드를 펼쳐 둔 채 뒤돌아서 블렌더를 보세요.",
                TutorialStage.ChooseCard => "Tab으로 메뉴 카드를 열고, 원하는 카드를 클릭해 확대하세요.",
                TutorialStage.ReopenMenu => "Tab을 눌러 카드 목록으로 돌아가세요. 목록에서 Tab을 한 번 더 누르면 닫혀요.",
                TutorialStage.CloseMenu => "Tab을 한 번 더 눌러 카드 목록을 닫으세요.",
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

        void DrawTutorial(float scale, Vector2 offset)
        {
            Rect screen = new Rect(-offset / scale, new Vector2(Screen.width, Screen.height) / scale);
            bool hasFocus = TryGetTutorialScreenBounds(scale, offset, screen, out Rect focus);
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
            if (GUI.Button(new Rect(panel.x + 22, panel.yMax - 45, 145, 30), "안내 건너뛰기", artButton))
                CloseTutorial(true, skip: true);
            else if (GUI.Button(new Rect(panel.xMax - 156, panel.yMax - 45, 134, 30), "계속  Enter", artButton))
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
