using UnityEngine;
using HorrorEngine;

namespace HorrorGame.Chapter1
{
    public sealed class PowerCircuitMapMarker : MonoBehaviour
    {
        private static readonly Color IdleColor = new Color(.57f, .57f, .57f);
        private static readonly Color HoverColor = new Color(.74f, .74f, .74f);
        private static readonly Color DisplacedColor = new Color(.28f, .28f, .28f);
        private static readonly Color ActiveColor = new Color(1f, .81f, .22f);
        private const float IdleScale = .53f;
        private const float HoverScale = .70f;
        private const float ScaleDuration = .14f;
        private const float ToggleDuration = .25f;
        private const float DisplacementDuration = .46f;

        public string CircuitId;
        public string RoomUniqueId { get; private set; }
        public bool IsInformational { get; private set; }

        private Transform visual;
        private Vector3 fullScale;
        private Renderer icon;
        private bool isHovered;
        private bool isPowered;
        private bool isReachable = true;
        private bool hasPowerState;
        private bool displaced;
        private float scaleFrom = IdleScale;
        private float scaleTarget = IdleScale;
        private float scaleStarted = float.NegativeInfinity;
        private float toggleTime = float.NegativeInfinity;
        private float waveStarted;
        private float displacementTime = float.NegativeInfinity;
        private MaterialPropertyBlock properties;
        private static readonly Texture2D[] WaveFrames = new Texture2D[16];
        private static readonly Sprite[] WaveSprites = new Sprite[16];
        private static readonly Texture2D[] StaticFrames = new Texture2D[8];
        private static readonly Sprite[] StaticSprites = new Sprite[8];
        private static Texture2D iconTexture;
        private static Sprite iconSprite;
        private static Texture2D hoverTexture;
        private static Sprite hoverSprite;
        private static Material lightningMaterial;

        public void SetHovered(bool hovered) => isHovered = hovered;

        internal void SetPowerVisual(bool powered, bool reachable)
        {
            powered &= reachable;
            if (powered != isPowered && reachable && isReachable && hasPowerState)
                toggleTime = Time.unscaledTime;
            if (powered && !isPowered)
            {
                displaced = false;
                displacementTime = float.NegativeInfinity;
                waveStarted = Time.unscaledTime;
            }
            if (!reachable)
            {
                toggleTime = float.NegativeInfinity;
                displacementTime = float.NegativeInfinity;
            }
            isReachable = reachable;
            isPowered = powered;
            hasPowerState = true;
        }

        internal void AnimateDisplacement()
        {
            if (!isReachable) return;
            displacementTime = Time.unscaledTime;
            displaced = true;
        }

        private void LateUpdate() => UpdateVisual();

        private void UpdateVisual()
        {
            float now = Time.unscaledTime;
            float idleScale = isHovered ? HoverScale : IdleScale;
            float target = isPowered ? 1f : idleScale;
            if (target != scaleTarget)
            {
                scaleFrom = CurrentScale(now);
                scaleTarget = target;
                scaleStarted = now;
            }
            float scale = CurrentScale(now);
            float toggle = Mathf.Clamp01((now - toggleTime) / ToggleDuration);
            scale *= 1f + Mathf.Sin(toggle * Mathf.PI) * (1f - toggle) * .38f;

            float progress = Mathf.Clamp01((now - displacementTime) / DisplacementDuration);
            float shake = 0f;
            Color color = isPowered ? ActiveColor : displaced ? DisplacedColor : isHovered ? HoverColor : IdleColor;
            if (progress < 1f)
            {
                shake = Mathf.Sin(progress * Mathf.PI * 8f) * (1f - progress) * fullScale.x * .22f;
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, 1f, progress));
                scale = Mathf.Lerp(1f, idleScale, fade);
                color = Color.Lerp(ActiveColor, DisplacedColor, fade);
            }
            else if (!isPowered)
                scale = Mathf.Min(scale, isReachable ? .84f : HoverScale);

            visual.localPosition = Vector3.right * shake;
            visual.localScale = fullScale * scale;

