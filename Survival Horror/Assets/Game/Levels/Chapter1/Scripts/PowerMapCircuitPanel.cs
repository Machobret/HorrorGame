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
        private DistrictPowerGrid grid;
        private UIMap mapUi;
        private readonly List<GameObject> points = new List<GameObject>();
        private PowerCircuitMapMarker hoveredPoint;

        public void Open(DistrictPowerGrid requestedGrid)
        {
            grid = requestedGrid;
            grid?.SyncDefinitionsFromData();
            mapUi = UIManager.Get<UIMap>(true);
            if (!mapUi || !MapData || !grid) return;
            mapUi.Show(MapData);
            Invoke(nameof(RenderTabletMap), 0f);
        }

        private void RenderTabletMap()
        {
            MapRenderer renderer = MapRenderer.Instance;
            if (renderer)
            {
                renderer.GenerateAll(MapData, true);
                MapPlayerFocus.FocusOnPlayer(renderer, MapData);
            }
            CreateCircuitPoints();
        }

        private void Update()
        {
            if (!mapUi || !mapUi.gameObject.activeInHierarchy) { hoveredPoint = null; ClearPoints(); return; }
            hoveredPoint = GetCrosshairPoint();
            IUIInput input = UIManager.Instance.GetComponent<IUIInput>();
            if (input != null && input.IsConfirmDown()) TryActivateCrosshairPoint();
        }

        private void CreateCircuitPoints()
        {
            ClearPoints();
            MapRenderer renderer = MapRenderer.Instance;
            if (!renderer || Layout == null || grid == null) return;
            Transform markerRoot = renderer.transform;
            int layer = LayerMask.NameToLayer("MapSystem");
            float width = MapData.Size.x * MapData.CellSize;
            float height = MapData.Size.y * MapData.CellSize;
            foreach (PowerPanelCircuitShape shape in Layout.Circuits)
            {
                // MapPosition is generated from the assigned MapRoom. Bounds belong to the
                // obsolete standalone layout editor and may be nowhere near this map.
                Vector2 mapPosition = shape.MapPosition == Vector2.zero
                    ? new Vector2(shape.Bounds.center.x / Layout.CanvasSize.x * width, shape.Bounds.center.y / Layout.CanvasSize.y * height)
                    : shape.MapPosition;
                PowerCircuitMapMarker marker = PowerCircuitMapMarker.Create(markerRoot, layer, shape.CircuitId, new Vector3(mapPosition.x, 6f, -mapPosition.y));
                points.Add(marker.gameObject);
            }
        }

        private void TryActivateCrosshairPoint()
        {
            PowerCircuitMapMarker point = GetCrosshairPoint();
            if (point && !grid.IsShutdown)
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
            if (grid == null || !mapUi || !mapUi.gameObject.activeInHierarchy) return;
            Rect energyBar = new Rect(16, 16, 260, 48);
            GUI.Box(energyBar, GUIContent.none);
            GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 7, energyBar.width - 24, 20), "DISTRICT POWER");
            string load = grid.IsShutdown ? "GRID SHUT DOWN — RESET AT FUSE BOX" : $"LOAD  {grid.CurrentConsumption} / {grid.MaximumCapacity}    AVAILABLE  {grid.RemainingCapacity}";
            GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 25, energyBar.width - 24, 20), load);
            if (!hoveredPoint) return;
            PowerCircuit circuit = grid.Circuits.Find(value => value.Id == hoveredPoint.CircuitId);
            string activatedName = circuit != null && !string.IsNullOrWhiteSpace(circuit.DisplayName) ? circuit.DisplayName : hoveredPoint.CircuitId;
            int consumption = circuit != null ? circuit.Consumption : 0;
            bool powered = circuit != null && grid.IsCircuitPowered(circuit.Id);

            Rect panel = new Rect(Screen.width * .5f - 135, Screen.height * .5f + 48, 270, 68);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 12, panel.y + 9, panel.width - 24, 22), activatedName);
            GUI.Label(new Rect(panel.x + 12, panel.y + 31, panel.width - 24, 22), $"{consumption} power   —   {(powered ? "ON" : "OFF")}");
        }

        private void ClearPoints()
        {
            hoveredPoint = null;
            foreach (GameObject point in points) if (point) Destroy(point);
            points.Clear();
        }
        private void OnDestroy() => ClearPoints();
    }
}
