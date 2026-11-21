using HorrorEngine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorGame.Environment
{
    [DisallowMultipleComponent]
    public sealed class EquippedFlashlightSwitch : MonoBehaviour
    {
        [SerializeField] private GameObject beam;
        [SerializeField] private DepleteEquipment battery;

        private void Update()
        {
            if (!TogglePressed() || !GameManager.Exists || !PauseController.Exists)
                return;

            var game = GameManager.Instance;
            if (game.Player && game.IsPlaying && transform.IsChildOf(game.Player.transform))
                SetLightOn(!beam.activeSelf);
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.fKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        private void SetLightOn(bool on)
        {
            beam.SetActive(on);
            battery.enabled = on;
        }
    }
}
