using HorrorEngine;
using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent]
    public sealed class ElectricalDeviceFeed : MonoBehaviour
    {
        [SerializeField] private ElectricalBoxPower supply;
        [SerializeField] private DeviceIndicator indicator;
        [SerializeField] private Interactive[] controls = System.Array.Empty<Interactive>();

        private bool localPower = true;
        private bool statusVisibleWithoutPower;
        public bool HasPower => localPower && (!supply || supply.IsOn);

        public void SetLocalPower(bool available)
        {
            localPower = available;
            Apply(HasPower);
        }

        public void SetStatusVisibleWithoutPower(bool visible)
        {
            if (statusVisibleWithoutPower == visible) return;
            statusVisibleWithoutPower = visible;
            Apply(HasPower);
        }

        private void OnEnable()
        {
            if (supply) supply.PowerChanged += OnSupplyChanged;
            foreach (var control in controls)
                if (control) control.PowerAvailable = CheckPower;
            Apply(HasPower);
        }

        private void OnDisable()
        {
            if (supply) supply.PowerChanged -= OnSupplyChanged;
            foreach (var control in controls)
                if (control && control.PowerAvailable == CheckPower) control.PowerAvailable = null;
            Apply(true);
        }

        private bool CheckPower() => HasPower;
        private void OnSupplyChanged(bool powered) => Apply(HasPower);

        private void Apply(bool powered)
        {
            if (!indicator) return;
            powered |= statusVisibleWithoutPower;
            var body = indicator.BodyIndicator;
            if (body) body.SetPowerAvailable(powered, true);
            var antenna = indicator.AntennaIndicator;
            if (antenna && antenna != body) antenna.SetPowerAvailable(powered, true);
        }
    }
}
