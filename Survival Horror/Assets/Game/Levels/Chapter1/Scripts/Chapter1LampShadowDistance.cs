using System;
using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Chapter1
{
    [DisallowMultipleComponent]
    public sealed class Chapter1LampShadowDistance : MonoBehaviour
    {
        [SerializeField, Min(.1f), Tooltip("Distance from the edge of the lamp's range where its illumination finishes fading out.")]
        private float indoorDistance = 15f;
        [SerializeField, Min(.1f), Tooltip("Length of the fade before the distance limit.")]
        private float fadeWidth = 3f;
        [SerializeField] private Lamp[] indoorLamps = Array.Empty<Lamp>();

        private readonly List<LampState> changedLamps = new();
        private readonly HashSet<Light> visited = new();
        private Camera renderingCamera;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= EndCameraRendering;
            RestoreLamps();
        }

        private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (renderingCamera || camera.cameraType != CameraType.Game) return;
            Camera gameplayCamera = CameraSystem.Exists ? CameraSystem.Instance.MainCamera : null;
            if (!gameplayCamera) gameplayCamera = Camera.main;
            if (camera != gameplayCamera) return;

            renderingCamera = camera;
            Vector3 position = camera.transform.position;
            visited.Clear();
            foreach (Lamp lamp in indoorLamps)
            {
                Light light = lamp.Light;
                if (!light || !light.isActiveAndEnabled || light.intensity <= 0f ||
                    (light.type != LightType.Point && light.type != LightType.Spot) ||
                    light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked ||
                    !visited.Add(light)) continue;

                float limit = lamp.DistanceOverride > 0f ? lamp.DistanceOverride : indoorDistance;
                // The range sphere is also a conservative bound for a spotlight's cone.
                float distance = Mathf.Max(0f, Vector3.Distance(position, light.transform.position) - light.range);
                float fadeStart = Mathf.Max(0f, limit - fadeWidth);
                if (distance <= fadeStart) continue;

                changedLamps.Add(new LampState(light));
                float brightness = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, limit, distance));
                light.intensity *= brightness;
                // Drop the shadow map only after the lamp contributes zero light. The circuit owns Light.enabled.
                if (brightness == 0f) light.shadows = LightShadows.None;
            }
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == renderingCamera) RestoreLamps();
        }

        private void RestoreLamps()
        {
            // Restore before gameplay, flicker, and other cameras read the authored light state.
            foreach (LampState state in changedLamps)
            {
                if (!state.Light) continue;
                if (state.Light.shadows != state.Shadows) state.Light.shadows = state.Shadows;
                state.Light.intensity = state.Intensity;
            }
            changedLamps.Clear();
            renderingCamera = null;
        }

        [Serializable]
        private struct Lamp
        {
            public Light Light;
            [Min(0f), Tooltip("Zero uses the indoor default. A positive value sets this lamp's own distance in metres.")]
            public float DistanceOverride;
        }

        private readonly struct LampState
        {
            public readonly Light Light;
            public readonly float Intensity;
            public readonly LightShadows Shadows;

            public LampState(Light light)
            {
                Light = light;
                Intensity = light.intensity;
                Shadows = light.shadows;
            }
        }
    }
}
