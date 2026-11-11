using HorrorEngine;
using UnityEngine;
using UnityEngine.Events;

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
        [Header("Map label")]
        [Tooltip("Text rendered beside this marker in the in-game map. Leave empty to use the type name.")]
        public string DisplayText;
        public bool ShowDisplayText = true;
        [Header("Polygon trigger")]
        [Tooltip("Optional Shape polygon. When assigned, entering this authored area fires the events below and replaces the distance-only reached check.")]
        public Shape TriggerArea;
        public UnityEvent OnAreaEntered;
        public UnityEvent OnAreaExited;
        public UnityEvent OnReached;
        [Tooltip("When enabled, OnReached only fires once. Enter/exit events still fire normally.")]
        public bool ReachEventOnce = true;
        public Color IndicatorColor = UnityEngine.Color.white;
        [Min(.1f)] public float Size = 28f;
        [Min(0)] public float ReachedDistance = 1.25f;
        public bool HideWhenReached = true;
        public bool IsEnabled = true;

        private GameObject marker;
        private MeshRenderer markerRenderer;
        private Material material;
        private GameObject labelObject;
        private TextMesh label;
        private bool wasInsideArea;
        private bool reachedEventFired;

        private void OnEnable() => EnsureMarker();
        private void OnDisable()
        {
            if (marker) marker.SetActive(false);
            if (labelObject) labelObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (marker) Destroy(marker);
            if (labelObject) Destroy(labelObject);
            if (material) Destroy(material);
        }

        private void Update()
        {
            EnsureMarker();
            if (!marker) return;

            MapController controller = ResolveController();
            Transform target = Target ? Target : transform;
            bool isInsideArea = IsEnabled && IsPlayerInsideArea();
            UpdateAreaEvents(isInsideArea);
            if (!IsEnabled || !controller || !controller.Data || !target || !GameManager.Exists || !GameManager.Instance.Player || !MapRenderer.Exists || !MapRenderer.Instance.gameObject.activeInHierarchy || MapRenderer.Instance.CurrentMap != controller.Data)
            {
                marker.SetActive(false);
                if (labelObject) labelObject.SetActive(false);
                return;
            }

            Vector3 targetPosition;
            if (TriggerArea && TryGetAreaCenter(out Vector3 areaCenter))
            {
                if (!TryGetMapPosition(controller, areaCenter, TriggerArea.transform, out targetPosition))
                {
                    marker.SetActive(false);
                    if (labelObject) labelObject.SetActive(false);
                    return;
                }
            }
            else if (!TryGetMapPosition(controller, target.position, target, out targetPosition))
            {
                marker.SetActive(false);
                if (labelObject) labelObject.SetActive(false);
                return;
            }

            if (!controller.TryGetTransformInRoom(GameManager.Instance.Player.transform, out Vector3 playerPosition, out _))
            {
                marker.SetActive(false);
                if (labelObject) labelObject.SetActive(false);
                return;
            }

            Vector3 targetWorld = target.position;
            bool reached = TriggerArea ? isInsideArea : Vector3.Distance(new Vector3(targetWorld.x, 0, targetWorld.z), new Vector3(GameManager.Instance.Player.transform.position.x, 0, GameManager.Instance.Player.transform.position.z)) <= ReachedDistance;
            if (HideWhenReached && reached)
            {
                marker.SetActive(false);
                if (labelObject) labelObject.SetActive(false);
                return;
            }

            marker.SetActive(true);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", IndicatorColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", IndicatorColor);
            Vector3 mapPosition = Type == MapNavigationIndicatorType.ObjectiveArrow ? playerPosition : targetPosition;
            // MapController returns map-space X/Z already. Do not flip Z a
            // second time here or the marker ends up mirrored off the drawing.
            marker.transform.localPosition = new Vector3(mapPosition.x, 7f, mapPosition.y);
            if (Type == MapNavigationIndicatorType.ObjectiveArrow)
            {
                Vector3 direction = targetPosition - playerPosition;
                marker.transform.localRotation = direction.sqrMagnitude > .001f
                    ? Quaternion.LookRotation(new Vector3(direction.x, 0, -direction.y), Vector3.up)
                    : Quaternion.identity;
            }
            else marker.transform.localRotation = Quaternion.identity;

            if (labelObject)
            {
                labelObject.SetActive(ShowDisplayText);
                if (ShowDisplayText)
                {
                    label.text = string.IsNullOrWhiteSpace(DisplayText)
                        ? (Type == MapNavigationIndicatorType.ObjectiveArrow ? "Objective" : "Blocked")
                        : DisplayText;
                    label.color = IndicatorColor;
                    labelObject.transform.localPosition = marker.transform.localPosition + new Vector3(0, .1f, -Size * .8f);
                    labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }
        }

        public void SetEnabled(bool value) => IsEnabled = value;

        private static bool TryGetMapPosition(MapController controller, Vector3 worldPosition, Transform authoredTransform, out Vector3 mapPosition)
        {
            if (!controller) { mapPosition = Vector3.zero; return false; }
            // The Map Editor renders direct MapController children in canvas
            // coordinates. Preserve that exact convention at runtime instead
            // of applying a room transform to them a second time.
            if (authoredTransform && authoredTransform.IsChildOf(controller.transform) && !authoredTransform.GetComponentInParent<MapRoom>())
            {
                mapPosition = controller.transform.InverseTransformPoint(worldPosition);
                return true;
            }
            if (controller.TryGetWorldPositionInRoom(worldPosition, out mapPosition)) return true;
            mapPosition = controller.transform.InverseTransformPoint(worldPosition);
            return true;
        }

        private bool IsPlayerInsideArea()
        {
            if (!TriggerArea || !GameManager.Exists || !GameManager.Instance.Player || TriggerArea.Points == null || TriggerArea.Points.Count < 3)
                return false;

            Vector3 localPlayer = TriggerArea.transform.InverseTransformPoint(GameManager.Instance.Player.transform.position);
            bool inside = false;
            for (int i = 0, j = TriggerArea.Points.Count - 1; i < TriggerArea.Points.Count; j = i++)
            {
                Vector3 a = TriggerArea.Points[i];
                Vector3 b = TriggerArea.Points[j];
                bool crosses = (a.z > localPlayer.z) != (b.z > localPlayer.z);
                if (crosses && localPlayer.x < (b.x - a.x) * (localPlayer.z - a.z) / (b.z - a.z) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private void UpdateAreaEvents(bool inside)
        {
            if (!TriggerArea || !GameManager.Exists || !GameManager.Instance.Player) return;
            if (inside && !wasInsideArea)
            {
                OnAreaEntered?.Invoke();
                if (!ReachEventOnce || !reachedEventFired)
                {
                    OnReached?.Invoke();
                    reachedEventFired = true;
                }
            }
            else if (!inside && wasInsideArea) OnAreaExited?.Invoke();
            wasInsideArea = inside;
        }

        private bool TryGetAreaCenter(out Vector3 center)
        {
            center = Vector3.zero;
            if (!TriggerArea || TriggerArea.Points == null || TriggerArea.Points.Count == 0) return false;
            foreach (Vector3 point in TriggerArea.Points) center += TriggerArea.transform.TransformPoint(point);
            center /= TriggerArea.Points.Count;
            return true;
        }

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
            // The map camera looks down at these flat meshes. Their authored
            // winding can face either direction, so never cull the indicator.
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", 0f);
            markerRenderer.sharedMaterial = material;

            labelObject = new GameObject("Map Navigation Label");
            labelObject.layer = marker.layer;
            labelObject.transform.SetParent(MapRenderer.Instance.transform, false);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = .22f;
            label.color = IndicatorColor;
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
