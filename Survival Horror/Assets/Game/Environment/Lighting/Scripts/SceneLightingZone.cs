using HorrorEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Lighting
{
    /// <summary>Player trigger that darkens the current scene lighting or restores its exact prior state.</summary>
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Horror Game/Lighting/Scene Lighting Zone")]
    public sealed class SceneLightingZone : MonoBehaviour
    {
        public enum ZoneAction { Darken, Restore }

        public ZoneAction Action = ZoneAction.Darken;
        [Range(0.1f, 1f)] public float AmbientColorMultiplier = .72f;
        [Range(0.1f, 1f)] public float AmbientIntensityMultiplier = .58f;
        [Range(0.1f, 1f)] public float KeyLightIntensityMultiplier = .55f;
        [Range(1f, 2f)] public float FogDensityMultiplier = 1.18f;
        [Range(0f, 1f)] public float FogDarkenAmount = .18f;
        public bool OneShot = true;

        private bool used;
        private static LightingState savedState;
        private static bool hasSavedState;
        private static int savedSceneHandle;

        private struct LightingState
        {
            public AmbientMode ambientMode;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public float ambientIntensity;
            public float reflectionIntensity;
            public Light sun;
            public Color sunColor;
            public float sunIntensity;
            public Quaternion sunRotation;
            public AtmosphericFog fog;
            public Color fogColor;
            public float fogDensity;
            public bool fogEnabled;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSavedState()
        {
            hasSavedState = false;
            savedSceneHandle = 0;
        }

        private void Reset()
        {
            BoxCollider volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(10f, 5f, 16f);
            volume.center = new Vector3(0f, 2.5f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || used || !other.GetComponentInParent<PlayerActor>()) return;
            if (Action == ZoneAction.Restore && !HasSnapshot) return;

            if (Action == ZoneAction.Darken) ApplyDarkness();
            else RestoreLighting();

            if (OneShot) used = true;
        }

        [ContextMenu("Apply Dark Lighting")]
        public void ApplyDarkness()
        {
            CaptureLighting();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = savedState.ambientSky * AmbientColorMultiplier;
            RenderSettings.ambientEquatorColor = savedState.ambientEquator * AmbientColorMultiplier;
            RenderSettings.ambientGroundColor = savedState.ambientGround * AmbientColorMultiplier;
            RenderSettings.ambientIntensity = savedState.ambientIntensity * AmbientIntensityMultiplier;
            RenderSettings.reflectionIntensity = savedState.reflectionIntensity * AmbientIntensityMultiplier;

            if (savedState.sun)
                savedState.sun.intensity = savedState.sunIntensity * KeyLightIntensityMultiplier;

            if (savedState.fog)
            {
                savedState.fog.fogEnabled = savedState.fogEnabled;
                savedState.fog.fogColor = Color.Lerp(savedState.fogColor, Color.black, FogDarkenAmount);
                savedState.fog.density = savedState.fogDensity * FogDensityMultiplier;
                savedState.fog.ApplySettings();
            }
            else if (RenderSettings.fog)
            {
                RenderSettings.fogColor = Color.Lerp(savedState.fogColor, Color.black, FogDarkenAmount);
                RenderSettings.fogDensity = savedState.fogDensity * FogDensityMultiplier;
            }
        }

        [ContextMenu("Restore Scene Lighting")]
        public void RestoreLighting()
        {
            if (!HasSnapshot) return;

            RenderSettings.ambientMode = savedState.ambientMode;
            RenderSettings.ambientSkyColor = savedState.ambientSky;
            RenderSettings.ambientEquatorColor = savedState.ambientEquator;
            RenderSettings.ambientGroundColor = savedState.ambientGround;
            RenderSettings.ambientIntensity = savedState.ambientIntensity;
            RenderSettings.reflectionIntensity = savedState.reflectionIntensity;

            if (savedState.sun)
            {
                savedState.sun.color = savedState.sunColor;
                savedState.sun.intensity = savedState.sunIntensity;
                savedState.sun.transform.rotation = savedState.sunRotation;
            }

            if (savedState.fog)
            {
                savedState.fog.fogEnabled = savedState.fogEnabled;
                savedState.fog.fogColor = savedState.fogColor;
                savedState.fog.density = savedState.fogDensity;
                savedState.fog.ApplySettings();
            }

            else
            {
                RenderSettings.fogColor = savedState.fogColor;
                RenderSettings.fogDensity = savedState.fogDensity;
                RenderSettings.fog = savedState.fogEnabled;
            }

            hasSavedState = false;
        }

        private bool HasSnapshot => hasSavedState && savedSceneHandle == gameObject.scene.handle;

        private void CaptureLighting()
        {
            if (hasSavedState && savedSceneHandle == gameObject.scene.handle) return;

            Light sun = RenderSettings.sun;
            AtmosphericFog fog = FindFirstObjectByType<AtmosphericFog>();
            savedState = new LightingState
            {
                ambientMode = RenderSettings.ambientMode,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                ambientIntensity = RenderSettings.ambientIntensity,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                sun = sun,
                sunColor = sun ? sun.color : Color.white,
                sunIntensity = sun ? sun.intensity : 0f,
                sunRotation = sun ? sun.transform.rotation : Quaternion.identity,
                fog = fog,
                fogColor = fog ? fog.fogColor : RenderSettings.fogColor,
                fogDensity = fog ? fog.density : RenderSettings.fogDensity,
                fogEnabled = fog ? fog.fogEnabled : RenderSettings.fog
            };
            hasSavedState = true;
            savedSceneHandle = gameObject.scene.handle;
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider volume = GetComponent<BoxCollider>();
            if (!volume) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Action == ZoneAction.Darken
                ? new Color(.22f, .08f, .08f, .7f)
                : new Color(.12f, .38f, .24f, .7f);
            Gizmos.DrawWireCube(volume.center, volume.size);
        }
    }
}
