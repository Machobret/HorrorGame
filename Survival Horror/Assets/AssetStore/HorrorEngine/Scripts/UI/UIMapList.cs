using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class UIMapList : MonoBehaviour
    {
        [SerializeField] private Transform m_MapListContent;
        [SerializeField] private GameObject m_MapListItemPrefab;
        [SerializeField] private string m_UnknownMapName = "???";

        public UnityEvent<MapData> OnMapSelected;
        private readonly List<MapData> m_DisplayedMaps = new List<MapData>();

        // --------------------------------------------------------------------

        void OnEnable()
        {
            SelectDefault();
        }

        // --------------------------------------------------------------------

        private void SelectDefault()
        {
            EventSystem.current.SetSelectedGameObject(null);
            
        }

        // --------------------------------------------------------------------

        public void Fill()
        {
            var gameData = GameManager.Instance;
            var mapDB = GameManager.Instance.GetDatabase<MapDatabase>();
            m_DisplayedMaps.Clear();
            if (mapDB && mapDB.Registers.Count > 0)
            {
                m_DisplayedMaps.AddRange(mapDB.Registers);
            }
            else
            {
                // Prototype chapters can own map assets before their database has
                // been rebuilt. The I list must still show those collected maps.
                foreach (MapData map in gameData.Inventory.Maps) m_DisplayedMaps.Add(map);
            }

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
                bool playerHasMap = gameData.Inventory.Maps.Contains(map);
                bool unveiled = playerHasMap || map.GetVisited();
                tmPro.text = unveiled ? map.Name : m_UnknownMapName;

                item.GetComponentInChildren<Button>().interactable = unveiled;
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
                if (selected == child.gameObject)
                {
                    if (i < m_DisplayedMaps.Count) OnMapSelected?.Invoke(m_DisplayedMaps[i]);
                }
            }
        }
    }
}
