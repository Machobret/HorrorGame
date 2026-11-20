using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent]
    public sealed class ElectricalBoxIndicator : MonoBehaviour
    {
        [SerializeField] bool startsPowered;
        [SerializeField] IndicatorLamp redIndicator;
        [SerializeField] IndicatorLamp greenIndicator;
        [SerializeField] IndicatorLamp antennaIndicator;
        [SerializeField, Tooltip("Let the indicators cast light into the scene.")]
        bool emitRealLights;
        [SerializeField, HideInInspector] GameObject antenna;

        bool powerInitialized;

        public GameObject Antenna => antenna;
        public IndicatorLamp AntennaIndicator => antennaIndicator;

        public void SetAntennaEnabled(bool enabled)
        {
            if (antenna) antenna.SetActive(enabled);
        }

        void Awake()
        {
            ApplyLightSetting();
            if (!powerInitialized)
                SetPowered(startsPowered);
        }

        void OnValidate() => ApplyLightSetting();

        private void ApplyLightSetting()
        {
            if (redIndicator) redIndicator.SetRealLightEnabled(emitRealLights);
            if (greenIndicator) greenIndicator.SetRealLightEnabled(emitRealLights);
            if (antennaIndicator) antennaIndicator.SetRealLightEnabled(emitRealLights);
        }

        public void SetPowered(bool powered)
        {
            powerInitialized = true;
            if (redIndicator) redIndicator.SetColor(IndicatorLamp.Red, powered ? 0f : .9f);
            if (greenIndicator) greenIndicator.SetLit(powered);
        }

        public void SetBreakerState(bool authorized, bool leverOn)
        {
            powerInitialized = true;
            bool powered = authorized && leverOn;
            Color status = !authorized ? IndicatorLamp.Red : powered ? IndicatorLamp.Green : IndicatorLamp.InactiveYellow;
            if (redIndicator) redIndicator.SetColor(status, powered ? 0f : .9f);
            if (greenIndicator) greenIndicator.SetLit(powered);
            if (antennaIndicator) antennaIndicator.SetColor(status, .9f);
        }
    }
}
