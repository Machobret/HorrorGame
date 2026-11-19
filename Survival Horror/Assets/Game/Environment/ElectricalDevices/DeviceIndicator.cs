using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent]
    public sealed class DeviceIndicator : MonoBehaviour
    {
        [SerializeField] GameObject antenna;
        [SerializeField] IndicatorLamp bodyIndicator;
        [SerializeField] IndicatorLamp antennaIndicator;
        [SerializeField, HideInInspector] GameObject postMount;

        public GameObject PostMount => postMount;
        public IndicatorLamp BodyIndicator => bodyIndicator;
        public IndicatorLamp AntennaIndicator => antennaIndicator;

        public void SetAntennaEnabled(bool enabled)
        {
            antenna.SetActive(enabled);
        }

        /// <summary>Sets this instance's antenna glow and point light. Zero strength turns both off.</summary>
        public void SetIndicator(Color color, float strength)
        {
            antennaIndicator.SetColor(color, strength);
        }
    }
}
