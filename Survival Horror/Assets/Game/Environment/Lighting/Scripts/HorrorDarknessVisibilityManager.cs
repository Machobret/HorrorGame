using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.Profiling;


namespace HorrorGame.Lighting
{
    /// <summary>Runtime-only lighting adaptation; source material assets are never edited.</summary>
    internal static class HorrorRuntimeMaterials
    {
        internal static bool IsLit(Material material)
        {
            string name = material.shader.name;
            // Custom URP lit shaders already apply scene lighting, including ambient light.
            return material.GetTag("UniversalMaterialType", false) == "Lit" ||
                name == "Standard" || name.StartsWith("Standard (") ||
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
        [SerializeField, Range(.4f, 2f)] private float closeGlowDistance = .85f;
        [SerializeField, Range(0f, .2f)] private float playerGlowIntensity = .05f;

        private const float DiscoveryInterval = 1f;
        private const int ReceiversPerUpdate = 24;
        private const int LightsPerReceiver = 4;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int TintColor = Shader.PropertyToID("_Color");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int Darkness = Shader.PropertyToID("_DarknessVisibility");
        private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly ProfilerMarker UpdateMarker = new("Horror Darkness.Update");
        private static readonly ProfilerMarker DiscoveryMarker = new("Horror Darkness.Discover World");
        private static readonly ProfilerMarker VisibilityMarker = new("Horror Darkness.Sample Visibility");

        private readonly Dictionary<(Material, bool), Material> variants = new();
        private readonly HashSet<Material> usedVariants = new();
        private readonly List<(Material, bool)> unusedVariants = new();
        private readonly Dictionary<Renderer, RendererState> world = new();
        private readonly Dictionary<Renderer, RendererState> player = new();
        private readonly HashSet<Renderer> visitedWorld = new();
        private readonly List<Renderer> rendererBuffer = new();
        private readonly List<Renderer> removedRenderers = new();
        private readonly List<RendererState> worldReceivers = new();
        private readonly List<ParticleState> particles = new();
        private readonly HashSet<ParticleSystem> knownParticles = new();
        private readonly List<Light> lights = new();
        private readonly LightSample[] strongestLights = new LightSample[LightsPerReceiver];
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly List<Material> materialBuffer = new();

        [System.NonSerialized] private MaterialPropertyBlock block;
        private Transform glowPlayer;
        private Transform glowModel;
        private float closeGlowTarget;
        private float closeGlowAmount;
        private float nextDiscovery;
        private float nextRefresh;
        private int nextWorldReceiver;
        private int nextParticle;
        private bool pruneVariants;

        private void OnEnable()
        {
            block = new MaterialPropertyBlock();
            nextDiscovery = nextRefresh = 0f;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => nextDiscovery = nextRefresh = 0f;

        private void SceneUnloaded(Scene scene)
        {
            pruneVariants = true;
            nextDiscovery = nextRefresh = 0f;
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
            using var scope = UpdateMarker.Auto();
            block ??= new MaterialPropertyBlock();
            float now = Time.unscaledTime;
            bool discover = now >= nextDiscovery;
            bool refresh = now >= nextRefresh;
            if (refresh)
            {
                nextRefresh = now + updateInterval;
                UpdatePlayerGlowTarget();
                RefreshPlayerRenderers();
                RefreshLights();
                RefreshParticles();
            }
            if (discover)
            {
                nextDiscovery = now + DiscoveryInterval;
                RefreshTrackedObjects();
                if (pruneVariants) PruneVariants();
            }
            closeGlowAmount = Mathf.MoveTowards(closeGlowAmount, closeGlowTarget, Time.deltaTime * 2.5f);
            ApplyVisibility(refresh);
        }

        private void RefreshTrackedObjects()
        {
            using var scope = DiscoveryMarker.Auto();
            visitedWorld.RemoveWhere(renderer => !renderer);
            removedRenderers.Clear();
            foreach (var pair in world)
                if (!pair.Key) removedRenderers.Add(pair.Key);
            foreach (var renderer in removedRenderers) RemoveWorldRenderer(renderer);

            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer is ParticleSystemRenderer || player.ContainsKey(renderer) || visitedWorld.Contains(renderer) ||
                    ShouldIgnore(renderer)) continue;
                visitedWorld.Add(renderer);
                renderer.GetSharedMaterials(materialBuffer);
                if (materialBuffer.TrueForAll(material => !material || HorrorRuntimeMaterials.IsLit(material))) continue;
                var state = new RendererState(renderer, materialBuffer, this, false);
                world.Add(renderer, state);
                if (state.NeedsVisibility) worldReceivers.Add(state);
            }
        }

