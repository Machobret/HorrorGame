using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HouseToolkit
{
    [AddComponentMenu("Procedural Environment/Marking")]
    public sealed class ProceduralMarking : ProceduralPath
    {
        [Min(.02f)] public float width = .1f;
        public Color color = new Color(.82f, .68f, .18f, 1);
        [Range(0, 2)] public float wear = .35f;
        [Range(.1f, 8)] public float wearScale = 1;
        public int seed = 1;
        public bool attachToSurface = true;
        public override bool Elevated => true;
        public override float PathWidth => Mathf.Max(.02f, width);
        public override Material Pavement => Paint;
        public const float SurfaceOffset = .003f;

        static Mesh quad;
        static Material paint;
        static Material Paint => paint ? paint : paint = Resources.Load<Material>("HouseToolkitMarking");
        Transform generated;
        readonly List<Vector3[]> patches = new List<Vector3[]>();
        internal IReadOnlyList<Vector3[]> Patches => patches;
        internal bool IsGeneratedPart(Transform part) => generated && part.IsChildOf(generated);
        static readonly int[] corners = { Shader.PropertyToID("_Corner0"), Shader.PropertyToID("_Corner1"),
            Shader.PropertyToID("_Corner2"), Shader.PropertyToID("_Corner3") };
        static readonly int tint = Shader.PropertyToID("_PaintTint"), weathering = Shader.PropertyToID("_PaintWear");

        // All spans (including mitered ends) use this same quad. The shader moves its four
        // corners using instanced data; bounds and picking use the very same positions.
        static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "Shared marking quad", hideFlags = HideFlags.HideAndDontSave };
                quad.vertices = new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.forward, Vector3.forward };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                quad.triangles = new[] { 0, 3, 2, 0, 2, 1 };
                quad.RecalculateNormals(); quad.RecalculateBounds();
                return quad;
            }
        }

        internal static Vector3 JointOffset(RoadGraph graph, RoadNode node, Vector3 direction, float halfWidth)
        {
            var flat = new Vector3(direction.x, 0, direction.z).normalized;
            var side = Vector3.Cross(Vector3.up, flat);
            var other = graph.spans.Where(s => s.start.node == node.id || s.end.node == node.id)
                .Select(s => graph.Node(s.start.node == node.id ? s.end.node : s.start.node).position - node.position)
                .FirstOrDefault(d => Vector3.Dot(d.normalized, direction.normalized) < .9999f);
            if (other.sqrMagnitude < 1e-8f) return side * halfWidth;
            var otherFlat = new Vector3(other.x, 0, other.z).normalized;
            var bisector = (side - Vector3.Cross(Vector3.up, otherFlat)).normalized;
            float denominator = Vector3.Dot(bisector, side);
            if (denominator < .0001f) return side * halfWidth;
            // Bound the miter rather than growing a spike on an acute turn. Both spans
            // share this boundary, so they taper into it without overlapping paint.
            var offset = bisector * Mathf.Min(halfWidth / denominator, halfWidth * 2);
            float advance = Mathf.Abs(Vector3.Dot(offset, flat));
            float retreat = Mathf.Abs(Vector3.Dot(offset, otherFlat));
            float safe = Mathf.Min(1, Mathf.Min(new Vector2(direction.x,direction.z).magnitude * .45f / Mathf.Max(.0001f,advance),
                new Vector2(other.x,other.z).magnitude * .45f / Mathf.Max(.0001f,retreat)));
            offset *= safe;
            float riseA = Vector3.Dot(offset, flat) * direction.y / Mathf.Max(.001f, new Vector2(direction.x, direction.z).magnitude);
            float riseB = Vector3.Dot(offset, otherFlat) * other.y / Mathf.Max(.001f, new Vector2(other.x, other.z).magnitude);
            offset.y = (riseA + riseB) * .5f;
            return offset;
        }

        protected override void BuildSurface()
        {
            ClearParts();
            if (!isActiveAndEnabled || !Paint || paths.spans.Count == 0) return;
            var root = new GameObject("Marking strips") { hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy };
            root.transform.SetParent(transform, false); generated = root.transform;
            var block = new MaterialPropertyBlock();
            var random = new System.Random(seed);
            var weather = new Vector4(Mathf.Clamp(wear,0,2), (float)random.NextDouble() * 128, (float)random.NextDouble() * 128,
                Mathf.Clamp(wearScale,.1f,8));
            foreach (var span in paths.spans)
            {
                var start = paths.Node(span.start.node); var end = paths.Node(span.end.node);
                if (start == null || end == null) continue;
                var delta = end.position - start.position;
                if (new Vector2(delta.x, delta.z).sqrMagnitude < 1e-6f) continue;
                var a = JointOffset(paths, start, delta, PathWidth * .5f);
                var b = -JointOffset(paths, end, -delta, PathWidth * .5f);
                var lift = Vector3.up * SurfaceOffset;
                var points = new[] { start.position - a + lift, start.position + a + lift,
                    end.position + b + lift, end.position - b + lift };
                patches.Add(points);
                var part = new GameObject("Paint " + span.id) { layer = gameObject.layer };
                part.transform.SetParent(generated, false);
                part.AddComponent<MeshFilter>().sharedMesh = Quad;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Paint;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                var bounds = new Bounds(points[0], Vector3.zero);
                for (int i = 0; i < 4; i++) { block.SetVector(corners[i], points[i]); bounds.Encapsulate(points[i]); }
                bounds.Expand(.02f); renderer.localBounds = bounds;
                block.SetColor(tint, color); block.SetVector(weathering, weather);
                renderer.SetPropertyBlock(block);
            }
        }

        void OnDisable() => ClearParts();
        void ClearParts()
        {
            patches.Clear();
            if (!generated) return;
            var old = generated.gameObject; generated = null; old.SetActive(false);
            if (Application.IsPlaying(old)) Destroy(old); else DestroyImmediate(old);
        }
    }
}
