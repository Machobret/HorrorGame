using UnityEngine;
using System.Collections.Generic;
using HorrorGame.Chapter1;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorEngine
{
    public class UIInputListener : MonoBehaviour
    {
        private IUIInput m_Input;

        private UIInventory m_Inventory;
        private UIMap m_Map;
        private UIPause m_Pause;
        private HorrorGame.UI.PinnedDocumentsOverlay m_PinnedDocuments;

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
            if (!GameManager.Exists || !GameManager.Instance.Player) return;
            if (PinnedDocumentsHaveFocus)
            {
                // The reader can release focus before this Update sees the key that closed it.
                if (m_PinnedDocuments.IsVisible && CanShowPinnedDocuments()) UpdatePinnedDocumentsInput();
                return;
            }
            if (HandleMenuInput()) return;
            if (IsBlocked() && m_PauseAllowedBlockingContext.Count == 0) return;

            // TODO - This can be redone a bit better now that we support UIContexts
            // Escape belongs to the UI Cancel action, while P/Start is the
            // configurable pause action. In gameplay there is no active UI to
            // cancel, so accept either action to open the pause screen. Once a
            // UI owns the pause context, its own cancel handler remains in charge.
            bool pausePressed = m_Input.IsTogglePauseDown() || m_Input.IsCancelDown();

            if (!pausePressed && IsPinnedDocumentsShortcutDown() && CanShowPinnedDocuments())
            {
                if (!m_Inventory) m_Inventory = GetComponentInChildren<UIInventory>(true);
                m_PinnedDocuments = m_PinnedDocuments ? m_PinnedDocuments
                    : gameObject.AddComponent<HorrorGame.UI.PinnedDocumentsOverlay>();
                m_PinnedDocuments.Show(GameManager.Instance.Inventory, m_Inventory ? m_Inventory.ReporterArt : null,
                    PauseController.Instance.Context as UIInventory);
                return;
            }

            if (GameManager.Instance.IsPlaying)
            {
                if (pausePressed)
                {
                    if (!m_Pause)
                        m_Pause = GetComponentInChildren<UIPause>(true);

                    if (m_Pause.CanBeShown())
                        m_Pause?.Show();
                }
            }
            else
            {
                if (pausePressed)
                {
                     
                    if (!m_Pause)
                        m_Pause = GetComponentInChildren<UIPause>(true);

                    if (PauseController.Instance.Context == m_Pause)
                        m_Pause?.Hide();
                }
            }
        }

        private void LateUpdate()
        {
            if (m_PinnedDocuments)
                m_PinnedDocuments.Tick(CanShowPinnedDocuments() ? GameManager.Instance.Inventory : null);
        }

        private void OnDisable()
        {
            if (m_PinnedDocuments) m_PinnedDocuments.Dismiss();
        }

        private bool PinnedDocumentsHaveFocus => m_PinnedDocuments && PauseController.Exists &&
            PauseController.Instance.Context == m_PinnedDocuments;

        private static bool ReporterInventoryHasFocus => PauseController.Exists &&
            PauseController.Instance.Context is UIInventory inventory && inventory.isActiveAndEnabled && inventory.UsesReporterMenu;

        private bool CanShowPinnedDocuments() => !IsBlocked() && GameManager.Exists && GameManager.Instance.Player &&
            GameManager.Instance.CharacterState != null &&
            (GameManager.Instance.IsPlaying || PinnedDocumentsHaveFocus || ReporterInventoryHasFocus);

        private void UpdatePinnedDocumentsInput()
        {
            if (m_Input.IsCancelDown() || IsInventoryShortcutDown() || IsPinnedDocumentsShortcutDown())
            {
                m_PinnedDocuments.Collapse();
                return;
            }

            int direction = 0;
            bool readPressed = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                direction = (keyboard.dKey.wasPressedThisFrame ? 1 : 0) - (keyboard.aKey.wasPressedThisFrame ? 1 : 0);
                readPressed = keyboard.eKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            direction = (Input.GetKeyDown(KeyCode.D) ? 1 : 0) - (Input.GetKeyDown(KeyCode.A) ? 1 : 0);
            readPressed = Input.GetKeyDown(KeyCode.E);
#endif
            if (direction != 0) m_PinnedDocuments.MoveSelection(direction);
            if (readPressed) m_PinnedDocuments.ReadSelected(GameManager.Instance.Inventory);
        }

        private static bool IsPinnedDocumentsShortcutDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.cKey.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.C);
#else
            return false;
#endif
        }

        private bool IsInventoryShortcutDown()
        {
            if (m_Input.IsToggleInventoryDown()) return true;
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.tabKey.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Tab);
#else
            return false;
#endif
        }

        private bool HandleMenuInput()
        {
            bool toggleInventory = IsInventoryShortcutDown();
            bool toggleMap = m_Input.IsToggleMapDown();
            bool returnToInventory = false;
#if ENABLE_INPUT_SYSTEM
            returnToInventory = Keyboard.current?.qKey.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            returnToInventory = Input.GetKeyDown(KeyCode.Q);
#endif
            if (!toggleInventory && !toggleMap && !returnToInventory)
                return false;
            if (!CanNavigateMenus()) return false;

            if (toggleInventory)
            {
                if (IsOpen(m_Map)) m_Map.Hide();
                else if (IsOpen(m_Inventory)) m_Inventory.Hide();
                else OpenInventory();
            }
            else if (returnToInventory && IsOpen(m_Map))
                OpenInventory();
            else if (toggleMap)
            {
                if (IsOpen(m_Map)) m_Map.Hide();
                else OpenMap();
            }
            else return false;
            return true;
        }

        private static bool IsOpen(Behaviour view) => view && view.gameObject.activeInHierarchy;

        private bool CanNavigateMenus()
        {
            if (!GameManager.Exists || GameManager.Instance.CharacterState == null) return false;
            if (!m_Inventory) m_Inventory = GetComponentInChildren<UIInventory>(true);
            if (!m_Map) m_Map = GetComponentInChildren<UIMap>(true);
            var context = PauseController.Instance.Context;
            if (context) return context == m_Inventory || context == m_Map;
            return !IsBlocked() && GameManager.Instance.IsPlaying;
        }

        internal void OpenInventory()
        {
            if (!CanNavigateMenus() || !m_Inventory || IsOpen(m_Inventory)) return;
            if (IsOpen(m_Map)) m_Map.Hide();
            if (!PauseController.Instance.IsPaused && m_Inventory.CanBeShown()) m_Inventory.Show();
        }

        internal void OpenMap()
        {
            if (!Chapter1MapTutorial.CanOpenMap || !CanNavigateMenus() || !m_Map) return;
            if (IsOpen(m_Map))
            {
                m_Map.OpenMapPage();
                return;
            }
            if (IsOpen(m_Inventory)) m_Inventory.Hide();
            if (!PauseController.Instance.IsPaused && m_Map.CanBeShown()) m_Map.Show();
        }

        // --------------------------------------------------------------------

        public void AddBlockingContext(Object context)
        {
            m_BlockingContext.Add(context);
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

        }

        // --------------------------------------------------------------------

        public void RemoveBlockingContext(Object context)
        {
            m_BlockingContext.Remove(context);
            m_PauseAllowedBlockingContext.Remove(context);

        }

        // --------------------------------------------------------------------

        internal bool IsBlocked()
        {
            return m_BlockingContext.Count > 0;
        }
    }
}
