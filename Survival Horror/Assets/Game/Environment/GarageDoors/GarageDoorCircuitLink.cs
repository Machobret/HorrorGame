using System.Collections.Generic;
using Game.Environment;
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
        public Transform TabletAnchor;
        public DeviceIndicator SecurityScreen;
        internal static readonly HashSet<GarageDoorCircuitLink> Active = new();
        GarageDoor door;
        private ElectricalDeviceFeed screenFeed;
        private bool? displayedOpen;
        private bool? manualOpenCommand;
        private GarageDoorCircuitLink openingInterlock;
        private bool waitingForInterlock;
        public bool IsOpen => door && door.IsOpen;

        void OnEnable()
        {
            door=GetComponent<GarageDoor>();
            screenFeed = SecurityScreen ? SecurityScreen.GetComponent<ElectricalDeviceFeed>() : null;
            displayedOpen = null;
            Active.Add(this);
            if(Grid)Refresh();
            LateUpdate();
        }
        void OnDisable()
        {
            Active.Remove(this);
            if (screenFeed) screenFeed.SetStatusVisibleWithoutPower(false);
        }

        private void LateUpdate()
        {
            if (waitingForInterlock && (!openingInterlock || IsInterlockClosed))
                Refresh();
            if (!SecurityScreen || !door || displayedOpen == door.IsOpen) return;
            displayedOpen = door.IsOpen;
            SecurityScreen.SetDeviceState(door.IsOpen ? IndicatorLamp.Green : IndicatorLamp.InactiveYellow);
            if (screenFeed) screenFeed.SetStatusVisibleWithoutPower(!door.IsOpen);
        }

        private bool IsInterlockClosed => openingInterlock.door && openingInterlock.door.IsClosed;

        internal void SetOpeningInterlock(GarageDoorCircuitLink other)
        {
            openingInterlock = other;
            Refresh();
        }

        internal void SetManualOpen(bool open, bool animateWhilePaused = false)
        {
            manualOpenCommand = open;
            Refresh(animateWhilePaused);
        }

        internal void RestoreManualOpen(bool open)
        {
            manualOpenCommand = open;
            if (!door) door = GetComponent<GarageDoor>();
            door.RestoreOpen(open);
            Refresh();
        }

        public override void Refresh() => Refresh(false);

        private void Refresh(bool animateWhilePaused)
        {
            if (GetComponent<CircuitShutterTeleport>()) return;
            if (!isActiveAndEnabled || !door || !Grid || string.IsNullOrEmpty(CircuitId)) return;
            if (manualOpenCommand.HasValue)
            {
                door.SetMotorPowered(Powered);
                door.SetOpen(manualOpenCommand.Value, animateWhilePaused);
                return;
            }
            door.SetMotorPowered(Grid.HasSupply);
            waitingForInterlock = Powered && openingInterlock && !IsInterlockClosed;
            if (Grid.HasSupply) door.SetOpen(Powered && !waitingForInterlock);
        }
        internal bool TryMarker(DistrictPowerGrid grid,MapData map,out string roomId,out Vector3 position)
        {
            roomId=null;position=default;
            if (GetComponent<CircuitShutterTeleport>()) return false;
            if(!ShowOnTablet || !door.Operable || Grid!=grid || !Room || !Room.TryGetComponent(out ObjectUniqueId id) ||
                string.IsNullOrEmpty(id.Id) || !grid.Circuits.Exists(c=>c.Id==CircuitId) || map.Rooms==null || !map.Rooms.Exists(r=>r.UniqueId==id.Id))return false;
            roomId=id.Id;
            var local=Room.transform.InverseTransformPoint(TabletAnchor ? TabletAnchor.position : transform.position);
            position=map.GetTRS(Room.Offset,Room.Rotation,Room.Scale,Room.ZOrder).MultiplyPoint3x4(new Vector3(local.x,0,local.z));
            position.y=6;
            return true;
        }
    }
}
