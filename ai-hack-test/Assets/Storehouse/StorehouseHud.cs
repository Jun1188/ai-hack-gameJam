using UnityEngine;

internal static class StorehouseHud
{
    internal static readonly Color Ink = new(0.067f, 0.09f, 0.086f, 0.96f);
    internal static readonly Color Paper = new(0.97f, 0.91f, 0.77f);
    internal static readonly Color Gold = new(0.96f, 0.73f, 0.38f);
    internal static readonly Color Mint = new(0.63f, 0.87f, 0.74f);
    private static Font font;

    internal static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    internal static GUIStyle Type(int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial Unicode MS" }, 24);
        }

        var style = new GUIStyle(GUI.skin.label)
        {
            font = font != null ? font : GUI.skin.font,
            fontSize = size,
            alignment = alignment,
            clipping = TextClipping.Clip
        };
        style.normal.textColor = color;
        return style;
    }

    internal static bool Button(Rect rect, string text, bool active = false)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        Fill(rect, active ? Gold : hover ? new Color(0.2f, 0.29f, 0.25f) : Ink);
        GUI.Label(rect, text, Type(14, active ? Ink : Paper, TextAnchor.MiddleCenter));
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    internal static Vector3 Project(Camera camera, Vector3 point)
    {
        Vector3 screen = camera.WorldToScreenPoint(point);
        return new Vector3(screen.x, Screen.height - screen.y, screen.z);
    }

    internal static void Line(Vector2 start, Vector2 end, Color color, float width)
    {
        Matrix4x4 previous = GUI.matrix;
        Vector2 difference = end - start;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg, start);
        Fill(new Rect(start.x, start.y - width / 2f, difference.magnitude, width), color);
        GUI.matrix = previous;
    }

    internal static void Ring(Vector2 center, float radius, Color color)
    {
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= 24; i++)
        {
            float angle = i * Mathf.PI / 12f;
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Line(previous, next, color, 2f);
            previous = next;
        }
    }
}
