using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace HorrorEngine
{
    public class UIDocument : MonoBehaviour
    {
        [SerializeField] private Image m_Image;
        [SerializeField] private Image m_PageImage;
        [SerializeField] private TextMeshProUGUI m_Text;
        [SerializeField] private TextMeshProUGUI m_PageNumber;
        [SerializeField] private TextMeshProUGUI m_PageImageCaption;
        [SerializeField] private Graphic m_PreviousArrow;
        [SerializeField] private Graphic m_NextArrow;
        [SerializeField] private Graphic m_ExitLabel;
        [SerializeField] private float m_ImageFadeInTime = 0.25f;
        [SerializeField] private float m_ImageFadeOutTime = 0.15f;
        [SerializeField] private Color m_ImageFadeOutColor = new Color(0,0,0,0);

        [Header("Callbacks")]
        [SerializeField] private UnityEvent m_OnFirstPageShown;
        [SerializeField] private UnityEvent m_OnMidPageShown;
        [SerializeField] private UnityEvent m_OnLastPageShown;
        [SerializeField] private UnityEvent m_OnUniquePageShown;

        private DocumentData m_CurrentData;
        private int m_CurrentPage;
        private int m_OpenedFrame;
        private bool m_PreviousSelected;
        private float m_HighlightStarted;

        private Color m_DefaultImageColor;
        // --------------------------------------------------------------------

        private void Awake()
        {
            gameObject.SetActive(false);

            m_DefaultImageColor = m_PageImage.color;
        }

        // --------------------------------------------------------------------

        public void Show(DocumentData data)
        {
            m_CurrentData = data;
            if(GameManager.Exists)GameManager.Instance.Inventory.ReadDocuments.Add(data);
            PauseController.Instance.Pause(this);
            
            Sprite cover = data.Image;
            m_Image.sprite = cover;
            m_Image.gameObject.SetActive(data.ShowImageOnRead && cover);
            m_PageNumber.gameObject.SetActive(data.ShowPageCount);

            m_CurrentPage = -1;
            m_OpenedFrame = Time.frameCount;
            SelectPrevious(false);
            m_PageImage.color = m_ImageFadeOutColor;
            
            gameObject.SetActive(true);
            if (data.ShowClip)
                UIManager.Get<UIAudio>().Play(data.ShowClip);
            ShowNextPage();
            UpdateNavigationHighlight();
        }

        // --------------------------------------------------------------------

        private void ShowNextPage()
        {
            ++m_CurrentPage;
            FillPage(m_CurrentPage);
            if (m_CurrentPage > 0 && m_CurrentData.PageClip)
                UIManager.Get<UIAudio>().Play(m_CurrentData.PageClip);
        }

        // --------------------------------------------------------------------

        private void ShowPrevPage()
        {
            m_CurrentPage = Mathf.Max(0, m_CurrentPage-1);
            FillPage(m_CurrentPage);
            if (m_CurrentPage > 0 && m_CurrentData.PageClip)
                UIManager.Get<UIAudio>().Play(m_CurrentData.PageClip);
        }

        // --------------------------------------------------------------------

        private void FillPage(int index)
        {
            if (index == 0 && m_PreviousSelected)
                SelectPrevious(false);

            var page = m_CurrentData.Pages[m_CurrentPage];
            m_Text.text = page.Text;
            m_PageImageCaption.text = page.Caption;
            m_PageNumber.text = $"{index + 1}/{m_CurrentData.Pages.Count}";

            if (page.Image)
            {
                if (page.ChangeImageColor)
                    StartCoroutine(FadeImageColor(page.ImageColor, m_ImageFadeInTime));
                else
                    StartCoroutine(FadeImageColor(m_DefaultImageColor, m_ImageFadeInTime));

                m_PageImage.sprite = page.Image;
            }
            else if (m_PageImage.color != m_ImageFadeOutColor)
            {
                StartCoroutine(FadeImageColor(m_ImageFadeOutColor, m_ImageFadeOutTime));
            }

            if (m_CurrentData.Pages.Count == 1)
            {
                m_OnUniquePageShown?.Invoke();
            }
            else if (index == 0)
            {
                m_OnFirstPageShown?.Invoke();
            }
            else if (index == m_CurrentData.Pages.Count - 1)
            {
                m_OnLastPageShown?.Invoke();
            }
            else
            {
                m_OnMidPageShown?.Invoke();
            }
        }

        // --------------------------------------------------------------------

        IEnumerator FadeImageColor(Color toColor, float time)
        {
            float t = 0;
            Color originalColor = m_PageImage.color;
            while(t < time)
            {
                t += Time.unscaledDeltaTime;
                m_PageImage.color = Color.Lerp(originalColor, toColor, t / time);
                yield return Yielders.EndOfFrame;
            }
            m_PageImage.color = toColor;
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (!m_CurrentData || Time.frameCount == m_OpenedFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.dKey.wasPressedThisFrame)
                    SelectPrevious(false);
                else if (m_CurrentPage > 0 && keyboard.aKey.wasPressedThisFrame)
                    SelectPrevious(true);
            }

            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (m_PreviousSelected)
                    ShowPrevPage();
                else if (m_CurrentPage == m_CurrentData.Pages.Count - 1)
                    Hide();
                else
                    ShowNextPage();
            }

            if (m_CurrentData) UpdateNavigationHighlight();
        }

        private void SelectPrevious(bool previous)
        {
            m_PreviousSelected = previous;
            m_HighlightStarted = Time.unscaledTime;
        }

        private void UpdateNavigationHighlight()
        {
            bool lastPage = m_CurrentPage == m_CurrentData.Pages.Count - 1;
            float blink = Mathf.Repeat(Time.unscaledTime - m_HighlightStarted, .62f) < .36f ? 1f : .4f;
            var yellow = new Color(1f, .82f, .24f, blink);
            if (m_PreviousArrow) m_PreviousArrow.color = m_PreviousSelected ? yellow : Color.white;
            if (m_NextArrow) m_NextArrow.color = !m_PreviousSelected && !lastPage ? yellow : Color.white;
            if (m_ExitLabel) m_ExitLabel.color = !m_PreviousSelected && lastPage ? yellow : Color.white;
        }

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            UIManager.Get<UIAudio>().Play(m_CurrentData.CloseClip);
            gameObject.SetActive(false);
            m_CurrentData = null;

            UIManager.PopAction();
        }

    }
}
