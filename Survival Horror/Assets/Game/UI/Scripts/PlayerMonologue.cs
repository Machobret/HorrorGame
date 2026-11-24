using HorrorEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    public sealed class PlayerMonologue : MonoBehaviour
    {
        private GameObject surface;
        private TextMeshProUGUI label;
        private float elapsed, fadeIn, hold, fadeOut;
        private PlayerMonologueTrigger zone;
        private Object persistentOwner;
        private bool waitForClear;

        internal static void ShowUntilCleared(Object owner, string text, float? holdSeconds = null)
        {
            if (!Show(text, .2f, holdSeconds ?? 3f)) return;
            var monologue = UIManager.Instance.GetComponent<PlayerMonologue>();
            monologue.persistentOwner = owner;
            monologue.waitForClear = !holdSeconds.HasValue;
            monologue.label.outlineColor = new Color32(24, 24, 24, 255);
            monologue.label.outlineWidth = .22f;
        }

        internal static void Clear(Object owner)
        {
            if (!UIManager.Exists || !UIManager.Instance.TryGetComponent(out PlayerMonologue monologue) ||
                monologue.persistentOwner != owner) return;
            monologue.persistentOwner = null;
            monologue.surface.SetActive(false);
        }

        public static bool Show(string text, float fadeIn = .8f, float hold = 3f, float fadeOut = 1f)
            => ShowOwned(text, fadeIn, hold, fadeOut, null);

        internal static bool ShowOwned(string text, float fadeIn, float hold, float fadeOut, PlayerMonologueTrigger owner)
        {
            if (string.IsNullOrWhiteSpace(text) || !UIManager.Exists) return false;
            var manager = UIManager.Instance;
            if (!manager.TryGetComponent(out PlayerMonologue monologue))
                monologue = manager.gameObject.AddComponent<PlayerMonologue>();
            monologue.ShowLine(text, fadeIn, hold, fadeOut);
            monologue.zone = owner;
            monologue.persistentOwner = null;
            monologue.waitForClear = false;
            monologue.label.outlineWidth = 0f;
            return true;
        }

        internal static void Release(PlayerMonologueTrigger owner)
        {
            if (UIManager.Exists && UIManager.Instance.TryGetComponent(out PlayerMonologue monologue) &&
                monologue.zone == owner)
                monologue.zone = null;
        }

        private void ShowLine(string text, float inSeconds, float holdSeconds, float outSeconds)
        {
            if (!label) BuildDisplay();
            elapsed = 0f;
            fadeIn = Mathf.Max(.01f, inSeconds);
            hold = Mathf.Max(0f, holdSeconds);
            fadeOut = Mathf.Max(.01f, outSeconds);
            GameText.SetLabel(label, text);
            label.alpha = 0f;
            surface.SetActive(true);
        }

        private void BuildDisplay()
        {
            surface = new GameObject("Player monologue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = surface.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = surface.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var textObject = new GameObject("Monologue text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(surface.transform, false);
            label = textObject.GetComponent<TextMeshProUGUI>();
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(.16f, .08f);
            rect.anchorMax = new Vector2(.84f, .25f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var dialogue = UIManager.Get<UIDialog>(true);
            if (dialogue && dialogue.DialogueFont) label.font = dialogue.DialogueFont;
            label.fontSize = 30f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Bottom;
            label.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (!surface || !surface.activeSelf) return;
            if (PauseController.Exists && PauseController.Instance.IsPaused)
            {
                label.alpha = 0f;
                return;
            }

            elapsed += Time.deltaTime;
            if (zone || (persistentOwner && waitForClear)) elapsed = Mathf.Min(elapsed, fadeIn);
            float total = fadeIn + hold + fadeOut;
            if (elapsed >= total)
            {
                surface.SetActive(false);
                return;
            }
            float visibility = Mathf.Min(elapsed / fadeIn, (total - elapsed) / fadeOut);
            label.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(visibility));
        }

        private void OnDisable()
        {
            if (surface) surface.SetActive(false);
        }

        private void OnDestroy()
        {
            if (surface) Destroy(surface);
        }
    }
}
