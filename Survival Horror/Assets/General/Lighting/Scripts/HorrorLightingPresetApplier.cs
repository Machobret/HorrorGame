using UnityEngine;

namespace HorrorGame.Lighting
{
    /// <summary>Applies a lighting mood at runtime. Use this to transition between authored variants.</summary>
    public sealed class HorrorLightingPresetApplier : MonoBehaviour
    {
        public HorrorLightingPreset preset;
        public Light keyLight;
        public bool applyOnAwake = true;

        private void Awake()
        {
            if (applyOnAwake) Apply();
        }

        [ContextMenu("Apply Preset")]
        public void Apply()
        {
            if (preset == null) return;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = preset.ambientSky;
            RenderSettings.ambientEquatorColor = preset.ambientEquator;
            RenderSettings.ambientGroundColor = preset.ambientGround;
            RenderSettings.ambientIntensity = preset.ambientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;

            if (keyLight == null) return;
            keyLight.color = preset.keyColor;
            keyLight.intensity = preset.keyIntensity;
            keyLight.transform.rotation = Quaternion.Euler(preset.keyEuler);
        }
    }
}
