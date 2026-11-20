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

        private void Awake()
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Logo")
                {
                    m_LogoImage = image;
                    image.sprite = FirstLogoSprite();
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
            StartCoroutine(FadeLogo());
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
