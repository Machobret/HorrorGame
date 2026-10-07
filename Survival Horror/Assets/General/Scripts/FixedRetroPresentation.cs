using UnityEngine;

namespace HorrorGame.General
{
    /// <summary>
    /// Keeps the game's authored 16:9 view intact. A fixed output resolution
    /// and camera viewport prevent wider or taller displays from revealing
    /// parts of a locked camera shot that were never composed for the player.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class FixedRetroPresentation : MonoBehaviour
    {
        private const bool Enabled = true;
        private const int Width = 1280;
        private const int Height = 720;
        private const float AspectRatio = (float)Width / Height;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (!Enabled) return;
            if (FindFirstObjectByType<FixedRetroPresentation>() != null) return;

            var presentation = new GameObject(nameof(FixedRetroPresentation));
            Object.DontDestroyOnLoad(presentation);
            presentation.AddComponent<FixedRetroPresentation>();
        }

        private void Awake()
        {
            ApplyResolution();
        }

        private void LateUpdate()
        {
            ApplyCameraViewport();
        }

        private static void ApplyResolution()
        {
#if !UNITY_EDITOR
            Screen.SetResolution(Width, Height, FullScreenMode.ExclusiveFullScreen);
#endif
        }

        private static void ApplyCameraViewport()
        {
            var camera = Camera.main;
            if (camera == null || camera.targetTexture != null) return;

            float currentAspect = (float)Screen.width / Screen.height;
            if (currentAspect > AspectRatio)
            {
                float width = AspectRatio / currentAspect;
                camera.rect = new Rect((1f - width) * .5f, 0f, width, 1f);
            }
            else
            {
                float height = currentAspect / AspectRatio;
                camera.rect = new Rect(0f, (1f - height) * .5f, 1f, height);
            }
        }
    }
}
