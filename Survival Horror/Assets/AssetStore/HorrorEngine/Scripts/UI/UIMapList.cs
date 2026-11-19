using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using HorrorGame.Chapter1;

namespace HorrorEngine
{
    public class UIMapList : MonoBehaviour
    {
        [SerializeField] private Transform m_MapListContent;
        [SerializeField] private GameObject m_MapListItemPrefab;

        public UnityEvent<MapData> OnMapSelected;
        private readonly List<MapData> m_DisplayedMaps = new List<MapData>();
        private MapData m_CurrentMap;
        private int m_KeyboardIndex;
        private bool m_KeyboardMode;

        // --------------------------------------------------------------------

        void OnEnable()
        {
            SelectDefault();
        }

        private void Update()
        {
            if (m_DisplayedMaps.Count == 0) return;

            // Any pointer motion/click immediately hands control back to the
            // normal Unity button hover/click flow.
            if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.001f || Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.001f || Input.GetMouseButtonDown(0))
            {
                m_KeyboardMode = false;
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            int move = 0;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) move = 1;

            if (move != 0)
            {
                m_KeyboardMode = true;
                int index = m_KeyboardIndex < 0 && move < 0 ? m_DisplayedMaps.Count - 1 : m_KeyboardIndex + move;
                while (index >= 0 && index < m_DisplayedMaps.Count && !CanSelect(m_DisplayedMaps[index]))
                    index += move;
                if (index >= 0 && index < m_DisplayedMaps.Count)
                    m_KeyboardIndex = index;
                SelectKeyboardItem();
            }

            if (m_KeyboardMode && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                SelectMap(m_KeyboardIndex);
        }

        // --------------------------------------------------------------------

        private void SelectDefault()
        {
            m_KeyboardMode = false;
            m_KeyboardIndex = -1;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        private void SelectKeyboardItem()
        {
            if (!EventSystem.current || m_KeyboardIndex < 0 || m_KeyboardIndex >= m_MapListContent.childCount) return;
            Button button = m_MapListContent.GetChild(m_KeyboardIndex).GetComponentInChildren<Button>();
            if (button) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        // --------------------------------------------------------------------

        /// <summary>Rebuilds the directory from maps in the player's inventory.</summary>
        public void Fill(MapData currentMap = null)
        {
            m_CurrentMap = currentMap;
            m_DisplayedMaps.Clear();

            if (GameManager.Exists && GameManager.Instance.Inventory is { } inventory)
                foreach (MapData map in inventory.Maps)
                    if (map && !m_DisplayedMaps.Contains(map))
                        m_DisplayedMaps.Add(map);

            for (int i = 0; i < m_DisplayedMaps.Count; ++i)
            {
                Transform item;
                if (i >= m_MapListContent.childCount)
                {
                    item = Instantiate(m_MapListItemPrefab).transform;
                    item.SetParent(m_MapListContent, false);

                    Button button = item.GetComponentInChildren<Button>();
                    int index = i;
                    button.onClick.AddListener(() => SelectMap(index));
                }
                else item = m_MapListContent.GetChild(i);

                item.gameObject.SetActive(true);
                var tmPro = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                MapData map = m_DisplayedMaps[i];
                tmPro.text = MapNavigationIndicator.HasActiveObjectiveFor(map) ? "★ " + map.Name : map.Name;
                item.GetComponentInChildren<Button>().interactable = CanSelect(map);
            }

            for (int i = m_DisplayedMaps.Count; i < m_MapListContent.childCount; ++i)
                m_MapListContent.GetChild(i).gameObject.SetActive(false);
        }

        private bool CanSelect(MapData map) =>
            map && map != m_CurrentMap && GameManager.Exists &&
            GameManager.Instance.Inventory is { } inventory && inventory.Maps.Contains(map);

        private void SelectMap(int index)
        {
            if (index < 0 || index >= m_DisplayedMaps.Count) return;
            if (!CanSelect(m_DisplayedMaps[index])) return;
            m_KeyboardIndex = index;
            OnMapSelected?.Invoke(m_DisplayedMaps[index]);
        }
    }
}
