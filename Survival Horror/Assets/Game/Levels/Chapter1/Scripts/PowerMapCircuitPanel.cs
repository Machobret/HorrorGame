using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame.Chapter1
{
    /// <summary>Uses Horror Engine's real UIMap/MapRenderer and overlays selectable circuit points in map space.</summary>
    [DefaultExecutionOrder(20)]
    public sealed class PowerMapCircuitPanel : MonoBehaviour
    {
        public MapData MapData;
        public PowerPanelLayout Layout;
        public DistrictPowerGrid Grid;
        public ShutterSequencePuzzle SequencePuzzle;
        private bool sequenceWasVisible;
        [Tooltip("Collected room-specific diagrams unlock only that room's circuit dots.")]
        public CircuitDiagramData[] CircuitDiagrams;
        [Tooltip("When enabled, dots appear on the normal player map instead of a separate power-panel UI.")]
        public bool DisplayInPlayerMap = true;
        private DistrictPowerGrid grid;
        private UIMap mapUi;
        private readonly List<GameObject> points = new List<GameObject>();
        private PowerCircuitMapMarker hoveredPoint;
        private float statusTweenStarted;
        private MeshFilter rangeBackground;
        private Material rangeMaterial;
        private PowerCircuitMapMarker rangeTarget;
        private readonly Vector3[] rangeCorners = new Vector3[4];
        private readonly Vector3[] legendCorners = new Vector3[4];
        public PowerCircuitMapMarker HoveredPoint => hoveredPoint;
        public bool HoveredInRange { get; private set; }
        private static readonly Texture2D[] TutorialFrames = new Texture2D[16];
        private float tutorialLastActive = float.NegativeInfinity;
        public static float EffectScale => MapRenderer.Exists ? MapRenderer.Instance.MapEffectScale : 1f;
        private static float TutorialRingSize => 52f * EffectScale;
        private float TutorialFade => Mathf.Clamp01(1f - (Time.unscaledTime - tutorialLastActive) / .9f);
        private UnityEngine.UI.RawImage tutorialRingOverlay;

        public DistrictPowerGrid SourceGrid => grid ? grid : Grid;
        private PowerCircuitPanelElement TutorialDot
        {
            get
            {
                var tutorial = Chapter1MapTutorial.Instance;
                var dot = tutorial ? tutorial.TutorialCircuit : null;
                return dot && Chapter1MapTutorial.IsTargetCircuit(MapData, dot.CircuitId) ? dot : null;
            }
        }
        public bool CanAccessCircuit(string id)
        {
            if(!PlayerCanViewMap())return false;
            if (TutorialDot && TutorialDot.CircuitId == id) return true;
            if (SequencePuzzle)
            {
                foreach (var dot in SequencePuzzle.SegmentDots)
                    if (dot && dot.CircuitId == id && IsSequenceDotAvailable(dot)) return true;
            }
            if(Layout)foreach(var shape in Layout.Circuits)if(shape.CircuitId==id && IsCircuitRoomAvailable(shape.RoomUniqueId,id))return true;
            foreach(var gate in HorrorGame.Environment.GarageDoorCircuitLink.Active)if(gate.TryMarker(SourceGrid,MapData,out var room,out var position) && gate.CircuitId==id && IsCircuitRoomAvailable(room,id))return true;
            return false;
        }

        public void Open(DistrictPowerGrid requestedGrid)
        {
            grid = requestedGrid;
            grid?.SyncDefinitionsFromData();
            mapUi = UIManager.Get<UIMap>(true);
            if (!mapUi || !MapData || !grid) return;
            if (!PlayerCanViewMap())
            {
                Debug.Log("This map area must be explored before its circuit diagram can be used.", this);
                return;
            }
            mapUi.Show(MapData);
            var workspace=mapUi.GetComponent<HorrorGame.UI.TabletWorkspace>();if(workspace)workspace.ShowPower();
        }

        private void Update()
        {
            if (grid == null) grid = Grid;
            if (mapUi == null) mapUi = UIManager.Get<UIMap>(true);
            bool sequenceVisible = SequencePuzzle && SequencePuzzle.OwnsControlDiagram;
            if (sequenceVisible != sequenceWasVisible) ClearPoints();
            sequenceWasVisible = sequenceVisible;
            bool showCircuits = DisplayInPlayerMap && mapUi && mapUi.gameObject.activeInHierarchy && !mapUi.IsTabletLoading && mapUi.AllowMapNavigation && mapUi.CurrentMap == MapData && PlayerCanViewMap();
            if (!showCircuits) { hoveredPoint = null; ClearPoints(); return; }
            if (points.Count == 0) CreateCircuitPoints();
            var nextPoint = mapUi.IsMapDirectoryOpen ? null : GetCrosshairPoint();
            bool nextInRange = nextPoint && IsCircuitInRange(nextPoint.CircuitId, out _, out _);
            if (hoveredPoint != nextPoint || HoveredInRange != nextInRange)
            {
                if (hoveredPoint != nextPoint || nextInRange)
                    statusTweenStarted = Time.unscaledTime;
                if (hoveredPoint) hoveredPoint.SetHovered(false);
                hoveredPoint = nextPoint;
                HoveredInRange = nextInRange;
                if (hoveredPoint) hoveredPoint.SetHovered(HoveredInRange);
            }
            IUIInput input = UIManager.Instance.GetComponent<IUIInput>();
            bool click = mapUi.HasTabletWorkspace && Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame && mapUi.TryGetMapPointer(out _);
            if (hoveredPoint && (click || input != null && input.IsConfirmDown()))
                TryToggleCircuit(hoveredPoint.CircuitId);
            foreach (var point in points)
                if (point && point.TryGetComponent(out PowerCircuitMapMarker marker))
                    marker.SetPowerVisual(grid && grid.IsCircuitPowered(marker.CircuitId),
                        marker == hoveredPoint, marker == hoveredPoint && !HoveredInRange);
        }

        private void CreateCircuitPoints()
        {
            ClearPoints();
            MapRenderer renderer = MapRenderer.Instance;
            if (!renderer || grid == null) return;
            Transform markerRoot = renderer.transform;
            int layer = LayerMask.NameToLayer("MapSystem");
            float width = MapData.Size.x * MapData.CellSize;
            float height = MapData.Size.y * MapData.CellSize;
            var tutorialDot = TutorialDot;
            if (tutorialDot)
            {
                var shape = tutorialDot.ToShape(MapData);
                var position = shape.MapPosition;
                points.Add(PowerCircuitMapMarker.Create(markerRoot, layer, tutorialDot.CircuitId,
                    new Vector3(position.x, 6f, -position.y), shape.RoomUniqueId).gameObject);
            }
            if(Layout) foreach (PowerPanelCircuitShape shape in Layout.Circuits)
            {
                if (tutorialDot && shape.CircuitId == tutorialDot.CircuitId) continue;
                if (!IsCircuitRoomAvailable(shape.RoomUniqueId, shape.CircuitId)) continue;
                // MapPosition is generated from the assigned MapRoom. Bounds belong to the
                // obsolete standalone layout editor and may be nowhere near this map.
                Vector2 mapPosition = shape.MapPosition == Vector2.zero
                    ? new Vector2(shape.Bounds.center.x / Layout.CanvasSize.x * width, shape.Bounds.center.y / Layout.CanvasSize.y * height)
                    : shape.MapPosition;
                PowerCircuitMapMarker marker = PowerCircuitMapMarker.Create(markerRoot, layer, shape.CircuitId,
                    new Vector3(mapPosition.x, 6f, -mapPosition.y), shape.RoomUniqueId);
                points.Add(marker.gameObject);
            }
            foreach(var gate in HorrorGame.Environment.GarageDoorCircuitLink.Active)
                if(gate.TryMarker(grid,MapData,out var roomId,out var position) && IsCircuitRoomAvailable(roomId, gate.CircuitId))
                    points.Add(PowerCircuitMapMarker.Create(markerRoot, layer, gate.CircuitId, position, roomId).gameObject);
            if (SequencePuzzle)
            {
                foreach (var dot in SequencePuzzle.SegmentDots) AddSequenceMarker(dot, markerRoot, layer);
            }
        }

        private void AddSequenceMarker(PowerCircuitPanelElement dot, Transform root, int layer)
        {
            if (!IsSequenceDotAvailable(dot) || points.Exists(point => point && point.GetComponent<PowerCircuitMapMarker>().CircuitId == dot.CircuitId)) return;
            var shape = dot.ToShape(MapData);
            Vector2 position = shape.MapPosition;
            points.Add(PowerCircuitMapMarker.Create(root, layer, dot.CircuitId,
                new Vector3(position.x, 6f, -position.y), shape.RoomUniqueId).gameObject);
        }

        private bool IsSequenceDotAvailable(PowerCircuitPanelElement dot)
        {
            return dot && dot.GetMapData() == MapData &&
                (SequencePuzzle.OwnsControlDiagram || IsCircuitRoomAvailable(dot.ToShape(MapData).RoomUniqueId, dot.CircuitId));
        }

        private bool IsCircuitRoomAvailable(string roomId, string circuitId)
        {
            // The tablet tutorial can expose its one highlighted target before
            // the player has collected a room diagram.
            if (Chapter1MapTutorial.IsTargetCircuit(MapData, circuitId)) return true;
            if (MapData == null || string.IsNullOrWhiteSpace(roomId) || !GameManager.Exists || GameManager.Instance.Inventory == null) return false;
            var room = MapData.Rooms.Find(candidate => candidate != null && candidate.UniqueId == roomId);
            if (room != null && ObjectStateManager.Instance != null && room.GetState() >= MapRoomState.Visited)
                return true;
            foreach (DocumentData document in GameManager.Instance.Inventory.Documents)
            {
                if (!(document is CircuitDiagramData diagram)) continue;
                if (diagram.Map != MapData || diagram.RoomUniqueId != roomId) continue;
                return true;
            }
            return false;
        }

        private bool PlayerCanViewMap()
        {
            return GameManager.Exists && GameManager.Instance.Inventory != null && MapData && MapData.IsKnownByPlayer();
        }

        public bool TryToggleCircuit(string circuitId)
        {
            DistrictPowerGrid source = SourceGrid;
            if (!source || source.IsShutdown || !CanAccessCircuit(circuitId) ||
                !IsCircuitInRange(circuitId, out _, out _)) return false;
            bool wasPowered = source.IsCircuitPowered(circuitId);
            source.SetCircuit(circuitId, !wasPowered);
            bool powered = source.IsCircuitPowered(circuitId);
            return powered != wasPowered;
        }

        private PowerCircuitMapMarker GetCrosshairPoint()
        {
            Camera mapCamera = MapRenderer.Instance ? MapRenderer.Instance.GetComponentInChildren<Camera>() : null;
            if (!mapCamera) return null;
            if (!mapUi.TryGetCrosshairViewport(out Vector2 cursor)) return null;
            if (!mapUi.TryProjectMapViewport(cursor, false, out Vector2 crosshair)) return null;
            PowerCircuitMapMarker nearest = null;
            float radius = 14f * EffectScale;
            float nearestDistance = radius * radius;
            foreach (var point in points)
            {
                if (!point || !TryProjectMarker(mapCamera, point.transform, out Vector2 screen)) continue;
                float distance = (screen - crosshair).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = point.GetComponent<PowerCircuitMapMarker>();
                nearestDistance = distance;
            }
            return nearest;
        }

        private bool TryProjectMarker(Camera camera, Transform marker, out Vector2 screen)
        {
            screen = default;
            Vector3 viewport = camera.WorldToViewportPoint(marker.position);
            return viewport.z > 0 && mapUi.TryProjectMapViewport(viewport, true, out screen);
        }

        private void OnGUI()
        {
            if (points.Count == 0 || grid == null || !mapUi || !mapUi.gameObject.activeInHierarchy || mapUi.CurrentMap != MapData || !PlayerCanViewMap()) return;
            if (mapUi.IsTabletLoading || !mapUi.AllowMapNavigation || mapUi.IsMapDirectoryOpen || !MapRenderer.Exists) return;
            if (!mapUi.HasTabletWorkspace)
            {
                Rect energyBar = new Rect(16, 16, 260, 48);
                GUI.Box(energyBar, GUIContent.none);
                GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 7, energyBar.width - 24, 20), "DISTRICT POWER");
                string load = grid.IsShutdown ? "GRID SHUT DOWN — RESET AT FUSE BOX" : $"LOAD  {grid.CurrentConsumption} / {grid.MaximumCapacity}    AVAILABLE  {grid.RemainingCapacity}";
                GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 25, energyBar.width - 24, 20), load);
            }
            if (!GetTabletOutput()) DrawTutorialHighlight();
            if (SequencePuzzle && SequencePuzzle.ShowingWrongAnswer)
                GUI.Box(new Rect(Screen.width * .5f - 125, Screen.height * .72f, 250, 32), "SHUTTER PATTERN REJECTED");
            Camera camera = MapRenderer.Instance.GetComponentInChildren<Camera>();
            if (!camera) return;
            var filter = mapUi.GetComponent<HorrorGame.UI.TabletDisplayFilter>();
            float textScale = EffectScale;
            if (!mapUi.TryProjectMapViewport(Vector2.zero, false, out Vector2 bottomLeft) ||
                !mapUi.TryProjectMapViewport(Vector2.one, false, out Vector2 topRight)) return;
            if (filter && filter.isActiveAndEnabled)
            {
                bottomLeft = filter.ProjectToGlass(bottomLeft);
                topRight = filter.ProjectToGlass(topRight);
            }
            var mapBounds = Rect.MinMaxRect(bottomLeft.x, Screen.height - topRight.y,
                topRight.x, Screen.height - bottomLeft.y);
            const float inset = 4f;
            var clip = Rect.MinMaxRect(mapBounds.xMin + inset, mapBounds.yMin + inset,
                mapBounds.xMax - inset, mapBounds.yMax - inset);
            if (clip.width <= 0f || clip.height <= 0f) return;
            Rect legendBounds = GetLegendBounds(filter);
            string currentRoomId = GetCurrentRoomId();
            var workspace = mapUi.GetComponent<HorrorGame.UI.TabletWorkspace>();
            var font = workspace && workspace.Footer && workspace.Footer.font
                ? workspace.Footer.font.sourceFontFile : null;
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = font ? FontStyle.Normal : FontStyle.Bold,
                fontSize = Mathf.Max(1, Mathf.RoundToInt(7f * textScale))
            };
            if (font) style.font = font;
            foreach (var point in points)
            {
                if (!point || !point.TryGetComponent(out PowerCircuitMapMarker marker) ||
                    currentRoomId == null || marker.RoomUniqueId != currentRoomId ||
                    marker == hoveredPoint ||
                    !TryProjectMarker(camera, point.transform, out Vector2 idleScreen)) continue;
                if (filter && filter.isActiveAndEnabled) idleScreen = filter.ProjectToGlass(idleScreen);
                bool idlePowered = grid.IsCircuitPowered(marker.CircuitId);
                style.normal.textColor = GetStatusColor(idlePowered);
                style.hover.textColor = style.normal.textColor;
                style.active.textColor = style.normal.textColor;
                DrawStatusLabel(idleScreen, clip, legendBounds, 8f * textScale, 11f * textScale, 42f * textScale,
                    idlePowered, style);
            }

            if (!hoveredPoint || !TryProjectMarker(camera, hoveredPoint.transform, out Vector2 screen)) return;
            PowerCircuit circuit = grid.Circuits.Find(value => value.Id == hoveredPoint.CircuitId);
            string activatedName = circuit != null && !string.IsNullOrWhiteSpace(circuit.DisplayName) ? circuit.DisplayName : hoveredPoint.CircuitId;
            if (filter && filter.isActiveAndEnabled) screen = filter.ProjectToGlass(screen);
            if (!HoveredInRange)
            {
                var alert = StatusRect(screen, mapBounds, 17f, 19f, 110f);
                style.fontSize = 13;
                style.normal.textColor = new Color(1f, .35f, .32f);
                style.hover.textColor = style.normal.textColor;
                style.active.textColor = style.normal.textColor;
                DrawClippedLabel(alert, clip, legendBounds, "NO SERVICE", style);
                return;
            }
            float elapsed = Time.unscaledTime - statusTweenStarted;
            float nameProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .26f));
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(9f, 15f, nameProgress) * textScale));
            Color nameColor = GetStatusColor(grid.IsCircuitPowered(hoveredPoint.CircuitId));
            nameColor.a = nameProgress;
            style.normal.textColor = nameColor;
            style.hover.textColor = nameColor;
            style.active.textColor = nameColor;
            var name = new GUIContent(activatedName.ToUpperInvariant());
            float width = Mathf.Min(style.CalcSize(name).x + 12f * textScale, Screen.width);
            float nameHeight = 22f * textScale;
            var label = new Rect(screen.x - width * .5f,
                Screen.height - screen.y - Mathf.Lerp(29f, 39f, nameProgress) * textScale, width, nameHeight);
            DrawClippedLabel(label, clip, legendBounds, name.text, style);
            if (currentRoomId == null || hoveredPoint.RoomUniqueId != currentRoomId) return;

            bool powered = grid.IsCircuitPowered(hoveredPoint.CircuitId);
            const float slideDuration = .18f;
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideDuration));
            float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - slideDuration) / .15f));
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(7f, 10f, growth) * textScale));
            style.normal.textColor = GetStatusColor(powered);
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            DrawStatusLabel(screen, clip, legendBounds, Mathf.Lerp(8f, 13f, slide) * textScale,
                12f * textScale, 42f * textScale, powered, style);
        }

        private Rect GetLegendBounds(HorrorGame.UI.TabletDisplayFilter filter)
        {
            var workspace = mapUi.GetComponent<HorrorGame.UI.TabletWorkspace>();
            if (!workspace || !workspace.Legend || !workspace.Legend.activeInHierarchy ||
                !(workspace.Legend.transform is RectTransform rect)) return default;

            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            rect.GetWorldCorners(legendCorners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in legendCorners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                if (filter && filter.isActiveAndEnabled) screen = filter.ProjectToGlass(screen);
                screen.y = Screen.height - screen.y;
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            // The glass curves the edges slightly beyond the projected corners.
            Vector2 padding = Vector2.one * 4f;
            return Rect.MinMaxRect(min.x - padding.x, min.y - padding.y, max.x + padding.x, max.y + padding.y);
        }

        private static void DrawStatusLabel(Vector2 screen, Rect clip, Rect legendBounds, float gap, float height,
            float width, bool powered, GUIStyle style)
        {
            var label = new Rect(screen.x - width * .5f, Screen.height - screen.y + gap, width, height);
            DrawClippedLabel(label, clip, legendBounds, powered ? "ON" : "OFF", style);
        }

        private static void DrawClippedLabel(Rect label, Rect clip, Rect legendBounds, string text, GUIStyle style)
        {
            if (legendBounds.width > 0f && legendBounds.Overlaps(label)) return;
            label.position -= clip.position;
            GUI.BeginGroup(clip);
            GUI.Label(label, text, style);
            GUI.EndGroup();
        }

        private string GetCurrentRoomId()
        {
            if (!GameManager.Exists || !GameManager.Instance.Player) return null;
            var controller = MapController.GetCurrent();
            if (!controller || controller.Data != MapData) return null;
            var room = controller.GetCurrentRoom();
            return room && room.TryGetComponent(out ObjectUniqueId id) && !string.IsNullOrEmpty(id.Id)
                ? id.Id : null;
        }

        private static Rect StatusRect(Vector2 screen, Rect bounds, float gap, float height, float width)
        {
            width = Mathf.Min(width, bounds.width);
            height = Mathf.Min(height, bounds.height);
            float y = Screen.height - screen.y;
            float top = y + gap;
            if (top + height > bounds.yMax) top = y - gap - height;
            return new Rect(Mathf.Clamp(screen.x - width * .5f, bounds.xMin, bounds.xMax - width),
                Mathf.Clamp(top, bounds.yMin, bounds.yMax - height), width, height);
        }

        private static Color GetStatusColor(bool powered) =>
            powered ? new Color(.47f, .94f, .59f) : new Color(1f, .43f, .40f);

        private void ClearPoints()
        {
            hoveredPoint = null;
            HoveredInRange = false;
            foreach (GameObject point in points) if (point) Destroy(point);
            points.Clear();
        }
        private void OnDestroy()
        {
            ClearPoints();
            if (tutorialRingOverlay) Destroy(tutorialRingOverlay.gameObject);
            if (rangeBackground)
            {
                Destroy(rangeBackground.sharedMesh);
                Destroy(rangeBackground.gameObject);
            }
            if (rangeMaterial) Destroy(rangeMaterial);
        }

        private bool IsCircuitInRange(string circuitId, out float distance, out float range)
        {
            distance = float.PositiveInfinity;
            range = 8f;
            if (!GameManager.Exists || !GameManager.Instance.Player || !TryGetCircuitWorldPosition(circuitId, out Vector3 worldPosition, out range)) return false;
            Vector3 player = GameManager.Instance.Player.transform.position;
            player.y = 0f;
            worldPosition.y = 0f;
            distance = Vector3.Distance(player, worldPosition);
            return distance <= range;
        }

        private bool TryGetCircuitWorldPosition(string circuitId, out Vector3 worldPosition, out float range)
        {
            worldPosition = Vector3.zero;
            range = 8f;
            if (Layout != null)
            {
                PowerPanelCircuitShape shape = Layout.Circuits.Find(value => value.CircuitId == circuitId);
                if (shape != null)
                {
                    worldPosition = shape.WorldPosition;
                    range = Mathf.Max(.1f, shape.ActivationRange);
                }
            }

            // Scene elements provide an exact position for legacy layout data
            // that was authored before world positions were serialized.
            foreach (PowerCircuitPanelElement element in FindObjectsByType<PowerCircuitPanelElement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!element || element.CircuitId != circuitId || element.GetMapData() != MapData) continue;
                worldPosition = element.Room ? element.Room.transform.TransformPoint(new Vector3(element.RoomOffset.x, 0f, element.RoomOffset.y)) : element.transform.position;
                range = Mathf.Max(.1f, element.ActivationRange);
                return true;
            }
            return worldPosition != Vector3.zero;
        }

        public bool TryGetRangeAxes(out Vector3 east, out Vector3 north)
        {
            east = north = default;
            if (!hoveredPoint || !TryGetCircuitWorldPosition(hoveredPoint.CircuitId, out _, out float range))
                return false;
            east = Vector3.right * range * MapData.GlobalScale;
            north = Vector3.forward * range * MapData.GlobalScale;
            foreach (var dot in FindObjectsByType<PowerCircuitPanelElement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (dot.CircuitId != hoveredPoint.CircuitId || dot.GetMapData() != MapData || !dot.Room) continue;
                var room = dot.Room;
                var matrix = MapData.GetTRS(room.Offset, room.Rotation, room.Scale, room.ZOrder);
                var renderer = MapRenderer.Instance;
                Vector3 origin = renderer.MapPositionToWorld(Vector3.zero);
                east = renderer.MapPositionToWorld(matrix.MultiplyVector(room.transform.InverseTransformVector(Vector3.right * range))) - origin;
                north = renderer.MapPositionToWorld(matrix.MultiplyVector(room.transform.InverseTransformVector(Vector3.forward * range))) - origin;
                return true;
            }
            return true;
        }

        private UnityEngine.UI.RawImage GetTabletOutput()
        {
            var filter = mapUi ? mapUi.GetComponent<HorrorGame.UI.TabletDisplayFilter>() : null;
            return filter && filter.isActiveAndEnabled ? filter.Output : null;
        }

        private void LateUpdate()
        {
            UpdateRangeBackground();
            if (Chapter1MapTutorial.Instance && Chapter1MapTutorial.Instance.TutorialActive)
                tutorialLastActive = Time.unscaledTime;
            var output = GetTabletOutput();
            if (!output || !mapUi.AllowMapNavigation || mapUi.IsMapDirectoryOpen ||
                mapUi.CurrentMap != MapData || !TryGetTutorialScreenPosition(out Vector2 screen))
            {
                if (tutorialRingOverlay) tutorialRingOverlay.enabled = false;
                return;
            }

            RectTransform parent = output.rectTransform;
            if (!tutorialRingOverlay)
            {
                var ring = new GameObject("Tutorial Circuit Highlight", typeof(RectTransform));
                ring.transform.SetParent(parent, false);
                tutorialRingOverlay = ring.AddComponent<UnityEngine.UI.RawImage>();
                tutorialRingOverlay.raycastTarget = false;
            }

            screen = mapUi.GetComponent<HorrorGame.UI.TabletDisplayFilter>().ProjectToGlass(screen);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
            var rect = tutorialRingOverlay.rectTransform;
            rect.localPosition = local;
            rect.sizeDelta = Vector2.one * (TutorialRingSize / output.canvas.scaleFactor);
            tutorialRingOverlay.texture = GetTutorialRing();
            tutorialRingOverlay.color = new Color(1f, 1f, 1f, TutorialFade);
            tutorialRingOverlay.enabled = true;
        }

        private void UpdateRangeBackground()
        {
            bool visible = hoveredPoint && mapUi && mapUi.isActiveAndEnabled &&
                !mapUi.IsTabletLoading && !mapUi.IsMapDirectoryOpen && mapUi.AllowMapNavigation && mapUi.CurrentMap == MapData;
            if (!visible)
            {
                if (rangeBackground) rangeBackground.gameObject.SetActive(false);
                rangeTarget = null;
                return;
            }
            if (!rangeBackground)
            {
                var shader = Resources.Load<Shader>("MapCircuitRange");
                if (!shader) return;
                var background = new GameObject("Circuit range background");
                background.layer = hoveredPoint.gameObject.layer;
                rangeBackground = background.AddComponent<MeshFilter>();
                rangeBackground.sharedMesh = new Mesh { name = "Circuit range quad" };
                rangeMaterial = new Material(shader);
                background.AddComponent<MeshRenderer>().sharedMaterial = rangeMaterial;
            }
            if (rangeTarget != hoveredPoint)
            {
                if (!TryGetRangeAxes(out Vector3 east, out Vector3 north))
                {
                    rangeBackground.gameObject.SetActive(false);
                    rangeTarget = null;
                    return;
                }
                Vector3 center = hoveredPoint.transform.position - Vector3.up * 2f;
                rangeCorners[0] = center - east - north;
                rangeCorners[1] = center + east - north;
                rangeCorners[2] = center - east + north;
                rangeCorners[3] = center + east + north;
                Mesh mesh = rangeBackground.sharedMesh;
                mesh.vertices = rangeCorners;
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                mesh.RecalculateBounds();
                rangeTarget = hoveredPoint;
            }
            rangeBackground.gameObject.SetActive(true);
            rangeMaterial.SetFloat("_Phase", Mathf.Repeat(Time.unscaledTime * 2.14f, 1f));
        }

        private bool TryGetTutorialScreenPosition(out Vector2 screen)
        {
            screen = default;
            var startup = mapUi ? mapUi.GetComponent<HorrorGame.UI.TabletStartupScreen>() : null;
            if (startup && startup.Screen && startup.Screen.gameObject.activeInHierarchy) return false;
            if (!Chapter1MapTutorial.Instance || TutorialFade <= 0f || !MapRenderer.Exists) return false;
            PowerCircuitMapMarker target = null;
            foreach (GameObject point in points)
            {
                if (!point || !point.TryGetComponent(out PowerCircuitMapMarker marker) || !Chapter1MapTutorial.IsTargetCircuit(MapData, marker.CircuitId)) continue;
                target = marker;
                break;
            }
            if (!target) return false;
            Camera camera = MapRenderer.Instance.GetComponentInChildren<Camera>();
            return camera && TryProjectMarker(camera, target.transform, out screen);
        }

        private void DrawTutorialHighlight()
        {
            if (!TryGetTutorialScreenPosition(out Vector2 screen)) return;
            float size = TutorialRingSize;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, TutorialFade);
            GUI.DrawTexture(new Rect(screen.x - size * .5f, Screen.height - screen.y - size * .5f, size, size), GetTutorialRing());
            GUI.color = previous;
        }
        private static Texture2D GetTutorialRing()
        {
            int frame = Mathf.FloorToInt(Time.unscaledTime * 26f) % TutorialFrames.Length;
            if (TutorialFrames[frame]) return TutorialFrames[frame];
            const int size = 64, low = 4, high = 60, side = high - low;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            Color32 border = new Color(.48f, .95f, 1f, .85f);
            Color32 warning = new Color(1f, .3f, .27f, .29f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int perimeter = -1;
                if (x >= low && x <= high && y >= low && y <= high)
                {
                    if (y >= high - 2) perimeter = x - low;
                    else if (x >= high - 2) perimeter = side + high - y;
                    else if (y <= low + 2) perimeter = 2 * side + high - x;
                    else if (x <= low + 2) perimeter = 3 * side + y - low;
                }
                bool dash = perimeter >= 0 && (perimeter - frame + 16) % 16 < 9;
                bool exclamation = x >= 28 && x <= 36 && (y >= 26 && y <= 51 || y >= 13 && y <= 20);
                if (dash) pixels[y * size + x] = border;
                else if (exclamation) pixels[y * size + x] = warning;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            TutorialFrames[frame] = texture;
            return texture;
        }
    }
}
