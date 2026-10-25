using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.General
{
    /// <summary>
    /// Restyles HorrorEngine's stock pause screen without changing its input,
    /// button events, or pause behaviour.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimplePauseMenuVisuals : MonoBehaviour
    {
        [SerializeField] private Font gameFont;
        [SerializeField] private Sprite exaltFallLogo;

        private TMP_FontAsset fontAsset;

        private void Awake()
        {
            if (gameFont == null)
                return;

            fontAsset = TMP_FontAsset.CreateFontAsset(gameFont);
            ApplyFont();
            SimplifyLayout();
        }

        private void ApplyFont()
        {
            foreach (var label in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.font = fontAsset;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void SimplifyLayout()
        {
            SetVisible("ControlInfo", false);
            SetVisible("KeyboardHelp", false);
            SetVisible("GamepadHelp", false);
            SetVisible("HelpNames", false);
            SetVisible("Image (1)", false);

            var dimmer = Find("Bg");
            if (dimmer != null && dimmer.TryGetComponent<Image>(out var dimmerImage))
                dimmerImage.color = new Color(0f, 0f, 0f, .76f);

            StyleTitle();
            CenterButtons();
            StyleButton("ButtonContinue", 30);
            StyleButton("ButtonSettings", 30);
            StyleButton("ButtonQuit", 30);
        }

        private void StyleTitle()
        {
            var title = Find("PauseText");
            if (title == null)
                return;

            if (title.TryGetComponent<TextMeshProUGUI>(out var label))
                label.enabled = false;

            if (exaltFallLogo == null)
                return;

            var logoTransform = title.Find("ExaltFallLogo") as RectTransform;
            if (logoTransform == null)
            {
                var logoObject = new GameObject("ExaltFallLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                logoObject.layer = title.gameObject.layer;
                logoTransform = logoObject.GetComponent<RectTransform>();
                logoTransform.SetParent(title, false);
                logoTransform.anchorMin = Vector2.zero;
                logoTransform.anchorMax = Vector2.one;
                logoTransform.offsetMin = Vector2.zero;
                logoTransform.offsetMax = Vector2.zero;
            }

            var logo = logoTransform.GetComponent<Image>();
            logo.sprite = exaltFallLogo;
            logo.preserveAspect = true;
            logo.color = Color.white;
            title.anchoredPosition = new Vector2(0f, 120f);
            title.sizeDelta = new Vector2(360f, 110f);
        }

        private void CenterButtons()
        {
            var buttons = Find("Buttons");
            if (buttons == null)
                return;

            buttons.anchorMin = buttons.anchorMax = new Vector2(.5f, .5f);
            buttons.anchoredPosition = new Vector2(0f, -24f);
            buttons.sizeDelta = new Vector2(300f, 165f);
        }

        private void StyleButton(string name, float fontSize)
        {
            var button = Find(name);
            if (button == null)
                return;

            button.sizeDelta = new Vector2(300f, 50f);

            if (button.TryGetComponent<Image>(out var image))
                image.color = new Color(.12f, .12f, .11f, .94f);

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
                return;

            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(.94f, .92f, .84f, 1f);
            label.alignment = TextAlignmentOptions.Center;
        }

        private void SetVisible(string name, bool visible)
        {
            var element = Find(name);
            if (element != null)
                element.gameObject.SetActive(visible);
        }

        private RectTransform Find(string name)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name)
                    return rect;

            return null;
        }
    }
}
