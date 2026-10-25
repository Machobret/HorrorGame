using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UISettings : MonoBehaviour
    {
        [SerializeField] RectTransform LayoutGroup;

        private IUIInput m_Input;

        public UnityEvent OnClose;
        
        private GameObject m_PrevSelected;
        private UISelectableCallbacks m_DefaultSelection;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();
        }

        // ----------------------------------------------------------------
        
        private void OnEnable()
        {
            m_PrevSelected = EventSystem.current.currentSelectedGameObject;
            this.InvokeActionNextFrame(() =>
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(LayoutGroup);
                m_DefaultSelection = GetComponentInChildren<UISelectableCallbacks>();
            });
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            gameObject.SetActive(false);
            OnClose?.Invoke();
            EventSystem.current.SetSelectedGameObject(m_PrevSelected);
        }

        // ----------------------------------------------------------------

        private void Update()
        {
            if (m_Input.IsCancelDown() && !InputBindingSettings.ShouldBlockMenuCancel)
            {
                DiscardSettings();
                Hide();
            }

            EventSystemUtils.SelectDefaultOnLostFocus(m_DefaultSelection.gameObject);
        }

        // ----------------------------------------------------------------

        public void ApplySettings()
        {
            SettingsManager.Instance.SaveAndApply();
            InputBindingSettings.SaveActive();
        }

        // ----------------------------------------------------------------

        public void DiscardSettings()
        {
            SettingsManager.Instance.Discard();
            InputBindingSettings.DiscardActive();
        }

        // ----------------------------------------------------------------

        public void ResetDefaults()
        {
            SettingsManager.Instance.ResetToDefaults();
            InputBindingSettings.ResetActive();

            foreach (var element in GetComponentsInChildren<UISettingsElement>(true))
            {
                if (element.Content != null) element.Fill(element.Content);
            }

            GetComponent<SettingsMenuEnhancer>()?.RefreshBindingLabels();
        }
    }
}
