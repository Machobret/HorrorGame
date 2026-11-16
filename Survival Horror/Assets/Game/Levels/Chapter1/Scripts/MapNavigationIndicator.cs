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
        [Tooltip("Optional exact room used to convert this marker into map coordinates. Assign this for map-only markers outside a room collider.")]
        public MapRoom Room;
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
                if (!TryGetMapPosition(controller, areaCenter, out targetPosition))
                {
                    marker.SetActive(false);
                    if (labelObject) labelObject.SetActive(false);
                    return;
                }
            }
            else if (!TryGetMapPosition(controller, target.position, out targetPosition))
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
            // Objectives mark their destination directly; they do not track or
            // rotate around the player like a compass arrow.
            Vector3 mapPosition = targetPosition;
            // MapController's packed map coordinate is (X, Z) in a Vector2;
            // MapRenderer uses that second value directly as its local Z.
            marker.transform.localPosition = new Vector3(mapPosition.x, 7f, mapPosition.y);
            marker.transform.localRotation = Quaternion.identity;

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

        /// <summary>Immediately clears marker instances that do not belong to a selected map.</summary>
        public static void HideMarkersOutside(MapData selectedMap)
        {
            foreach (MapNavigationIndicator indicator in Resources.FindObjectsOfTypeAll<MapNavigationIndicator>())
            {
                if (!indicator || indicator.ResolveController()?.Data == selectedMap) continue;
                indicator.HideMarker();
            }
        }

        /// <summary>Used by the map-selection UI to mark the map containing the current objective.</summary>
        public static bool HasActiveObjectiveFor(MapData map)
        {
            if (!map) return false;
            foreach (MapNavigationIndicator indicator in FindObjectsByType<MapNavigationIndicator>(FindObjectsSortMode.None))
            {
                if (indicator && indicator.Type == MapNavigationIndicatorType.ObjectiveArrow && indicator.IsEnabled && indicator.ResolveController()?.Data == map)
                    return true;
            }
            return false;
        }

        private void HideMarker()
        {
            if (marker) marker.SetActive(false);
            if (labelObject) labelObject.SetActive(false);
        }

        private bool TryGetMapPosition(MapController controller, Vector3 worldPosition, out Vector3 mapPosition)
        {
            if (!controller) { mapPosition = Vector3.zero; return false; }
            if (Room)
            {
                Matrix4x4 roomTRS = controller.Data.GetTRS(Room.Offset, Room.Rotation, Room.Scale, Room.ZOrder);
                // These markers are authored as children of the MapRoom, just
                // like the room's shape points. Convert from that exact local
                // space before applying the room's map transform.
                Vector3 roomLocalPosition = Room.transform.InverseTransformPoint(worldPosition);
                // MapController stores a map point as X/Z (a Vector2). Keep
                // that convention here: Vector3.y in raw map space is only
                // the map elevation/Z-order and must never drive the marker.
                mapPosition = roomTRS.MultiplyPoint(roomLocalPosition).ToXZ();
                return true;
            }
            if (controller.TryGetWorldPositionInRoom(worldPosition, out Vector3 mappedPosition))
            {
                mapPosition = mappedPosition.ToXZ();
                return true;
            }
            mapPosition = controller.transform.InverseTransformPoint(worldPosition).ToXZ();
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
            if (Type == MapNavigationIndicatorType.BlockedPathCross)
            {
                // Line renderers are the same rendering primitive used by the
                // map contours, which guarantees the blocked marker remains
                // visible to the orthographic map camera.
                CreateCrossLine(marker.transform, new Vector3(-Size * .5f, .05f, -Size * .5f), new Vector3(Size * .5f, .05f, Size * .5f));
                CreateCrossLine(marker.transform, new Vector3(-Size * .5f, .05f, Size * .5f), new Vector3(Size * .5f, .05f, -Size * .5f));
            }

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

        private void CreateCrossLine(Transform parent, Vector3 from, Vector3 to)
        {
            GameObject lineObject = new GameObject("Blocked Cross Stroke");
            lineObject.layer = parent.gameObject.layer;
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = Mathf.Max(1f, Size * .16f);
            line.numCapVertices = 4;
            line.sharedMaterial = material;
        }

        private Mesh CreateMesh(MapNavigationIndicatorType type)
        {
            float half = Size * .5f;
            Mesh mesh = new Mesh { name = type == MapNavigationIndicatorType.ObjectiveArrow ? "Map objective arrow" : "Map blocked-path cross" };
            if (type == MapNavigationIndicatorType.ObjectiveArrow)
            {
                const int segments = 32;
                Vector3[] vertices = new Vector3[segments + 1];
                int[] triangles = new int[segments * 3];
                vertices[0] = Vector3.zero;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    vertices[i + 1] = new Vector3(Mathf.Cos(angle) * half, 0, Mathf.Sin(angle) * half);
                    int next = i == segments - 1 ? 1 : i + 2;
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = next;
                }
                mesh.vertices = vertices;
                mesh.triangles = triangles;
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
