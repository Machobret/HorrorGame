using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>One selectable circuit dot authored in the Map Editor.</summary>
    public sealed class PowerCircuitPanelElement : MonoBehaviour
    {
        public string CircuitId;
        [HideInInspector] public string LastSyncedCircuitId;
        public string Label;
        [Min(0)] public int Consumption = 20;
        public bool StartsEnabled;
        [Tooltip("The MapData this circuit belongs to. Set automatically when the circuit is created in the Map Editor.")]
        public MapData MapData;
        public MapRoom Room;
        public Vector2 RoomOffset;
        public List<CircuitDrivenObject> LinkedTargets = new List<CircuitDrivenObject>();
        public Vector2 Size = new Vector2(220, 65);
        public List<Vector2> BorderPoints = new List<Vector2>();

        public MapData GetMapData()
        {
            if (MapData) return MapData;
            if (!Room) return null;
            MapController roomMap = Room.GetComponentInParent<MapController>();
            if (roomMap && roomMap.Data) return roomMap.Data;
            if (!Room.TryGetComponent(out ObjectUniqueId roomId)) return null;
            foreach (MapController mapController in FindObjectsByType<MapController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!mapController || !mapController.Data || mapController.Data.Rooms == null) continue;
                foreach (MapRoomSerializedData mapRoom in mapController.Data.Rooms)
                    if (mapRoom.UniqueId == roomId.Id) return mapController.Data;
            }
            return null;
        }

        public PowerPanelCircuitShape ToShape(MapData fallbackMap)
        {
            MapData ownMap = GetMapData();
            MapData map = ownMap ? ownMap : fallbackMap;
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
            string roomId = string.Empty;
            if (Room && Room.TryGetComponent(out ObjectUniqueId uniqueId)) roomId = uniqueId.Id;
            return new PowerPanelCircuitShape { CircuitId = CircuitId, Label = string.IsNullOrWhiteSpace(Label) ? CircuitId : Label, Bounds = bounds, BorderPoints = points, MapPosition = mapPosition, RoomName = Room ? Room.Name : string.Empty, MapUniqueId = map ? map.UniqueId : string.Empty, RoomUniqueId = roomId };
        }
    }
}
