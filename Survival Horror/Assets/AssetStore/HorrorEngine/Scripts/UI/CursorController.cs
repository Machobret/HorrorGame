using UnityEngine;
using System;

namespace HorrorEngine
{
    [Serializable]
    public class CursorState
    {
        public CursorLockMode Mode;
        public bool Visible;
    }

    public class CursorController : SingletonBehaviour<CursorController>
    {
        [SerializeField] bool m_StartInUI;
        [SerializeField] CursorState m_InUIState;
        [SerializeField] CursorState m_OutOfUIState;

        private int m_InUICount;
        private bool m_UseSoftwareCursor;

        public void SetSoftwareCursorActive(bool active)
        {
            m_UseSoftwareCursor = active;
            Cursor.visible = !active && (m_InUICount > 0 ? m_InUIState.Visible : m_OutOfUIState.Visible);
        }

        protected override void Awake()
        {
            base.Awake();
        
            if (!m_StartInUI)
                SetNonUICursor();
        }

        public void SetInUI(bool inUI)
        {
            if (!inUI)
            {
                --m_InUICount;
                Debug.Assert(m_InUICount >= 0, "Cursor InUI count went negative. This shouldn't happen. Something is calling SetInUI multiple times with the same value");

                if (m_InUICount == 0)
                {
                    SetNonUICursor();
                }
            }
            else
            {
                SetUICursor();
                 ++m_InUICount;
            }
        }

        private void SetNonUICursor()
        {
            Cursor.lockState = m_OutOfUIState.Mode;
            Cursor.visible = !m_UseSoftwareCursor && m_OutOfUIState.Visible;
        }

        private void SetUICursor()
        {
            Cursor.lockState = m_InUIState.Mode;
            Cursor.visible = !m_UseSoftwareCursor && m_InUIState.Visible;
        }

    }
}
