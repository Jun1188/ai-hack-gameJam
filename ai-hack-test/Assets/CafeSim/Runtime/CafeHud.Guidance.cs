using UnityEngine;

namespace CafeSim
{
    public sealed partial class CafeHud
    {
        [SerializeField, Range(.75f, 2.5f)] float recipeGuideArrowScale = 1.65f;
        [SerializeField, Min(0)] float recipeGuideArrowTipGap = 14f;
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
            float arrowScale = Mathf.Clamp(recipeGuideArrowScale, .75f, 2.5f);
            float headHeight = 14f * arrowScale, headWidth = 28f * arrowScale;
            float arrowHeight = 42f * arrowScale;
            float tipGap = Mathf.Max(0, recipeGuideArrowTipGap);
            float topLimit = 155f + Mathf.Max(0, arrowHeight - 42f) + tipGap;
            // Offscreen targets do not add a central instruction popup.
            if (point.z <= 0 || ui.x < 40 || ui.x > 940 || ui.y < topLimit || ui.y > 610)
                return;
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 4f;
            float tipY = ui.y - tipGap + bob;
            var previous = GUI.color;
            GUI.color = TutorialHighlightColor;
            float shaftWidth = 6f * arrowScale;
            GUI.DrawTexture(new Rect(ui.x - shaftWidth * .5f, tipY - arrowHeight,
                shaftWidth, arrowHeight - headHeight + 1), Texture2D.whiteTexture);
            // Filled downward triangle, independent of the font's arrow glyph coverage.
            int rows = Mathf.CeilToInt(headHeight);
            for (int row = 0; row <= rows; row++)
            {
                float width = Mathf.Max(1, headWidth * (1f - (float)row / rows));
                GUI.DrawTexture(new Rect(ui.x - width * .5f, tipY - headHeight + row, width, 1.5f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
