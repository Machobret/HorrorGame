using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [Serializable]
    public sealed class RoadNode
    {
        public int id, firstArm;
        public Vector3 position;
        public Material junctionMaterial;
        public List<RoadCorner> corners = new List<RoadCorner>();
    }

    [Serializable]
    public sealed class RoadCorner
    {
        public int arm, nextArm;
        public bool sidewalk = true;
        public Material material;
        public RoadCurb curb = new RoadCurb();
    }

    [Serializable]
    public sealed class RoadSide
    {
        public RoadOverride sidewalk;
        public Material material;
        public RoadCurb curb = new RoadCurb();
    }

    [Serializable]
    public sealed class RoadEnd
    {
        public int node, port;
        public bool crosswalk, stopLine;
        public string symbol, crosswalkStyle;
    }

    [Serializable]
    public sealed class RoadSpan
    {
        public int id, run;
        public RoadEnd start, end;
        public RoadFamily family;
        public RoadSide left = new RoadSide(), right = new RoadSide();
        public RoadFamily Style(RoadFamily network) => family ? family : network ? network : RoadFamily.Default;
    }

    [Serializable]
    public sealed class RoadGraph
    {
        public int nextId = 1;
        public List<RoadNode> nodes = new List<RoadNode>();
        public List<RoadSpan> spans = new List<RoadSpan>();
        public RoadNode Node(int id) => nodes.Find(n => n.id == id);
        public RoadSpan Span(int id) => spans.Find(s => s.id == id);
        public RoadNode AddNode(Vector3 point)
        {
            var node = new RoadNode { id = nextId++, position = point };
            nodes.Add(node);
            return node;
        }
        public RoadEnd End(int node) => new RoadEnd { node = node, port = nextId++ };
        public RoadSpan Connect(int a, int b, int run, RoadFamily family)
        {
            var span = new RoadSpan { id = nextId++, run = run == 0 ? nextId++ : run,
                start = End(a), end = End(b), family = family };
            spans.Add(span);
            return span;
        }
    }

    public enum RoadPart { None, Point, Road, Approach, Corner, Sidewalk }

    public struct RoadHit
    {
        public RoadPart part;
        public int id, arm, next;
        public bool left;
        public RoadHit(RoadPart part, int id, int arm = 0, int next = 0, bool left = false)
        { this.part = part; this.id = id; this.arm = arm; this.next = next; this.left = left; }
    }

    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class RoadNetwork : MonoBehaviour
    {
        public RoadFamily family;
        [HideInInspector] public RoadGraph graph = new RoadGraph();
        [NonSerialized] public RoadHit[] faces = Array.Empty<RoadHit>();
        Mesh surface, collision;
        void OnEnable() => Rebuild();
#if UNITY_EDITOR
        bool dirty;
        void OnValidate() => dirty = true;
        void Update() { if (dirty) Rebuild(); }
#endif
        public void Rebuild()
        {
#if UNITY_EDITOR
            dirty = false;
#endif
            if (!surface) surface = NewMesh("Road surface");
            if (!collision) collision = NewMesh("Road collision");
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null;
            var geometry = RoadGeometry.Build(graph, family);
            geometry.Apply(surface, collision, out faces, out var materials);
            GetComponent<MeshFilter>().sharedMesh = surface;
            GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (faces.Length > 0) collider.sharedMesh = collision;
        }
        static Mesh NewMesh(string name) => new Mesh { name = name, hideFlags = HideFlags.DontSave,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        void OnDestroy() { Release(surface); Release(collision); }
        static void Release(Mesh mesh)
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
