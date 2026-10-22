using UnityEngine;
using UnityEngine.InputSystem;

namespace TemporaryHouseDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DemoWalker : MonoBehaviour
    {
        [SerializeField] private Transform view;
        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;
        private Vector3 spawn;
        private Quaternion spawnRotation;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
        }
        private void Start() { if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }
        private void OnDisable() { if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }

        private void Update()
        {
            if (Application.isBatchMode) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * .09f;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -85, 85);
                view.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float z = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            Vector3 movement = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move((movement * 2.2f + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (transform.position.y < spawn.y - 5)
            {
                controller.enabled = false;
                transform.SetPositionAndRotation(spawn, spawnRotation);
                controller.enabled = true;
                pitch = 0;
                verticalSpeed = 0;
                view.localRotation = Quaternion.identity;
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(16, 14, 600, 30), "TEMPORARY HOUSE DEMO  |  WASD move / Mouse look / Esc release / Click capture");
        }
    }
}
