using System.Collections.Generic;
using HorrorEngine;
using Unity.Profiling;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [DefaultExecutionOrder(11000), DisallowMultipleComponent]
    public sealed class Chapter1ShadowBudget : MonoBehaviour
    {
        [SerializeField, Min(0)] private int shadowSlices = 7;
        [SerializeField] private Light[] priorityLights = System.Array.Empty<Light>();

        private const float RefreshInterval = .15f;
        private const float DiscoveryInterval = 1f;
        private static readonly ProfilerMarker UpdateMarker = new("Chapter 1.Select Shadow Casters");
        private readonly Dictionary<Light, LightShadows> originalShadows = new();
        private readonly List<Light> removedLights = new();
        private readonly List<Candidate> candidates = new();
        private readonly HashSet<Light> selected = new();
        private readonly HashSet<Light> priorities = new();
        private readonly Plane[] frustum = new Plane[6];
        private float nextRefresh;
        private float nextDiscovery;

        private void OnEnable()
        {
            priorities.UnionWith(priorityLights);
            nextRefresh = nextDiscovery = 0f;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now < nextRefresh) return;
            var camera = CameraSystem.Exists ? CameraSystem.Instance.MainCamera : Camera.main;
            if (!camera) return;
            nextRefresh = now + RefreshInterval;
            using var scope = UpdateMarker.Auto();
            if (now >= nextDiscovery)
            {
                DiscoverLights();
                nextDiscovery = now + DiscoveryInterval;
            }

            var player = GameManager.Exists ? GameManager.Instance.Player : null;
            Vector3 focus = player ? player.transform.position : camera.transform.position + camera.transform.forward * 5f;
            SelectShadows(camera, focus);
        }

        private void DiscoverLights()
        {
            removedLights.Clear();
            foreach (var light in originalShadows.Keys)
                if (!light || light.gameObject.scene != gameObject.scene) removedLights.Add(light);
            foreach (var light in removedLights)
            {
                if (light) light.shadows = originalShadows[light];
                originalShadows.Remove(light);
                selected.Remove(light);
            }
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != gameObject.scene || light.shadows == LightShadows.None ||
                    light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked ||
                    (light.type != LightType.Point && light.type != LightType.Spot)) continue;
                originalShadows.TryAdd(light, light.shadows);
            }
        }

        private void SelectShadows(Camera camera, Vector3 focus)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            candidates.Clear();
            foreach (var light in originalShadows.Keys)
            {
                if (!light || !light.isActiveAndEnabled || light.intensity <= 0f ||
                    (light.cullingMask & camera.cullingMask) == 0) continue;

                float range = light.range;
                Vector3 position = light.transform.position;
                if (!GeometryUtility.TestPlanesAABB(frustum, new Bounds(position, Vector3.one * (2f * range)))) continue;
                float reach = range * range;
                float distance = (position - focus).sqrMagnitude;
                float score = reach / Mathf.Max(1f, distance);
                if (light.type == LightType.Spot && Vector3.Dot(light.transform.forward, focus - position) < 0f)
                    score *= .25f;
                if (priorities.Contains(light)) score *= 3f;
                if (selected.Contains(light)) score *= 1.25f; // Stabilize ties while the camera moves.
                candidates.Add(new Candidate(light, score));
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            selected.Clear();
            int remaining = shadowSlices;
            foreach (var candidate in candidates)
            {
                // Point shadows need all six cubemap faces. Spot shadows need one slice.
                int cost = candidate.Light.type == LightType.Point ? 6 : 1;
                if (cost > remaining) continue;
                selected.Add(candidate.Light);
                remaining -= cost;
            }

            foreach (var pair in originalShadows)
            {
                if (!pair.Key) continue;
                LightShadows mode = selected.Contains(pair.Key) ? pair.Value : LightShadows.None;
                if (pair.Key.shadows != mode) pair.Key.shadows = mode;
            }
        }

        private void OnDisable()
        {
            foreach (var pair in originalShadows)
                if (pair.Key) pair.Key.shadows = pair.Value;
            originalShadows.Clear();
            selected.Clear();
            priorities.Clear();
        }

        private readonly struct Candidate
        {
            public Light Light { get; }
            public float Score { get; }

            public Candidate(Light light, float score)
            {
                Light = light;
                Score = score;
            }
        }
    }
}
