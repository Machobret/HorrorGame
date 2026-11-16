using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Uses Horror Engine's real UIMap/MapRenderer and overlays selectable circuit points in map space.</summary>
    public sealed class PowerMapCircuitPanel : MonoBehaviour
    {
        public MapData MapData;
        public PowerPanelLayout Layout;
        public DistrictPowerGrid Grid;
        [Tooltip("Collected room-specific diagrams unlock only that room's circuit dots.")]
        public CircuitDiagramData[] CircuitDiagrams;
        [Tooltip("When enabled, dots appear on the normal player map instead of a separate power-panel UI.")]
        public bool DisplayInPlayerMap = true;
        private DistrictPowerGrid grid;
        private UIMap mapUi;
        private readonly List<GameObject> points = new List<GameObject>();
        private PowerCircuitMapMarker hoveredPoint;
        private static Texture2D tutorialRing;

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
        }

        private void Update()
        {
            if (grid == null) grid = Grid;
            if (mapUi == null) mapUi = UIManager.Get<UIMap>(true);
            bool tutorialAllowsTarget = Chapter1MapTutorial.Instance != null && Chapter1MapTutorial.Instance.TutorialActive && Chapter1MapTutorial.Instance.MapToUnlock == MapData;
            bool showCircuits = DisplayInPlayerMap && mapUi && mapUi.gameObject.activeInHierarchy && mapUi.CurrentMap == MapData && PlayerCanViewMap() && (PlayerOwnsCircuitDiagram() || tutorialAllowsTarget);
            if (!showCircuits) { hoveredPoint = null; ClearPoints(); return; }
            if (points.Count == 0) CreateCircuitPoints();
            hoveredPoint = GetCrosshairPoint();
            IUIInput input = UIManager.Instance.GetComponent<IUIInput>();
            if (input != null && input.IsConfirmDown()) TryActivateCrosshairPoint();
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
            if(Layout) foreach (PowerPanelCircuitShape shape in Layout.Circuits)
            {
                if (!IsCircuitRoomAvailable(shape.RoomUniqueId, shape.CircuitId)) continue;
                // MapPosition is generated from the assigned MapRoom. Bounds belong to the
                // obsolete standalone layout editor and may be nowhere near this map.
                Vector2 mapPosition = shape.MapPosition == Vector2.zero
                    ? new Vector2(shape.Bounds.center.x / Layout.CanvasSize.x * width, shape.Bounds.center.y / Layout.CanvasSize.y * height)
                    : shape.MapPosition;
                PowerCircuitMapMarker marker = PowerCircuitMapMarker.Create(markerRoot, layer, shape.CircuitId, new Vector3(mapPosition.x, 6f, -mapPosition.y));
                points.Add(marker.gameObject);
            }
            foreach(var gate in HorrorGame.Environment.GarageDoorCircuitLink.Active)
                if(gate.TryMarker(grid,MapData,out var roomId,out var position) && IsCircuitRoomAvailable(roomId, gate.CircuitId))
                    points.Add(PowerCircuitMapMarker.Create(markerRoot,layer,gate.CircuitId,position).gameObject);
        }

        private bool IsCircuitRoomAvailable(string roomId, string circuitId)
        {
            // The tablet tutorial can expose its one highlighted target before
            // the player has collected a room diagram.
            if (Chapter1MapTutorial.IsTargetCircuit(MapData, circuitId)) return true;
            if (MapData == null || string.IsNullOrWhiteSpace(roomId) || !GameManager.Exists || GameManager.Instance.Inventory == null) return false;
            foreach (DocumentData document in GameManager.Instance.Inventory.Documents)
            {
                if (!(document is CircuitDiagramData diagram)) continue;
                if (diagram.Map != MapData || diagram.RoomUniqueId != roomId) continue;
                // A diagram unlocks only the room it belongs to, and only when
                // the player is physically inside that explored room.
                MapController controller = MapController.GetCurrent();
                if (!controller || controller.Data != MapData) return false;
                MapRoom currentRoom = controller.GetCurrentRoom();
                if (!currentRoom || !currentRoom.TryGetComponent(out ObjectUniqueId id) || id.Id != roomId) return false;
                return currentRoom.State >= MapRoomState.Visited;
            }
            return false;
        }

        private bool PlayerCanViewMap()
        {
            return GameManager.Exists && GameManager.Instance.Inventory != null && MapData && MapData.IsKnownByPlayer();
        }

        private bool PlayerOwnsCircuitDiagram()
        {
            if (!GameManager.Exists || GameManager.Instance.Inventory == null) return false;
            foreach (DocumentData document in GameManager.Instance.Inventory.Documents)
                if (document is CircuitDiagramData) return true;
            return false;
        }

        private void TryActivateCrosshairPoint()
        {
            PowerCircuitMapMarker point = GetCrosshairPoint();
            if (point && !grid.IsShutdown && IsCircuitInRange(point.CircuitId, out _, out _))
                grid.SetCircuit(point.CircuitId, !grid.IsCircuitPowered(point.CircuitId));
        }

        private PowerCircuitMapMarker GetCrosshairPoint()
        {
            Camera mapCamera = MapRenderer.Instance ? MapRenderer.Instance.GetComponentInChildren<Camera>() : null;
            if (!mapCamera) return null;
            Ray ray = mapCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            int layer = LayerMask.GetMask("MapSystem");
            PowerCircuitMapMarker nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 2000f, layer, QueryTriggerInteraction.Collide))
            {
                if (!hit.collider.TryGetComponent(out PowerCircuitMapMarker point) || hit.distance >= nearestDistance) continue;
                nearest = point;
                nearestDistance = hit.distance;
            }
            return nearest;
        }

        private void OnGUI()
        {
            bool tutorialAllowsTarget = Chapter1MapTutorial.Instance != null && Chapter1MapTutorial.Instance.TutorialActive && Chapter1MapTutorial.Instance.MapToUnlock == MapData;
            if (grid == null || !mapUi || !mapUi.gameObject.activeInHierarchy || mapUi.CurrentMap != MapData || !PlayerCanViewMap() || (!PlayerOwnsCircuitDiagram() && !tutorialAllowsTarget)) return;
            Rect energyBar = new Rect(16, 16, 260, 48);
            GUI.Box(energyBar, GUIContent.none);
            GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 7, energyBar.width - 24, 20), "DISTRICT POWER");
            string load = grid.IsShutdown ? "GRID SHUT DOWN — RESET AT FUSE BOX" : $"LOAD  {grid.CurrentConsumption} / {grid.MaximumCapacity}    AVAILABLE  {grid.RemainingCapacity}";
            GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 25, energyBar.width - 24, 20), load);
            DrawTutorialHighlight();
            if (!hoveredPoint) return;
            PowerCircuit circuit = grid.Circuits.Find(value => value.Id == hoveredPoint.CircuitId);
            string activatedName = circuit != null && !string.IsNullOrWhiteSpace(circuit.DisplayName) ? circuit.DisplayName : hoveredPoint.CircuitId;
            int consumption = circuit != null ? circuit.Consumption : 0;
            bool powered = circuit != null && grid.IsCircuitPowered(circuit.Id);
            bool inRange = IsCircuitInRange(hoveredPoint.CircuitId, out float distance, out float range);

            Rect panel = new Rect(Screen.width * .5f - 135, Screen.height * .5f + 48, 270, 68);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 12, panel.y + 9, panel.width - 24, 22), activatedName);
            GUI.Label(new Rect(panel.x + 12, panel.y + 31, panel.width - 24, 18), $"{consumption} power   —   {(powered ? "ON" : "OFF")}");
            GUI.Label(new Rect(panel.x + 12, panel.y + 48, panel.width - 24, 18), inRange ? $"IN RANGE  ({distance:0.0}/{range:0.0}m)" : $"TOO FAR  ({distance:0.0}/{range:0.0}m)");
        }

        private void ClearPoints()
        {
            hoveredPoint = null;
            foreach (GameObject point in points) if (point) Destroy(point);
            points.Clear();
        }
        private void OnDestroy() => ClearPoints();

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

        private void DrawTutorialHighlight()
        {
            if (Chapter1MapTutorial.Instance == null || !Chapter1MapTutorial.Instance.TutorialActive || !MapRenderer.Exists) return;
            PowerCircuitMapMarker target = null;
            foreach (GameObject point in points)
            {
                if (!point || !point.TryGetComponent(out PowerCircuitMapMarker marker) || !Chapter1MapTutorial.IsTargetCircuit(MapData, marker.CircuitId)) continue;
                target = marker;
                break;
            }
            if (!target) return;
            Camera camera = MapRenderer.Instance.GetComponentInChildren<Camera>();
            if (!camera) return;
            Vector3 screen = camera.WorldToScreenPoint(target.transform.position);
            if (screen.z < 0) return;
            float size = 76f + Mathf.Sin(Time.unscaledTime * 6f) * 10f;
            float alpha = .55f + Mathf.Sin(Time.unscaledTime * 6f) * .35f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, .82f, .08f, alpha);
            GUI.DrawTexture(new Rect(screen.x - size * .5f, Screen.height - screen.y - size * .5f, size, size), GetTutorialRing());
            GUI.color = previous;
        }

        private static Texture2D GetTutorialRing()
        {
            if (tutorialRing) return tutorialRing;
            const int size = 64;
            tutorialRing = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * .5f);
                float alpha = distance > .76f && distance < .94f ? 1f : 0f;
                tutorialRing.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            tutorialRing.Apply();
            return tutorialRing;
        }
    }
}
