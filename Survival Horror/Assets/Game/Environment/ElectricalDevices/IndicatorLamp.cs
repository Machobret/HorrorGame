using UnityEngine;

namespace Game.Environment
{
    public enum IndicatorLampMode
    {
        Universal,
        RedOnly,
        GreenOnly
    }

    [DisallowMultipleComponent]
    public sealed class IndicatorLamp : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly Color Red = new(1f, .015f, .01f);
        public static readonly Color Green = new(.035346147f, 1f, 0f);
        public static readonly Color InactiveYellow = new(1f, .75f, .025f);

        [SerializeField] IndicatorLampMode mode;
        [SerializeField] Renderer lensRenderer;
        [SerializeField] int emissionSlot;
        [SerializeField] Light pointLight;
        [SerializeField, Min(0f)] float lightIntensityPerStrength = .08f;

        MaterialPropertyBlock properties;
        private Color requestedColor;
        private float requestedStrength;
        private bool noPower;
        private bool showOutage;
        private bool blinkOn;
        private float nextBlink;
        private bool requested;
        private bool realLightEnabled = true;

        public void SetRealLightEnabled(bool enabled)
        {
            realLightEnabled = enabled;
            if (pointLight) pointLight.enabled = enabled && pointLight.intensity > 0f;
        }

        public void SetLit(bool lit, float strength = .9f)
        {
            SetColor(Green, lit ? strength : 0f);
        }

        /// <summary>Sets the glow and point light together. Zero strength turns both off.</summary>
        public void SetColor(Color color, float strength)
        {
            requested = true;
            requestedColor = mode switch
            {
                IndicatorLampMode.RedOnly => Red,
                IndicatorLampMode.GreenOnly => Green,
                _ => color
            };
            requestedStrength = Mathf.Max(0f, strength);
            if (!noPower) Apply(requestedColor, requestedStrength);
        }

        public void SetNoPower(bool value)
        {
            SetPowerAvailable(!value, true);
        }

        public void SetPowerAvailable(bool powered, bool outageIndicator)
        {
            if (noPower == !powered && (!noPower || showOutage == outageIndicator)) return;
            if (!requested && pointLight)
                SetColor(pointLight.color, pointLight.intensity / Mathf.Max(.001f, lightIntensityPerStrength));
            noPower = !powered;
            showOutage = outageIndicator;
            blinkOn = true;
            nextBlink = Time.unscaledTime + 1f;
            if (noPower) Apply(Red, showOutage ? .65f : 0f);
            else Apply(requestedColor, requestedStrength);
        }

        private void Update()
        {
            if (!noPower || !showOutage || Time.unscaledTime < nextBlink) return;
            blinkOn = !blinkOn;
            nextBlink = Time.unscaledTime + 1f;
            Apply(Red, blinkOn ? .65f : 0f);
        }

        private void Apply(Color tint, float strength)
        {
            properties ??= new MaterialPropertyBlock();
            if (lensRenderer)
            {
                lensRenderer.GetPropertyBlock(properties, emissionSlot);
                properties.SetColor(BaseColor, tint);
                properties.SetColor(EmissionColor, tint * strength);
                lensRenderer.SetPropertyBlock(properties, emissionSlot);
            }

            if (!pointLight) return;
            pointLight.color = tint;
            pointLight.intensity = strength * lightIntensityPerStrength;
            pointLight.enabled = realLightEnabled && strength > 0f && tint.maxColorComponent > 0f;
        }
    }
}
