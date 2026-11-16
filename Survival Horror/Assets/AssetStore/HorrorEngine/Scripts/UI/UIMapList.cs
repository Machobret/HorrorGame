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
        [SerializeField] private string m_UnknownMapName = "???";

        public UnityEvent<MapData> OnMapSelected;
        private readonly List<MapData> m_DisplayedMaps = new List<MapData>();
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
                m_KeyboardIndex = Mathf.Clamp(m_KeyboardIndex + move, 0, m_DisplayedMaps.Count - 1);
                SelectKeyboardItem();
            }

            if (m_KeyboardMode && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                SelectMap(m_KeyboardIndex);
        }

        // --------------------------------------------------------------------

        private void SelectDefault()
        {
            m_KeyboardMode = false;
            m_KeyboardIndex = 0;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        private void SelectKeyboardItem()
        {
            if (!EventSystem.current || m_KeyboardIndex < 0 || m_KeyboardIndex >= m_MapListContent.childCount) return;
            Button button = m_MapListContent.GetChild(m_KeyboardIndex).GetComponentInChildren<Button>();
            if (button) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        // --------------------------------------------------------------------

        /// <summary>
        /// Rebuilds the I-key list from every available source. A map is shown
        /// only once it is owned or its controller has been visited.
        /// </summary>
        public void Fill(MapData currentMap = null)
        {
            var gameData = GameManager.Instance;
            var mapDB = GameManager.Instance.GetDatabase<MapDatabase>();
            m_DisplayedMaps.Clear();

            void AddCandidate(MapData map)
            {
                if (map && !m_DisplayedMaps.Contains(map) && map.IsKnownByPlayer())
                    m_DisplayedMaps.Add(map);
            }

            // The map currently on screen is the most reliable source in
            // prototype chapters, where a database may not yet be rebuilt.
            if (currentMap && currentMap.MapSet)
                foreach (MapData map in currentMap.MapSet.Maps) AddCandidate(map);

            foreach (MapData map in gameData.Inventory.Maps) AddCandidate(map);

            if (mapDB && mapDB.Registers.Count > 0)
            {
                foreach (MapData map in mapDB.Registers) AddCandidate(map);
            }

            // Map assets referenced directly by scenes/pickups can exist
            // outside a rebuilt MapDatabase. Include loaded assets so their
            // explored map sets are still selectable with I.
            foreach (MapData map in Resources.FindObjectsOfTypeAll<MapData>())
                AddCandidate(map);

            for (int i = 0; i < m_DisplayedMaps.Count; ++i)
            {
                Transform item;
                if (i >= m_MapListContent.childCount)
                {
                    item = Instantiate(m_MapListItemPrefab).transform;
                    item.SetParent(m_MapListContent);

                    Button button = item.GetComponentInChildren<Button>();
                    button.onClick.AddListener(OnMapEntryClicked);
                }
                else item = m_MapListContent.GetChild(i);

                item.gameObject.SetActive(true);
                var tmPro = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                MapData map = m_DisplayedMaps[i];
                tmPro.text = MapNavigationIndicator.HasActiveObjectiveFor(map) ? "★ " + map.Name : map.Name;
                item.GetComponentInChildren<Button>().interactable = true;
            }

            for (int i = m_DisplayedMaps.Count; i < m_MapListContent.childCount; ++i)
                m_MapListContent.GetChild(i).gameObject.SetActive(false);
        }

        private void OnMapEntryClicked()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < m_MapListContent.childCount; ++i)
            {
                var child = m_MapListContent.GetChild(i);
                if (selected && (selected == child.gameObject || selected.transform.IsChildOf(child)))
                {
                    SelectMap(i);
                    return;
                }
            }
        }

        private void SelectMap(int index)
        {
            if (index < 0 || index >= m_DisplayedMaps.Count) return;
            m_KeyboardIndex = index;
            OnMapSelected?.Invoke(m_DisplayedMaps[index]);
        }
    }
}
