using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace HorrorGame.Lighting
{
    /// <summary>Runtime-only lighting adaptation; source material assets are never edited.</summary>
    internal static class HorrorRuntimeMaterials
    {
        internal static bool IsLit(Material material)
        {
            string name = material.shader.name;
            return name == "Standard" || name.StartsWith("Standard (") ||
                name.StartsWith("Universal Render Pipeline/") && !name.Contains("Unlit") ||
                name.Contains("Vertex Lit") || name.Contains("VertexLit") ||
                name.Contains("Tri-Planar");
        }

        internal static Material Create(Material source)
        {
            bool unlit = source.shader.name.IndexOf("unlit", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var litShader = unlit ? Shader.Find("Universal Render Pipeline/Lit") : null;
            var result = litShader != null ? new Material(litShader) : new Material(source);
            result.name = source.name + " (Runtime Lighting)";
            result.hideFlags = HideFlags.DontSave;
            if (litShader != null)
            {
                string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (source.HasProperty(textureProperty))
                {
                    result.SetTexture("_BaseMap", source.GetTexture(textureProperty));
                    result.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                    result.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
                }
                result.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                    source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                result.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : .15f);
                result.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
                if (source.HasProperty("_Cull")) result.SetFloat("_Cull", source.GetFloat("_Cull"));
                string renderType = source.GetTag("RenderType", false);
                bool cutout = renderType == "TransparentCutout" || source.shader.name.Contains("Cutout") ||
                    source.IsKeywordEnabled("_ALPHATEST_ON");
                bool transparent = !cutout && (renderType == "Transparent" || source.renderQueue >= 3000);
                if (cutout)
                {
                    result.SetFloat("_AlphaClip", 1f);
                    result.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f);
                    result.EnableKeyword("_ALPHATEST_ON");
                    result.SetOverrideTag("RenderType", "TransparentCutout");
                    result.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else if (transparent)
                {
                    result.SetFloat("_Surface", 1f);
                    result.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    result.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    result.SetFloat("_ZWrite", 0f);
                    result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    result.SetOverrideTag("RenderType", "Transparent");
                    result.SetShaderPassEnabled("ShadowCaster", false);
                    result.renderQueue = (int)RenderQueue.Transparent;
                }
            }
            // Ordinary models are not light sources. The player fill is set separately.
            if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", Color.black);
            result.DisableKeyword("_EMISSION");
            return result;
        }
    }
}

namespace HorrorGame.Lighting
{
    /// <summary>
    /// Makes world props, characters, and particles respect local gameplay lighting.
    /// This is deliberately independent of global ambient light so unlit areas stay dark.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class HorrorDarknessVisibilityManager : MonoBehaviour
    {
        [Range(0f, .15f)] [SerializeField] private float minimumVisibility = 0f;
        [Range(.05f, 1f)] [SerializeField] private float updateInterval = .12f;
        [Range(.01f, 1f)] [SerializeField] private float directionalLightContribution = .22f;

        private readonly Dictionary<Renderer, RendererState> renderers = new();
        private readonly Dictionary<ParticleSystem, ParticleState> particles = new();
        private readonly List<Renderer> expiredRenderers = new();
        private readonly List<ParticleSystem> expiredParticles = new();
        private readonly List<Light> lights = new();
        // Native Unity objects must be recreated after a Play-mode script reload.
        [System.NonSerialized] private MaterialPropertyBlock block;
        [SerializeField, Range(.2f, 2f)] private float closeGlowDistance = .85f;
        [SerializeField, Range(0f, .1f)] private float closeGlowIntensity = .025f;
        private Transform glowPlayer;
        private float closeGlowTarget;
        private float closeGlowAmount;
        private float nextRefresh;

        private void OnEnable()
        {
            block = new MaterialPropertyBlock();
            nextRefresh = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateManager()
        {
            if (FindFirstObjectByType<HorrorDarknessVisibilityManager>() != null) return;
            var manager = new GameObject("Horror Darkness Visibility Manager");
            DontDestroyOnLoad(manager);
            manager.AddComponent<HorrorDarknessVisibilityManager>();
        }

        private void LateUpdate()
        {
            if (block == null) block = new MaterialPropertyBlock();
            bool refresh = Time.unscaledTime >= nextRefresh;
            if (refresh)
            {
                nextRefresh = Time.unscaledTime + updateInterval;
                RefreshTrackedObjects();
                UpdateCloseGlowProximity();
                RefreshLights();
            }
            closeGlowAmount = Mathf.MoveTowards(closeGlowAmount, closeGlowTarget, Time.deltaTime * 2.5f);
            // Catch newly spawned dust before its first rendered frame.
            RefreshParticles();
            ApplyVisibility(refresh);
        }

        private void RefreshTrackedObjects()
        {
            expiredRenderers.Clear();
            foreach (var pair in renderers)
                if (pair.Key == null)
                {
                    pair.Value.Restore(null);
                    expiredRenderers.Add(pair.Key);
                }
            foreach (var key in expiredRenderers) renderers.Remove(key);
            expiredParticles.Clear();
            foreach (var pair in particles)
                if (pair.Key == null) expiredParticles.Add(pair.Key);
            foreach (var key in expiredParticles) particles.Remove(key);
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer is ParticleSystemRenderer || ShouldIgnore(renderer) || renderers.ContainsKey(renderer)) continue;
                renderers.Add(renderer, new RendererState(renderer));
            }
        }

