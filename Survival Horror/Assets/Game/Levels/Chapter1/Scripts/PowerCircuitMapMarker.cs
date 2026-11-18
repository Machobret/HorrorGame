using UnityEngine;
using HorrorEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Flat, top-down lightning marker for a selectable circuit. It replaces the clipping 3D sphere.</summary>
    public sealed class PowerCircuitMapMarker : MonoBehaviour
    {
        public string CircuitId;
        public string RoomUniqueId { get; private set; }
        private Vector3 normalScale;
        private bool isHovered;
        private bool isPowered;
        private bool hasPowerState;
        private bool showNoServiceStatic;
        private float toggleTime = float.NegativeInfinity;
        private float waveStarted = float.NegativeInfinity;
        private static readonly Texture2D[] WaveFrames = new Texture2D[16];
        private static readonly Sprite[] WaveSprites = new Sprite[16];
        private static readonly Texture2D[] StaticFrames = new Texture2D[8];
        private static readonly Sprite[] StaticSprites = new Sprite[8];

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            UpdateScale();
            Color tint = hovered ? (isPowered ? new Color(1f, .87f, .51f) : Color.white) : new Color(.72f, .94f, 1f);
            Texture2D texture = hovered ? GetGlowTexture(!isPowered) : GetTexture();
            if (TryGetComponent(out SpriteRenderer sprite))
            {
                sprite.color = tint;
                if (hovered && !isPowered && !outlinedSprite)
                    outlinedSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 1f);
                sprite.sprite = hovered && !isPowered ? outlinedSprite : GetSprite();
            }
            else if (TryGetComponent(out MeshRenderer mesh))
            {
                var properties = new MaterialPropertyBlock();
                mesh.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", tint);
                properties.SetColor("_Color", tint);
                properties.SetTexture("_BaseMap", texture);
                mesh.SetPropertyBlock(properties);
            }
        }

        public void SetPowerVisual(bool powered, bool selected, bool outOfRange)
        {
            if (isPowered != powered)
            {
                if (hasPowerState) toggleTime = Time.unscaledTime;
                isPowered = powered;
                SetHovered(isHovered);
            }
            hasPowerState = true;
            if (showNoServiceStatic != outOfRange)
            {
                showNoServiceStatic = outOfRange;
                if (!outOfRange) SetHovered(isHovered);
            }
            bool active = powered && !selected;
            bool wasActive = !float.IsNegativeInfinity(waveStarted);
            if (active == wasActive) return;
            waveStarted = active ? Time.unscaledTime : float.NegativeInfinity;
            if (!active) SetHovered(isHovered);
        }

        private void Update()
        {
            UpdateScale();
            if (showNoServiceStatic)
            {
                int staticFrame = Mathf.FloorToInt(Time.unscaledTime * 18f) % StaticFrames.Length;
                if (!StaticFrames[staticFrame])
                    StaticFrames[staticFrame] = CreateStaticFrame(staticFrame);
                ApplyAnimatedFrame(StaticFrames[staticFrame], StaticSprites, staticFrame);
                return;
            }
            if (float.IsNegativeInfinity(waveStarted)) return;
            const float waveDuration = .72f;
            float elapsed = Mathf.Repeat(Time.unscaledTime - waveStarted, waveDuration);
            int frame = Mathf.Clamp((int)(elapsed / waveDuration * WaveFrames.Length), 0, WaveFrames.Length - 1);
            if (!WaveFrames[frame])
            {
                var pixels = GetTexture().GetPixels();
                float wave = Mathf.Lerp(38f, -7f, frame / (float)(WaveFrames.Length - 1));
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0f) continue;
                    float intensity = Mathf.Clamp01((11f - Mathf.Abs(i / 32 - wave)) / 6f);
                    pixels[i] = Color.Lerp(new Color(.65f, .88f, .95f), new Color(1f, .9f, .24f), intensity);
                }
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                texture.SetPixels(pixels);
                texture.Apply();
                WaveFrames[frame] = texture;
            }
            ApplyAnimatedFrame(WaveFrames[frame], WaveSprites, frame);
        }

        private void ApplyAnimatedFrame(Texture2D texture, Sprite[] sprites, int frame)
        {
            if (TryGetComponent(out SpriteRenderer sprite))
            {
                if (!sprites[frame])
                    sprites[frame] = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 1f);
                sprite.color = Color.white;
                sprite.sprite = sprites[frame];
                return;
            }
            if (!TryGetComponent(out MeshRenderer mesh)) return;
            waveProperties ??= new MaterialPropertyBlock();
            mesh.GetPropertyBlock(waveProperties);
            waveProperties.SetColor("_BaseColor", Color.white);
            waveProperties.SetColor("_Color", Color.white);
            waveProperties.SetTexture("_BaseMap", texture);
            mesh.SetPropertyBlock(waveProperties);
        }

        private static Texture2D CreateStaticFrame(int frame)
        {
            const int size = 32;
            var pixels = GetTexture().GetPixels();
            var noise = new System.Random(1357 + frame * 193);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0f) continue;
                int x = i % size;
                int y = i / size;
                bool rim = x == 0 || x == size - 1 || y == 0 || y == size - 1 ||
                    pixels[i - 1].a == 0f || pixels[i + 1].a == 0f ||
                    pixels[i - size].a == 0f || pixels[i + size].a == 0f;
                float gray = rim ? .61f : Mathf.Lerp(.25f, .71f, (float)noise.NextDouble());
                pixels[i] = new Color(gray, gray, gray + .025f, 1f);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void UpdateScale()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - toggleTime) / .25f);
            float pop = 1f + Mathf.Sin(progress * Mathf.PI) * (1f - progress) * .38f;
            transform.localScale = normalScale * (isHovered ? 1.24f : 1f) * (isPowered ? 1.22f : 1f) * pop;
        }

        private MaterialPropertyBlock waveProperties;
        private static Texture2D iconTexture;
        private static Texture2D glowTexture;
        private static Texture2D outlinedTexture;
        private static Sprite outlinedSprite;
        private static Sprite iconSprite;
        private static Material lightningMaterial;

        public static PowerCircuitMapMarker Create(Transform parent, int layer, string circuitId, Vector3 localPosition, string roomId = null)
        {
            GameObject marker = new GameObject("Circuit Icon — " + circuitId);
            marker.name = "Circuit Icon — " + circuitId;
            marker.layer = layer;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            bool usesMapMarkerMesh = CopyWorkingMapMarkerMesh(marker);
            if (!usesMapMarkerMesh)
            {
                marker.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                SpriteRenderer spriteRenderer = marker.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GetSprite();
                spriteRenderer.color = Color.white;
                spriteRenderer.sortingOrder = 100;
            }
            BoxCollider hitCollider = marker.AddComponent<BoxCollider>();
            // A small margin around the visual; large hitboxes made the hover readout linger.
            hitCollider.size = usesMapMarkerMesh ? new Vector3(2.2f, .95f, 1f) : new Vector3(38f, 38f, 1f);
            PowerCircuitMapMarker result = marker.AddComponent<PowerCircuitMapMarker>();
            result.CircuitId = circuitId;
            result.RoomUniqueId = roomId;
            result.normalScale = marker.transform.localScale * .83f;
            result.SetHovered(false);
            return result;
        }

        private static bool CopyWorkingMapMarkerMesh(GameObject marker)
        {
            if (!MapRenderer.Exists) return false;
            Renderer[] renderers = MapRenderer.Instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer.gameObject.name != "PlayerSprite") continue;
                MeshFilter sourceFilter = renderer.GetComponent<MeshFilter>();
                if (!sourceFilter || !sourceFilter.sharedMesh || !renderer.sharedMaterial) return false;
                MeshFilter filter = marker.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer meshRenderer = marker.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = GetLightningMaterial(renderer.sharedMaterial);
                marker.transform.localRotation = renderer.transform.localRotation;
                marker.transform.localScale = new Vector3(18f, 36f, 1f);
                return true;
            }
            return false;
        }

        private static Material GetLightningMaterial(Material source)
        {
            if (lightningMaterial) return lightningMaterial;
            lightningMaterial = new Material(source) { name = "Circuit Lightning Marker" };
            if (lightningMaterial.HasProperty("_BaseMap")) lightningMaterial.SetTexture("_BaseMap", GetTexture());
            if (lightningMaterial.HasProperty("_MainTex")) lightningMaterial.SetTexture("_MainTex", GetTexture());
            if (lightningMaterial.HasProperty("_BaseColor")) lightningMaterial.SetColor("_BaseColor", Color.white);
            if (lightningMaterial.HasProperty("_Color")) lightningMaterial.SetColor("_Color", Color.white);
            return lightningMaterial;
        }

        private static Sprite GetSprite()
        {
            if (iconSprite) return iconSprite;
            iconSprite = Sprite.Create(GetTexture(), new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 1f);
            return iconSprite;
        }

        internal static Texture2D GetTexture()
        {
            if (iconTexture) return iconTexture;
            const int size = 32;
            iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Vector2[] bolt = { new Vector2(18, 30), new Vector2(6, 15), new Vector2(14, 15), new Vector2(10, 2), new Vector2(26, 20), new Vector2(17, 20), new Vector2(22, 30) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool lightning = Contains(new Vector2(x + .5f, y + .5f), bolt);
                    iconTexture.SetPixel(x, y, lightning ? Color.white : Color.clear);
                }
            iconTexture.Apply();
            return iconTexture;
        }

        private static Texture2D GetGlowTexture(bool outlineOnly)
        {
            var cached = outlineOnly ? outlinedTexture : glowTexture;
            if (cached) return cached;
            const int size = 32;
            var pixels = GetTexture().GetPixels();
            var result = new Color[pixels.Length];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                float alpha = pixels[index].a;
                if (alpha == 0f)
                {
                    for (int sy = Mathf.Max(0, y - 2); sy <= Mathf.Min(size - 1, y + 2); sy++)
                    for (int sx = Mathf.Max(0, x - 2); sx <= Mathf.Min(size - 1, x + 2); sx++)
                    {
                        if (pixels[sy * size + sx].a == 0f) continue;
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(sx, sy));
                        alpha = Mathf.Max(alpha, (outlineOnly ? .88f : .35f) / distance);
                    }
                }
                Color color = !outlineOnly ? Color.white : pixels[index].a > 0f
                    ? new Color(.53f, .83f, 1f) : new Color(1f, .88f, .49f);
                color.a = alpha;
                result[index] = color;
            }
            cached = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            cached.SetPixels(result);
            cached.Apply();
            if (outlineOnly) outlinedTexture = cached;
            else glowTexture = cached;
            return cached;
        }

        private static bool Contains(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) == (polygon[j].y > point.y)) continue;
                if (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
            }
            return inside;
        }
    }
}
