using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("Procedural Environment/Sidewalk")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ProceduralSidewalk : ProceduralPath
    {
        [Min(.1f)] public float width = 1.5f;
        public Material material;
        public override float PathWidth => Mathf.Max(.1f,width);
        public override Material Pavement => material;
    }
}
