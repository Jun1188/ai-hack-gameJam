using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class GrandfatherMovement : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.28f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 720f;
    [SerializeField] private Transform view;

    private CharacterController controller;
    private GrandfatherGait gait;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        gait = GetComponent<GrandfatherGait>();
        if (view == null && Camera.main != null)
            view = Camera.main.transform;
    }

    private void Update()
    {
        Move(ReadInput(), Time.deltaTime);
    }

    private static Vector2 ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard == null ? Vector2.zero : new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
    }

    private void Move(Vector2 input, float deltaTime)
    {
        Vector3 forward = view == null ? Vector3.forward : Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 direction = right * input.x + forward * input.y;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion facing = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * deltaTime);
        }

        verticalSpeed = controller.isGrounded && verticalSpeed < 0f
            ? -2f : Mathf.Max(verticalSpeed + Physics.gravity.y * deltaTime, -22f);
        float distance = direction.magnitude * walkSpeed * deltaTime;
        if (gait != null)
            distance = gait.Advance(direction.sqrMagnitude > 0.001f, walkSpeed, deltaTime);

        Vector3 previous = transform.position;
        controller.Move(direction.normalized * distance + Vector3.up * (verticalSpeed * deltaTime));
        if (gait != null)
            gait.RecordTravel(Vector3.Dot(transform.position - previous, direction.normalized), distance);
    }
}

