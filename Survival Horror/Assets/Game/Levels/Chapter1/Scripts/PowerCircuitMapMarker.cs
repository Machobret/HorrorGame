using UnityEngine;
using HorrorEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Flat, top-down lightning marker for a selectable circuit. It replaces the clipping 3D sphere.</summary>
    public sealed class PowerCircuitMapMarker : MonoBehaviour
    {
        public string CircuitId;
        private static Texture2D iconTexture;
        private static Sprite iconSprite;
        private static Material lightningMaterial;

        public static PowerCircuitMapMarker Create(Transform parent, int layer, string circuitId, Vector3 localPosition)
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

        private static Texture2D GetTexture()
        {
            if (iconTexture) return iconTexture;
            const int size = 32;
            iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Vector2[] bolt = { new Vector2(18, 31), new Vector2(5, 17), new Vector2(14, 17), new Vector2(10, 1), new Vector2(27, 20), new Vector2(18, 20) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    iconTexture.SetPixel(x, y, Contains(new Vector2(x + .5f, y + .5f), bolt) ? Color.white : Color.clear);
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
