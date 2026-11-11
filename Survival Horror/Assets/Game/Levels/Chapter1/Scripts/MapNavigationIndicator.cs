using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    public enum MapNavigationIndicatorType { ObjectiveArrow, BlockedPathCross }

    /// <summary>Place in a MapController hierarchy. It draws either a player-centred direction arrow or a blocked-route cross in the normal map UI.</summary>
    public sealed class MapNavigationIndicator : MonoBehaviour
    {
        public MapNavigationIndicatorType Type = MapNavigationIndicatorType.ObjectiveArrow;
        [Tooltip("Optional explicit controller. When empty, this component uses its parent MapController.")]
        public MapController Map;
        [Tooltip("World destination for the arrow/cross. Empty means this object's position.")]
        public Transform Target;
        public Color IndicatorColor = UnityEngine.Color.white;
        [Min(.1f)] public float Size = 28f;
        [Min(0)] public float ReachedDistance = 1.25f;
        public bool HideWhenReached = true;
        public bool IsEnabled = true;

        private GameObject marker;
        private MeshRenderer markerRenderer;
        private Material material;

        private void OnEnable() => EnsureMarker();
        private void OnDisable() { if (marker) marker.SetActive(false); }
        private void OnDestroy()
        {
            if (marker) Destroy(marker);
            if (material) Destroy(material);
        }

        private void Update()
        {
            EnsureMarker();
            if (!marker) return;

            MapController controller = ResolveController();
            UIMap uiMap = UIManager.Get<UIMap>(true);
            Transform target = Target ? Target : transform;
            if (!IsEnabled || !controller || !controller.Data || !target || !GameManager.Exists || !GameManager.Instance.Player || !uiMap || !uiMap.gameObject.activeInHierarchy || uiMap.CurrentMap != controller.Data)
            {
                marker.SetActive(false);
                return;
            }

            if (!controller.TryGetTransformInRoom(target, out Vector3 targetPosition, out _)
                || !controller.TryGetTransformInRoom(GameManager.Instance.Player.transform, out Vector3 playerPosition, out _))
            {
                marker.SetActive(false);
                return;
            }

            Vector3 targetWorld = target.position;
            bool reached = Vector3.Distance(new Vector3(targetWorld.x, 0, targetWorld.z), new Vector3(GameManager.Instance.Player.transform.position.x, 0, GameManager.Instance.Player.transform.position.z)) <= ReachedDistance;
            if (HideWhenReached && reached)
            {
                marker.SetActive(false);
                return;
            }

            marker.SetActive(true);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", IndicatorColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", IndicatorColor);
            Vector3 mapPosition = Type == MapNavigationIndicatorType.ObjectiveArrow ? playerPosition : targetPosition;
            marker.transform.localPosition = new Vector3(mapPosition.x, 7f, -mapPosition.y);
            if (Type == MapNavigationIndicatorType.ObjectiveArrow)
            {
                Vector3 direction = targetPosition - playerPosition;
                marker.transform.localRotation = direction.sqrMagnitude > .001f
                    ? Quaternion.LookRotation(new Vector3(direction.x, 0, -direction.y), Vector3.up)
                    : Quaternion.identity;
            }
            else marker.transform.localRotation = Quaternion.identity;
        }

        public void SetEnabled(bool value) => IsEnabled = value;

        private MapController ResolveController()
        {
            if (Map && Map.Data) return Map;
            Map = GetComponentInParent<MapController>();
            return Map && Map.Data ? Map : null;
        }

        private void EnsureMarker()
        {
            if (marker || !MapRenderer.Exists) return;
            marker = new GameObject("Map Navigation — " + Type);
            int mapLayer = LayerMask.NameToLayer("MapSystem");
            if (mapLayer >= 0) marker.layer = mapLayer;
            marker.transform.SetParent(MapRenderer.Instance.transform, false);
            MeshFilter filter = marker.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateMesh(Type);
            markerRenderer = marker.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "Map Navigation Indicator" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", IndicatorColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", IndicatorColor);
            markerRenderer.sharedMaterial = material;
        }

        private Mesh CreateMesh(MapNavigationIndicatorType type)
        {
            float half = Size * .5f;
            Mesh mesh = new Mesh { name = type == MapNavigationIndicatorType.ObjectiveArrow ? "Map objective arrow" : "Map blocked-path cross" };
            if (type == MapNavigationIndicatorType.ObjectiveArrow)
            {
                mesh.vertices = new[] { new Vector3(0, 0, half), new Vector3(-half, 0, -half), new Vector3(-half * .38f, 0, -half * .25f), new Vector3(half, 0, -half), new Vector3(half * .38f, 0, -half * .25f) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 4, 0, 4, 3 };
            }
            else
            {
                float width = Size * .18f;
                mesh.vertices = new[]
                {
                    new Vector3(-half, 0, -half + width), new Vector3(-half + width, 0, -half), new Vector3(half, 0, half - width), new Vector3(half - width, 0, half),
                    new Vector3(-half, 0, half - width), new Vector3(-half + width, 0, half), new Vector3(half, 0, -half + width), new Vector3(half - width, 0, -half)
                };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
