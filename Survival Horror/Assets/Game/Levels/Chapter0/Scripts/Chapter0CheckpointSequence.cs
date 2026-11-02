using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter0
{
    /// <summary>
    /// Opens the Chapter 0 introduction and keeps the player disabled until
    /// the built-in dialog has been fully dismissed.
    /// </summary>
    public sealed class Chapter0CheckpointSequence : MonoBehaviour
    {
        [TextArea]
        [SerializeField] private string[] m_Lines =
        {
            "...",
            "The device hums to life.",
            "I should find out where I am."
        };

        private IEnumerator Start()
        {
            // UIDialog is created by the persistent Horror Engine UI core.
            // Waiting a frame lets it finish registering after the scene swap.
            yield return null;

            var player = GameManager.Instance.Player;
            player.Disable(this);

            var dialog = UIManager.Get<UIDialog>();
            dialog.Show(CreateDialog());

            yield return new WaitUntil(() => !dialog.gameObject.activeSelf);
            player.Enable(this);
        }

        private DialogData CreateDialog()
        {
            var lines = new DialogLine[m_Lines.Length];
            for (var i = 0; i < m_Lines.Length; ++i)
            {
                lines[i] = new DialogLine
                {
                    Delay = 0f,
                    LineText = new LocalizableText { IsLocalized = false, Unlocalized = m_Lines[i] }
                };
            }

            var dialog = new DialogData { PauseGame = true, CanBeDismissed = true };
            dialog.SetLines(lines);
            return dialog;
        }
    }
}
