using HorrorEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorGame.Items
{
    public sealed class PistolFieldManualData : DocumentData
    {
        public override string GetPageCaption(int pageIndex)
        {
#if ENABLE_INPUT_SYSTEM
            if (GameManager.Exists && GameManager.Instance.Player &&
                GameManager.Instance.Player.TryGetComponent(out PlayerInputListener listener) && listener.Actions != null)
            {
                var controls = listener.Actions.Gameplay;
                return BindingRow(controls, "KeyboardAndMouse", "Keyboard / Mouse") + "\n" +
                    BindingRow(controls, "Gamepad", "Controller");
            }
#endif
            return base.GetPageCaption(pageIndex);
        }

#if ENABLE_INPUT_SYSTEM
        private static string BindingRow(PlayerActions.GameplayActions controls, string group, string label) =>
            $"<b>{label}</b>   Aim {Binding(controls.Aiming, group)}   Fire {Binding(controls.Attack, group)}   " +
            $"Reload {Binding(controls.Reload, group)}";

        private static string Binding(InputAction action, string group)
        {
            string display = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions, group);
            return string.IsNullOrWhiteSpace(display) ? action.name : display;
        }
#endif
    }
}
