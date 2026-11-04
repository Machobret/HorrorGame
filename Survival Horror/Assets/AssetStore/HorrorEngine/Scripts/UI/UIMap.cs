using System;
using HorrorGame.Chapter1;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIMap : MonoBehaviour
    {
        [SerializeField] private RawImage m_MapImage;
        [SerializeField] private TMPro.TextMeshProUGUI m_MapName;
        [SerializeField] private float m_MovementSpeed = 100;
        [SerializeField] private float m_InputSnapThreshold = 0.5f;
        [SerializeField] private UIMapList m_MapList;
        [SerializeField] private Transform m_FloorsParent;
        [SerializeField] private GameObject m_FloorsPrefab;
        [SerializeField] private GameObject m_FloorsLegend;
        [SerializeField] private Color m_CurrentFloorColor;
        [SerializeField] private Color m_NotCurrentFloorColor;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_CloseClip;
        [SerializeField] private AudioClip m_ErrorClip;

        private IUIInput m_Input;
        private MapData m_CurrentMap;
        private UIContext m_UIContext;
        private Image m_CrosshairVertical;
        private Image m_CrosshairHorizontal;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_UIContext = GetComponent<UIContext>();
            m_Input = GetComponentInParent<IUIInput>();
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            gameObject.SetActive(false);
            m_MapList.gameObject.SetActive(false);
            m_MapList.OnMapSelected.AddListener(OnMapSelected);
        }

        // --------------------------------------------------------------------


        private void OnMapSelected(MapData map)
        {
            m_CurrentMap = map;

            MapController currentMapCtrl = MapController.GetCurrent();
            bool isCurrent = currentMapCtrl && currentMapCtrl.Data == map;
            MapRenderer.Instance.GenerateAll(map);
            MapRenderer.Instance.SetPlayerVisible(isCurrent);
            if (isCurrent)
            {
                currentMapCtrl.UpdateContent();
            }
            MapPlayerFocus.FocusOnPlayer(MapRenderer.Instance, map);
            
            m_MapList.gameObject.SetActive(false);
            m_MapName.text = map.Name;

            UpdateFloors();

            UIManager.Get<UIAudio>().Play(m_ShowClip);
        }

        // --------------------------------------------------------------------

        private void UpdateFloors()
        {
            int setIndex = -1;

            MapDataSet set = m_CurrentMap.MapSet;
            if (set)
            {
                setIndex = Array.IndexOf(set.Maps, m_CurrentMap);
                for (int i = m_FloorsParent.childCount; i < set.Maps.Length; ++i)
                {
                    Instantiate(m_FloorsPrefab, m_FloorsParent);
                }
            }

            int activeCount = 0;
            Transform child = null;
            for (int i = 0; i < m_FloorsParent.childCount; ++i)
            {
                child = m_FloorsParent.GetChild(i);
                Image image = child.GetComponentInChildren<Image>();
                MapData map = set ? set.Maps[i] : null;

                bool isActive = set && i < set.Maps.Length && map.IsKnownByPlayer();
                child.gameObject.SetActive(isActive);

                if (isActive)
                {
                    image.color = (setIndex == i) ? m_CurrentFloorColor : m_NotCurrentFloorColor;
                    TMPro.TextMeshProUGUI text = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    text.text = set.Maps[i].Abbreviation;
                    ++activeCount;
                }
            }

            if (child)
                child.gameObject.SetActive(activeCount > 1);

            m_FloorsLegend.gameObject.SetActive(activeCount > 1);
        }

        // ---------------------- ---------------------------------------------

        private void Update()
        {
            if (m_Input.IsToggleMapListDown())
            {
                ShowMapList();
            }

            if (m_MapList.gameObject.activeSelf)
            {
                if (m_Input.IsCancelDown())
                {
                    m_MapList.gameObject.SetActive(false);
                    CursorController.Instance.SetInUI(false);
                }

                return;
            }
            else
            {
                if (m_CurrentMap && m_CurrentMap.MapSet)
                    UpdateFloorChange();
            }

            if (m_Input.IsCancelDown() || m_Input.IsToggleMapDown())
            {
                Hide();
            }

            Vector2 move = m_Input.GetPrimaryAxis();
            move.x = Mathf.Abs(move.x) > m_InputSnapThreshold ? move.x : 0f;
            move.y = Mathf.Abs(move.y) > m_InputSnapThreshold ? move.y : 0f;

            if (MapRenderer.Exists && m_CurrentMap)
                MapRenderer.Instance.MoveTarget(move* Time.unscaledDeltaTime * m_MovementSpeed);

            UpdateCrosshairPosition();
        }

        // --------------------------------------------------------------------

        private void ShowMapList()
        {
            m_MapList.gameObject.SetActive(!m_MapList.gameObject.activeSelf);
            CursorController.Instance.SetInUI(m_MapList.gameObject.activeSelf);
        }

        // --------------------------------------------------------------------

        private void UpdateFloorChange()
        {
            if (m_Input.IsPrevSubmapDown())
            {
                ShowPrevSubmap();
            }

            if (m_Input.IsNextSubmapDown())
            {
                ShowNextSubmap();
            }
        }

        // --------------------------------------------------------------------

        private  void ShowNextSubmap()
        {
            int currentIndex = Array.IndexOf(m_CurrentMap.MapSet.Maps, m_CurrentMap);

            int prevIndex = currentIndex;
            for (int i = 0; i < m_CurrentMap.MapSet.Maps.Length; ++i)
            {
                ++currentIndex;
                if (currentIndex >= m_CurrentMap.MapSet.Maps.Length)
                    currentIndex = 0;

                if (m_CurrentMap.MapSet.Maps[currentIndex].IsKnownByPlayer())
                    break;
            }

            if (currentIndex != prevIndex)
                OnMapSelected(m_CurrentMap.MapSet.Maps[currentIndex]);
            else
                UIManager.Get<UIAudio>().Play(m_ErrorClip);
        }

        // --------------------------------------------------------------------

        private  void ShowPrevSubmap()
        {
            int currentIndex = Array.IndexOf(m_CurrentMap.MapSet.Maps, m_CurrentMap);

            int prevIndex = currentIndex;
            for (int i = 0; i < m_CurrentMap.MapSet.Maps.Length; ++i)
            {
                --currentIndex;
                if (currentIndex < 0)
                    currentIndex = m_CurrentMap.MapSet.Maps.Length - 1;

                if (m_CurrentMap.MapSet.Maps[currentIndex].IsKnownByPlayer())
                    break;
            }

            if (currentIndex != prevIndex)
                OnMapSelected(m_CurrentMap.MapSet.Maps[currentIndex]);
            else
                UIManager.Get<UIAudio>().Play(m_ErrorClip);
        }

        // --------------------------------------------------------------------

        public void Show(MapData map = null)
        {
            PauseController.Instance.Pause(this);

            gameObject.SetActive(true);

            if (MapController.Exists)
            {
                MapController mapCtrl = MapController.GetCurrent();
                if (map == null)
                {
                    map = mapCtrl.Data;
                }
            }
            else
            {
                Debug.LogWarning("There isn't a MapController in the scene. Map won't be visible in the map screen");
            }

            if (MapRenderer.Exists)
            {
                RenderTexture renderTexture = MapRenderer.Instance.RenderTexture;
                m_MapImage.texture = renderTexture;
                // Do not sample the render texture's outermost pixels. Those
                // pixels can contain a transient clear colour while the map
                // camera pans, which shows up as a flashing panel-edge border.
                float insetX = 1.5f / renderTexture.width;
                float insetY = 1.5f / renderTexture.height;
                m_MapImage.uvRect = new Rect(insetX, insetY, 1f - insetX * 2f, 1f - insetY * 2f);
                m_MapImage.gameObject.SetActive(true);
                CreateCrosshair();
                UpdateCrosshairPosition();

                MapRenderer.Instance.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("There isn't a MapRenderer in the scene. Map won't be visible in the map screen");
            }

            m_MapList.Fill();

            if (map)
                OnMapSelected(map);

            m_UIContext.Activate();
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);

            if (MapRenderer.Exists)
                MapRenderer.Instance.gameObject.SetActive(false);

            UIManager.Get<UIAudio>().Play(m_CloseClip);


            m_UIContext.Deactivate();

            UIManager.PopAction();
        }

        // --------------------------------------------------------------------

        private void CreateCrosshair()
        {
            if (m_CrosshairVertical != null) return;
            m_CrosshairVertical = CreateCrosshairLine("MapCrosshairVertical");
            m_CrosshairHorizontal = CreateCrosshairLine("MapCrosshairHorizontal");
        }

        private Image CreateCrosshairLine(string name)
        {
            var lineObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lineObject.transform.SetParent(m_MapImage.transform, false);
            var line = lineObject.GetComponent<Image>();
            line.color = Color.white;
            line.raycastTarget = false;
            return line;
        }

        private void UpdateCrosshairPosition()
        {
            if (m_CrosshairVertical == null || m_CrosshairHorizontal == null || !MapRenderer.Exists) return;
            Vector2 position = MapRenderer.Instance.TargetViewportPosition;
            position.x = Mathf.Clamp01(position.x);
            position.y = Mathf.Clamp01(position.y);

            RectTransform vertical = m_CrosshairVertical.rectTransform;
            vertical.anchorMin = new Vector2(position.x, 0);
            vertical.anchorMax = new Vector2(position.x, 1);
            vertical.anchoredPosition = Vector2.zero;
            vertical.sizeDelta = new Vector2(2, 0);

            RectTransform horizontal = m_CrosshairHorizontal.rectTransform;
            horizontal.anchorMin = new Vector2(0, position.y);
            horizontal.anchorMax = new Vector2(1, position.y);
            horizontal.anchoredPosition = Vector2.zero;
            horizontal.sizeDelta = new Vector2(0, 2);
        }

        // --------------------------------------------------------------------

        public bool CanBeShown()
        {
            return !m_UIContext.IsBlocked();
        }
    }
}