        private void RemoveWorldRenderer(Renderer renderer)
        {
            var state = world[renderer];
            state.Restore();
            if (state.NeedsVisibility) worldReceivers.Remove(state);
            world.Remove(renderer);
        }

        private void RefreshPlayerRenderers()
        {
            removedRenderers.Clear();
            foreach (var pair in player)
                if (!pair.Key || !IsPlayerRenderer(pair.Key.transform) || !pair.Value.OwnsMaterials(materialBuffer))
                    removedRenderers.Add(pair.Key);
            foreach (var renderer in removedRenderers)
            {
                player[renderer].Restore();
                player.Remove(renderer);
                visitedWorld.Remove(renderer);
                nextDiscovery = 0f;
            }
            TrackPlayerHierarchy(glowPlayer);
            if (glowModel && !glowModel.IsChildOf(glowPlayer)) TrackPlayerHierarchy(glowModel);
        }

        private void TrackPlayerHierarchy(Transform root)
        {
            if (!root) return;
            root.GetComponentsInChildren(false, rendererBuffer);
            foreach (var renderer in rendererBuffer)
            {
                if (renderer is ParticleSystemRenderer || player.ContainsKey(renderer) || ShouldIgnore(renderer)) continue;
                if (world.ContainsKey(renderer)) RemoveWorldRenderer(renderer);
                renderer.GetSharedMaterials(materialBuffer);
                player.Add(renderer, new RendererState(renderer, materialBuffer, this, true));
            }
        }

        private bool IsPlayerRenderer(Transform target) =>
            (glowPlayer && target.IsChildOf(glowPlayer)) || (glowModel && target.IsChildOf(glowModel));

        private void RefreshParticles()
        {
            particles.RemoveAll(particle => !particle.Source);
            knownParticles.RemoveWhere(particle => !particle);
            foreach (var particle in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!knownParticles.Contains(particle) && !ShouldIgnore(particle))
                {
                    knownParticles.Add(particle);
                    var state = new ParticleState(particle);
                    particles.Add(state);
                    state.Apply(minimumVisibility);
                }
        }

        private void RefreshLights()
        {
            lights.Clear();
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (IsOldHelper(light)) light.enabled = false;
                else if (light.enabled && light.intensity > 0f) lights.Add(light);
            }
        }

