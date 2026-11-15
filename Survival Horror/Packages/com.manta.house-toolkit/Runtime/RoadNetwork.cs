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

    [Serializable]
    public sealed class RoadGraph
    {
        public int nextId = 1;
        public List<RoadNode> nodes = new List<RoadNode>();
        public List<RoadSpan> spans = new List<RoadSpan>();
        public List<SidewalkRunStyle> runStyles = new List<SidewalkRunStyle>();
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
        internal override RoadMesh CreateGeometry() => WithPavement(RoadGeometry.Build(graph,family));
        internal override RoadGraph Paths { get => paths; set => paths=value; }
        internal override GroundPlan Areas { get => ground; set => ground=value; }
        public override float PathWidth => pavementWidth > 0 ? pavementWidth : (family ? family : RoadFamily.Default).sidewalkWidth;
        public override Material Pavement => pavementMaterial ? pavementMaterial : (family ? family : RoadFamily.Default).sidewalk;
        public override float PavementElevation => (family ? family : RoadFamily.Default).sidewalkHeight;
    }
}
