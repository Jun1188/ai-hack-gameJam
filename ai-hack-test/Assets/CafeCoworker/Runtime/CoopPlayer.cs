using UnityEngine;
using UnityEngine.InputSystem;

namespace CafeCoworker
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CoopPlayer : MonoBehaviour
    {
        public Camera eyes;
        public bool MenuOpen { get; set; }
        public float walkSpeed = 2.6f;
        CharacterController body;
        float pitch, vertical;
        bool previousMenu;
        int ignoreLookUntilFrame;
        void Awake() { body = GetComponent<CharacterController>(); Lock(true); }
        public void Lock(bool locked)
        { ignoreLookUntilFrame=Time.frameCount+3;Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        void OnApplicationFocus(bool focused){if(focused)Lock(!MenuOpen);}
        void Update()
        {
            if (previousMenu != MenuOpen) { previousMenu = MenuOpen; Lock(!MenuOpen); }
            if (MenuOpen) return;
            if(!Application.isFocused)return;
            var mouse = Mouse.current; var key = Keyboard.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked&&Time.frameCount>ignoreLookUntilFrame)
            {
                Vector2 look = mouse.delta.ReadValue() * .12f;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -75, 75);
                eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            if (key == null) return;
            Vector2 input = new Vector2((key.dKey.isPressed ? 1 : 0) - (key.aKey.isPressed ? 1 : 0),
                (key.wKey.isPressed ? 1 : 0) - (key.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            vertical = body.isGrounded ? -1.5f : vertical - 18 * Time.deltaTime;
            body.Move(((transform.right * input.x + transform.forward * input.y) * walkSpeed + Vector3.up * vertical) * Time.deltaTime);
        }
        void OnDisable() { Lock(false); }
    }
}
