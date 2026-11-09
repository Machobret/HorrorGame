using UnityEngine;

namespace EnvironmentAuthoring
{
    [SelectionBase, DisallowMultipleComponent, AddComponentMenu("Environment/Town/Road")]
    public sealed class TownRoad : TownAuthoring
    {
        [Min(.1f)] public float Width = 4;
        [Min(.01f)] public float Thickness = .15f;
        [Min(.01f)] public float MetresPerRepeat = 2;
        public Material Material;
        public bool LeftSidewalk = true, RightSidewalk = true;
        [Min(.1f)] public float SidewalkWidth = 1.5f;
        [Min(.01f)] public float SidewalkHeight = .15f;
        public Material SidewalkMaterial;
        public bool Curbs;
        [Min(.01f), Tooltip("Height above the sidewalk surface, in metres.")] public float CurbHeight = .08f;
        [Tooltip("Empty inherits the sidewalk material.")] public Material CurbMaterial;
        public bool GenerateCollision = true;
    }
}
