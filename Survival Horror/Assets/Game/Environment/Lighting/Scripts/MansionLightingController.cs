using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Lighting
{
    /// <summary>Two-state tutorial lighting. Call Blackout from the power-failure event.</summary>
    public sealed class MansionLightingController : MonoBehaviour
    {
        public HorrorLightingPreset normalPreset;
        public HorrorLightingPreset blackoutPreset;
        public Light moonlight;
        public Light[] electricalLights;
        public Light[] emergencyLights;
        [Tooltip("Visible bulb/glow objects which must go out with electrical power.")]
        public GameObject[] electricalGlowObjects;
        public bool startBlackedOut;
        public bool IsBlackedOut { get; private set; }

        private void Awake() => SetBlackout(startBlackedOut);

        [ContextMenu("Preview Normal Lighting")]
        public void RestorePower() => SetBlackout(false);

        [ContextMenu("Preview Blackout Lighting")]
        public void Blackout() => SetBlackout(true);

        public void TogglePower() => SetBlackout(!IsBlackedOut);

        public void SetBlackout(bool blackout)
        {
            var preset = blackout ? blackoutPreset : normalPreset;
            if (preset == null)
            {
                Debug.LogWarning("Assign both mansion lighting presets.", this);
                return;
            }
            IsBlackedOut = blackout;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = preset.ambientSky;
            RenderSettings.ambientEquatorColor = preset.ambientEquator;
            RenderSettings.ambientGroundColor = preset.ambientGround;
            RenderSettings.ambientIntensity = preset.ambientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;
            RenderSettings.reflectionIntensity = blackout ? .035f : .18f;
            if (moonlight != null)
            {
                moonlight.color = preset.keyColor;
                moonlight.intensity = preset.keyIntensity;
                moonlight.transform.rotation = Quaternion.Euler(preset.keyEuler);
            }
            SetLights(electricalLights, !blackout);
            SetLights(emergencyLights, blackout);
            if (electricalGlowObjects != null)
                foreach (var glow in electricalGlowObjects)
                    if (glow != null) glow.SetActive(!blackout);
        }

        private static void SetLights(Light[] lights, bool enabled)
        {
            if (lights == null) return;
            foreach (var light in lights)
                if (light != null) light.enabled = enabled;
        }
    }
}
