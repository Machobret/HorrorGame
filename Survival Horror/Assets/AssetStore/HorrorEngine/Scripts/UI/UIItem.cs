using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class UIItem : MonoBehaviour
    {
        [SerializeField] private Image m_Image;
        [SerializeField] private GameObject m_InfoPanel;
        [SerializeField] private TextMeshProUGUI m_Text;
        
        [Header("Examination")]
        [SerializeField] private bool OpenExamination = true;
        [SerializeField] private bool InteractDuringExamination = true;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_CloseClip;

        private IUIInput m_Input;
        private System.Action m_OnDismissed;
        private int m_ShownFrame;
        private bool m_ShowingExamination;
        

        // --------------------------------------------------------------------

        void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();
            MessageBuffer<ItemPickedUpMessage>.Subscribe(OnItemPickedUp);
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<ItemPickedUpMessage>.Unsubscribe(OnItemPickedUp);
        }

        // --------------------------------------------------------------------

        void OnItemPickedUp(ItemPickedUpMessage msg)
        {
            if (!msg.ShowItemSplash)
                return;

            var itemData = msg.Data;
            var onDismissed = msg.OnSplashDismissed;
            bool playShowSound = !msg.SuppressSplashSound;

            if (gameObject.activeInHierarchy)
            {
                UIManager.PushAction(new UIStackedAction()
                {
                    Action = () => { Show(itemData, onDismissed, playShowSound); },
                    StopProcessingActions = true,
                    Name = "UIItem.OnItemPickeUp (Show)"
                });
            }
            else
            {
                Show(itemData, onDismissed, playShowSound);
            }
        }

        // --------------------------------------------------------------------

        public void Show(ItemData item)
        {
            Show(item, null);
        }

        internal void Show(ItemData item, System.Action onDismissed, bool playShowSound = true)
        {
            BeginSplash(onDismissed, OpenExamination && item.ExamineModel);

            if (m_ShowingExamination)
            {
                this.InvokeActionNextFrame(() => // This is needed in case we come from UIExamineItem itself
                {
                    UIManager.Get<UIExamineItem>().Show(item, InteractDuringExamination, false);
                });
            }
            else
            {
                m_Image.sprite = item.Image;
                m_Text.text = item.Name;
            }

            if (playShowSound)
                UIManager.Get<UIAudio>().Play(m_ShowClip);
        }

        // --------------------------------------------------------------------

        internal void ShowDocumentPickup(DocumentData document, System.Action onDismissed)
        {
            m_Image.sprite = document.Image;
            m_Text.text = document.Name;
            BeginSplash(onDismissed, false);
            if (m_ShowClip) UIManager.Get<UIAudio>().Play(m_ShowClip);
        }

        private void BeginSplash(System.Action onDismissed, bool examine)
        {
            m_OnDismissed = onDismissed;
            m_ShownFrame = Time.frameCount;
            m_ShowingExamination = examine;
            PauseController.Instance.Pause(this);
            m_Image.gameObject.SetActive(!examine);
            m_InfoPanel.SetActive(!examine);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (m_ShownFrame == Time.frameCount) return;
            bool isDismissed = m_Input.IsConfirmDown() || m_Input.IsCancelDown();
            if (isDismissed)
            {
                Hide();
            }
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            var onDismissed = m_OnDismissed;
            m_OnDismissed = null;
            if (m_ShowingExamination)
            {
                UIManager.Get<UIExamineItem>().Hide();
            }

            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);

            if (!UIManager.PopAction())
                UIManager.Get<UIAudio>().Play(m_CloseClip);

            if (onDismissed != null)
                UIManager.Instance.InvokeActionNextFrame(onDismissed);
        }
    }
}
