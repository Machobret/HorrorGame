using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("Procedural Environment/Sidewalk")]
    public sealed class ProceduralSidewalk : ProceduralSurface
    {
        [HideInInspector] public RoadGraph paths = new RoadGraph();
        [Min(.1f)] public float width = 1.5f;
        public Material material;
        internal override RoadGraph Paths { get => paths; set => paths=value; }
        public override float PathWidth => Mathf.Max(.1f,width);
        public override Material Pavement => material;
    }
}
