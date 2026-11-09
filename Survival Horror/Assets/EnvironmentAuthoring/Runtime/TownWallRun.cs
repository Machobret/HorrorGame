using UnityEngine;

namespace EnvironmentAuthoring
{
    [SelectionBase, DisallowMultipleComponent, AddComponentMenu("Environment/Town/Wall or Fence")]
    public sealed class TownWallRun : TownAuthoring
    {
        public TownModule Model;
        [Min(.01f)] public float Height = 2;
        public Material FrontMaterial, BackMaterial;
        public bool Posts = true;
        public Mesh PostMesh;
        [Tooltip("Optional uniform override; empty retains authored post materials.")]
        public Material PostMaterial;
        [HideInInspector] public Material[] PostMaterials = System.Array.Empty<Material>();
        [Min(0), Tooltip("Zero matches fence height; a positive value retains an authored post height.")]
        public float PostHeight;
        [Min(.01f)] public float PostWidth = .15f;
        public bool GenerateCollision = true;
    }
}
