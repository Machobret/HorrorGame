using System.Collections;
using HorrorEngine;
using HorrorGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Chapter1
{
    public sealed class Chapter1Intro : MonoBehaviour
    {
        private GameObject surface;
        private CanvasGroup screen;
        private TextMeshProUGUI caption;
        private UIInputListener input;
        private PauseController pause;

        private IEnumerator Start()
        {
            pause = PauseController.Instance;
            pause.Pause(this);
            input = UIManager.Get<UIInputListener>(true);
            if (input) input.AddBlockingContext(this);
            BuildScreen();

            yield return new WaitForSecondsRealtime(.35f);
            yield return ShowLine("I need to investigate this town");
            yield return ShowLine("I cannot turn back now");
            yield return FadeScreen();
            Finish();
            yield return new WaitForSeconds(1f);
            PlayerMonologue.Show("\"W\" to move forward");
        }

        private void BuildScreen()
        {
            surface = new GameObject("Chapter 1 intro", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = surface.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = surface.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            screen = surface.GetComponent<CanvasGroup>();

            var backdrop = new GameObject("Black backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(backdrop.rectTransform, Vector2.zero, Vector2.one);
            backdrop.color = Color.black;
            backdrop.raycastTarget = false;

            caption = new GameObject("Intro text", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            Place(caption.rectTransform, new Vector2(.1f, .075f), new Vector2(.9f, .22f));
            var dialogue = UIManager.Get<UIDialog>(true);
            if (dialogue && dialogue.DialogueFont) caption.font = dialogue.DialogueFont;
            caption.fontSize = 34f;
            caption.color = Color.white;
            caption.alpha = 0f;
            caption.alignment = TextAlignmentOptions.Bottom;
            caption.raycastTarget = false;
        }

        private void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.SetParent(surface.transform, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private IEnumerator ShowLine(string text)
        {
            GameText.SetLabel(caption, text);
            const float fade = .8f, hold = 1.65f, duration = fade * 2f + hold;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                float visibility = Mathf.Min(time, duration - time) / fade;
                caption.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(visibility));
                yield return null;
            }
            caption.alpha = 0f;
        }

        private IEnumerator FadeScreen()
        {
            const float duration = 1.15f;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                screen.alpha = 1f - Mathf.SmoothStep(0f, 1f, time / duration);
                yield return null;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Finish();
        }

        private void Finish()
        {
            if (surface) Destroy(surface);
            surface = null;
            if (input) input.RemoveBlockingContext(this);
            input = null;
            if (pause) pause.Resume(this);
            pause = null;
        }
    }
}
