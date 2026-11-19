using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace HorrorEngine
{
    public class UIInventory : MonoBehaviour
    {
        [SerializeField] private bool m_PauseGame = true;
        [SerializeField] private GameObject m_Content;
        [SerializeField] private UIInventoryContextMenu m_ContextMenu;
        [SerializeField] private UIInventoryItem[] m_ItemSlots;
        [SerializeField] private UIItemFiller m_ItemInfoFiller;
        [SerializeField] private UIInventoryItem m_Equipped;
        [SerializeField] private UIInventoryItem m_EquippedSecondary;
        [SerializeField] private float m_ExpandingInteractionDelay = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_UseClip;
        [SerializeField] private AudioClip m_CantUseClip;
        [SerializeField] private AudioClip m_NavigateClip;
        [SerializeField] private AudioClip m_CloseClip;
        [SerializeField] private AudioClip m_ExpandClip;

        [Header("Advanced Customization")]
        [SerializeField] private bool m_SelectWithOneClick;
        [SerializeField] private bool m_AlwaysRebuild;
        [SerializeField] private bool m_RebuildOnlyOnCapacityChange;
        [SerializeField] private Transform m_RebuildItemParent;
        [SerializeField] private GameObject m_RebuildItemPrefab;

        public UnityEvent<string> OnFeedback=new UnityEvent<string>();
        public InventoryEntry CombiningEntry => m_CombiningSlot ? m_CombiningSlot.InventoryEntry : null;
        public InventoryEntry SelectedEntry => m_SelectedSlot ? m_SelectedSlot.InventoryEntry : null;
        [SerializeField,Range(0,1)] private float m_MenuVolume=1f;

        private UIInventoryItem m_SelectedSlot;
        private UIInventoryItem m_CombiningSlot;

        private IUIInput m_Input;
        private bool m_Expanding;
        private int m_OpenedFrame = -1;
        private UIContext m_UIContext;

        // --------------------------------------------------------------------

        protected void Awake()
        {
            m_UIContext = GetComponent<UIContext>();
            m_Input = GetComponentInParent<IUIInput>();

            foreach (var slot in m_ItemSlots)
            {
                RegisterItemCallbacks(slot);
            }

            // Equipment cells are status displays, not inventory cells.  The
            // actual item remains selectable from its original inventory
            // position; this mirrors the weapon slot and keeps the tool slot
            // out of mouse/keyboard navigation.
            SetEquipmentSlotDisplayOnly(m_Equipped);
            SetEquipmentSlotDisplayOnly(m_EquippedSecondary);

            m_ContextMenu.UseButton.onClick.AddListener(OnUse);
            m_ContextMenu.EquipButton.onClick.AddListener(OnEquip);
            m_ContextMenu.CombineButton.onClick.AddListener(OnCombine);
            m_ContextMenu.ExamineButton.onClick.AddListener(OnExamine);
            m_ContextMenu.DropButton.onClick.AddListener(OnDrop);
        }

        // --------------------------------------------------------------------

        void RegisterItemCallbacks(UIInventoryItem slot)
        {
            UISelectableCallbacks selectable = slot.GetComponent<UISelectableCallbacks>();
            selectable.OnSelected.AddListener(OnSlotSelected);

            UIPointerClickEvents pointerEvents = slot.GetComponent<UIPointerClickEvents>();
            if (!m_SelectWithOneClick)
                pointerEvents.OnDoubleClick.AddListener(OnSubmit);
        }

        // --------------------------------------------------------------------

        void UnregisterItemCallbacks(UIInventoryItem slot)
        {
            UISelectableCallbacks selectable = slot.GetComponent<UISelectableCallbacks>();
            selectable.OnSelected.RemoveListener(OnSlotSelected);

            UIPointerClickEvents pointerEvents = slot.GetComponent<UIPointerClickEvents>();
            if (!m_SelectWithOneClick)
                pointerEvents.OnDoubleClick.RemoveListener(OnSubmit);
        }

        private static void SetEquipmentSlotDisplayOnly(UIInventoryItem slot)
        {
            if (!slot) return;
            var button = slot.GetComponent<UnityEngine.UI.Button>();
            if (!button) return;
            button.interactable = false;
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
        }


        // --------------------------------------------------------------------

        void OnSlotSelected(GameObject obj)
        {
            if (m_SelectedSlot) // Cancel previous selection
            {
                if (m_ContextMenu.isActiveAndEnabled)
                {
                    m_ContextMenu.gameObject.SetActive(false);
                }
                
                if (m_SelectedSlot != m_CombiningSlot)
                    m_SelectedSlot.SetSelectionLocked(false);

                m_SelectedSlot = null;
            }

            UIInventoryItem slot = obj.GetComponent<UIInventoryItem>();
            m_SelectedSlot = slot;
            if (slot.InventoryEntry != null && slot.InventoryEntry.Item)
            {
                m_ItemInfoFiller.Fill(slot.InventoryEntry);
                UIManager.Get<UIAudio>().Play(m_NavigateClip,m_MenuVolume);
            }
            else
            {
                m_ItemInfoFiller.Clear();
            }

            if (m_SelectWithOneClick)
            {
                StartCoroutine(SubmitNextFrame());
            }
        }

        // --------------------------------------------------------------------

        IEnumerator SubmitNextFrame()
        {
            yield return null;
            OnSubmit();
        }

        // --------------------------------------------------------------------

        void Start()
        {
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        void Update()
        {
            if (m_Expanding || Time.frameCount == m_OpenedFrame)
                return;

            if (m_Input.IsCancelDown() || m_Input.IsToggleInventoryDown())
            {
                OnCancel();
            }

            if (m_SelectedSlot && m_SelectedSlot.InventoryEntry != null && (!EventSystem.current || EventSystem.current.currentSelectedGameObject==m_SelectedSlot.gameObject))
            {
                if (m_Input.IsConfirmDown())
                {
                    OnSubmit();
                }
            }

            if (!m_SelectWithOneClick)
            {
                var fallback=m_ItemSlots.FirstOrDefault(slot=>slot && slot.gameObject.activeInHierarchy && slot.GetComponent<UnityEngine.UI.Button>().IsInteractable());
                GameObject defaultSelection = m_SelectedSlot && m_SelectedSlot.GetComponent<UnityEngine.UI.Button>().IsInteractable() ? m_SelectedSlot.gameObject : (fallback ? fallback.gameObject : null);
                EventSystemUtils.SelectDefaultOnLostFocus(defaultSelection);
            }
        }

        // --------------------------------------------------------------------

        public void Show()
        {
            m_OpenedFrame = Time.frameCount;
            HorrorGame.UI.GameplayNotifications.DismissAll();
            RefreshMapTabVisibility(transform);
            if (m_PauseGame)
                PauseController.Instance.Pause(this);

            CursorController.Instance.SetInUI(true);
            
            m_SelectedSlot = null;
            m_CombiningSlot = null;
            m_Expanding = false;

            gameObject.SetActive(true); // Needs to happen before fill or animations wont play

            Fill();
            FillEquipped();
            
            m_ContextMenu.gameObject.SetActive(false);

            UIManager.Get<UIAudio>().Play(m_ShowClip,m_MenuVolume);

            if (m_Expanding)
            {
                UIManager.Get<UIAudio>().Play(m_ExpandClip,m_MenuVolume);
                this.InvokeActionUnscaled(EndExpansion, m_ExpandingInteractionDelay);
            }

            m_UIContext.Activate();
        }

        // --------------------------------------------------------------------

        void EndExpansion()
        {
            m_Expanding = false;
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            if (m_PauseGame)
                PauseController.Instance.Resume(this);

            CursorController.Instance.SetInUI(false);
            m_ContextMenu.gameObject.SetActive(false);
            gameObject.SetActive(false);

            m_UIContext.Deactivate();

            UIManager.PopAction();
        }

        // --------------------------------------------------------------------

        public void OnCancel()
        {
            UIManager.Get<UIAudio>().Play(m_CloseClip,m_MenuVolume);
            if (m_CombiningSlot)
            {
                CancelCombine();
            }
            else if (m_ContextMenu.isActiveAndEnabled)
            {
                CloseContextMenu();
            }
            else
            {
                Hide();
            }
        }

        // --------------------------------------------------------------------

        public void CloseContextMenu()
        {
            m_ContextMenu.gameObject.SetActive(false);
            if (m_SelectedSlot)
                m_SelectedSlot.SetSelectionLocked(false);
        }

        // --------------------------------------------------------------------

        private void CancelCombine()
        {
            OnFeedback.Invoke("Combination cancelled");
            m_CombiningSlot.SetSelectionLocked(false);
            m_CombiningSlot = null;
        }

        // --------------------------------------------------------------------

        private void OnSubmit()
        {
            if (m_SelectedSlot)
                m_SelectedSlot.SetSelectionLocked(false);

            m_SelectedSlot = EventSystem.current.currentSelectedGameObject.GetComponent<UIInventoryItem>();
            if (!IsInventorySlot(m_SelectedSlot)) // Selected obj is not an inventory item (this can rarely happen if a quick slot gets selected)
            {
                m_SelectedSlot = null;
                if (m_CombiningSlot)
                    CancelCombine();

                return;
            }

            if (!m_SelectedSlot)
                return;

            ItemData item = m_SelectedSlot.InventoryEntry.Item;
            if (item == null)
                return;

            if (m_CombiningSlot && m_SelectedSlot != m_CombiningSlot)
            {
                Combine();
            }
            else
            {
                if (!m_ContextMenu.Show(item))
                {
                    OnUse();
                }
                else
                {
                    m_SelectedSlot.SetSelectionLocked(true);
                }
            }
        }

        // --------------------------------------------------------------------

        private bool IsInventorySlot(UIInventoryItem slot)
        {
            return slot && (m_ItemSlots.Contains(slot) || slot == m_Equipped || slot == m_EquippedSecondary);
        }

        // --------------------------------------------------------------------

        private void OnUse()
        {
            if (GameManager.Instance.Inventory.Use(m_SelectedSlot.InventoryEntry))
            {
                UIManager.Get<UIAudio>().Play(m_UseClip,m_MenuVolume);

                ItemData item = m_SelectedSlot.InventoryEntry.Item;
                if (item && item.Flags.HasFlag(ItemFlags.UseOnInteractive))
                {
                    Hide();
                }
                else
                {
                    Fill();
                    FillEquipped();
                }
            }
            else
            {
                UIManager.Get<UIAudio>().Play(m_CantUseClip,m_MenuVolume);
                OnFeedback.Invoke("Cannot use this item here");
            }

            m_Input.Flush(); // Flush so an object is not immediately selected after this
        }

        // --------------------------------------------------------------------

        private void OnEquip()
        {
            EquipableItemData equipableItem = m_SelectedSlot.Data as EquipableItemData;
            bool wasSomethingEquipped = GameManager.Instance.Inventory.GetEquipped(equipableItem.Slot) != null;
            var selectedEntry=m_SelectedSlot.InventoryEntry;
            GameManager.Instance.Inventory.ToggleEquip(selectedEntry);
            OnFeedback.Invoke(GameManager.Instance.Inventory.GetEquipped(equipableItem.Slot)==selectedEntry ? "Equipped" : "Unequipped");
            UIManager.Get<UIAudio>().Play(m_UseClip,m_MenuVolume);

            if (equipableItem.MoveOutOfInventoryOnEquip || wasSomethingEquipped)
                Fill();

            FillEquipped();

            m_Input.Flush(); // Flush so an object is not immediately selected after this
        }

        // --------------------------------------------------------------------

        private void OnCombine()
        {
            if (m_CombiningSlot)
            {
                if (m_SelectedSlot.InventoryEntry.Item)
                    Combine();
                else
                    CancelCombine();
            }
            else if (m_SelectedSlot.InventoryEntry.Item)
            {
                // First item of the combination picked
                m_SelectedSlot.SetSelectionLocked(true);
                m_CombiningSlot = m_SelectedSlot;
                OnFeedback.Invoke("Select an item to combine");
                
                if (!m_SelectWithOneClick)
                    EventSystem.current.SetSelectedGameObject(m_SelectedSlot.gameObject);
            }
        }


        // --------------------------------------------------------------------

        private void OnExamine()
        {
            ItemData item = m_SelectedSlot.InventoryEntry.Item;
            if (item == null)
                return;

            Hide();

            UIManager.Get<UIExamineItem>().Show(item);
        }

        // --------------------------------------------------------------------


        private void OnDrop()
        {
            ItemData item = m_SelectedSlot.InventoryEntry.Item;
            if (item == null)
                return;

            var gameMgr = GameManager.Instance;
            
            if (item.Flags.HasFlag(ItemFlags.CreatePickupOnDrop))
            {
                gameMgr.Player.GetComponentInChildren<PickupDropper>().Drop(m_SelectedSlot.InventoryEntry);
            }

            gameMgr.Inventory.Drop(m_SelectedSlot.InventoryEntry); 

            Hide();
        }

        // --------------------------------------------------------------------

        public void OnFilesCategory()
        {
            Hide();
            UIManager.Get<UIDocs>().Show();
        }

        // --------------------------------------------------------------------

        public void OnMapCategory()
        {
            if (!HorrorGame.Chapter1.Chapter1MapTutorial.CanOpenMap) return;
            Hide();
            UIManager.Get<UIMap>().Show();
        }


        // --------------------------------------------------------------------

        internal static void RefreshMapTabVisibility(Transform menu)
        {
            bool unlocked = HorrorGame.Chapter1.Chapter1MapTutorial.CanOpenMap;
            foreach (var button in menu.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.name == "MAP tab" || button.name == "MapButton")
                    button.gameObject.SetActive(unlocked);
            }
        }

        private void Combine()
        {
            var inventory = GameManager.Instance.Inventory;
            bool requestedReload = inventory.CanRequestWeaponReload(
                m_SelectedSlot.InventoryEntry, m_CombiningSlot.InventoryEntry);
            InventoryEntry highlightedEntry = inventory.Combine(m_SelectedSlot.InventoryEntry, m_CombiningSlot.InventoryEntry,out bool combined);

            // A reload must begin in gameplay, never underneath the paused
            // inventory. Close immediately after a valid ammo/weapon combine;
            // the pending request starts the reload on the next gameplay frame.
            if (requestedReload)
            {
                InventoryEntry weaponEntry = m_SelectedSlot.InventoryEntry.Item is ReloadableWeaponData
                    ? m_SelectedSlot.InventoryEntry
                    : m_CombiningSlot.InventoryEntry;
                inventory.RequestInventoryReopenAfterReload(weaponEntry);
                m_CombiningSlot = null;
                Hide();
                m_Input.Flush();
                return;
            }
            
            Fill();
            FillEquipped();

            OnFeedback.Invoke(combined ? "Combined" : "Cannot combine these items");
            if (highlightedEntry != null)
            {
                for (int i = 0; i < m_ItemSlots.Length; ++i)
                {
                    if (m_ItemSlots[i].InventoryEntry == highlightedEntry)
                    {
                        EventSystem.current.SetSelectedGameObject(m_ItemSlots[i].gameObject);
                    }
                }
            }

            m_CombiningSlot = null;
        }

        // --------------------------------------------------------------------

        private void Fill()
        {
            var inventory = GameManager.Instance.Inventory;
            var items = inventory.Items;
            // Fixed inventories display exactly their capacity.  The prefab's
            // authoring-time slot count is irrelevant (the folder prefab has
            // extra template cells), so a six-slot character gets six cells.
            int slotCount = inventory.IsInfinite() ? items.Count : inventory.Capacity;
            int itemsCount = Mathf.Min(items.Count, slotCount);

            m_Expanding = inventory.Expanded;
            inventory.Expanded = false;

            if (m_AlwaysRebuild && (!m_RebuildOnlyOnCapacityChange || m_ItemSlots.Length != slotCount))
            {
                RebuildItems(slotCount);
            }

            for (int i = 0; i < itemsCount; ++i)
            {
                m_ItemSlots[i].gameObject.SetActive(i < itemsCount);

                if (i >= inventory.PreExpansionSize && m_Expanding)
                {
                    m_ItemSlots[i].GetComponent<Animator>().Play("Expand");
                }

                m_ItemSlots[i].Fill(items[i]);
            }

            for (int i = itemsCount; i < m_ItemSlots.Length; ++i)
            {
                m_ItemSlots[i].gameObject.SetActive(false);
            }
        }

        // --------------------------------------------------------------------

        private void DestroyItems()
        {
            for (int i = m_ItemSlots.Length - 1; i >= 0; --i)
            {
                UnregisterItemCallbacks(m_ItemSlots[i]);
                var pooled=m_ItemSlots[i].GetComponent<PooledGameObject>();
                if(pooled && pooled.Owner)pooled.ReturnToPool();else Destroy(m_ItemSlots[i].gameObject);
            }
        }

        // --------------------------------------------------------------------

        private void RebuildItems(int itemCount)
        {
            DestroyItems();

            Array.Resize(ref m_ItemSlots, itemCount);
            for (int i = 0; i < itemCount; ++i)
            {
                var pooledItem = GameObjectPool.Instance.GetFromPool(m_RebuildItemPrefab);
                GameObject instance = pooledItem.gameObject;
                instance.transform.SetParent(m_RebuildItemParent, false);

                var inventoryItem = instance.GetComponent<UIInventoryItem>();
                Debug.Assert(inventoryItem, "Inventory item prefab didn't have an expected UIInventoryItem component");
                RegisterItemCallbacks(inventoryItem);

                m_ItemSlots[i] = inventoryItem;
            }
        }

        // --------------------------------------------------------------------

        private void FillEquipped()
        {
            FillEquipped(EquipmentSlot.Primary, m_Equipped);
            FillEquipped(EquipmentSlot.Secondary, m_EquippedSecondary);
        }

        // --------------------------------------------------------------------

        private void FillEquipped(EquipmentSlot slot, UIInventoryItem uiItem)
        {
            InventoryEntry equippedPrim = GameManager.Instance.Inventory.GetEquipped(slot);
            if (equippedPrim != null)
                uiItem.Fill(equippedPrim);
            else
                uiItem.Fill(null);
        }


        // --------------------------------------------------------------------

        public int GetSelectedSlotIndex()
        {
            return m_SelectedSlot ? GameManager.Instance.Inventory.IndexOf(m_SelectedSlot.InventoryEntry) : -1;
        }

        // --------------------------------------------------------------------

        public bool CanBeShown()
        {
            return !m_UIContext.IsBlocked();
        }
    }
}
