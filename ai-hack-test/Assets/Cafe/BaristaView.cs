using UnityEngine;
using UnityEngine.InputSystem;

namespace MapleCafe
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class BaristaView : MonoBehaviour
    {
        [SerializeField] private Camera eyes;
        [SerializeField] private float walkSpeed = 2.6f;
        [SerializeField] private float mouseSensitivity = .13f;
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioClip[] footstepClips;
        private const float StepLength = .98f;
        private CharacterController controller;
        private Vector3 eyeRestPosition;
        private float pitch, verticalSpeed;
        private float gaitPhase, gaitWeight;
        private int nextFootstep;
        private bool choosingMenuCard;
        private int menuChangedFrame = -1;

        private bool externalUIActive;
        private int ignoreLookUntilFrame;
        public bool ExternalUIActive
        {
            get=>externalUIActive || CafeSim.CafeStartIris.Active;
            set {if(externalUIActive!=value){ignoreLookUntilFrame=Time.frameCount+2;menuChangedFrame=Time.frameCount;}externalUIActive=value;}
        }
        public bool ChoosingMenuCard => choosingMenuCard;
        public float MouseSensitivity { get => mouseSensitivity; set => mouseSensitivity = Mathf.Clamp(value, CafeSim.CafeSettings.MinSensitivity, CafeSim.CafeSettings.MaxSensitivity); }
        public bool CanInteract => !ExternalUIActive && !choosingMenuCard && menuChangedFrame != Time.frameCount
            && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

        public void SetChoosingMenuCard(bool choosing)
        {
            choosingMenuCard = choosing;
            menuChangedFrame = Time.frameCount;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (eyes == null) eyes = GetComponentInChildren<Camera>();
            eyeRestPosition = eyes.transform.localPosition;
            pitch = Mathf.DeltaAngle(0f, eyes.transform.localEulerAngles.x);
        }

        private void Update()
        {
            if (ExternalUIActive || choosingMenuCard || menuChangedFrame == Time.frameCount)
            {
                UpdateGait(0f, Time.deltaTime);
                return;
            }
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (!Application.isFocused || keys?.escapeKey.wasPressedThisFrame == true)
            {
                ReleaseCursor();
                UpdateGait(0f, Time.deltaTime);
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse?.leftButton.wasPressedThisFrame == true)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    ignoreLookUntilFrame=Time.frameCount+2;
                }
                UpdateGait(0f, Time.deltaTime);
                return;
            }
            if (mouse != null && Time.frameCount > ignoreLookUntilFrame) Look(mouse.delta.ReadValue() * mouseSensitivity);
            Vector2 input = keys == null ? Vector2.zero : new Vector2(
                (keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f),
                (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f));
            Walk(input, Time.deltaTime);
        }

        private void Look(Vector2 degrees)
        {
            transform.Rotate(0f, degrees.x, 0f);
            pitch = Mathf.Clamp(pitch - degrees.y, -80f, 80f);
        }

        private void Walk(Vector2 input, float dt)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            verticalSpeed = controller.isGrounded && verticalSpeed < 0f ? -1.5f : verticalSpeed - 18f * dt;
            Vector3 movement = (transform.right * input.x + transform.forward * input.y) * walkSpeed;
            Vector3 previousPosition = transform.position;
            controller.Move((movement + Vector3.up * verticalSpeed) * dt);
            Vector3 travel = transform.position - previousPosition;
            travel.y = 0f;
            // Only count progress in the requested direction, excluding collision recovery.
            float distance = controller.isGrounded
                ? Mathf.Clamp(Vector3.Dot(travel, movement.normalized), 0f, movement.magnitude * dt)
                : 0f;
            UpdateGait(distance, dt);
        }

        private void UpdateGait(float distance, float dt)
        {
            float speed = distance / Mathf.Max(dt, .001f);
            float targetWeight = speed < .12f ? 0f : Mathf.Clamp01(speed / walkSpeed);
            gaitWeight = Mathf.MoveTowards(gaitWeight, targetWeight, dt * 6f);
            if (targetWeight > 0f)
            {
                float nextPhase = gaitPhase + distance / StepLength;
                // The dip at each half-stride coincides with the foot hitting the floor.
                if (Mathf.FloorToInt(nextPhase + .5f) > Mathf.FloorToInt(gaitPhase + .5f))
                    PlayFootstep();
                gaitPhase = Mathf.Repeat(nextPhase, 2f);
            }
            else if (gaitWeight == 0f)
            {
                gaitPhase = 0f;
            }

            float stride = gaitPhase * Mathf.PI;
            float side = Mathf.Sin(stride);
            float step = stride * 2f;
            eyes.transform.localPosition = eyeRestPosition + new Vector3(
                side * .019f, Mathf.Cos(step) * .027f, Mathf.Sin(step) * .014f) * gaitWeight;
            eyes.transform.localRotation = Quaternion.Euler(
                pitch + Mathf.Sin(step) * .33f * gaitWeight, 0f, side * -.55f * gaitWeight);
        }

        private void PlayFootstep()
        {
            if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;
            footstepSource.pitch = nextFootstep % 2 == 0 ? .97f : 1.03f;
            footstepSource.PlayOneShot(footstepClips[nextFootstep % footstepClips.Length]);
            nextFootstep++;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseCursor();
        }

        private void OnDisable()
        {
            ReleaseCursor();
            if (eyes != null) UpdateGait(0f, 1f);
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