        private void UpdatePlayerGlowTarget()
        {
            var actor = HorrorEngine.GameManager.Exists ? HorrorEngine.GameManager.Instance.Player : null;
            if (!actor) actor = FindFirstObjectByType<HorrorEngine.PlayerActor>();
            glowPlayer = actor ? actor.transform : null;
            glowModel = actor && actor.MainAnimator ? actor.MainAnimator.transform : glowPlayer;
            closeGlowTarget = 0f;
            if (!glowPlayer) return;

            float nearest = closeGlowDistance;
            for (int height = 0; height < 3; height++)
            {
                Vector3 origin = glowPlayer.position + Vector3.up * (.45f + height * .45f);
                for (int direction = 0; direction < 16; direction++)
                {
                    float angle = direction * Mathf.PI * 2f / 16f;
                    Vector3 heading = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    int count = Physics.RaycastNonAlloc(origin, heading, hits, nearest, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        var hit = hits[i];
                        if (IsPlayerRenderer(hit.transform) || Mathf.Abs(hit.normal.y) > .65f) continue;
                        nearest = Mathf.Min(nearest, hit.distance);
                    }
                }
            }
            closeGlowTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(closeGlowDistance, .3f, nearest));
        }

        private static bool IsOldHelper(Light light) =>
            light.name is "__FaintSafetyGlow" or "__FeetSafetyGlow" or "__WallBounceGlow" or
                "__CloseBodyFillFront" or "__CloseBodyFillBack";

        private void ApplyVisibility(bool refresh)
        {
            foreach (var state in player.Values)
                if (state.Renderer) state.ApplyGlow(block, closeGlowAmount * playerGlowIntensity);
            if (!refresh) return;

            using var scope = VisibilityMarker.Auto();
            // Split the sample budget when both types of receiver are present.
            int worldBudget = particles.Count == 0 ? ReceiversPerUpdate : ReceiversPerUpdate / 2;
            int worldCount = Mathf.Min(worldBudget, worldReceivers.Count);
            for (int i = 0; i < worldCount; i++)
            {
                nextWorldReceiver %= worldReceivers.Count;
                var state = worldReceivers[nextWorldReceiver++];
                if (state.Renderer && state.Renderer.gameObject.activeInHierarchy)
                    state.ApplyVisibility(block, LocalVisibility(state.Renderer.bounds.center, state.Renderer));
            }
            int particleCount = Mathf.Min(ReceiversPerUpdate - worldCount, particles.Count);
            for (int i = 0; i < particleCount; i++)
            {
                nextParticle %= particles.Count;
                var state = particles[nextParticle++];
                if (state.Source && state.Source.gameObject.activeInHierarchy)
                    state.Apply(LocalVisibility(state.Source.transform.position, state.Renderer));
            }
        }

        private float LocalVisibility(Vector3 point, Renderer receiver)
        {
            System.Array.Clear(strongestLights, 0, strongestLights.Length);
            int mask = receiver ? 1 << receiver.gameObject.layer : ~0;
            foreach (var light in lights)
            {
                if (!light || (light.cullingMask & mask) == 0) continue;
                var sample = new LightSample { Source = light };
                if (light.type == LightType.Directional)
                {
                    sample.Direction = -light.transform.forward;
                    sample.Distance = 100f;
                    sample.Contribution = light.intensity * directionalLightContribution;
                }
                else
                {
                    Vector3 toLight = light.transform.position - point;
                    float range = light.range;
                    float distanceSquared = toLight.sqrMagnitude;
                    if (distanceSquared >= range * range || range <= 0f) continue;
                    float distance = Mathf.Sqrt(distanceSquared);
                    sample.Direction = distance > .001f ? toLight / distance : Vector3.zero;
                    if (light.type == LightType.Spot && distance > .001f &&
                        Vector3.Dot(light.transform.forward, -sample.Direction) <
                        Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * .5f)) continue;
                    sample.Distance = distance;
                    float falloff = 1f - distance / range;
                    sample.Contribution = light.intensity * light.color.maxColorComponent * falloff * falloff * .1f;
                }
                for (int i = 0; i < strongestLights.Length; i++)
                {
                    if (sample.Contribution <= strongestLights[i].Contribution) continue;
                    (sample, strongestLights[i]) = (strongestLights[i], sample);
                }
            }

            float brightness = minimumVisibility;
            foreach (var sample in strongestLights)
            {
                if (!sample.Source || brightness >= 1f) break;
                if (!IsOccluded(point, sample, receiver)) brightness += sample.Contribution;
            }
            return Mathf.Clamp01(brightness);
        }

        private bool IsOccluded(Vector3 point, LightSample sample, Renderer receiver)
        {
            if (sample.Distance <= .04f) return false;
            int count = Physics.RaycastNonAlloc(point + sample.Direction * .02f, sample.Direction, hits,
                sample.Distance - .04f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform obstacle = hits[i].transform;
                if (receiver && (obstacle.IsChildOf(receiver.transform) || receiver.transform.IsChildOf(obstacle))) continue;
                if (sample.Source.transform.IsChildOf(obstacle)) continue;
                return true;
            }
            // A saturated buffer can omit a blocker. Keep the receiver dark in that case.
            return count == hits.Length;
        }

        private struct LightSample
        {
            public Light Source;
            public Vector3 Direction;
            public float Distance;
            public float Contribution;
        }

        private Material Variant(Material source, bool forPlayer)
        {
            if (!source || !forPlayer && HorrorRuntimeMaterials.IsLit(source)) return source;
            var key = (source, forPlayer);
            if (variants.TryGetValue(key, out var cached)) return cached;
            var material = HorrorRuntimeMaterials.Create(source);
            if (forPlayer && material.HasProperty(EmissionColor))
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty(EmissionMap) && material.HasProperty(BaseMap))
                {
                    material.SetTexture(EmissionMap, material.GetTexture(BaseMap));
                    material.SetTextureScale(EmissionMap, material.GetTextureScale(BaseMap));
                    material.SetTextureOffset(EmissionMap, material.GetTextureOffset(BaseMap));
                }
            }
            variants.Add(key, material);
            return material;
        }

        private void PruneVariants()
        {
            pruneVariants = false;
            usedVariants.Clear();
            foreach (var state in world.Values) state.CollectMaterials(usedVariants);
            foreach (var state in player.Values) state.CollectMaterials(usedVariants);
            unusedVariants.Clear();
            foreach (var variant in variants)
                if (!usedVariants.Contains(variant.Value)) unusedVariants.Add(variant.Key);
            foreach (var key in unusedVariants)
            {
                Destroy(variants[key]);
                variants.Remove(key);
            }
            unusedVariants.Clear();
            usedVariants.Clear();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            foreach (var state in world.Values) state.Restore();
            foreach (var state in player.Values) state.Restore();
            foreach (var state in particles) state.Restore();
            foreach (var material in variants.Values)
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            variants.Clear();
            world.Clear();
            player.Clear();
            worldReceivers.Clear();
            visitedWorld.Clear();
            particles.Clear();
            knownParticles.Clear();
            closeGlowTarget = closeGlowAmount = 0f;
            pruneVariants = false;
        }

        private static bool ShouldIgnore(Component component)
        {
            if (component.gameObject.layer == 5) return true;
            if (component.gameObject.layer == LayerMask.NameToLayer("MapSystem")) return true;
            if (component.GetComponentInParent<HorrorEngine.MapRenderer>() != null) return true;
            if (component.GetComponentInParent<Canvas>() != null) return true;
            if (component.GetComponentInParent<Light>() != null) return true;
            if (component.GetComponentInParent<HouseToolkit.FixtureAttachment>() is { HasLight: true }) return true;
            if (component is Renderer renderer)
            {
                if (renderer is MeshRenderer && renderer.GetComponentInChildren<Light>(true)) return true;
                if (renderer.sharedMaterial is { } material &&
                    (string.Equals(material.GetTag("FixtureEmission", false), "True", System.StringComparison.OrdinalIgnoreCase) ||
                     material.GetTag("HorrorVisibilityExempt", false) == "True")) return true;
            }
            return component.GetComponentInParent<HorrorLightSource>() != null;
        }

        private sealed class RendererState
        {
            public Renderer Renderer { get; }
            public bool NeedsVisibility { get; }
            private readonly Material[] originals;
            private readonly Material[] applied;
            private readonly MaterialPropertyBlock[] originalBlocks;
            private readonly bool originalForceOff;
            private readonly bool canDim;
            private float lastValue = -1f;

            public RendererState(Renderer renderer, List<Material> materials, HorrorDarknessVisibilityManager manager, bool forPlayer)
            {
                Renderer = renderer;
                originalForceOff = renderer.forceRenderingOff;
                originals = materials.ToArray();
                applied = new Material[originals.Length];
                originalBlocks = new MaterialPropertyBlock[originals.Length];
                bool allSlotsCanDim = true;
                bool hasLitSlot = false;
                for (int i = 0; i < originals.Length; i++)
                {
                    applied[i] = manager.Variant(originals[i], forPlayer);
                    var material = applied[i];
                    if (!material) continue;
                    bool needsVisibility = !HorrorRuntimeMaterials.IsLit(material);
                    NeedsVisibility |= needsVisibility;
                    hasLitSlot |= !needsVisibility;
                    allSlotsCanDim &= !needsVisibility || material.HasProperty(BaseColor) ||
                        material.HasProperty(TintColor) || material.HasProperty(Darkness);
                    if (forPlayer || needsVisibility)
                    {
                        originalBlocks[i] = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(originalBlocks[i], i);
                    }
                }
                canDim = hasLitSlot || allSlotsCanDim;
                renderer.sharedMaterials = applied;
            }

            public void CollectMaterials(HashSet<Material> materials) => materials.UnionWith(applied);

            public bool OwnsMaterials(List<Material> buffer)
            {
                Renderer.GetSharedMaterials(buffer);
                if (buffer.Count != applied.Length) return false;
                for (int i = 0; i < buffer.Count; i++)
                    if (buffer[i] != applied[i]) return false;
                return true;
            }

            public void ApplyGlow(MaterialPropertyBlock block, float glow)
            {
                if (Mathf.Approximately(lastValue, glow)) return;
                lastValue = glow;
                for (int i = 0; i < applied.Length; i++)
                {
                    var material = applied[i];
                    if (!material || !material.HasProperty(EmissionColor)) continue;
                    Renderer.GetPropertyBlock(block, i);
                    Color color = material.HasProperty(BaseColor) ? material.GetColor(BaseColor) :
                        material.HasProperty(TintColor) ? material.GetColor(TintColor) : Color.white;
                    if (material.HasProperty(BaseColor) && !block.HasColor(BaseColor)) block.SetColor(BaseColor, color);
                    block.SetColor(EmissionColor, color * glow);
                    Renderer.SetPropertyBlock(block, i);
                }
            }

            public void ApplyVisibility(MaterialPropertyBlock block, float visibility)
            {
                if (Mathf.Approximately(lastValue, visibility)) return;
                lastValue = visibility;
                for (int i = 0; i < applied.Length; i++)
                {
                    var material = applied[i];
                    if (originalBlocks[i] == null || !material) continue;
                    Renderer.GetPropertyBlock(block, i);
                    if (material.HasProperty(BaseColor)) block.SetColor(BaseColor, Dim(material.GetColor(BaseColor), visibility));
                    if (material.HasProperty(TintColor)) block.SetColor(TintColor, Dim(material.GetColor(TintColor), visibility));
                    if (material.HasProperty(Darkness)) block.SetFloat(Darkness, visibility);
                    Renderer.SetPropertyBlock(block, i);
                }
                Renderer.forceRenderingOff = originalForceOff || (!canDim && visibility <= .001f);
            }

            private static Color Dim(Color color, float visibility) =>
                new(color.r * visibility, color.g * visibility, color.b * visibility, color.a);

            public void Restore()
            {
                if (!Renderer) return;
                var current = Renderer.sharedMaterials;
                for (int i = 0; i < Mathf.Min(current.Length, applied.Length); i++)
                {
                    if (current[i] == applied[i]) current[i] = originals[i];
                    if (originalBlocks[i] != null) Renderer.SetPropertyBlock(originalBlocks[i], i);
                }
                Renderer.sharedMaterials = current;
                if (NeedsVisibility) Renderer.forceRenderingOff = originalForceOff;
            }
        }

        private sealed class ParticleState
        {
            public ParticleSystem Source { get; }
            public ParticleSystemRenderer Renderer { get; }
            private readonly float rateMultiplier;
            private readonly float distanceMultiplier;
            private readonly bool originalForceOff;
            private float lastVisibility = -1f;

            public ParticleState(ParticleSystem source)
            {
                Source = source;
                var emission = source.emission;
                rateMultiplier = emission.rateOverTimeMultiplier;
                distanceMultiplier = emission.rateOverDistanceMultiplier;
                Renderer = source.GetComponent<ParticleSystemRenderer>();
                originalForceOff = Renderer && Renderer.forceRenderingOff;
            }

            public void Apply(float visibility)
            {
                if (Mathf.Approximately(lastVisibility, visibility)) return;
                lastVisibility = visibility;
                var emission = Source.emission;
                emission.rateOverTimeMultiplier = rateMultiplier * visibility;
                emission.rateOverDistanceMultiplier = distanceMultiplier * visibility;
                if (Renderer) Renderer.forceRenderingOff = originalForceOff || visibility < .025f;
            }

            public void Restore()
            {
                if (Renderer) Renderer.forceRenderingOff = originalForceOff;
                if (!Source) return;
                var emission = Source.emission;
                emission.rateOverTimeMultiplier = rateMultiplier;
                emission.rateOverDistanceMultiplier = distanceMultiplier;
            }
        }
    }
}
