using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class StorehouseCamera : MonoBehaviour
{
    private static readonly Vector3 Offset = new(5.7f, 7.6f, -8.7f);
    private Vector3 focus = new(-.55f, .62f, 0f);
    private Camera view;
    private StorehouseAssembly assembly;
    private float zoom = 4.35f;
    private bool framingComplete;

    private void Awake()
    {
        view = GetComponent<Camera>();
        assembly = FindFirstObjectByType<StorehouseAssembly>();
        Apply();
    }

    public void FrameCompletedStorehouse() => framingComplete = true;

    private void LateUpdate()
    {
        if (StorehouseTransition.Busy || (assembly != null && assembly.IsDragging))
            return;
        float dt = Time.unscaledDeltaTime;
        if (framingComplete)
        {
            focus = Vector3.MoveTowards(focus, new Vector3(.72f, 1.22f, 0f), 1.6f * dt);
            zoom = Mathf.MoveTowards(zoom, 3.75f, 1.4f * dt);
            if (Mathf.Abs(zoom - 3.75f) < .001f && Vector3.Distance(focus, new Vector3(.72f, 1.22f, 0f)) < .001f)
                framingComplete = false;
            Apply();
            return;
        }

        if (!Application.isFocused)
            return;

        Vector2 pan = Vector2.zero;
        Keyboard keys = Keyboard.current;
        if (keys != null)
        {
            pan = new Vector2((keys.rightArrowKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed ? 1 : 0),
                (keys.upArrowKey.isPressed ? 1 : 0) - (keys.downArrowKey.isPressed ? 1 : 0));
            if (keys.homeKey.wasPressedThisFrame)
            {
                focus = new Vector3(-.55f, .62f, 0f);
                zoom = 4.35f;
            }
        }
        if (Mouse.current is Mouse mouse)
        {
            if (mouse.middleButton.isPressed)
                Pan(-mouse.delta.ReadValue() * (2f * zoom / Mathf.Max(1, Screen.height)));
            else if (pan == Vector2.zero)
                pan = QuarterViewCameraPan.EdgeDirection(mouse.position.ReadValue(),
                    QuarterViewCameraPan.VisibleViewport(), 15f);
            zoom = Mathf.Clamp(zoom - mouse.scroll.ReadValue().y * .0025f, 3.55f, 4.7f);
        }
        Pan(Vector2.ClampMagnitude(pan, 1f) * (2.4f * dt));
        Apply();
    }

    private void Pan(Vector2 movement)
    {
        Vector3 forward = Vector3.ProjectOnPlane(-Offset, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        focus += right * movement.x + forward * movement.y;
        focus.x = Mathf.Clamp(focus.x, -1.15f, .9f);
        focus.z = Mathf.Clamp(focus.z, -.82f, .82f);
    }

    private void Apply()
    {
        transform.SetPositionAndRotation(focus + Offset, Quaternion.LookRotation(-Offset));
        // Keep the workbench and assembly in view on narrower displays.
        view.orthographicSize = Mathf.Max(zoom, 5.25f / Mathf.Max(view.aspect, .1f));
    }
}
