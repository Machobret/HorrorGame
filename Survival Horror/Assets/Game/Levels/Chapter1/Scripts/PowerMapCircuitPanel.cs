using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.UI;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Uses Horror Engine's real UIMap/MapRenderer and overlays selectable circuit points in map space.</summary>
    [DefaultExecutionOrder(20)]
    public sealed class PowerMapCircuitPanel : MonoBehaviour
    {
        internal const string BreakerCircuitId = "alley_breaker";
        internal const string ConnectionLostLabel = "CONNECTION LOST";
        internal const string OutOfServiceLabel = "OLD / NO SERVICE";
        private const string BreakerLabel = "Electricity box";
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
        private PowerCircuitMapMarker snappedPoint;
        private DistrictPowerGrid subscribedGrid;
        private float statusTweenStarted;
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

        private MapRoom BreakerMapRoom
        {
            get
            {
                var tutorial = Chapter1MapTutorial.Instance;
                if (tutorial && tutorial.UsesBreakerObjective && tutorial.Grid == SourceGrid && tutorial.MapToUnlock == MapData)
                    return tutorial.BreakerRoom;
                var dot = TutorialDot;
                return dot ? dot.Room : null;
            }
        }
        public bool CanAccessCircuit(string id) => HasAccessibleCircuit(id, null);

        public bool HasCircuitService(string id)
        {
            string roomId = GetCurrentRoomId();
            return roomId != null && HasAccessibleCircuit(id, roomId);
        }

        private bool HasAccessibleCircuit(string id, string requiredRoom)
        {
            if (!PlayerCanViewMap()) return false;
            var tutorial = TutorialDot;
            if (IsBreakerCircuit(id))
            {
                var room = BreakerMapRoom;
                return room && room.TryGetComponent(out ObjectUniqueId roomId) &&
                    MatchesCircuitRoom(id, roomId.Id, requiredRoom);
            }
            if (tutorial && tutorial.CircuitId == id &&
                (requiredRoom == null || MatchesCircuitRoom(id, tutorial.ToShape(MapData).RoomUniqueId, requiredRoom))) return true;

            if (SequencePuzzle)
                foreach (var dot in SequencePuzzle.SegmentDots)
                    if (dot && dot.CircuitId == id && IsSequenceDotAvailable(dot) &&
                        (requiredRoom == null || MatchesCircuitRoom(id, dot.ToShape(MapData).RoomUniqueId, requiredRoom))) return true;

            if (Layout)
                foreach (var shape in Layout.Circuits)
                    if (shape.CircuitId == id && MatchesCircuitRoom(id, shape.RoomUniqueId, requiredRoom) &&
                        IsCircuitRoomAvailable(shape.RoomUniqueId, id)) return true;

            foreach (var gate in HorrorGame.Environment.GarageDoorCircuitLink.Active)
                if (gate.CircuitId == id && gate.TryMarker(SourceGrid, MapData, out var room, out _) &&
                    MatchesCircuitRoom(id, room, requiredRoom) && IsCircuitRoomAvailable(room, id)) return true;
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
            if (!showCircuits) { ClearPoints(); return; }
            if (points.Count == 0) CreateCircuitPoints();
            if (subscribedGrid != grid)
            {
                StopWatchingDisplacement();
                subscribedGrid = grid;
                if (subscribedGrid) subscribedGrid.CircuitDisplaced += OnCircuitDisplaced;
            }
            var nextPoint = mapUi.IsMapDirectoryOpen || mapUi.IsMapPointerDragging ? null : GetCrosshairPoint();
            snappedPoint = mapUi.UsesMouseOnMap ? null : nextPoint;
            mapUi.SetCircuitCrosshair(snappedPoint ? snappedPoint.transform : null);
            string currentRoomId = GetCurrentRoomId();
            // The player room stays readable above the dimmed context even if a
            // circuit marker is visible in a neighbouring room.
            if (MapRenderer.Exists) MapRenderer.Instance.FocusCircuitRoom(nextPoint ? currentRoomId : null);
            bool nextInRange = nextPoint && (nextPoint.IsInformational || IsMarkerReachable(nextPoint, currentRoomId));
            if (hoveredPoint != nextPoint || HoveredInRange != nextInRange)
            {
                if (hoveredPoint != nextPoint || nextInRange)
                    statusTweenStarted = Time.unscaledTime;
                if (hoveredPoint) hoveredPoint.SetHovered(false);
                hoveredPoint = nextPoint;
                HoveredInRange = nextInRange;
                if (hoveredPoint) hoveredPoint.SetHovered(true);
            }
            IUIInput input = UIManager.Instance.GetComponent<IUIInput>();
            bool click = mapUi.MapClickedThisFrame;
            bool activateCircuit = click || input != null && input.IsConfirmDown();
            if (hoveredPoint && !hoveredPoint.IsInformational && activateCircuit)
            {
                if (HoveredInRange)
                    TryToggleCircuit(hoveredPoint.CircuitId);
                else
                    HorrorGame.UI.GameplayNotifications.ShowCircuitMessage(
                        "You have to be in the same room to activate this circuit");
            }
            foreach (var point in points)
                if (point && point.TryGetComponent(out PowerCircuitMapMarker marker))
                {
                    bool reachable = marker.IsInformational || IsMarkerReachable(marker, currentRoomId);
                    marker.SetPowerVisual(reachable && !marker.IsInformational && IsCircuitShownPowered(marker.CircuitId), reachable);
                }
        }

        private void OnCircuitDisplaced(string circuitId)
        {
            foreach (var point in points)
            {
                if (!point || !point.TryGetComponent(out PowerCircuitMapMarker marker) || marker.CircuitId != circuitId) continue;
                marker.AnimateDisplacement();
            }
        }

        private void StopWatchingDisplacement()
        {
            if (subscribedGrid) subscribedGrid.CircuitDisplaced -= OnCircuitDisplaced;
            subscribedGrid = null;
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
            if (TryGetBreakerMapPosition(out Vector3 breakerPosition, out var breakerRoomId))
                points.Add(PowerCircuitMapMarker.Create(markerRoot, layer, BreakerCircuitId, breakerPosition,
                    breakerRoomId, informational: !IsBreakerCircuit(BreakerCircuitId)).gameObject);
            if(Layout) foreach (PowerPanelCircuitShape shape in Layout.Circuits)
            {
                if (tutorialDot && shape.CircuitId == tutorialDot.CircuitId) continue;
                var depot = Chapter1Opening.Instance;
                if (depot && depot.IsDepotCircuit(grid, shape.CircuitId) && depot.DepotMotor &&
                    depot.DepotMotor.TryMarker(grid, MapData, out _, out _)) continue;
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

        private bool TryGetBreakerMapPosition(out Vector3 position, out string roomId)
        {
            position = default;
            roomId = null;
            var room = BreakerMapRoom;
            var source = SourceGrid;
            if (!room || !source || !source.Supply || !room.TryGetComponent(out ObjectUniqueId id)) return false;

            Vector3 local = room.transform.InverseTransformPoint(source.Supply.transform.position);
            position = MapData.GetTRS(room.Offset, room.Rotation, room.Scale, room.ZOrder)
                .MultiplyPoint3x4(new Vector3(local.x, 0f, local.z));
            position.y = 6f;
            roomId = id.Id;
            return true;
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
            var opening = Chapter1Opening.Instance;
            if (opening && opening.DepotMotor && opening.IsDepotCircuit(SourceGrid, circuitId)) return true;
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

        public bool HasControlPower(string circuitId)
        {
            if (IsCircuitOutOfService(circuitId)) return false;
            if (IsBreakerCircuit(circuitId)) return true;
            var source = SourceGrid;
            if (!source || !source.HasSupply || source.IsShutdown) return false;
            var opening = Chapter1Opening.Instance;
            return !opening || !opening.IsDepotCircuit(source, circuitId) || opening.DepotControlHasPower;
        }

        public bool IsCircuitOutOfService(string circuitId) =>
            SourceGrid && SourceGrid.IsCircuitOutOfService(circuitId);

        public bool TryGetQueuedCircuit(string circuitId, out bool requested)
        {
            requested = false;
            var bays = RandomShutterKeyPuzzle.TabletBayPuzzle;
            return bays && bays.TryGetQueuedCircuit(SourceGrid, circuitId, out requested);
        }

        private bool IsBreakerCircuit(string circuitId) => circuitId == BreakerCircuitId &&
            SourceGrid && SourceGrid.Supply && SourceGrid.Supply.RequiresTabletAuthorization;

        public bool IsCircuitShownPowered(string circuitId)
        {
            if (IsBreakerCircuit(circuitId)) return SourceGrid.Supply.IsAuthorized;
            return TryGetQueuedCircuit(circuitId, out bool requested) ? requested :
                SourceGrid && SourceGrid.IsCircuitPowered(circuitId);
        }

        public bool TryToggleCircuit(string circuitId)
        {
            DistrictPowerGrid source = SourceGrid;
            if (!source || !HasCircuitService(circuitId)) return false;
            if (IsCircuitOutOfService(circuitId)) return false;
            if (IsBreakerCircuit(circuitId)) return source.Supply.SetTabletAuthorization(!source.Supply.IsAuthorized);
            if (source.IsShutdown) return false;
            if (!HasControlPower(circuitId))
            {
                PlayerMonologue.Show("No power");
                return false;
            }
            var opening = Chapter1Opening.Instance;
            if (opening && opening.IsDepotCircuit(source, circuitId))
                return opening.TryToggleDepotPower();
            var bays = RandomShutterKeyPuzzle.TabletBayPuzzle;
            if (bays && bays.TryQueueCircuit(source, circuitId, out bool changed)) return changed;
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
            bool mouse = mapUi.UsesMouseOnMap;
            float radius = (mouse ? 23f : 24f) * Mathf.Max(.75f, EffectScale);
            float releaseRadius = radius * 1.45f;
            if (!mouse && snappedPoint && TryProjectMarker(mapCamera, snappedPoint.transform, out Vector2 snappedScreen) &&
                (crosshair - snappedScreen).sqrMagnitude < releaseRadius * releaseRadius) return snappedPoint;
            PowerCircuitMapMarker nearest = null;
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
                GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 7, energyBar.width - 24, 20), GameText.Translate("DISTRICT POWER"), GameText.DisplayStyle(GUI.skin.label));
                string load = grid.IsShutdown ? "GRID SHUT DOWN — RESET AT FUSE BOX" : !grid.EnforceCapacityLimit ? "CIRCUITS ONLINE" : $"LOAD  {grid.CurrentConsumption} / {grid.MaximumCapacity}    AVAILABLE  {grid.RemainingCapacity}";
                GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 25, energyBar.width - 24, 20), GameText.Translate(load), GameText.DisplayStyle(GUI.skin.label));
            }
            if (!GetTabletOutput()) DrawTutorialHighlight();
            if (SequencePuzzle && SequencePuzzle.ShowingWrongAnswer)
                GUI.Box(new Rect(Screen.width * .5f - 125, Screen.height * .72f, 250, 32), GameText.Translate("SHUTTER PATTERN REJECTED"), GameText.DisplayStyle(GUI.skin.box));
            Camera camera = MapRenderer.Instance.GetComponentInChildren<Camera>();
            if (!camera) return;
            var filter = mapUi.GetComponent<HorrorGame.UI.TabletDisplayFilter>();
            if (!mapUi.TryProjectMapViewport(Vector2.zero, false, out Vector2 bottomLeft) ||
                !mapUi.TryProjectMapViewport(Vector2.one, false, out Vector2 topRight)) return;
            if (filter && filter.isActiveAndEnabled)
            {
                bottomLeft = filter.ProjectToGlass(bottomLeft);
                topRight = filter.ProjectToGlass(topRight);
            }
            var mapBounds = Rect.MinMaxRect(bottomLeft.x, Screen.height - topRight.y,
                topRight.x, Screen.height - bottomLeft.y);
            // These labels are OnGUI overlays, not children of the tablet Canvas.
            // Scale them from the map viewport that is actually visible on the
            // glass. This is the same screen-space area that scales the marker
            // icons, including aspect-ratio changes and display filtering.
            const float referenceMapWidth = (0.935f - 0.065f) * 1280f;
            const float referenceMapHeight = (0.82f - 0.14f) * 720f;
            float resolutionScale = Mathf.Sqrt(
                (mapBounds.width / referenceMapWidth) * (mapBounds.height / referenceMapHeight));
            float textScale = EffectScale * resolutionScale;
            const float inset = 4f;
            var clip = Rect.MinMaxRect(mapBounds.xMin + inset, mapBounds.yMin + inset,
                mapBounds.xMax - inset, mapBounds.yMax - inset);
            if (clip.width <= 0f || clip.height <= 0f) return;
            Rect legendBounds = GetLegendBounds(filter);
            string currentRoomId = GetCurrentRoomId();
            var workspace = mapUi.GetComponent<HorrorGame.UI.TabletWorkspace>();
            var font = workspace && workspace.Footer && workspace.Footer.font
                ? workspace.Footer.font.sourceFontFile : null;
            var style = new GUIStyle(GameText.DisplayStyle(GUI.skin.label))
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = font ? FontStyle.Normal : FontStyle.Bold,
                fontSize = Mathf.Max(1, Mathf.RoundToInt(7f * textScale))
            };
            if (font) style.font = font;
            foreach (var point in points)
            {
                if (!point || !point.TryGetComponent(out PowerCircuitMapMarker marker) ||
                    marker.IsInformational || !IsMarkerReachable(marker, currentRoomId) ||
                    marker == hoveredPoint ||
                    !TryProjectMarker(camera, point.transform, out Vector2 idleScreen)) continue;
                if (filter && filter.isActiveAndEnabled) idleScreen = filter.ProjectToGlass(idleScreen);
                bool idlePowered = IsCircuitShownPowered(marker.CircuitId);
                bool idleAvailable = HasControlPower(marker.CircuitId);
                style.normal.textColor = GetStatusColor(idlePowered, idleAvailable);
                style.hover.textColor = style.normal.textColor;
                style.active.textColor = style.normal.textColor;
                if (IsCircuitOutOfService(marker.CircuitId))
                {
                    float statusWidth = style.CalcSize(new GUIContent(GameText.Translate(OutOfServiceLabel))).x + 12f * textScale;
                    DrawClippedLabel(StatusRect(idleScreen, mapBounds, 8f * textScale, 13f * textScale, statusWidth),
                        clip, legendBounds, OutOfServiceLabel, style);
                    continue;
                }
                DrawStatusLabel(idleScreen, clip, legendBounds, 8f * textScale, 11f * textScale, 42f * textScale,
                    idlePowered, idleAvailable, TryGetQueuedCircuit(marker.CircuitId, out _), style);
            }

            if (!hoveredPoint || !TryProjectMarker(camera, hoveredPoint.transform, out Vector2 screen)) return;
            if (filter && filter.isActiveAndEnabled) screen = filter.ProjectToGlass(screen);
            bool outOfService = IsCircuitOutOfService(hoveredPoint.CircuitId);
            if (!HoveredInRange || outOfService)
            {
                style.fontSize = Mathf.Max(1, Mathf.RoundToInt(13f * textScale));
                style.normal.textColor = new Color(1f, .35f, .32f);
                style.hover.textColor = style.normal.textColor;
                style.active.textColor = style.normal.textColor;
                string alertLabel = outOfService ? OutOfServiceLabel : ConnectionLostLabel;
                float alertWidth = style.CalcSize(new GUIContent(GameText.Translate(alertLabel))).x + 12f * textScale;
                var alert = StatusRect(screen, mapBounds, 17f * textScale, 19f * textScale, alertWidth);
                DrawClippedLabel(alert, clip, legendBounds, alertLabel, style);
                return;
            }

            PowerCircuit circuit = grid.Circuits.Find(value => value.Id == hoveredPoint.CircuitId);
            string activatedName = hoveredPoint.CircuitId == BreakerCircuitId ? BreakerLabel :
                circuit != null && !string.IsNullOrWhiteSpace(circuit.DisplayName) ? circuit.DisplayName : hoveredPoint.CircuitId;
            bool controlAvailable = HasControlPower(hoveredPoint.CircuitId);
            var opening = Chapter1Opening.Instance;
            if (opening && opening.IsDepotCircuit(grid, hoveredPoint.CircuitId))
                activatedName = opening.DepotMotor ? "Main shutter power" : opening.DepotUnlocked ? "Depot unlocked" : "Entrance shutter power";
            float elapsed = Time.unscaledTime - statusTweenStarted;
            float nameProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .26f));
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(9f, 15f, nameProgress) * textScale));
            Color nameColor = hoveredPoint.IsInformational ? new Color(1f, .82f, .36f) :
                GetStatusColor(IsCircuitShownPowered(hoveredPoint.CircuitId), controlAvailable);
            nameColor.a = nameProgress;
            style.normal.textColor = nameColor;
            style.hover.textColor = nameColor;
            style.active.textColor = nameColor;
            var name = new GUIContent(GameText.Translate(activatedName).ToUpperInvariant());
            float width = Mathf.Min(style.CalcSize(name).x + 12f * textScale, Screen.width);
            float nameHeight = 22f * textScale;
            var label = new Rect(screen.x - width * .5f,
                Screen.height - screen.y - Mathf.Lerp(29f, 39f, nameProgress) * textScale, width, nameHeight);
            DrawClippedLabel(label, clip, legendBounds, name.text, style);
            if (hoveredPoint.IsInformational || !IsMarkerReachable(hoveredPoint, currentRoomId)) return;

            bool powered = IsCircuitShownPowered(hoveredPoint.CircuitId);
            const float slideDuration = .18f;
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideDuration));
            float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - slideDuration) / .15f));
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(7f, 10f, growth) * textScale));
            style.normal.textColor = GetStatusColor(powered, controlAvailable);
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            DrawStatusLabel(screen, clip, legendBounds, Mathf.Lerp(8f, 13f, slide) * textScale,
                12f * textScale, 42f * textScale, powered, controlAvailable, TryGetQueuedCircuit(hoveredPoint.CircuitId, out _), style);
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
            float width, bool powered, bool available, bool queued, GUIStyle style)
        {
            if (queued) width *= 1.5f;
            var label = new Rect(screen.x - width * .5f, Screen.height - screen.y + gap, width, height);
            string status = !available ? "NO POWER" : powered ? "ON" : "OFF";
            if (available && queued) status = powered ? "QUEUED ON" : "QUEUED OFF";
            DrawClippedLabel(label, clip, legendBounds, status, style);
        }

        private static void DrawClippedLabel(Rect label, Rect clip, Rect legendBounds, string text, GUIStyle style)
        {
            if (legendBounds.width > 0f && legendBounds.Overlaps(label)) return;
            label.position -= clip.position;
            GUI.BeginGroup(clip);
            GUI.Label(label, GameText.Translate(text), style);
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

        private static Color GetStatusColor(bool powered, bool available) =>
            !available ? new Color(1f, .43f, .40f) : powered ? new Color(.47f, .94f, .59f) : new Color(1f, .75f, .25f);

        private void ClearPoints()
        {
            if (points.Count > 0)
            {
                if (mapUi && mapUi.CurrentMap == MapData) mapUi.SetCircuitCrosshair(null);
                if (MapRenderer.Exists && MapRenderer.Instance.CurrentMap == MapData)
                    MapRenderer.Instance.FocusCircuitRoom(null);
            }
            StopWatchingDisplacement();
            hoveredPoint = null;
            snappedPoint = null;
            HoveredInRange = false;
            foreach (GameObject point in points) if (point) Destroy(point);
            points.Clear();
        }
        private void OnDisable() => ClearPoints();
        private void OnDestroy()
        {
            ClearPoints();
            if (tutorialRingOverlay) Destroy(tutorialRingOverlay.gameObject);
        }

        private bool IsMarkerReachable(PowerCircuitMapMarker marker, string roomId) =>
            roomId != null && MatchesCircuitRoom(marker.CircuitId, marker.RoomUniqueId, roomId);

        private bool MatchesCircuitRoom(string circuitId, string primaryRoomId, string requiredRoom)
        {
            if (requiredRoom == null || primaryRoomId == requiredRoom) return true;
            var source = SourceGrid;
            if (source && source.HasAdditionalTabletAccess(circuitId, requiredRoom)) return true;
            var opening = Chapter1Opening.Instance;
            return opening && opening.CanUseDepotCircuitFromOffice(source, circuitId, requiredRoom);
        }

        private UnityEngine.UI.RawImage GetTabletOutput()
        {
            var filter = mapUi ? mapUi.GetComponent<HorrorGame.UI.TabletDisplayFilter>() : null;
            return filter && filter.isActiveAndEnabled ? filter.Output : null;
        }

        private void LateUpdate()
        {
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

        private bool TryGetTutorialScreenPosition(out Vector2 screen)
        {
            screen = default;
            var startup = mapUi ? mapUi.GetComponent<HorrorGame.UI.TabletStartupScreen>() : null;
            if (startup && startup.Screen && startup.Screen.gameObject.activeInHierarchy) return false;
            var tutorial = Chapter1MapTutorial.Instance;
            if (!tutorial || TutorialFade <= 0f || !MapRenderer.Exists) return false;
            PowerCircuitMapMarker target = null;
            foreach (GameObject point in points)
            {
                if (!point || !point.TryGetComponent(out PowerCircuitMapMarker marker)) continue;
                bool isTarget = tutorial.WaitingForCircuitPower ? marker.CircuitId == BreakerCircuitId :
                    !marker.IsInformational && Chapter1MapTutorial.IsTargetCircuit(MapData, marker.CircuitId) &&
                    HasControlPower(marker.CircuitId);
                if (!isTarget) continue;
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
