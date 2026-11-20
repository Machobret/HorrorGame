using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent]
    public sealed class DeviceIndicator : MonoBehaviour
    {
        [SerializeField, HideInInspector] GameObject antenna;
        [SerializeField] IndicatorLamp bodyIndicator;
        [SerializeField] IndicatorLamp antennaIndicator;
        [SerializeField, Tooltip("Let the indicators cast light into the scene.")]
        bool emitRealLights;
        [SerializeField, HideInInspector] GameObject postMount;

        public GameObject Antenna => antenna;
        public GameObject PostMount => postMount;
        public IndicatorLamp BodyIndicator => bodyIndicator;
        public IndicatorLamp AntennaIndicator => antennaIndicator;

        private void Awake() => ApplyLightSetting();

        private void OnValidate() => ApplyLightSetting();

        private void ApplyLightSetting()
        {
            if (bodyIndicator) bodyIndicator.SetRealLightEnabled(emitRealLights);
            if (antennaIndicator) antennaIndicator.SetRealLightEnabled(emitRealLights);
        }

        public void SetAntennaEnabled(bool enabled)
        {
            if (antenna) antenna.SetActive(enabled);
        }

        /// <summary>Sets this instance's antenna glow and point light. Zero strength turns both off.</summary>
        public void SetIndicator(Color color, float strength)
        {
            if (antennaIndicator) antennaIndicator.SetColor(color, strength);
        }

        public void SetDeviceState(Color color, float strength = .9f)
        {
            if (bodyIndicator) bodyIndicator.SetColor(color, strength);
            if (antennaIndicator && antennaIndicator != bodyIndicator) antennaIndicator.SetColor(color, strength);
        }
    }
}
