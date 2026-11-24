using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.General
{
    public sealed class MainMenuBackgroundCarousel : MonoBehaviour
    {
        [SerializeField] private Sprite m_Logo;
        [SerializeField] private Sprite m_Border;
        [SerializeField] private bool m_EnableLogoSequence;
        [SerializeField] private Sprite[] m_LogoSequence;
        [SerializeField, Range(0f, 1f)] private float m_BorderOpacity = 1f;
        [SerializeField, Min(.1f)] private float m_ExitFadeDuration = 2.5f;

        private Image m_LogoImage;
        private bool m_ExitFadeStarted;
        private float m_LogoHomeAlpha;
        private Coroutine m_LogoSetupFade;

        private void Awake()
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Logo")
                {
                    m_LogoImage = image;
                    image.sprite = FirstLogoSprite();
                    m_LogoHomeAlpha = image.color.a;
                }
                else if (image.name == "Background Border" && m_Border)
                {
                    image.sprite = m_Border;
                    image.color = new Color(1f, 1f, 1f, m_BorderOpacity);
                    image.raycastTarget = false;
                }
            }
        }

        public void PlayExitFade()
        {
            if (m_ExitFadeStarted || !m_LogoImage) return;
            m_ExitFadeStarted = true;
            if (m_LogoSetupFade != null) StopCoroutine(m_LogoSetupFade);
            StartCoroutine(FadeLogo());
        }

        public void FadeLogoForNewGameSetup(bool showSetup, System.Action onComplete = null)
        {
            if (!m_LogoImage || m_ExitFadeStarted)
            {
                onComplete?.Invoke();
                return;
            }
            if (m_LogoSetupFade != null) StopCoroutine(m_LogoSetupFade);
            m_LogoSetupFade = StartCoroutine(FadeSetupLogo(showSetup ? 0f : m_LogoHomeAlpha, onComplete));
        }

        private IEnumerator FadeSetupLogo(float targetAlpha, System.Action onComplete)
        {
            Color color = m_LogoImage.color;
            float initialAlpha = color.a;
            const float duration = .28f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                color.a = Mathf.Lerp(initialAlpha, targetAlpha, t);
                m_LogoImage.color = color;
                yield return null;
            }
            color.a = targetAlpha;
            m_LogoImage.color = color;
            m_LogoSetupFade = null;
            onComplete?.Invoke();
        }

        private IEnumerator FadeLogo()
        {
            float duration = Mathf.Max(.1f, m_ExitFadeDuration);
            bool animate = m_EnableLogoSequence && m_LogoSequence != null && m_LogoSequence.Length > 0;
            Color color = m_LogoImage.color;
            float initialAlpha = color.a;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = elapsed / duration;
                if (animate)
                    m_LogoImage.sprite = m_LogoSequence[Mathf.Min((int)(progress * m_LogoSequence.Length), m_LogoSequence.Length - 1)];
                color.a = Mathf.Lerp(initialAlpha, 0f, progress);
                m_LogoImage.color = color;
                yield return null;
            }
            color.a = 0f;
            m_LogoImage.color = color;
        }

        private Sprite FirstLogoSprite()
        {
            return m_EnableLogoSequence && m_LogoSequence != null && m_LogoSequence.Length > 0 ? m_LogoSequence[0] : m_Logo;
        }
    }
}
