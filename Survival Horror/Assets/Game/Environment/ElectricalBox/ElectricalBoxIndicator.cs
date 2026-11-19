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

        bool powerInitialized;

        public IndicatorLamp AntennaIndicator => antennaIndicator;

        void Awake()
        {
            if (!powerInitialized)
                SetPowered(startsPowered);
        }

        public void SetPowered(bool powered)
        {
            powerInitialized = true;
            redIndicator.SetLit(!powered);
            greenIndicator.SetLit(powered);
        }
    }
}
