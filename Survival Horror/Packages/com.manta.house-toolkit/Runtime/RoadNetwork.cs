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
        public float pathWidth;
        public Material pathMaterial;
        public RoadSide left = new RoadSide(), right = new RoadSide();
        public RoadFamily Style(RoadFamily network) => family ? family : network ? network : RoadFamily.Default;
    }

    [Serializable]
    public sealed class SidewalkRunStyle
    {
        public int run;
        public float width;
        public Material material;
    }

    public enum StreetlightSide { Left = 1, Right = 0, Both = 2, Alternating = 3 }

    [Serializable]
    public sealed class StreetlightRun
    {
        public int run;
        public bool enabled;
        public GameObject prefab;
        public StreetlightSide side = StreetlightSide.Right;
        public float period = 16, offset = 4;
    }

    [Serializable]
    public sealed class RoadGraph
    {
        public int nextId = 1;
        public List<RoadNode> nodes = new List<RoadNode>();
        public List<RoadSpan> spans = new List<RoadSpan>();
        public List<SidewalkRunStyle> runStyles = new List<SidewalkRunStyle>();
        public List<StreetlightRun> streetlights = new List<StreetlightRun>();
        internal StreetlightRun Lights(int run) => streetlights.Find(s => s.run == run);
        public SidewalkRunStyle RunStyle(int run) => runStyles.Find(s=>s.run==run);
        public float RunWidth(int run, float fallback) => RunStyle(run) is SidewalkRunStyle s && s.width>0 ? s.width : fallback;
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

    public enum RoadPart { None, Point, Road, Approach, Corner, Sidewalk, Path, PathPoint, Ground, Boundary, GroundPoint }

    public struct RoadHit
    {
        public RoadPart part;
        public int id, arm, next;
        public bool left;
        public RoadHit(RoadPart part, int id, int arm = 0, int next = 0, bool left = false)
        { this.part = part; this.id = id; this.arm = arm; this.next = next; this.left = left; }
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class RoadNetwork : ProceduralSurface
    {
        public RoadFamily family;
        [HideInInspector] public RoadGraph graph = new RoadGraph();
        [HideInInspector] public RoadGraph paths = new RoadGraph();
        [HideInInspector] public GroundPlan ground = new GroundPlan();
        public Material pavementMaterial;
        [Min(0)] public float pavementWidth;
        internal override RoadGraph Streets => graph;
        internal override RoadFamily StreetFamily => family;
        Transform lampRoot;
        readonly List<(GameObject source, Transform instance)> lamps = new List<(GameObject, Transform)>();
        int usedLamps;

        internal override RoadMesh CreateGeometry()
        {
            usedLamps = 0;
            var mesh = RoadGeometry.Build(graph, family, PlaceLamp);
            if (usedLamps == 0) ClearLamps();
            else while (lamps.Count > usedLamps)
            {
                int last = lamps.Count - 1;
                RemoveLampObject(lamps[last].instance);
                lamps.RemoveAt(last);
            }
            return WithPavement(mesh);
        }

        void PlaceLamp(GameObject source, Vector3 position, Quaternion rotation)
        {
            if (!lampRoot)
            {
                lampRoot = new GameObject("Streetlights").transform;
                lampRoot.SetParent(transform, false);
                lampRoot.gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            }
            if (usedLamps == lamps.Count) lamps.Add(default);
            var slot = lamps[usedLamps];
            if (!slot.instance || slot.source != source)
            {
                RemoveLampObject(slot.instance);
                slot = (source, Instantiate(source, lampRoot).transform);
                lamps[usedLamps] = slot;
            }
            slot.instance.SetLocalPositionAndRotation(position, rotation);
            usedLamps++;
        }

        void OnDisable() => ClearLamps();
        void ClearLamps()
        {
            RemoveLampObject(lampRoot);
            lampRoot = null;
            lamps.Clear();
        }
        static void RemoveLampObject(Transform item)
        {
            if (!item) return;
            item.gameObject.SetActive(false);
            if (Application.IsPlaying(item.gameObject)) Destroy(item.gameObject);
            else DestroyImmediate(item.gameObject);
        }
        internal override RoadGraph Paths { get => paths; set => paths=value; }
        internal override GroundPlan Areas { get => ground; set => ground=value; }
        public override float PathWidth => pavementWidth > 0 ? pavementWidth : (family ? family : RoadFamily.Default).sidewalkWidth;
        public override Material Pavement => pavementMaterial ? pavementMaterial : (family ? family : RoadFamily.Default).sidewalk;
        public override float PavementElevation => (family ? family : RoadFamily.Default).sidewalkHeight;
    }
}
