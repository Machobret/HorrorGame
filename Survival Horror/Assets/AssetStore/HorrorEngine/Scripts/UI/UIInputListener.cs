using UnityEngine;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class UIInputListener : MonoBehaviour
    {
        private IUIInput m_Input;

        private UIInventory m_Inventory;
        private UIMap m_Map;
        private UIPause m_Pause;

        private HashSet<Object> m_BlockingContext = new HashSet<Object>();
        // Some scripted sequences need the normal UI blocked while still allowing
        // the player to pause. Keep those contexts separate from the default,
        // fully-blocking behavior.
        private HashSet<Object> m_PauseAllowedBlockingContext = new HashSet<Object>();

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponent<IUIInput>();
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            // TODO - This can be redone a bit better now that we support UIContexts

            if (GameManager.Instance.IsPlaying)
            {
                if (m_BlockingContext.Count == 0 && m_Input.IsToggleInventoryDown())
                {
                    if (!m_Inventory)
                        m_Inventory = GetComponentInChildren<UIInventory>(true);

                    if (m_Inventory.CanBeShown())
                        m_Inventory?.Show();
                }

                if (m_BlockingContext.Count == 0 && m_Input.IsToggleMapDown())
                {
                    if (!m_Map)
                        m_Map = GetComponentInChildren<UIMap>(true);

                    if (m_Map.CanBeShown())
                        m_Map?.Show();
                }

                if (m_Input.IsTogglePauseDown())
                {
                    if (!m_Pause)
                        m_Pause = GetComponentInChildren<UIPause>(true);

                    if (m_Pause.CanBeShown())
                        m_Pause?.Show();
                }
            }
            else
            {
                if (m_Input.IsTogglePauseDown())
                {
                     
                    if (!m_Pause)
                        m_Pause = GetComponentInChildren<UIPause>(true);

                    if (PauseController.Instance.Context == m_Pause)
                        m_Pause?.Hide();
                }
            }
        }

        // --------------------------------------------------------------------

        public void AddBlockingContext(Object context)
        {
            m_BlockingContext.Add(context);

            RefreshEnabledState();
        }

        /// <summary>
        /// Blocks inventory and map UI for a scripted context. When
        /// <paramref name="allowPause"/> is true, the pause menu remains usable.
        /// </summary>
        public void AddBlockingContext(Object context, bool allowPause)
        {
            m_BlockingContext.Add(context);
            if (allowPause)
                m_PauseAllowedBlockingContext.Add(context);

            RefreshEnabledState();
        }

        // --------------------------------------------------------------------

        public void RemoveBlockingContext(Object context)
        {
            m_BlockingContext.Remove(context);
            m_PauseAllowedBlockingContext.Remove(context);

            RefreshEnabledState();
        }

        private void RefreshEnabledState()
        {
            enabled = m_BlockingContext.Count == 0 || m_PauseAllowedBlockingContext.Count > 0;
        }

        // --------------------------------------------------------------------

        internal bool IsBlocked()
        {
            return m_BlockingContext.Count > 0;
        }
    }
}
