using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.General
{
    public sealed class MainMenuBackgroundCarousel : MonoBehaviour
    {
        [SerializeField] private Sprite m_Logo;
        [SerializeField] private Sprite[] m_Backgrounds;
        [SerializeField] private Sprite m_Border;
        [SerializeField] private bool m_EnableLogoSequence;
        [SerializeField] private Sprite[] m_LogoSequence;
        [SerializeField, Range(0f, 1f)] private float m_BorderOpacity = 1f;
        [SerializeField, Min(1f)] private float m_DisplayDuration = 8f;
        [SerializeField, Min(.1f)] private float m_FadeDuration = 1f;
        [SerializeField, Min(.1f)] private float m_ExitFadeDuration = 2.5f;
        [SerializeField, Min(1f)] private float m_ZoomScale = 1.1f;

        private Image m_Background;
        private Image m_LogoImage;
        private Image m_BorderOverlay;
        private bool m_ExitFadeStarted;
        private Coroutine m_CarouselRoutine;

        private void Awake()
        {
            Image logo = null;
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Bg") m_Background = image;
                else if (image.name == "Logo") { logo = image; m_LogoImage = image; }
                else if (image.name == "Background Border") m_BorderOverlay = image;
            }

            if (logo) logo.sprite = FirstLogoSprite();
            CreateBorderOverlay();
        }

        private void CreateBorderOverlay()
        {
            if (!m_Background || !m_Border) return;

            var image = m_BorderOverlay;
            if (!image)
            {
                var overlay = new GameObject("Background Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(m_Background.transform.parent, false);
                image = overlay.GetComponent<Image>();
            }

            image.transform.SetSiblingIndex(m_Background.transform.GetSiblingIndex() + 1);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.sprite = m_Border;
            image.color = new Color(1f, 1f, 1f, m_BorderOpacity);
            image.raycastTarget = false;
        }

        private void OnEnable()
        {
            if (m_Background && m_Backgrounds != null && m_Backgrounds.Length > 0)
                m_CarouselRoutine = StartCoroutine(RotateBackgrounds());
        }

        public void PlayExitFade()
        {
            if (m_ExitFadeStarted || !m_Background) return;
            m_ExitFadeStarted = true;
            if (m_CarouselRoutine != null)
            {
                StopCoroutine(m_CarouselRoutine);
                m_CarouselRoutine = null;
            }
            StartCoroutine(Fade(1f, 0f, m_ExitFadeDuration));
        }

        private IEnumerator RotateBackgrounds()
        {
            int index = 0;
            while (true)
            {
                m_Background.sprite = m_Backgrounds[index];
                m_Background.rectTransform.localScale = Vector3.one;
                yield return FadeInAndZoom();

                float elapsed = 0f;
                while (elapsed < m_DisplayDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetZoom(m_FadeDuration + elapsed);
                    yield return null;
                }

                yield return Fade(1f, 0f, m_FadeDuration);
                index = (index + 1) % m_Backgrounds.Length;
            }
        }

        private IEnumerator FadeInAndZoom()
        {
            if (m_LogoImage) m_LogoImage.sprite = FirstLogoSprite();
            if (m_LogoImage) m_LogoImage.enabled = true;
            float elapsed = 0f;
            var color = m_Background.color;
            while (elapsed < m_FadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(0f, 1f, elapsed / m_FadeDuration);
                m_Background.color = color;
                SetLogoAlpha(color.a);
                SetZoom(elapsed);
                yield return null;
            }

            color.a = 1f;
            m_Background.color = color;
            SetLogoAlpha(1f);
        }

        private void SetZoom(float elapsed)
        {
            float duration = m_FadeDuration + m_DisplayDuration;
            float t = Mathf.Clamp01(elapsed / duration);
            m_Background.rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * m_ZoomScale, t);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            float frameElapsed = 0f;
            int frameIndex = 0;
            bool animateLogoSheet = m_EnableLogoSequence && to < from && m_ExitFadeStarted && m_LogoSequence != null && m_LogoSequence.Length > 0;
            if (animateLogoSheet) m_LogoImage.sprite = m_LogoSequence[0];
            var color = m_Background.color;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (animateLogoSheet)
                {
                    frameElapsed += Time.unscaledDeltaTime;
                    if (frameElapsed >= duration / m_LogoSequence.Length)
                    {
                        frameElapsed = 0f;
                        frameIndex = Mathf.Min(frameIndex + 1, m_LogoSequence.Length - 1);
                        m_LogoImage.sprite = m_LogoSequence[frameIndex];
                    }
                }
                color.a = Mathf.Lerp(from, to, elapsed / duration);
                m_Background.color = color;
                SetLogoAlpha(color.a);
                yield return null;
            }
            color.a = to;
            m_Background.color = color;
            SetLogoAlpha(to);
        }

        private void SetLogoAlpha(float alpha)
        {
            if (m_LogoImage)
            {
                var color = m_LogoImage.color;
                color.a = alpha;
                m_LogoImage.color = color;
            }

        }

        private Sprite FirstLogoSprite()
        {
            return m_EnableLogoSequence && m_LogoSequence != null && m_LogoSequence.Length > 0 ? m_LogoSequence[0] : m_Logo;
        }
    }
}