            if (!isReachable)
            {
                int frame = Mathf.FloorToInt(now * 18f) % StaticFrames.Length;
                if (!StaticFrames[frame]) StaticFrames[frame] = CreateStaticFrame(frame);
                ApplyFrame(StaticFrames[frame], StaticSprites, frame);
            }
            else if (isPowered && !isHovered && progress >= 1f)
            {
                const float waveDuration = .72f;
                int frame = Mathf.Min(WaveFrames.Length - 1,
                    (int)(Mathf.Repeat(now - waveStarted, waveDuration) / waveDuration * WaveFrames.Length));
                if (!WaveFrames[frame]) WaveFrames[frame] = CreateWaveFrame(frame);
                ApplyFrame(WaveFrames[frame], WaveSprites, frame);
            }
            else
            {
                bool glow = isHovered && progress >= 1f;
                Texture2D texture = glow ? GetHoverTexture() : GetTexture();
                if (glow && icon is SpriteRenderer && !hoverSprite) hoverSprite = MakeSprite(texture);
                ApplyTexture(texture, glow ? hoverSprite : GetSprite(), color);
            }
        }

        private float CurrentScale(float now) =>
            Mathf.SmoothStep(scaleFrom, scaleTarget, Mathf.Clamp01((now - scaleStarted) / ScaleDuration));

        private void ApplyFrame(Texture2D texture, Sprite[] sprites, int frame)
        {
            if (icon is SpriteRenderer && !sprites[frame]) sprites[frame] = MakeSprite(texture);
            ApplyTexture(texture, sprites[frame], Color.white);
        }

        private void ApplyTexture(Texture2D texture, Sprite sprite, Color color)
        {
            if (icon is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = color;
                return;
            }

            properties ??= new MaterialPropertyBlock();
            icon.GetPropertyBlock(properties);
            properties.SetTexture("_BaseMap", texture);
            properties.SetTexture("_MainTex", texture);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            icon.SetPropertyBlock(properties);
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
                pixels[i] = new Color(gray, gray, gray);
            }
            return MakeTexture(pixels, FilterMode.Point);
        }

        private static Texture2D CreateWaveFrame(int frame)
        {
            var pixels = GetTexture().GetPixels();
            float wave = Mathf.Lerp(38f, -7f, frame / (float)(WaveFrames.Length - 1));
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0f) continue;
                float intensity = Mathf.Clamp01((11f - Mathf.Abs(i / 32 - wave)) / 6f);
                pixels[i] = Color.Lerp(ActiveColor, new Color(1f, .98f, .69f), intensity);
            }
            return MakeTexture(pixels, FilterMode.Point);
        }

        private static Texture2D GetHoverTexture()
        {
            if (hoverTexture) return hoverTexture;
            const int size = 32;
            var source = GetTexture().GetPixels();
            var pixels = new Color[source.Length];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                float alpha = source[index].a;
                if (alpha == 0f)
                {
                    for (int sy = Mathf.Max(0, y - 2); sy <= Mathf.Min(size - 1, y + 2); sy++)
                    for (int sx = Mathf.Max(0, x - 2); sx <= Mathf.Min(size - 1, x + 2); sx++)
                    {
                        if (source[sy * size + sx].a == 0f) continue;
                        alpha = Mathf.Max(alpha, .45f / Vector2.Distance(new Vector2(x, y), new Vector2(sx, sy)));
                    }
                }
                pixels[index] = new Color(1f, 1f, 1f, alpha);
            }
            return hoverTexture = MakeTexture(pixels, FilterMode.Bilinear);
        }

        private static Texture2D MakeTexture(Color[] pixels, FilterMode filter)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Sprite MakeSprite(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 1f);

        public static PowerCircuitMapMarker Create(Transform parent, int layer, string circuitId, Vector3 localPosition,
            string roomId, bool informational = false)
        {
            var marker = new GameObject("Circuit Icon - " + circuitId) { layer = layer };
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;

            var graphic = new GameObject("Lightning") { layer = layer };
            graphic.transform.SetParent(marker.transform, false);
            bool usesMesh = CopyWorkingMapMarkerMesh(graphic);
            if (!usesMesh)
            {
                graphic.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                graphic.AddComponent<SpriteRenderer>().sprite = GetSprite();
            }

            var renderer = graphic.GetComponent<Renderer>();
            renderer.sortingOrder = 100;
            marker.transform.localRotation = graphic.transform.localRotation;
            graphic.transform.localRotation = Quaternion.identity;
            if (!informational)
            {
                var collider = marker.AddComponent<BoxCollider>();
                collider.size = usesMesh
                    ? Vector3.Scale(new Vector3(2.2f, .95f, 1f), graphic.transform.localScale)
                    : new Vector3(38f, 38f, 1f);
            }

            var result = marker.AddComponent<PowerCircuitMapMarker>();
            result.CircuitId = circuitId;
            result.RoomUniqueId = roomId;
            result.IsInformational = informational;
            result.visual = graphic.transform;
            result.icon = renderer;
            result.fullScale = graphic.transform.localScale * 1.24f;
            result.UpdateVisual();
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
            iconSprite = MakeSprite(GetTexture());
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
