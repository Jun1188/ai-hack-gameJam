using UnityEngine;
using UnityEngine.InputSystem;

// Quarter-view camera that trails grandfather; right-drag orbits, wheel zooms.
// Yaw is limited so the cutaway room walls (behind the build site) never block the view.
[RequireComponent(typeof(Camera))]
public sealed class BarnFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new(5.7f, 7.6f, -8.7f);
    [SerializeField, Range(0f, 90f)] private float yawLimit = 55f;
    [SerializeField] private float orbitSpeed = .25f;
    [SerializeField] private Vector2 zoomRange = new(3.4f, 6.8f);
    [SerializeField] private float zoom = 4.8f;
    [SerializeField] private float follow = 4f;

    private Camera view;
    private Vector3 focus;
    private float yaw;

    private void Awake()
    {
        view = GetComponent<Camera>();
        if (target == null)
            target = GameObject.FindGameObjectWithTag("Player")?.transform;
        focus = target != null ? target.position : Vector3.zero;
        Apply();
    }

    private void LateUpdate()
    {
        if (StorehouseTransition.Busy)
            return;
        float dt = Time.unscaledDeltaTime;
        if (Application.isFocused && Mouse.current is Mouse mouse)
        {
            if (mouse.rightButton.isPressed)
                yaw = Mathf.Clamp(yaw + mouse.delta.ReadValue().x * orbitSpeed, -yawLimit, yawLimit);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f)
                zoom = Mathf.Clamp(zoom - Mathf.Sign(scroll) * .25f, zoomRange.x, zoomRange.y);
        }
        if (target != null)
        {
            Vector3 goal = target.position + Vector3.up * .8f;
            focus = Vector3.Lerp(focus, goal, 1f - Mathf.Exp(-follow * dt));
        }
        Apply();
    }

    private void Apply()
    {
        Vector3 rotated = Quaternion.Euler(0f, yaw, 0f) * offset;
        transform.SetPositionAndRotation(focus + rotated, Quaternion.LookRotation(-rotated));
        if (view.orthographic)
            view.orthographicSize = Mathf.Max(zoom, 4.4f / Mathf.Max(view.aspect, .1f));
    }
}
