using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(Interactive))]
    public sealed class Chapter1DemoExit : MonoBehaviour
    {
        public void ShowEnding()
        {
            var dialog = UIManager.Get<UIDialog>();
            if (dialog.isActiveAndEnabled) return;

            var message = new DialogData { PauseGame = true };
            message.SetLines(new[]
            {
                new DialogLine
                {
                    LineText = new LocalizableText { Unlocalized = "Thank you for playing the Exaltfall demo!" }
                }
            });

            UIManager.PushAction(new UIStackedAction
            {
                Name = "Return to main menu after demo ending",
                Action = () => GameManager.Instance.QuitGame(),
                StopProcessingActions = true
            });
            dialog.Show(message);
        }
    }
}
