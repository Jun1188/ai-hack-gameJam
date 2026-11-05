using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        bool TryRecipeStep(out Collider target, out string instruction)
        {
            target = null; instruction = null;
            var item = tutorialMenu != null ? tutorialMenu.PinnedItem : null;
            if (brewStation == null || item == null || session.Selected?.State != VisitState.Active) return false;
            bool iced = !item.hot || item.ice && session.Order?.temperature == "ICE";
            return brewStation.TryGetRecipeGuidance(item, iced, out target, out instruction);
        }

        void DrawRecipeGuide()
        {
            if (promptOpen || TutorialOpen || TutorialTabRequired || eyes == null ||
                !TryRecipeStep(out var target, out var instruction) || target == null) return;
            Vector3 point = eyes.WorldToScreenPoint(target.bounds.center + Vector3.up * target.bounds.extents.y);
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector2 offset = (new Vector2(Screen.width, Screen.height) - new Vector2(1280f, 720f) * scale) * .5f;
            Vector2 ui = (new Vector2(point.x, Screen.height - point.y) - offset) / scale;
            // An offscreen target is described explicitly, instead of pinning a downward arrow to an unrelated object.
            if (point.z <= 0 || ui.x < 40 || ui.x > 940 || ui.y < 155 || ui.y > 610)
            {
                string direction = point.z <= 0 ? "뒤쪽" : ui.x < 40 ? "왼쪽" : ui.x > 940 ? "오른쪽" : ui.y < 155 ? "위쪽" : "아래쪽";
                Paper(new Rect(420, 116, 360, 34));
                GUI.Label(new Rect(432, 122, 336, 24), "다음 위치 · " + direction + "을 둘러보세요", artSmall);
                return;
            }
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 4f;
            var previous = GUI.color;
            GUI.color = TutorialHighlightColor;
            GUI.DrawTexture(new Rect(ui.x - 3, ui.y - 42 + bob, 6, 25), Texture2D.whiteTexture);
            // Filled downward triangle, independent of the font's arrow glyph coverage.
            for (int row = 0; row < 14; row++)
                GUI.DrawTexture(new Rect(ui.x - 14 + row, ui.y - 18 + bob + row, 28 - row * 2, 1.5f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
