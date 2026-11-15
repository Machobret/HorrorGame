using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class UnityEventInteractive : UnityEvent<Interactive> { }

    public class Interaction3DContext : MonoBehaviour, IInteractor
    {
        public UnityEventInteractive OnSelected;
        public UnityEventInteractive OnHover;
        public UnityEvent OnActive;
        public UnityEvent OnCancel;

        [SerializeField] Interactive[] m_Interactives;
        [SerializeField] Transform m_Cursor;
        [SerializeField] Interactive m_DefaultSelection;

        private IUIInput m_UIInput;
        private Interactive m_SelectedInteractive;
        private bool m_CanSelect;
        private bool m_HasBeenActivated;
        private int m_ActivationVersion;
        // Owned panels can supply direct keyboard input without a second Update processing it.
        public bool ExternalInput { get; set; }
        public Interactive Selected => m_SelectedInteractive;

        // --------------------------------------------------------------------

        void Start()
        {
            m_UIInput = UIManager.Instance.GetComponent<IUIInput>();

            if (!m_HasBeenActivated)
            {
                Deactivate();
            }
        }

        // --------------------------------------------------------------------


        public void Activate()
        {
            m_HasBeenActivated = true;
            int version = ++m_ActivationVersion;
            m_SelectedInteractive = null;
            this.InvokeActionNextFrame(() =>
            {
                if (!enabled || version != m_ActivationVersion) return;
                foreach (var interactive in m_Interactives)
                {
                    interactive.enabled = true;
                }

                m_SelectedInteractive = m_DefaultSelection;

                m_Cursor.gameObject.SetActive(true);
                m_Cursor.transform.position = m_DefaultSelection.transform.position;
            });

            enabled = true;
        }

        // --------------------------------------------------------------------

        public void Deactivate()
        {
            ++m_ActivationVersion;
            foreach(var interactive in m_Interactives)
            {
                interactive.enabled = false;
            }

            m_Cursor.gameObject.SetActive(false);

            enabled = false;
        }

        // --------------------------------------------------------------------

        void Update()
        {
            if (ExternalInput) return;
            Vector2 axis = m_UIInput.GetPrimaryAxis();
            if (axis.magnitude > 0.5)
            {
                if (m_CanSelect)
                {
                    Interactive interactive = FindTargetInteractive(axis);
                    if (interactive)
                    {
                        m_SelectedInteractive = interactive;
                        m_Cursor.transform.position = interactive.transform.position;
                        m_CanSelect = false;
                        OnHover?.Invoke(m_SelectedInteractive);
                    }
                }
            }
            else
            {
                m_CanSelect = true;
            }

            if (m_UIInput.IsCancelDown())
            {
                Deactivate();
                OnCancel?.Invoke();
            }

            if (m_UIInput.IsConfirmDown())
            {
                if (m_SelectedInteractive)
                {
                    OnSelected?.Invoke(m_SelectedInteractive);
                    m_SelectedInteractive.OnInteract?.Invoke(this);
                }
            }
        }
        
        // --------------------------------------------------------------------

        private Interactive FindTargetInteractive(Vector3 desiredDir)
        {
            float minDistance = float.MaxValue;
            Interactive canditate = null;
            Vector3 cursorOnScreen = Camera.main.WorldToScreenPoint(m_Cursor.position);
            foreach (var interactive in m_Interactives)
            {
                if (interactive == m_SelectedInteractive)
                    continue;


                Vector3 interactiveOnScreen = Camera.main.WorldToScreenPoint(interactive.transform.position);
                Vector3 dirToButton = interactiveOnScreen - cursorOnScreen;
                Vector3 projection = Vector3.Project(dirToButton, desiredDir);
                if (Vector3.Dot(desiredDir, dirToButton.normalized) <= 0)
                    continue;

                float dist = Vector3.Distance(projection, dirToButton) + dirToButton.magnitude;

                if (dist < minDistance)
                {
                    minDistance = dist;
                    canditate = interactive;
                }
            }
            return canditate;
        }

        public void Navigate(Vector2 direction)
        {
            if (!enabled || !m_SelectedInteractive) return;
            var target = FindTargetInteractive(direction);
            if (target) Select(target);
        }

        public void Select(Interactive target)
        {
            if (!enabled || !target || !target.enabled) return;
            m_SelectedInteractive = target;
            m_Cursor.position = target.transform.position;
            OnHover?.Invoke(target);
        }

        public void PressSelected()
        {
            if (!enabled || !m_SelectedInteractive) return;
            OnSelected?.Invoke(m_SelectedInteractive);
            m_SelectedInteractive.OnInteract?.Invoke(this);
        }

        // --------------------------------------------------------------------

        public void SetDefault(Interactive interactive)
        {
            m_DefaultSelection = interactive;
        }

        // --------------------------------------------------------------------

        public void SetInteractives(Interactive[] interactives)
        {
            bool wasEnabled = enabled;
            if (enabled)
                Deactivate();

            m_Interactives = interactives;

            if (wasEnabled)
                Activate();
        }
    }
}
