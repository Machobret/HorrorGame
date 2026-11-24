using System;
using HorrorGame.Chapter1;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace HorrorEngine
{
    public class UIMap : MonoBehaviour
    {
        [SerializeField] private RawImage m_MapImage;
        [SerializeField] private TMPro.TextMeshProUGUI m_MapName;
        [SerializeField] private float m_MovementSpeed = 100;
        [SerializeField] private float m_InputSnapThreshold = 0.5f;
        [SerializeField] private UIMapList m_MapList;
        [SerializeField] private bool m_SidebarDirectory;
        [SerializeField] private float m_MapViewportLeft=.065f;
        [SerializeField] private RectTransform m_PanFrame;
        [SerializeField] private Color m_CrosshairColor = Color.white;
        public UnityEvent OnShown = new UnityEvent();
        public event Action Closed;
        [SerializeField] private Transform m_FloorsParent;
        [SerializeField] private GameObject m_FloorsPrefab;
        [SerializeField] private GameObject m_FloorsLegend;
        [SerializeField] private Color m_CurrentFloorColor;
        [SerializeField] private Color m_NotCurrentFloorColor;

        [Header("Audio")]
        [SerializeField] private AudioClip m_OpenClip;
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_CloseClip;
        [SerializeField] private AudioClip m_ErrorClip;

        private IUIInput m_Input;
        private MapData m_CurrentMap;
        private UIContext m_UIContext;
        private Image m_CrosshairVertical;
        private Image m_CrosshairHorizontal;
        private bool m_PersistentCursorAcquired;
        private bool m_MouseMapMode;
        private Transform m_CrosshairSnap;
        internal bool UsesMouseOnMap => m_MouseMapMode;
        internal bool IsMapPointerDragging => m_MapPointerHeld && m_MapPointerDragged;
        private bool m_MapPointerHeld, m_MapPointerDragged;
        private Vector2 m_MapPressPosition, m_LastDragViewport;
        public bool IsMapPointerHeld => m_MapPointerHeld;
        public bool MapClickedThisFrame { get; private set; }

        public Vector2 ConstrainDragCursor(Vector2 screen, Vector2 halfCursorSize)
        {
            if (!m_MapPointerHeld || !TryGetMapPointer(out var viewport, true) ||
                !TryProjectMapViewport(Vector2.zero, false, out var bottomLeft) ||
                !TryProjectMapViewport(Vector2.one, false, out var topRight)) return screen;
            Vector2 size = topRight - bottomLeft;
            if (size.x <= 0f || size.y <= 0f) return screen;
            Vector2 padding = (halfCursorSize + Vector2.one * 2f) / size;
            padding = Vector2.Min(padding, Vector2.one * .5f);
            viewport.x = Mathf.Clamp(viewport.x, padding.x, 1f - padding.x);
            viewport.y = Mathf.Clamp(viewport.y, padding.y, 1f - padding.y);
            Vector2 clampedScreen = bottomLeft + Vector2.Scale(viewport, size);
            return m_DisplayFilter && m_DisplayFilter.isActiveAndEnabled
                ? m_DisplayFilter.ProjectToGlass(clampedScreen) : clampedScreen;
        }
        private HorrorGame.UI.TabletStartupScreen m_Startup;
        private HorrorGame.UI.TabletWorkspace m_Workspace;
        private HorrorGame.UI.TabletDisplayFilter m_DisplayFilter;
        public bool IsTabletLoading => m_Startup && m_Startup.Screen && m_Startup.Screen.gameObject.activeInHierarchy;
        public bool AllowMapNavigation { get; set; } = true;
        public bool HasTabletWorkspace { get; set; }
        public MapData CurrentMap => m_CurrentMap;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_UIContext = GetComponent<UIContext>();
            m_Input = GetComponentInParent<IUIInput>();
            m_Startup = GetComponent<HorrorGame.UI.TabletStartupScreen>();
            m_Workspace = GetComponent<HorrorGame.UI.TabletWorkspace>();
            m_DisplayFilter = GetComponent<HorrorGame.UI.TabletDisplayFilter>();
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            gameObject.SetActive(false);
            m_MapList.gameObject.SetActive(false);
            m_MapList.OnMapSelected.AddListener(OnMapSelected);
        }

        // --------------------------------------------------------------------


        private bool CanChangeMap(MapData map) =>
            map && map != m_CurrentMap && GameManager.Exists &&
            GameManager.Instance.Inventory is { } inventory && inventory.Maps.Contains(map);

        private void OnMapSelected(MapData map)
        {
            if (!CanChangeMap(map)) return;
            SelectMap(map);
            UIManager.Get<UIAudio>().Play(m_ShowClip);
        }

        private void SelectMap(MapData map)
        {
            m_CurrentMap = map;

            MapController currentMapCtrl = MapController.GetCurrent();
            bool isCurrent = currentMapCtrl && currentMapCtrl.Data == map;
            if (isCurrent)
                currentMapCtrl.UpdateContent();
            MapRenderer.Instance.GenerateAll(map);
            MapNavigationIndicator.HideMarkersOutside(map);
            // A selected map can belong to another area/set. Do not project
            // the player arrow onto that map; it must only appear on the map
            // that currently contains the player.
            MapRenderer.Instance.SetPlayerVisible(isCurrent);
            if (isCurrent)
                MapPlayerFocus.FocusOnPlayer(MapRenderer.Instance, map);
            
            if (m_MapList.gameObject.activeSelf) ShowMapList();
            UpdateMapViewport();
            m_MapName.text = map.Name;

            UpdateFloors();
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
                MapData map = set && i < set.Maps.Length ? set.Maps[i] : null;

                // A map set can contain a deleted/missing map asset. Skip it
                // rather than dereferencing a null entry while opening M.
                bool isActive = map && (map == m_CurrentMap || CanChangeMap(map));
                child.gameObject.SetActive(isActive);

                if (isActive)
                {
                    image.color = (setIndex == i) ? m_CurrentFloorColor : m_NotCurrentFloorColor;
                    TMPro.TextMeshProUGUI text = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    text.text = map.Abbreviation;
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
            MapClickedThisFrame = false;
            if (IsTabletLoading || IsMapDirectoryOpen || !AllowMapNavigation)
                m_MapPointerHeld = false;
            if (IsTabletLoading)
            {
                m_Input.ConsumeScrollDelta();
                return;
            }
            if (m_Workspace && m_Workspace.IsControlsGuideOpen)
            {
                m_Input.ConsumeScrollDelta();
                if (m_Input.IsCancelDown()) Hide();
                else if (m_Input.IsConfirmDown()) m_Workspace.AdvanceControlsGuide();
                return;
            }
            if (!HasTabletWorkspace && AllowMapNavigation && m_Input.IsToggleMapListDown())
                ShowMapList();

            if (m_MapList.gameObject.activeSelf)
            {
                if (m_Input.IsCancelDown())
                {
                    ShowMapList();
                    return;
                }

                if (!m_SidebarDirectory) return;
            }
            else
            {
                if (AllowMapNavigation && m_CurrentMap && m_CurrentMap.MapSet)
                    UpdateFloorChange();
            }

            if (m_Input.IsCancelDown())
            {
                Hide();
                return;
            }

            if(!AllowMapNavigation)return;
            Vector2 move = m_Input.GetPrimaryAxis();
            move.x = Mathf.Abs(move.x) > m_InputSnapThreshold ? move.x : 0f;
            move.y = Mathf.Abs(move.y) > m_InputSnapThreshold ? move.y : 0f;
            var mouse = Mouse.current;
            bool mouseInside = HasTabletWorkspace && TryGetMapPointer(out _, true);
            bool mouseMoved = mouse != null && mouse.delta.ReadValue().sqrMagnitude > .25f;
            if (mouseInside && mouse.leftButton.wasPressedThisFrame)
                m_MouseMapMode = true;
            else if (move != Vector2.zero)
                m_MouseMapMode = false;
            else if (mouseInside && mouseMoved)
                m_MouseMapMode = true;

            UpdateMousePan(mouse);
            if (MapRenderer.Exists && m_CurrentMap)
            {
                float panSpeed = m_MovementSpeed * MapRenderer.Instance.GetPanSpeedMultiplier();
                if (!m_MouseMapMode && !m_MapPointerHeld && move != Vector2.zero)
                    MapRenderer.Instance.MoveTarget(move * Time.unscaledDeltaTime * panSpeed);
                float scroll = m_Input.ConsumeScrollDelta();
                if (Mathf.Abs(scroll) > 0.001f)
                    MapRenderer.Instance.Zoom(Mathf.Sign(scroll));
            }

            UpdateCrosshairPosition();
        }

        private void LateUpdate()
        {
            if (MapRenderer.Exists) RefreshMapTexture(MapRenderer.Instance);
        }

        // --------------------------------------------------------------------

        private void ShowMapList()
        {
            // Discovery can change while the map screen is open, so rebuild
            // the I-key list every time it is opened.
            if (!m_MapList.gameObject.activeSelf)
                m_MapList.Fill(m_CurrentMap);
            m_MapList.gameObject.SetActive(!m_MapList.gameObject.activeSelf);
            if (!m_SidebarDirectory) CursorController.Instance.SetInUI(m_MapList.gameObject.activeSelf);
            UpdateMapViewport();
        }

        public void ToggleMapDirectory() => ShowMapList();
        public void OpenInventory()
        {
            GetComponentInParent<UIInputListener>().OpenInventory();
        }
        internal void OpenMapPage()
        {
            if (m_Workspace && m_Workspace.Page != 0) m_Workspace.ShowMap();
        }
        public void CloseDirectory(){m_MapList.gameObject.SetActive(false);UpdateMapViewport();}

        private void UpdateMapViewport()
        {
            if (!m_SidebarDirectory) return;
            Vector2 min = m_MapImage.rectTransform.anchorMin;
            min.x = m_MapViewportLeft;
            m_MapImage.rectTransform.anchorMin = min;
            if (m_PanFrame) m_PanFrame.anchorMin = min;
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

                if (CanChangeMap(m_CurrentMap.MapSet.Maps[currentIndex]))
                    break;
            }

            if (currentIndex != prevIndex && CanChangeMap(m_CurrentMap.MapSet.Maps[currentIndex]))
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

                if (CanChangeMap(m_CurrentMap.MapSet.Maps[currentIndex]))
                    break;
            }

            if (currentIndex != prevIndex && CanChangeMap(m_CurrentMap.MapSet.Maps[currentIndex]))
                OnMapSelected(m_CurrentMap.MapSet.Maps[currentIndex]);
            else
                UIManager.Get<UIAudio>().Play(m_ErrorClip);
        }

        // --------------------------------------------------------------------

        public void Show(MapData map = null)
        {
            m_MouseMapMode = false;
            PauseController.Instance.Pause(this);

            if (MapController.Exists)
            {
                MapController mapCtrl = MapController.GetCurrent();
                if (map == null)
                {
                    // A controller can exist while the player is outside every
                    // authored map room. In that case M must not open a blank UI.
                    if (mapCtrl && mapCtrl.Data)
                    {
                        mapCtrl.UpdateContent();
                        if (GameManager.Exists && GameManager.Instance.Inventory != null && mapCtrl.Data.IsKnownByPlayer())
                            map = mapCtrl.Data;
                    }
                }
            }
            else
            {
                Debug.LogWarning("There isn't a MapController in the scene. Map won't be visible in the map screen");
            }

            // Door transitions and prototype areas can briefly place the player
            // outside a MapRoom. Continue showing an owned map in that case.
            if (map == null && GameManager.Exists && GameManager.Instance.Inventory != null)
            {
                foreach (MapData ownedMap in GameManager.Instance.Inventory.Maps)
                {
                    if (!ownedMap) continue;
                    map = ownedMap;
                    break;
                }
            }

            // A physical map and an explored area both unlock the map screen.
            // Room rendering itself still hides every undiscovered room.
            bool mapIsKnown = map != null && GameManager.Exists && GameManager.Instance.Inventory != null && map.IsKnownByPlayer();

            // Generate the selected map before exposing its render texture. This
            // prevents the previous map frame from briefly showing in the UI.
            if (mapIsKnown)
                SelectMap(map);
            else
                m_CurrentMap = null;

            gameObject.SetActive(true);

            if (MapRenderer.Exists && mapIsKnown)
            {
                m_MapImage.gameObject.SetActive(true);
                UpdateMapViewport();
                RefreshMapTexture(MapRenderer.Instance);
                CreateCrosshair();
                UpdateCrosshairPosition();

                MapRenderer.Instance.gameObject.SetActive(true);
            }
            else if (MapRenderer.Exists)
            {
                // The player can still open M before finding a map. Keep the
                // familiar map frame/list visible but don't show a district layout.
                m_MapImage.gameObject.SetActive(false);
                MapRenderer.Instance.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("There isn't a MapRenderer in the scene. Map won't be visible in the map screen");
            }

            m_MapList.Fill(m_CurrentMap);
            UpdateMapViewport();
            if (m_SidebarDirectory && !m_PersistentCursorAcquired)
            {
                CursorController.Instance.SetInUI(true);
                m_PersistentCursorAcquired = true;
            }

            m_UIContext.Activate();
            if (m_OpenClip || mapIsKnown)
                UIManager.Get<UIAudio>().Play(m_OpenClip ? m_OpenClip : m_ShowClip);
            OnShown.Invoke();
        }

        // --------------------------------------------------------------------

        private void RefreshMapTexture(MapRenderer renderer)
        {
            if (HasTabletWorkspace && m_MapImage.isActiveAndEnabled && m_MapImage.canvas &&
                TryProjectMapViewport(Vector2.zero, false, out var bottomLeft) &&
                TryProjectMapViewport(Vector2.one, false, out var topRight))
            {
                Vector2 size = topRight - bottomLeft;
                if (size.x >= 1f && size.y >= 1f) renderer.SetViewportSize(size);
            }

            if (renderer.RenderTexture && m_MapImage.texture != renderer.RenderTexture)
                SetMapTexture(renderer.RenderTexture);
        }

        private void SetMapTexture(RenderTexture renderTexture)
        {
            m_MapImage.texture = renderTexture;
            if (HasTabletWorkspace)
            {
                // The tablet samples a target that already fits its viewport.
                m_MapImage.uvRect = new Rect(0, 0, 1, 1);
                return;
            }
            // Avoid bilinear sampling of the render texture's transient outer
            // clear pixels when its size changes with the display resolution.
            float insetX = 1.5f / renderTexture.width;
            float insetY = 1.5f / renderTexture.height;
            m_MapImage.uvRect = new Rect(insetX, insetY, 1f - insetX * 2f, 1f - insetY * 2f);
        }

        // --------------------------------------------------------------------

        internal void Hide()
        {
            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);

            if (MapRenderer.Exists)
                MapRenderer.Instance.gameObject.SetActive(false);

            UIManager.Get<UIAudio>().Play(m_CloseClip);


            m_UIContext.Deactivate();

            Closed?.Invoke();
            UIManager.PopAction();
        }

        private void OnDisable()
        {
            if (HasTabletWorkspace && MapRenderer.Exists) MapRenderer.Instance.ClearViewportSize();
            m_MapPointerHeld = false;
            MapClickedThisFrame = false;
            if (!m_PersistentCursorAcquired) return;
            if (CursorController.Exists) CursorController.Instance.SetInUI(false);
            m_PersistentCursorAcquired = false;
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
            line.color = m_CrosshairColor;
            line.raycastTarget = false;
            return line;
        }

        public bool IsMapDirectoryOpen => m_MapList && m_MapList.gameObject.activeInHierarchy;

        private void UpdateMousePan(Mouse mouse)
        {
            if (!HasTabletWorkspace || !m_CurrentMap || !MapRenderer.Exists || mouse == null ||
                !TryGetMapPointer(out var viewport, true, false))
            {
                m_MapPointerHeld = false;
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            bool inside = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f &&
                !IsPointerOverLegend(screen);
            viewport = new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
            if (mouse.leftButton.wasPressedThisFrame && inside)
            {
                m_MapPointerHeld = true;
                m_MapPointerDragged = false;
                m_MapPressPosition = screen;
                m_LastDragViewport = viewport;
            }

            if (!m_MapPointerHeld) return;
            m_MouseMapMode = true;
            if (m_MapPointerDragged || (screen - m_MapPressPosition).sqrMagnitude >= 16f)
            {
                m_MapPointerDragged = true;
                Vector2 delta = Vector2.Scale(viewport - m_LastDragViewport, m_MapImage.uvRect.size);
                MapRenderer.Instance.DragViewport(delta);
                m_LastDragViewport = viewport;
            }

            if (!mouse.leftButton.isPressed)
            {
                MapClickedThisFrame = mouse.leftButton.wasReleasedThisFrame && !m_MapPointerDragged && inside;
                m_MapPointerHeld = false;
            }
        }

        private bool IsPointerOverLegend(Vector2 screen)
        {
            var workspace = GetComponent<HorrorGame.UI.TabletWorkspace>();
            if (!workspace || !workspace.Legend || !workspace.Legend.activeInHierarchy ||
                !(workspace.Legend.transform is RectTransform legend)) return false;
            if (m_DisplayFilter && m_DisplayFilter.isActiveAndEnabled) screen = m_DisplayFilter.GlassToScreen(screen);
            var canvas = m_MapImage.canvas.rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(legend, screen, camera);
        }

        public bool TryGetMapPointer(out Vector2 position, bool clampToEdge = false, bool limitViewport = true)
        {
            position = default;
            if (Mouse.current == null || !m_MapImage || !m_MapImage.isActiveAndEnabled ||
                IsTabletLoading || IsMapDirectoryOpen || !AllowMapNavigation) return false;
            Vector2 screen = Mouse.current.position.ReadValue();
            if (m_DisplayFilter && m_DisplayFilter.isActiveAndEnabled) screen = m_DisplayFilter.GlassToScreen(screen);
            var image = m_MapImage.rectTransform;
            var canvas = m_MapImage.canvas.rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(image, screen, camera, out Vector2 local))
                return false;
            Rect bounds = image.rect;
            if (!clampToEdge && !bounds.Contains(local)) return false;
            position = (local - bounds.min) / bounds.size;
            if (limitViewport)
                position = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));
            return true;
        }

        public bool TryGetCrosshairViewport(out Vector2 position)
        {
            if (m_MouseMapMode)
            {
                if (TryGetMapPointer(out position) && !IsPointerOverLegend(Mouse.current.position.ReadValue())) return true;
                return false;
            }
            position = MapRenderer.Exists ? ToMapViewport(MapRenderer.Instance.TargetViewportPosition) : Vector2.one * .5f;
            position = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));
            return !IsTabletLoading;
        }

        internal void SetCircuitCrosshair(Transform target)
        {
            m_CrosshairSnap = target;
            UpdateCrosshairPosition();
        }

        private Vector2 ToMapViewport(Vector2 textureViewport)
        {
            Rect uv = m_MapImage.uvRect;
            return new Vector2((textureViewport.x - uv.x) / uv.width, (textureViewport.y - uv.y) / uv.height);
        }

        public bool TryProjectMapViewport(Vector2 viewport, bool sampleTexture, out Vector2 screen, bool allowOutside = false)
        {
            screen = default;
            if (!m_MapImage || !m_MapImage.gameObject.activeInHierarchy) return false;
            if (sampleTexture) viewport = ToMapViewport(viewport);
            if (!allowOutside && (viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1)) return false;
            RectTransform image = m_MapImage.rectTransform;
            Rect rect = image.rect;
            Canvas canvas = m_MapImage.canvas.rootCanvas;
            screen = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                image.TransformPoint(rect.min + Vector2.Scale(rect.size, viewport)));
            return true;
        }

        private void UpdateCrosshairPosition()
        {
            if (m_CrosshairVertical == null || m_CrosshairHorizontal == null || !MapRenderer.Exists) return;
            bool visible = TryGetCrosshairViewport(out Vector2 position);
            if (visible && !m_MouseMapMode && m_CrosshairSnap && MapRenderer.Instance.RenderCamera)
                position = ToMapViewport(MapRenderer.Instance.RenderCamera.WorldToViewportPoint(m_CrosshairSnap.position));
            m_CrosshairVertical.enabled = visible;
            m_CrosshairHorizontal.enabled = visible;
            if (!visible) return;
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

        public void OnItemsCategory() => OpenInventory();

        public void OnFilesCategory()
        {
            Hide();
            UIManager.Get<UIDocs>().Show();
        }
    }
}