        private void RefreshParticles()
        {
            foreach (var particle in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (ShouldIgnore(particle) || particles.ContainsKey(particle)) continue;
                particles.Add(particle, new ParticleState(particle));
            }
        }

        private void RefreshLights()
        {
            lights.Clear();
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (light.enabled && light.gameObject.activeInHierarchy) lights.Add(light);
        }

        private void UpdateCloseGlowProximity()
        {
            var actor = FindFirstObjectByType<HorrorEngine.PlayerActor>();
            glowPlayer = actor != null ? actor.transform : null;
            closeGlowTarget = 0f;
            if (glowPlayer == null) return;

            // Retire old helpers. The bounce is now a texture-colored material fill.
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (IsOldHelper(light)) light.enabled = false;

            float nearest = closeGlowDistance;
            // Horizontal rays work with concave mesh colliders too. Ignore the floor
            // and the character's own colliders instead of using Collider.ClosestPoint.
            for (int height = 0; height < 3; height++)
            {
                Vector3 origin = glowPlayer.position + Vector3.up * (.45f + height * .45f);
                for (int direction = 0; direction < 16; direction++)
                {
                    float angle = direction * Mathf.PI * 2f / 16f;
                    Vector3 heading = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    foreach (var hit in Physics.RaycastAll(origin, heading, closeGlowDistance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(glowPlayer) || Mathf.Abs(hit.normal.y) > .65f) continue;
                        nearest = Mathf.Min(nearest, hit.distance);
                    }
                }
            }
            closeGlowTarget = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(closeGlowDistance, .3f, nearest));
        }

        private static bool IsOldHelper(Light light)
        {
            return light.name == "__FaintSafetyGlow" || light.name == "__FeetSafetyGlow" ||
                   light.name == "__WallBounceGlow" || light.name == "__CloseBodyFillFront" ||
                   light.name == "__CloseBodyFillBack";
        }

        private void ApplyVisibility(bool refresh)
        {
            foreach (var pair in renderers)
            {
                if (pair.Key == null) continue;
                bool isPlayerRenderer = glowPlayer != null && pair.Key.transform.IsChildOf(glowPlayer) &&
                    !(pair.Key is ParticleSystemRenderer);
                if (refresh) pair.Value.Visibility = LocalVisibility(pair.Key.bounds.center, pair.Key);
                float visibility = pair.Value.Visibility;
                pair.Value.Apply(pair.Key, block, visibility, isPlayerRenderer ? closeGlowAmount * closeGlowIntensity : 0f, isPlayerRenderer);
            }
            foreach (var pair in particles)
            {
                if (pair.Key == null) continue;
                pair.Value.Apply(pair.Key, LocalVisibility(pair.Key.transform.position, pair.Key.GetComponent<Renderer>()));
            }
        }

        private float LocalVisibility(Vector3 point, Renderer receiver)
        {
            float brightness = minimumVisibility;
            foreach (var light in lights)
            {
                if (light == null) continue;
                // The fill contributes uniformly to the body and never reveals world props or dust.
                if (IsOldHelper(light)) continue;
                if (receiver != null && (light.cullingMask & (1 << receiver.gameObject.layer)) == 0) continue;
                if (!light.enabled || !light.gameObject.activeInHierarchy) continue;
                if (light.type == LightType.Directional)
                {
                    if (!IsOccluded(point, -light.transform.forward, 100f, receiver, light))
                        brightness += light.intensity * directionalLightContribution;
                    continue;
                }
                float distance = Vector3.Distance(point, light.transform.position);
                if (distance >= light.range || light.range <= 0f) continue;
                float falloff = 1f - distance / light.range;
                if (light.type == LightType.Spot)
                {
                    var toPoint = (point - light.transform.position).normalized;
                    float edge = Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * .5f);
                    if (Vector3.Dot(light.transform.forward, toPoint) < edge) continue;
                }
                if (IsOccluded(point, (light.transform.position - point).normalized, distance, receiver, light)) continue;
                brightness += light.intensity * light.color.maxColorComponent * falloff * falloff * .1f;
            }
            return Mathf.Clamp01(brightness);
        }

        private bool IsOccluded(Vector3 point, Vector3 direction, float distance, Renderer receiver, Light light)
        {
            foreach (var hit in Physics.RaycastAll(point + direction * .02f, direction,
                         Mathf.Max(0f, distance - .04f), ~0, QueryTriggerInteraction.Ignore))
            {
                if (receiver != null && (hit.transform == receiver.transform ||
                    hit.transform.IsChildOf(receiver.transform) || receiver.transform.IsChildOf(hit.transform))) continue;
                if (glowPlayer != null && receiver != null && receiver.transform.IsChildOf(glowPlayer) &&
                    hit.transform.IsChildOf(glowPlayer)) continue;
                if (hit.transform == light.transform || light.transform.IsChildOf(hit.transform)) continue;
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            foreach (var pair in renderers) pair.Value.Restore(pair.Key);
            foreach (var pair in particles) pair.Value.Restore();
        }

        private static bool ShouldIgnore(Component component)
        {
            if (component.gameObject.layer == 5) return true; // Unity UI
            if (component.GetComponentInParent<Canvas>() != null) return true;
            if (component.GetComponentInParent<Light>() != null) return true;
            return component.GetComponentInParent<HorrorLightSource>() != null;
        }

        private sealed class RendererState
        {
            public float Visibility;
            private readonly Material[] originalMaterials;
            private Material[] playerMaterials;
            private bool glowPrepared;
            private float blendedVisibility;
            private readonly bool originalForceOff;
            private readonly Color[] baseColors;
            private readonly Color[] tintColors;
            private readonly Color[] emissionColors;

            public RendererState(Renderer renderer)
            {
                var materials = renderer.sharedMaterials;
                originalMaterials = materials;
                originalForceOff = renderer.forceRenderingOff;
                playerMaterials = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null) playerMaterials[i] = HorrorRuntimeMaterials.Create(materials[i]);
                renderer.sharedMaterials = playerMaterials;
                materials = playerMaterials;
                baseColors = new Color[materials.Length]; tintColors = new Color[materials.Length]; emissionColors = new Color[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    baseColors[i] = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                    tintColors[i] = material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    emissionColors[i] = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                }
            }

            public void Apply(Renderer renderer, MaterialPropertyBlock block, float visibility, float glow, bool isPlayer)
            {
                blendedVisibility = Mathf.MoveTowards(blendedVisibility, visibility, Time.deltaTime * 3f);
                if (isPlayer && !glowPrepared)
                {
                    glowPrepared = true;
                    for (int i = 0; i < playerMaterials.Length; i++)
                    {
                        var material = playerMaterials[i];
                        if (material == null) continue;
                        if (!material.HasProperty("_EmissionColor")) continue;
                        material.EnableKeyword("_EMISSION");
                        if (material.HasProperty("_EmissionMap") && material.HasProperty("_BaseMap"))
                        {
                            material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
                            material.SetTextureScale("_EmissionMap", material.GetTextureScale("_BaseMap"));
                            material.SetTextureOffset("_EmissionMap", material.GetTextureOffset("_BaseMap"));
                        }
                    }
                    renderer.sharedMaterials = playerMaterials;
                }
                var materials = playerMaterials ?? originalMaterials;
                bool canDim = true;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    canDim &= material.HasProperty("_BaseColor") || material.HasProperty("_Color") ||
                              material.HasProperty("_DarknessVisibility");
                    renderer.GetPropertyBlock(block, i);
                    // A lit character already responds to realtime lights, ambient
                    // light and probes. Multiplying albedo by our light estimate
                    // applies darkness twice and ignores baked/environment lighting.
                    bool naturallyLit = HorrorRuntimeMaterials.IsLit(material);
                    float albedoVisibility = naturallyLit ? 1f : blendedVisibility;
                    if (material.HasProperty("_BaseColor")) block.SetColor("_BaseColor", Dim(baseColors[i], albedoVisibility));
                    if (material.HasProperty("_Color")) block.SetColor("_Color", Dim(tintColors[i], albedoVisibility));
                    if (material.HasProperty("_EmissionColor")) block.SetColor("_EmissionColor", isPlayer ? baseColors[i] * glow : Color.black);
                    if (material.HasProperty("_DarknessVisibility")) block.SetFloat("_DarknessVisibility", albedoVisibility);
                    renderer.SetPropertyBlock(block, i);
                }
                // Fallback for custom shaders without any supported tint property.
                renderer.forceRenderingOff = originalForceOff || (!canDim && blendedVisibility <= .001f && glow <= .001f &&
                    !System.Array.Exists(materials, m => m != null && HorrorRuntimeMaterials.IsLit(m)));
            }

            private static Color Dim(Color color, float visibility)
            {
                return new Color(color.r * visibility, color.g * visibility, color.b * visibility, color.a);
            }

            public void Restore(Renderer renderer)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = originalForceOff;
                    if (playerMaterials != null) renderer.sharedMaterials = originalMaterials;
                    for (int i = 0; i < originalMaterials.Length; i++) renderer.SetPropertyBlock(null, i);
                }
                if (playerMaterials != null)
                    foreach (var material in playerMaterials)
                        if (material != null)
                        {
                            if (Application.isPlaying) Object.Destroy(material);
                            else Object.DestroyImmediate(material);
                        }
            }
        }

        private readonly struct ParticleState
        {
            private readonly ParticleSystem source;
            private readonly float rateMultiplier;
            private readonly float distanceMultiplier;
            private readonly bool originalForceOff;
            private readonly ParticleSystemRenderer renderer;

            public ParticleState(ParticleSystem particle)
            {
                source = particle;
                var emission = particle.emission;
                rateMultiplier = emission.rateOverTimeMultiplier;
                distanceMultiplier = emission.rateOverDistanceMultiplier;
                renderer = particle.GetComponent<ParticleSystemRenderer>();
                originalForceOff = renderer != null && renderer.forceRenderingOff;
            }

            public void Restore()
            {
                if (renderer != null) renderer.forceRenderingOff = originalForceOff;
                if (source != null)
                {
                    var emission = source.emission;
                    emission.rateOverTimeMultiplier = rateMultiplier;
                    emission.rateOverDistanceMultiplier = distanceMultiplier;
                }
            }

            public void Apply(ParticleSystem particle, float visibility)
            {
                var emission = particle.emission;
                emission.rateOverTimeMultiplier = rateMultiplier * visibility;
                emission.rateOverDistanceMultiplier = distanceMultiplier * visibility;
                // Footstep particles are not a light source. Hide existing particles too,
                // rather than merely reducing future emission, when there is no local light.
                if (renderer != null) renderer.forceRenderingOff = originalForceOff || visibility < .025f;
            }
        }
    }
}
