using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>
    /// Applies one responsive scaling policy to every screen-space canvas.
    /// This also covers UI spawned after a scene loads, which cannot be kept
    /// consistent by prefab-only CanvasScaler settings.
    /// </summary>
    public static class ResponsiveCanvasScaler
    {
        private static readonly HashSet<int> ConfiguredCanvases = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (_, __) => ConfigureAll();
            var host = new GameObject(nameof(ResponsiveCanvasScaler));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<CanvasScanPump>();
        }

        private static void ConfigureAll()
        {
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                // Resources also returns prefab assets in the editor. Only touch
                // live scene objects, never their source assets.
                if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                int id = canvas.GetInstanceID();
                if (!ConfiguredCanvases.Add(id)) continue;

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

                // Preserve authored reference sizes: legacy menus are designed
                // at 640x480, while newer custom menus use 1280x720. Matching
                // both dimensions prevents stretch at ultrawide and 4:3 sizes.
                if (scaler.referenceResolution.x <= 0 || scaler.referenceResolution.y <= 0)
                    scaler.referenceResolution = new Vector2(1280, 720);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                scaler.referencePixelsPerUnit = 100;
            }
        }

        private sealed class CanvasScanPump : MonoBehaviour
        {
            private float m_NextScan;

            private void Update()
            {
                if (Time.unscaledTime < m_NextScan) return;
                m_NextScan = Time.unscaledTime + .5f;
                ConfigureAll();
            }
        }
    }
}
