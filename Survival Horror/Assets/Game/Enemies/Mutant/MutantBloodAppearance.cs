using UnityEngine;

namespace HorrorGame.Enemies
{
    [ExecuteAlways]
    public sealed class MutantBloodAppearance : MonoBehaviour
    {
        [SerializeField] private Material baseSkin;
        private Texture2D stainedTexture;
        private SkinnedMeshRenderer skinRenderer;
        private MaterialPropertyBlock originalProperties;
        private int skinSlot;
        private static readonly Vector4[] Smears =
        {
            new Vector4(.375f,.743f,.09f,.04f),
            new Vector4(.40f,.49f,.12f,.14f),
            new Vector4(.13f,.47f,.10f,.08f),
            new Vector4(.83f,.54f,.05f,.19f),
            new Vector4(.93f,.42f,.048f,.17f),
            new Vector4(.51f,.20f,.10f,.11f)
        };

        private void OnEnable()
        {
            if (!gameObject.scene.IsValid() || !baseSkin) return;
            var renderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var source = baseSkin.GetTexture("_BaseMap");
            if (!renderer || !source) return;
            var materials = renderer.sharedMaterials;
            skinSlot = System.Array.IndexOf(materials, baseSkin);
            if (skinSlot < 0)
            {
                skinSlot = 0;
                if (materials.Length == 0) materials = new Material[1];
                materials[skinSlot] = baseSkin;
                renderer.sharedMaterials = materials;
            }

            var snapshot = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = RenderTexture.active;
            try
            {
                Graphics.Blit(source, snapshot);
                RenderTexture.active = snapshot;
                stainedTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
                {
                    name = "Mutant bloodied skin",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode
                };
                stainedTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                RenderTexture.ReleaseTemporary(snapshot);
            }

            var pixels = stainedTexture.GetPixels32();
            var edgeDistance = MeasureIslandPadding(pixels, source.width, source.height);
            float fadeWidth = source.height * .04f;
            for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                float u = (x + .5f) / source.width;
                float v = (y + .5f) / source.height;
                float noise = Mathf.PerlinNoise(u * 57f, v * 72f);
                float stain = 0f;
                foreach (var smear in Smears)
                {
                    float dy = (v - smear.y) / smear.w;
                    float dx = (u - smear.x) / smear.z + dy * .24f;
                    float edge = Mathf.Sqrt(dx * dx + dy * dy) + (noise - .5f) * .65f;
                    stain = Mathf.Max(stain, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.57f, 1.1f, edge)));
                }
                if (noise > .72f) stain = Mathf.Max(stain, .48f);
                int index = y * source.width + x;
                // The atlas packs disconnected body parts against black padding.
                // Leave a clean margin, then feather inward to keep UV seams skin-colored.
                stain *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, fadeWidth, edgeDistance[index]));
                Color32 pixel = pixels[index];
                pixels[index] = new Color32(
                    (byte)(pixel.r * Mathf.Lerp(1f, .59f, stain)),
                    (byte)(pixel.g * Mathf.Lerp(1f, .13f, stain)),
                    (byte)(pixel.b * Mathf.Lerp(1f, .10f, stain)),
                    pixel.a);
            }
            stainedTexture.SetPixels32(pixels);
            stainedTexture.Apply(true, true);

            originalProperties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalProperties, skinSlot);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, skinSlot);
            properties.SetTexture("_BaseMap", stainedTexture);
            renderer.SetPropertyBlock(properties, skinSlot);
            skinRenderer = renderer;
        }

        private static float[] MeasureIslandPadding(Color32[] pixels, int width, int height)
        {
            var distances = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                bool padding = pixel.a < 16 || (pixel.r < 18 && pixel.g < 18 && pixel.b < 18);
                distances[i] = padding ? 0f : width + height;
            }

            const float diagonal = 1.414214f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (x > 0) distances[i] = Mathf.Min(distances[i], distances[i - 1] + 1f);
                if (y == 0) continue;
                distances[i] = Mathf.Min(distances[i], distances[i - width] + 1f);
                if (x > 0) distances[i] = Mathf.Min(distances[i], distances[i - width - 1] + diagonal);
                if (x + 1 < width) distances[i] = Mathf.Min(distances[i], distances[i - width + 1] + diagonal);
            }

            for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                if (x + 1 < width) distances[i] = Mathf.Min(distances[i], distances[i + 1] + 1f);
                if (y + 1 >= height) continue;
                distances[i] = Mathf.Min(distances[i], distances[i + width] + 1f);
                if (x > 0) distances[i] = Mathf.Min(distances[i], distances[i + width - 1] + diagonal);
                if (x + 1 < width) distances[i] = Mathf.Min(distances[i], distances[i + width + 1] + diagonal);
            }
            return distances;
        }

        private void OnDisable()
        {
            if (skinRenderer) skinRenderer.SetPropertyBlock(originalProperties, skinSlot);
            if (!stainedTexture) return;
            if (Application.IsPlaying(gameObject)) Destroy(stainedTexture);
            else DestroyImmediate(stainedTexture);
            stainedTexture = null;
        }
    }
}
