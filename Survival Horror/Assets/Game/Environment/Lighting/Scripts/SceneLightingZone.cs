using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Lighting
{
    /// <summary>Player trigger that darkens the current scene lighting or restores its exact prior state.</summary>
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Horror Game/Lighting/Scene Lighting Zone")]
    public sealed class SceneLightingZone : MonoBehaviour
    {
        public enum ZoneAction { Darken, Restore }

        public ZoneAction Action = ZoneAction.Darken;
        [Range(0f, 1f)] public float AmbientColorMultiplier = .72f;
        [Range(0f, 1f)] public float AmbientIntensityMultiplier = .58f;
        [Range(0f, 1f)] public float KeyLightIntensityMultiplier = .55f;
        [Range(1f, 2f)] public float FogDensityMultiplier = 1.18f;
        [Range(0f, 1f)] public float FogDarkenAmount = .18f;
        public bool OneShot = true;
        [Min(0f)] public float FadeSeconds = 1.5f;
        private static SceneLightingZone transitionOwner;
        private static Coroutine transition;

        private bool used;
        private bool playerInside;
        private BoxCollider volume;
        private static LightingState savedState;
        private static bool hasSavedState;
        private static int savedSceneHandle;

        private struct LightingState
        {
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public float ambientIntensity;
            public float reflectionIntensity;
            public Light sun;
            public float sunIntensity;
            public AtmosphericFog fog;
            public Color fogColor;
            public float fogDensity;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSavedState()
        {
            hasSavedState = false;
            savedSceneHandle = 0;
            transitionOwner = null;
            transition = null;
        }

        private void Reset()
        {
            BoxCollider volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(10f, 5f, 16f);
            volume.center = new Vector3(0f, 2.5f, 0f);
        }

        private void Awake() => volume = GetComponent<BoxCollider>();

        private void OnDisable() => playerInside = false;

        private void Update()
        {
            if (used) return;
            var player = GameManager.Exists ? GameManager.Instance.Player : null;
            bool inside = false;
            if (player && volume.enabled)
            {
                // Sample slightly above the actor's feet so a floor-level
                // trigger also covers players whose pivot rests on its base.
                Vector3 offset = transform.InverseTransformPoint(player.transform.position + Vector3.up * .15f)
                                 - volume.center;
                Vector3 halfSize = volume.size * .5f;
                inside = Mathf.Abs(offset.x) <= halfSize.x &&
                         Mathf.Abs(offset.y) <= halfSize.y &&
                         Mathf.Abs(offset.z) <= halfSize.z;
            }
            bool entered = inside && !playerInside;
            playerInside = inside;
            if (!entered) return;
            if (Action == ZoneAction.Restore && !HasSnapshot) return;

            if (Action == ZoneAction.Darken) ApplyDarkness();
            else RestoreLighting();

            if (OneShot) used = true;
        }

        [ContextMenu("Apply Dark Lighting")]
        public void ApplyDarkness()
        {
            CaptureLighting();
            var target = savedState;
            target.ambientSky *= AmbientColorMultiplier;
            target.ambientEquator *= AmbientColorMultiplier;
            target.ambientGround *= AmbientColorMultiplier;
            target.ambientIntensity *= AmbientIntensityMultiplier;
            target.reflectionIntensity *= AmbientIntensityMultiplier;
            target.sunIntensity *= KeyLightIntensityMultiplier;
            target.fogColor = Color.Lerp(target.fogColor, Color.black, FogDarkenAmount);
            target.fogDensity *= FogDensityMultiplier;
            BeginFade(target, false);
        }

        [ContextMenu("Restore Scene Lighting")]
        public void RestoreLighting()
        {
            if (HasSnapshot) BeginFade(savedState, true);
        }

        private void BeginFade(LightingState target, bool restoring)
        {
            if (transitionOwner && transition != null) transitionOwner.StopCoroutine(transition);
            transitionOwner = null;
            transition = null;
            if (!Application.isPlaying || FadeSeconds <= 0f)
            {
                ApplyBlend(target, target, 1f);
                if (restoring) hasSavedState = false;
                return;
            }

            transitionOwner = this;
            transition = StartCoroutine(Fade(ReadLighting(), target, restoring, FadeSeconds));
        }

        private System.Collections.IEnumerator Fade(LightingState from, LightingState target, bool restoring, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                ApplyBlend(from, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            }

            if (restoring) hasSavedState = false;
            transition = null;
            transitionOwner = null;
        }

        private static void ApplyBlend(LightingState from, LightingState target, float amount)
        {
            RenderSettings.ambientSkyColor = Color.Lerp(from.ambientSky, target.ambientSky, amount);
            RenderSettings.ambientEquatorColor = Color.Lerp(from.ambientEquator, target.ambientEquator, amount);
            RenderSettings.ambientGroundColor = Color.Lerp(from.ambientGround, target.ambientGround, amount);
            RenderSettings.ambientIntensity = Mathf.Lerp(from.ambientIntensity, target.ambientIntensity, amount);
            RenderSettings.reflectionIntensity = Mathf.Lerp(from.reflectionIntensity, target.reflectionIntensity, amount);
            if (target.sun) target.sun.intensity = Mathf.Lerp(from.sunIntensity, target.sunIntensity, amount);

            Color fogColor = Color.Lerp(from.fogColor, target.fogColor, amount);
            float fogDensity = Mathf.Lerp(from.fogDensity, target.fogDensity, amount);
            if (target.fog)
            {
                target.fog.fogColor = fogColor;
                target.fog.density = fogDensity;
                target.fog.ApplySettings();
            }
            else
            {
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }
        }

        private bool HasSnapshot => hasSavedState && savedSceneHandle == gameObject.scene.handle;

        private void CaptureLighting()
        {
            if (HasSnapshot) return;
            savedState = ReadLighting();
            hasSavedState = true;
            savedSceneHandle = gameObject.scene.handle;
        }

        private static LightingState ReadLighting()
        {
            Light sun = RenderSettings.sun;
            AtmosphericFog fog = FindFirstObjectByType<AtmosphericFog>();
            return new LightingState
            {
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                ambientIntensity = RenderSettings.ambientIntensity,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                sun = sun,
                sunIntensity = sun ? sun.intensity : 0f,
                fog = fog,
                fogColor = fog ? fog.fogColor : RenderSettings.fogColor,
                fogDensity = fog ? fog.density : RenderSettings.fogDensity
            };
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
