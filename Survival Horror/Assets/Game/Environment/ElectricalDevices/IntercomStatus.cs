using System;
using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent, RequireComponent(typeof(DeviceIndicator))]
    public sealed class IntercomStatus : MonoBehaviour
    {
        [SerializeField] private bool requiresUnlock;
        [SerializeField] private bool unlocked;

        private DeviceIndicator indicator;
        private bool connected;
        private bool powered;

        public bool AccessGranted => !requiresUnlock || unlocked;
        public event Action AccessChanged;

        private void Awake() => indicator = GetComponent<DeviceIndicator>();

        public void SetPowerState(bool connected, bool powered)
        {
            this.connected = connected;
            this.powered = powered;
            Refresh();
        }

        public void SetUnlocked(bool unlocked)
        {
            if (this.unlocked == unlocked) return;
            this.unlocked = unlocked;
            Refresh();
            AccessChanged?.Invoke();
        }

        private void Refresh()
        {
            if (!indicator) return;
            Color color = powered ? (AccessGranted ? IndicatorLamp.Green : IndicatorLamp.InactiveYellow) : IndicatorLamp.Red;
            indicator.SetDeviceState(connected ? color : Color.gray, connected ? .9f : 0f);
        }
    }
}
