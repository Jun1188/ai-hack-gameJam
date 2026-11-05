using UnityEngine;
using UnityEngine.InputSystem;

namespace CafeSim
{
    // Adapted from Jun1188/CoreDawn PlayerController: WishDirWorld,
    // ApplyGroundMove, HandleLook/ApplyLookFallback. See docs/cafe-simulator.md.
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CafeFirstPersonController : MonoBehaviour
    {
        public Transform eye;
        public float moveSpeed = 3.2f, acceleration = 30f, friction = 35f, mouseSensitivity = 1f;
        public bool Looking => Cursor.lockState == CursorLockMode.Locked;
        public bool TextInputActive {get;set;}
        Rigidbody body;
        Vector2 input;
        float pitch;
        bool focused = true;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !focused) { input = Vector2.zero; return; }
            if (TextInputActive) { input = Vector2.zero; return; }
            if (keyboard.tabKey.wasPressedThisFrame) SetLooking(!Looking);
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame) SetLooking(false);
            if (!Looking) { input = Vector2.zero; return; }
            input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            if (Mouse.current == null) return;
            Vector2 look = Mouse.current.delta.ReadValue() * (mouseSensitivity * 0.1f);
            transform.Rotate(Vector3.up * look.x, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            if (eye != null) eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        void FixedUpdate()
        {
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            wish = Vector3.ClampMagnitude(wish, 1f);
            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            planar = Vector3.MoveTowards(planar, wish * moveSpeed,
                (wish.sqrMagnitude > 0.001f ? acceleration : friction) * Time.fixedDeltaTime);
            body.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        }
        public void SetLooking(bool value)
        {
            input = Vector2.zero;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value) SetLooking(false); }
        void OnDisable() { SetLooking(false); }
    }
}
