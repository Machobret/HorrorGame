using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace HorrorGame.Lighting
{
    /// <summary>Removable distance fog with matching fog-colored sky/background.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Horror Game/Lighting/Atmospheric Fog")]
    public sealed class AtmosphericFog : MonoBehaviour
    {
        [Tooltip("Turn the added fog on/off. Removing this component restores the scene's base fog.")]
        public bool fogEnabled = true;
        public Color fogColor = new Color(.40f, .44f, .43f, 1f);
        [Tooltip("Distance fog is measured from each rendering camera, never the player or this object's position. Higher density brings fog closer: 0.045 is moderate, 0.08 is close, 0.12 is very dense.")]
        [Range(0f, .15f)] public float density = .08f;
        [Min(0f)] public float fadeSeconds = 2f;
        [Tooltip("Hide the open sky behind the fog color, so fog surrounds the camera even where no geometry exists.")]
        public bool fogOpenSky = true;

        private static AtmosphericFog owner;
        [SerializeField, HideInInspector] private FogState baseline;
        [SerializeField, HideInInspector] private bool hasBaseline;
        private bool ownsFog;
        private float amount;
        private readonly Dictionary<Camera, CameraBackground> cameraBackgrounds = new();
        private readonly Dictionary<Camera, FogState> cameraFogStates = new();
        public float CurrentStrength => amount;

        private struct CameraBackground
        {
            public CameraClearFlags clearFlags;
            public Color color;
        }

        [System.Serializable]
        private struct FogState
        {
            public bool enabled;
            public FogMode mode;
            public Color color;
            public float density, start, end;
            public static FogState Capture() => new FogState {
                enabled = RenderSettings.fog, mode = RenderSettings.fogMode,
                color = RenderSettings.fogColor, density = RenderSettings.fogDensity,
                start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance
            };
            public void Apply()
            {
                RenderSettings.fog = enabled;
                RenderSettings.fogMode = mode;
                RenderSettings.fogColor = color;
                RenderSettings.fogDensity = density;
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = end;
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            Camera.onPreCull += BeginBuiltInCamera;
            Camera.onPostRender += EndBuiltInCamera;
            RefreshOwnership();
            ApplyOverlay();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Camera.onPreCull -= BeginBuiltInCamera;
            Camera.onPostRender -= EndBuiltInCamera;
            RestoreCameraBackgrounds();
            Release();
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera) => BeginCameraFog(camera);
        private void EndCamera(ScriptableRenderContext context, Camera camera) => EndCameraFog(camera);
        private void BeginBuiltInCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) BeginCameraFog(camera);
        }
        private void EndBuiltInCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) EndCameraFog(camera);
        }

        private static bool IsInterfaceCamera(Camera camera)
        {
            if (camera.GetComponentInParent<HorrorEngine.MapRenderer>() != null) return true;
            int interfaceMask = LayerMask.GetMask("UI", "MapSystem");
            return camera.cullingMask != 0 && (camera.cullingMask & ~interfaceMask) == 0;
        }

        private void BeginCameraFog(Camera camera)
        {
            if (!ownsFog || camera == null) return;
            if (IsInterfaceCamera(camera))
            {
                if (!cameraFogStates.ContainsKey(camera))
                    cameraFogStates.Add(camera, FogState.Capture());
                // The map renders meshes into a texture. Disable surface fog as
                // well as the sky override, then restore before the world camera.
                RenderSettings.fog = false;
                return;
            }
            ApplySkyFog(camera);
        }

        private void EndCameraFog(Camera camera)
        {
            RestoreCameraBackground(camera);
            if (camera != null && cameraFogStates.TryGetValue(camera, out var state))
            {
                state.Apply();
                cameraFogStates.Remove(camera);
            }
        }

        private void ApplySkyFog(Camera camera)
        {
            if (!ownsFog || !fogOpenSky || amount <= 0f || density <= 0f || camera == null) return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            // Preserve depth-only/overlay cameras; they must not erase the base camera.
            if (camera.clearFlags != CameraClearFlags.Skybox && camera.clearFlags != CameraClearFlags.SolidColor) return;
            if (cameraBackgrounds.ContainsKey(camera)) return;
            cameraBackgrounds.Add(camera, new CameraBackground {
                clearFlags = camera.clearFlags, color = camera.backgroundColor
            });
            // Infinite-distance sky is fully obscured. Use the exact surface-fog
            // color so silhouettes disappear seamlessly into the background.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
        }

        private void RestoreCameraBackground(Camera camera)
        {
            if (camera == null || !cameraBackgrounds.TryGetValue(camera, out var state)) return;
            camera.clearFlags = state.clearFlags;
            camera.backgroundColor = state.color;
            cameraBackgrounds.Remove(camera);
        }

        private void RestoreCameraBackgrounds()
        {
            foreach (var pair in cameraBackgrounds)
            {
                if (pair.Key == null) continue;
                pair.Key.clearFlags = pair.Value.clearFlags;
                pair.Key.backgroundColor = pair.Value.color;
            }
            cameraBackgrounds.Clear();
            foreach (var state in cameraFogStates.Values) state.Apply();
            cameraFogStates.Clear();
        }

        private bool CanApply()
        {
            if (!isActiveAndEnabled || gameObject.scene != SceneManager.GetActiveScene()) return false;
#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return false;
#endif
            return true;
        }

        private void ActiveSceneChanged(Scene previous, Scene next)
        {
            // Retain this scene's baseline when it becomes inactive. Never write
            // its settings into the newly activated scene.
            if (ownsFog) { ownsFog = false; if (owner == this) owner = null; }
            RefreshOwnership();
        }

        private void RefreshOwnership()
        {
            if (!CanApply()) { Release(); return; }
            // One owner per active scene prevents two fog components fighting.
            if (owner != null && owner != this) return;
            if (!ownsFog)
            {
                if (!hasBaseline) { baseline = FogState.Capture(); hasBaseline = true; }
                owner = this;
                ownsFog = true;
                amount = fogEnabled ? 1f : 0f;
            }
        }

        private void Release()
        {
            if (!ownsFog) return;
            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                baseline.Apply();
                hasBaseline = false;
            }
            ownsFog = false;
            if (owner == this) owner = null;
        }

        private void Update()
        {
            RefreshOwnership();
            if (!ownsFog) return;
            float target = fogEnabled ? 1f : 0f;
            amount = !Application.isPlaying || fadeSeconds <= 0f ? target :
                Mathf.MoveTowards(amount, target, Time.deltaTime / fadeSeconds);
            ApplyOverlay();
        }

        private void ApplyOverlay()
        {
            if (!ownsFog) return;
            if (amount <= 0f) { baseline.Apply(); return; }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(baseline.color, fogColor, amount);
            RenderSettings.fogDensity = Mathf.Lerp(baseline.enabled ? baseline.density : 0f, density, amount);
        }

        public void SetFogEnabled(bool value) { fogEnabled = value; ApplySettings(); }
        [ContextMenu("Fade In Fog")] public void FadeIn() { fogEnabled = true; if (!Application.isPlaying) ApplySettings(); }
        [ContextMenu("Fade Out Fog")] public void FadeOut() { fogEnabled = false; if (!Application.isPlaying) ApplySettings(); }
        public void ToggleFog() => SetFogEnabled(!fogEnabled);
        [ContextMenu("Show Fog Immediately")]
        public void ShowImmediately() { fogEnabled = true; RefreshOwnership(); amount = 1f; ApplyOverlay(); }
        [ContextMenu("Remove Fog Immediately")]
        public void HideImmediately() { fogEnabled = false; amount = 0f; ApplyOverlay(); }
        public void SetDensity(float value) { density = Mathf.Clamp(value, 0f, .15f); ApplySettings(); }

        public void ApplySettings()
        {
            RefreshOwnership();
            amount = fogEnabled ? 1f : 0f;
            ApplyOverlay();
        }

        // Lighting presets update the underlying state without removing the overlay.
        public static void ApplyBaseFog(bool enabled, FogMode mode, Color color, float density)
        {
            if (owner != null && owner.ownsFog && owner.gameObject.scene == SceneManager.GetActiveScene())
            {
                owner.baseline.enabled = enabled;
                owner.baseline.mode = mode;
                owner.baseline.color = color;
                owner.baseline.density = density;
                owner.ApplyOverlay();
                return;
            }
            RenderSettings.fog = enabled;
            RenderSettings.fogMode = mode;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;
        }
    }
}
