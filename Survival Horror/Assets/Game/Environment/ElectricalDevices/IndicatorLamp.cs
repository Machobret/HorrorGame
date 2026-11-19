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
        public static readonly Color Red = new(1f, .015f, .01f);
        public static readonly Color Green = new(.035346147f, 1f, 0f);

        [SerializeField] IndicatorLampMode mode;
        [SerializeField] Renderer lensRenderer;
        [SerializeField] int emissionSlot;
        [SerializeField] Light pointLight;
        [SerializeField, Min(0f)] float lightIntensityPerStrength = .08f;

        MaterialPropertyBlock properties;

        public void SetLit(bool lit, float strength = .9f)
        {
            SetColor(Green, lit ? strength : 0f);
        }

        /// <summary>Sets the glow and point light together. Zero strength turns both off.</summary>
        public void SetColor(Color color, float strength)
        {
            Color tint = mode switch
            {
                IndicatorLampMode.RedOnly => Red,
                IndicatorLampMode.GreenOnly => Green,
                _ => color
            };
            strength = Mathf.Max(0f, strength);
            properties ??= new MaterialPropertyBlock();
            lensRenderer.GetPropertyBlock(properties, emissionSlot);
            properties.SetColor(EmissionColor, tint * strength);
            lensRenderer.SetPropertyBlock(properties, emissionSlot);

            pointLight.color = tint;
            pointLight.intensity = strength * lightIntensityPerStrength;
            pointLight.enabled = strength > 0f && tint.maxColorComponent > 0f;
        }
    }
}
