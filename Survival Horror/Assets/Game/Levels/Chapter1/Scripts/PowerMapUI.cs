using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Chapter1
{
    /// <summary>Private map screen for the electrical tablet. It owns a local MapRenderer and never opens the player's UIMap.</summary>
    public sealed class PowerMapUI : MonoBehaviour
    {
        public MapRenderer LocalRenderer;
        public RawImage MapImage;
        public DistrictPowerGrid Grid;
        public MapData MapData;
        public PowerPanelLayout Layout;
        public float MovementSpeed = 200f;

        private readonly List<GameObject> points = new List<GameObject>();
        private IUIInput input;
        private bool open;
        private string hoveredCircuitId;

        public void Open(DistrictPowerGrid grid, MapData map, PowerPanelLayout layout)
        {
            Grid = grid; MapData = map; Layout = layout;
            Grid?.SyncDefinitionsFromData();
            if (!Grid || !MapData || !LocalRenderer) return;
            gameObject.SetActive(true); open = true;
            if (PauseController.Instance) PauseController.Instance.Pause(this);
            Invoke(nameof(RenderMap), 0f);
        }
        private void RenderMap()
        {
            LocalRenderer.gameObject.SetActive(true);
            LocalRenderer.GenerateAll(MapData, true);
            LocalRenderer.SetPlayerVisible(false);
            MapPlayerFocus.FocusOnPlayer(LocalRenderer, MapData);
            if (MapImage) MapImage.texture = LocalRenderer.RenderTexture;
            CreatePoints();
            input = UIManager.Instance ? UIManager.Instance.GetComponent<IUIInput>() : null;
            input?.Flush();
        }
        private void Update()
        {
            if (!open || input == null) return;
            Vector2 move = input.GetPrimaryAxis();
            if (move.sqrMagnitude > .01f) LocalRenderer.MoveTarget(move * Time.unscaledDeltaTime * MovementSpeed);
            hoveredCircuitId = GetCrosshairCircuitId();
            if (input.IsConfirmDown()) ToggleCrosshairPoint();
            if (input.IsCancelDown()) Close();
        }
        public void Close()
        {
            if (!open) return;
            open = false; hoveredCircuitId = null; ClearPoints();
            if (LocalRenderer) LocalRenderer.gameObject.SetActive(false);
            if (PauseController.Instance) PauseController.Instance.Resume(this);
            gameObject.SetActive(false);
        }
        private void CreatePoints()
        {
            ClearPoints();
            Transform markerRoot = LocalRenderer.transform;
            if (!Layout) return;
            int layer = LayerMask.NameToLayer("MapSystem");
            float width = MapData.Size.x * MapData.CellSize;
            float height = MapData.Size.y * MapData.CellSize;
            foreach (PowerPanelCircuitShape shape in Layout.Circuits)
            {
                Vector2 mapPosition = shape.MapPosition == Vector2.zero ? new Vector2(shape.Bounds.center.x / Layout.CanvasSize.x * width, shape.Bounds.center.y / Layout.CanvasSize.y * height) : shape.MapPosition;
                PowerCircuitMapMarker point = PowerCircuitMapMarker.Create(markerRoot, layer, shape.CircuitId, new Vector3(mapPosition.x, 6f, -mapPosition.y));
                points.Add(point.gameObject);
            }
        }
        private void ToggleCrosshairPoint()
        {
            if (Grid.IsShutdown) return;
            string circuitId = GetCrosshairCircuitId();
            if (!string.IsNullOrEmpty(circuitId)) Grid.SetCircuit(circuitId, !Grid.IsCircuitPowered(circuitId));
        }
        private string GetCrosshairCircuitId()
        {
            Camera camera = LocalRenderer ? LocalRenderer.GetComponentInChildren<Camera>() : null;
            if (!camera) return null;
            PowerCircuitMapMarker nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(camera.ViewportPointToRay(new Vector3(.5f, .5f)), 2000f, LayerMask.GetMask("MapSystem"), QueryTriggerInteraction.Collide))
            {
                if (!hit.collider.TryGetComponent(out PowerCircuitMapMarker point) || hit.distance >= nearestDistance) continue;
                nearest = point;
                nearestDistance = hit.distance;
            }
            return nearest ? nearest.CircuitId : null;
        }
        private void OnGUI()
        {
            if (!open || !Grid) return;
            Rect energyBar = new Rect(16, 16, 260, 48);
            GUI.Box(energyBar, GUIContent.none);
            GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 7, energyBar.width - 24, 20), "DISTRICT POWER");
            string load = Grid.IsShutdown ? "GRID SHUT DOWN — RESET AT FUSE BOX" : $"LOAD  {Grid.CurrentConsumption} / {Grid.MaximumCapacity}    AVAILABLE  {Grid.RemainingCapacity}";
            GUI.Label(new Rect(energyBar.x + 12, energyBar.y + 25, energyBar.width - 24, 20), load);
            if (string.IsNullOrEmpty(hoveredCircuitId)) return;
            PowerCircuit circuit = Grid.Circuits.Find(value => value.Id == hoveredCircuitId);
            string activatedName = circuit != null && !string.IsNullOrWhiteSpace(circuit.DisplayName) ? circuit.DisplayName : hoveredCircuitId;
            int consumption = circuit != null ? circuit.Consumption : 0;
            bool powered = circuit != null && Grid.IsCircuitPowered(circuit.Id);
            Rect panel = new Rect(Screen.width * .5f - 135, Screen.height * .5f + 48, 270, 68);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 12, panel.y + 9, panel.width - 24, 22), activatedName);
            GUI.Label(new Rect(panel.x + 12, panel.y + 31, panel.width - 24, 22), $"{consumption} power   —   {(powered ? "ON" : "OFF")}");
        }
        private void ClearPoints() { foreach (GameObject point in points) if (point) Destroy(point); points.Clear(); }
        private void OnDestroy() => ClearPoints();
    }
}
