using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class QuarterViewCameraPan : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 13f;
    [SerializeField, Min(0)] private float edgePixels = 22f;
    [SerializeField] private Vector2 focusMin = new Vector2(-15f, -21f);
    [SerializeField] private Vector2 focusMax = new Vector2(22f, 14f);

    private Camera view;
    private Vector3 homePosition;
    private Vector3 homeFocus;

#if UNITY_EDITOR
    private static readonly System.Type GameViewType =
        System.Type.GetType("UnityEditor.GameView, UnityEditor.CoreModule")
        ?? System.Type.GetType("UnityEditor.GameView, UnityEditor");
    private const System.Reflection.BindingFlags ViewProperties =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    private static readonly System.Reflection.PropertyInfo GameViewArea =
        GameViewType?.GetProperty("viewInWindow", ViewProperties);
    private static readonly System.Reflection.PropertyInfo GameViewImage =
        GameViewType?.GetProperty("targetInView", ViewProperties);
#endif

    private void Awake()
    {
        view = GetComponent<Camera>();
        homePosition = transform.position;
        var ground = new Plane(Vector3.up, Vector3.zero);
        Ray centerRay = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        homeFocus = ground.Raycast(centerRay, out float distance) ? centerRay.GetPoint(distance) : homePosition;
    }

    private void LateUpdate()
    {
        if (!Application.isFocused)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.homeKey.wasPressedThisFrame)
        {
            transform.position = homePosition;
            return;
        }

        Pan(ReadDirection(keyboard), Time.unscaledDeltaTime);
    }

    private Vector2 ReadDirection(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed) input.x -= 1;
            if (keyboard.rightArrowKey.isPressed) input.x += 1;
            if (keyboard.downArrowKey.isPressed) input.y -= 1;
            if (keyboard.upArrowKey.isPressed) input.y += 1;
        }
        if (input != Vector2.zero)
            return input;

        Mouse mouse = Mouse.current;
        return mouse == null ? input : EdgeDirection(mouse.position.ReadValue(), VisibleViewport());
    }

    private static Rect VisibleViewport()
    {
        var viewport = new Rect(0f, 0f, Screen.width, Screen.height);
#if UNITY_EDITOR
        var window = UnityEditor.EditorWindow.mouseOverWindow;
        if (window == null || window.GetType() != GameViewType)
            window = UnityEditor.EditorWindow.focusedWindow;

        // The Input System reports pixels in the render target, which the Game view can crop.
        if (window != null && window.GetType() == GameViewType
            && GameViewArea?.GetValue(window) is Rect area
            && GameViewImage?.GetValue(window) is Rect image
            && image.width > 0f && image.height > 0f)
        {
            float left = Mathf.Clamp01(-image.xMin / image.width);
            float right = Mathf.Clamp01((area.width - image.xMin) / image.width);
            float bottom = 1f - Mathf.Clamp01((area.height - image.yMin) / image.height);
            float top = 1f - Mathf.Clamp01(-image.yMin / image.height);
            return Rect.MinMaxRect(left * viewport.width, bottom * viewport.height,
                right * viewport.width, top * viewport.height);
        }
#endif
        return viewport;
    }

    private Vector2 EdgeDirection(Vector2 pointer, Rect viewport)
    {
        if (edgePixels <= 0 || viewport.width <= 0 || viewport.height <= 0)
            return Vector2.zero;

        // Begin scrolling before the pointer reaches the edge of a scaled Game view.
        float horizontalMargin = Mathf.Clamp(edgePixels, viewport.width * 0.06f, viewport.width * 0.25f);
        float verticalMargin = Mathf.Clamp(edgePixels, viewport.height * 0.06f, viewport.height * 0.25f);
        const float edgeTolerance = 4f;
        if (pointer.x < viewport.xMin - edgeTolerance || pointer.x > viewport.xMax + edgeTolerance
            || pointer.y < viewport.yMin - edgeTolerance || pointer.y > viewport.yMax + edgeTolerance)
            return Vector2.zero;

        float x = pointer.x <= viewport.xMin + horizontalMargin ? -1
            : pointer.x >= viewport.xMax - horizontalMargin ? 1 : 0;
        float y = pointer.y <= viewport.yMin + verticalMargin ? -1
            : pointer.y >= viewport.yMax - verticalMargin ? 1 : 0;
        return new Vector2(x, y);
    }

    private void Pan(Vector2 input, float deltaTime)
    {
        if (input == Vector2.zero)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector2 direction = Vector2.ClampMagnitude(input, 1f);
        Vector3 movement = (right * direction.x + forward * direction.y) * (speed * deltaTime);
        Vector3 focus = homeFocus + (transform.position - homePosition) + movement;
        focus.x = Mathf.Clamp(focus.x, focusMin.x, focusMax.x);
        focus.z = Mathf.Clamp(focus.z, focusMin.y, focusMax.y);
        transform.position = homePosition + new Vector3(focus.x - homeFocus.x, 0, focus.z - homeFocus.z);
    }
}
