using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.Chapter1;
using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Environment
{
    [RequireComponent(typeof(GarageDoor))]
    public sealed class GarageDoorCircuitLink : CircuitDrivenObject
    {
        public bool ShowOnTablet;
        [Tooltip("Map room containing this door. Its collected circuit diagram reveals the optional marker.")]
        public MapRoom Room;
        internal static readonly HashSet<GarageDoorCircuitLink> Active = new();
        GarageDoor door;

        void OnEnable()
        {
            door=GetComponent<GarageDoor>();
            Active.Add(this);
            if(Grid)Refresh();
        }
        void OnDisable() => Active.Remove(this);
        public override void Refresh()
        {
            if(isActiveAndEnabled && Grid && !string.IsNullOrEmpty(CircuitId))door.SetOpen(Powered);
        }
        internal bool TryMarker(DistrictPowerGrid grid,MapData map,out string roomId,out Vector3 position)
        {
            roomId=null;position=default;
            if(!ShowOnTablet || !door.Operable || Grid!=grid || !Room || !Room.TryGetComponent(out ObjectUniqueId id) ||
                string.IsNullOrEmpty(id.Id) || !grid.Circuits.Exists(c=>c.Id==CircuitId) || map.Rooms==null || !map.Rooms.Exists(r=>r.UniqueId==id.Id))return false;
            roomId=id.Id;
            var local=Room.transform.InverseTransformPoint(transform.position);
            position=map.GetTRS(Room.Offset,Room.Rotation,Room.Scale,Room.ZOrder).MultiplyPoint3x4(new Vector3(local.x,0,local.z));
            position.y=6;
            return true;
        }
    }
}
