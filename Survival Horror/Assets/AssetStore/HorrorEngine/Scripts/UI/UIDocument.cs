using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        [Header("Callbacks")]
        [SerializeField] private UnityEvent m_OnFirstPageShown;
        [SerializeField] private UnityEvent m_OnMidPageShown;
        [SerializeField] private UnityEvent m_OnLastPageShown;
        [SerializeField] private UnityEvent m_OnUniquePageShown;

        private DocumentData m_CurrentData;
        private Sprite m_Cover;
        // Page zero is the first text block. The cover is the page immediately before it.
        private int m_CurrentPage;
        private int m_OpenedFrame;
        private IUIInput m_Input;
        private GameObject m_ArtworkView;
        private Image m_Artwork;
        private TextMeshProUGUI m_PreviousKey;
        private TextMeshProUGUI m_NextKey;

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();
            gameObject.SetActive(false);
        }

        public void Show(DocumentData data) => Show(data, playShowSound: true);

        internal void Show(DocumentData data, bool playShowSound)
        {
            if (!data || data.Pages == null || data.Pages.Count == 0) return;
            m_CurrentData = data;
            if (GameManager.Exists) GameManager.Instance.Inventory.ReadDocuments.Add(data);
            PauseController.Instance.Pause(this);

            var firstPage = data.Pages[0];
            m_Cover = firstPage.Artwork;
            if (!m_Cover) m_Cover = data.Image;
            if (!m_Cover) m_Cover = firstPage.Image;
            m_OpenedFrame = Time.frameCount;

            gameObject.SetActive(true);
            if (!m_ArtworkView) CreateArtworkView();
            m_Image.gameObject.SetActive(false);
            m_PageImage.gameObject.SetActive(false);
            if (playShowSound && data.ShowClip) UIManager.Get<UIAudio>().Play(data.ShowClip);
            ShowPage(m_Cover ? -1 : 0);
        }

        private void ShowPage(int index)
        {
            m_CurrentPage = index;
            bool cover = index < 0;
            m_ArtworkView.SetActive(cover);
            m_Artwork.sprite = cover ? m_Cover : null;
            m_PageNumber.gameObject.SetActive(!cover && m_CurrentData.ShowPageCount);
            m_Text.gameObject.SetActive(false);
            m_PageImageCaption.gameObject.SetActive(false);

            int lastPage = m_CurrentData.Pages.Count - 1;
            if (!cover)
            {
                var page = m_CurrentData.Pages[index];
                m_Text.text = page.Text;
                m_PageImageCaption.text = page.Caption;
                m_Text.gameObject.SetActive(!string.IsNullOrWhiteSpace(m_Text.text));
                m_PageImageCaption.gameObject.SetActive(!string.IsNullOrWhiteSpace(m_PageImageCaption.text));
                m_PageNumber.text = $"{index + 1}/{lastPage + 1}";
            }

            if (cover) m_OnFirstPageShown?.Invoke();
            else if (!m_Cover && lastPage == 0) m_OnUniquePageShown?.Invoke();
            else if (index == lastPage) m_OnLastPageShown?.Invoke();
            else if (!m_Cover && index == 0) m_OnFirstPageShown?.Invoke();
            else m_OnMidPageShown?.Invoke();

            bool canGoBack = index > (m_Cover ? -1 : 0);
            bool canGoForward = index < lastPage;
            m_PreviousArrow.gameObject.SetActive(canGoBack);
            m_PreviousKey.gameObject.SetActive(canGoBack);
            m_NextArrow.gameObject.SetActive(canGoForward);
            m_NextKey.gameObject.SetActive(canGoForward);
            m_ExitLabel.gameObject.SetActive(!canGoForward);
            if (!canGoForward && m_ExitLabel is TMP_Text exit)
            {
                exit.text = "E  " + GameText.Translate("EXIT");
                exit.rectTransform.anchoredPosition = new Vector2(-74f, exit.rectTransform.anchoredPosition.y);
                exit.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 144f);
            }
        }

        private void CreateArtworkView()
        {
            var dimmer = new GameObject("Document cover", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            m_ArtworkView = dimmer.gameObject;
            Fit(dimmer.rectTransform, transform, Vector2.zero, Vector2.one);
            dimmer.color = new Color(.025f, .024f, .021f, .98f);
            dimmer.raycastTarget = false;

            m_Artwork = new GameObject("Printed page", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Fit(m_Artwork.rectTransform, dimmer.transform, new Vector2(.17f, .055f), new Vector2(.83f, .945f));
            m_Artwork.preserveAspect = true;
            m_Artwork.raycastTarget = false;

            m_PreviousArrow.transform.SetAsLastSibling();
            m_NextArrow.transform.SetAsLastSibling();
            m_ExitLabel.transform.SetAsLastSibling();
            m_PreviousKey = CreateKeyLabel(m_PreviousArrow, "Q", 29f);
            m_NextKey = CreateKeyLabel(m_NextArrow, "E", -29f);
        }

        private TextMeshProUGUI CreateKeyLabel(Graphic arrow, string key, float keyOffset)
        {
            var text = new GameObject(key + " Key", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var arrowRect = arrow.rectTransform;
            var rect = text.rectTransform;
            rect.SetParent(arrowRect.parent, false);
            rect.anchorMin = arrowRect.anchorMin;
            rect.anchorMax = arrowRect.anchorMax;
            rect.sizeDelta = new Vector2(20f, 28f);
            rect.anchoredPosition = arrowRect.anchoredPosition + Vector2.right * keyOffset;
            text.font = m_Text.font;
            text.fontSize = 17f;
            text.text = key;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Fit(RectTransform rect, Transform parent, Vector2 min, Vector2 max)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (!m_CurrentData || Time.frameCount == m_OpenedFrame || m_Input == null) return;
            if (m_Input.IsCancelDown())
            {
                Hide();
                return;
            }

#if ENABLE_INPUT_SYSTEM
            bool back = Keyboard.current?.qKey.wasPressedThisFrame == true;
#else
            bool back = Input.GetKeyDown(KeyCode.Q);
#endif
            if (back || m_Input.IsPrevSubmapDown())
            {
                if (m_CurrentPage > (m_Cover ? -1 : 0)) TurnPage(m_CurrentPage - 1);
            }
            else if (m_Input.IsConfirmDown())
            {
                if (m_CurrentPage < m_CurrentData.Pages.Count - 1) TurnPage(m_CurrentPage + 1);
                else Hide();
            }
        }

        private void TurnPage(int index)
        {
            ShowPage(index);
            if (m_CurrentData.PageClip) UIManager.Get<UIAudio>().Play(m_CurrentData.PageClip);
        }

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            UIManager.Get<UIAudio>().Play(m_CurrentData.CloseClip);
            gameObject.SetActive(false);
            m_CurrentData = null;
            m_Cover = null;
            m_Artwork.sprite = null;
            UIManager.PopAction();
        }
    }
}
