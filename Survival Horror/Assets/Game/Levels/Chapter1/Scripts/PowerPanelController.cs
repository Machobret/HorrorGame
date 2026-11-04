using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Scene-authored equivalent of MapController for the tablet overlay. Child PowerCircuitPanelElements are the source of truth.</summary>
    public sealed class PowerPanelController : MonoBehaviour
    {
        public PowerPanelLayout Data;
        public HorrorEngine.MapController SourceMap;
        [Tooltip("Authoring data for the grid. If empty, the controller finds the DistrictPowerGrid in this scene automatically.")]
        public DistrictPowerGridData GridData;
        public DistrictPowerGrid Grid;
        public Vector2 CanvasSize = new Vector2(800, 500);
        public bool AutoSerializeChildren = true;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || !AutoSerializeChildren || !Data) return;
            UnityEditor.EditorApplication.delayCall += RebuildAuthoringDataAfterValidation;
        }

        private void RebuildAuthoringDataAfterValidation()
        {
            if (!this || Application.isPlaying || !AutoSerializeChildren || !Data) return;
            SerializeToData();
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        /// <summary>Finds the actual authored map, skipping empty/template MapControllers.</summary>
        public HorrorEngine.MapController ResolveSourceMap()
        {
            if (SourceMap && SourceMap.Data) return SourceMap;

            HorrorEngine.MapController[] maps = FindObjectsByType<HorrorEngine.MapController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (HorrorEngine.MapController map in maps)
            {
                if (!map || !map.Data) continue;
                SourceMap = map;
                return map;
            }
            return null;
        }

        /// <summary>
        /// Converts dots created by the earlier free-floating editor into room-relative dots.
        /// New dots are assigned immediately by the Map Editor, so this only affects legacy ones.
        /// </summary>
        private void BindLegacyDotsToRooms()
        {
            HorrorEngine.MapController mapController = ResolveSourceMap();
            if (!mapController || !mapController.Data) return;

            HorrorEngine.MapRoom[] rooms = mapController.GetComponentsInChildren<HorrorEngine.MapRoom>(true);
            if (rooms.Length == 0) return;

            foreach (PowerCircuitPanelElement element in GetComponentsInChildren<PowerCircuitPanelElement>(true))
            {
                if (element.Room) continue;

                Vector2 oldPosition = new Vector2(element.transform.localPosition.x, element.transform.localPosition.z);
                HorrorEngine.MapRoom nearestRoom = null;
                float nearestDistance = float.MaxValue;
                foreach (HorrorEngine.MapRoom room in rooms)
                {
                    float distance = (room.Offset - oldPosition).sqrMagnitude;
                    if (distance < nearestDistance) { nearestDistance = distance; nearestRoom = room; }
                }
                if (!nearestRoom) continue;

                Vector3 scale = nearestRoom.Scale;
                scale.z *= -1f;
                Matrix4x4 roomTransform = Matrix4x4.TRS(
                    new Vector3(nearestRoom.Offset.x, 0, nearestRoom.Offset.y),
                    Quaternion.Euler(0, nearestRoom.Rotation, 0),
                    scale * mapController.Data.GlobalScale);
                Vector3 local = roomTransform.inverse.MultiplyPoint(new Vector3(oldPosition.x, 0, oldPosition.y));
                element.Room = nearestRoom;
                element.RoomOffset = new Vector2(local.x, local.z);
            }
        }

        public DistrictPowerGrid ResolveGrid()
        {
            if (Grid && Grid.Data) { if (!GridData) GridData = Grid.Data; return Grid; }
            DistrictPowerGrid[] grids = FindObjectsByType<DistrictPowerGrid>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (DistrictPowerGrid candidate in grids)
            {
                if (!candidate) continue;
                Grid = candidate;
                if (!GridData) GridData = candidate.Data;
                return candidate;
            }
            return null;
        }

        /// <summary>Copies this map dot's editable properties to the grid asset and its linked scene targets.</summary>
        public void SyncCircuitData(PowerCircuitPanelElement element, string previousId = null)
        {
            if (!element || string.IsNullOrWhiteSpace(element.CircuitId)) return;
            DistrictPowerGrid resolvedGrid = ResolveGrid();
            if (!GridData && resolvedGrid) GridData = resolvedGrid.Data;
            if (GridData)
            {
                // Prefer an existing circuit with the generated ID. This lets a newly placed
                // "Pharmacy Refrigerator" dot attach to the authored pharmacy_refrigerator data.
                PowerCircuit circuit = GridData.Circuits.Find(value => value.Id == element.CircuitId);
                PowerCircuit previousCircuit = null;
                if (!string.IsNullOrWhiteSpace(previousId) && previousId != element.CircuitId)
                    previousCircuit = GridData.Circuits.Find(value => value.Id == previousId);
                if (circuit == null) circuit = previousCircuit;
                if (circuit == null) { circuit = new PowerCircuit(); GridData.Circuits.Add(circuit); }
                else if (previousCircuit != null && previousCircuit != circuit) GridData.Circuits.Remove(previousCircuit);
                circuit.Id = element.CircuitId;
                circuit.DisplayName = string.IsNullOrWhiteSpace(element.Label) ? element.CircuitId : element.Label;
                circuit.Consumption = Mathf.Max(0, element.Consumption);
                circuit.StartsEnabled = element.StartsEnabled;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(GridData);
#endif
            }

            foreach (CircuitDrivenObject target in element.LinkedTargets)
            {
                if (!target) continue;
                target.Grid = resolvedGrid;
                target.CircuitId = element.CircuitId;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(target);
#endif
            }
        }

        /// <summary>Creates a stable readable key from the circuit name. Designers never need to type IDs.</summary>
        public void RefreshAutomaticCircuitId(PowerCircuitPanelElement element)
        {
            if (!element) return;
            string previousId = element.CircuitId;
            string source = string.IsNullOrWhiteSpace(element.Label) ? "circuit" : element.Label.Trim().ToLowerInvariant();
            StringBuilder builder = new StringBuilder(source.Length);
            bool previousWasSeparator = false;
            foreach (char character in source)
            {
                if (char.IsLetterOrDigit(character)) { builder.Append(character); previousWasSeparator = false; }
                else if (!previousWasSeparator && builder.Length > 0) { builder.Append('_'); previousWasSeparator = true; }
            }
            string baseId = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(baseId)) baseId = "circuit";

            HashSet<string> occupied = new HashSet<string>();
            foreach (PowerCircuitPanelElement other in GetComponentsInChildren<PowerCircuitPanelElement>(true))
                if (other != element && !string.IsNullOrEmpty(other.CircuitId)) occupied.Add(other.CircuitId);

            string candidate = baseId;
            int number = 2;
            while (occupied.Contains(candidate)) candidate = baseId + "_" + number++;
            element.CircuitId = candidate;
            element.gameObject.name = "Circuit Dot — " + (string.IsNullOrWhiteSpace(element.Label) ? candidate : element.Label.Trim());
            SyncCircuitData(element, previousId);
        }

        public void SerializeToData()
        {
            if (!Data) return;
            Data.CanvasSize = CanvasSize;
            BindLegacyDotsToRooms();
            ResolveGrid();
            ResolveSourceMap();
            foreach (PowerCircuitPanelElement element in GetComponentsInChildren<PowerCircuitPanelElement>(true))
            {
                SyncCircuitData(element);
                PowerPanelCircuitShape shape = element.ToShape(SourceMap ? SourceMap.Data : null);
                int index = Data.Circuits.FindIndex(value => value.CircuitId == shape.CircuitId);
                if (index >= 0) Data.Circuits[index] = shape;
                else Data.Circuits.Add(shape);
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(Data);
#endif
        }
    }

    /// <summary>Place one of these under PowerPanelController for every selectable power circuit.</summary>
    public sealed class PowerCircuitPanelElement : MonoBehaviour
    {
        public string CircuitId;
        public string Label;
        [Min(0)] public int Consumption = 20;
        public bool StartsEnabled;
        [Tooltip("The map room this circuit dot belongs to. The editor assigns the currently selected room when adding a circuit.")]
        public HorrorEngine.MapRoom Room;
        [Tooltip("Dot position in the selected room's local map space.")]
        public Vector2 RoomOffset;
        [Tooltip("Lamps, shutters, and other CircuitDrivenObjects powered by this circuit.")]
        public List<CircuitDrivenObject> LinkedTargets = new List<CircuitDrivenObject>();
        public Vector2 Size = new Vector2(220, 65);
        public List<Vector2> BorderPoints = new List<Vector2>();

        public PowerPanelCircuitShape ToShape(HorrorEngine.MapData map)
        {
            Vector2 center = Room ? RoomOffset : new Vector2(transform.localPosition.x, transform.localPosition.z);
            Rect bounds = new Rect(center - Size * .5f, Size);
            List<Vector2> points = new List<Vector2>(BorderPoints);
            if (points.Count == 0) points.AddRange(new[] { bounds.min, new Vector2(bounds.xMax, bounds.yMin), bounds.max, new Vector2(bounds.xMin, bounds.yMax) });
            Vector2 mapPosition = center;
            if (Room && map)
            {
                Matrix4x4 matrix = map.GetTRS(Room.Offset, Room.Rotation, Room.Scale, Room.ZOrder);
                Vector3 point = matrix.MultiplyPoint(new Vector3(RoomOffset.x, 0, RoomOffset.y));
                mapPosition = new Vector2(point.x, -point.z);
            }
            return new PowerPanelCircuitShape { CircuitId = CircuitId, Label = string.IsNullOrWhiteSpace(Label) ? CircuitId : Label, Bounds = bounds, BorderPoints = points, MapPosition = mapPosition };
        }
    }
}
