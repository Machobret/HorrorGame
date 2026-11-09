using UnityEngine;

namespace EnvironmentAuthoring
{
    [SelectionBase, DisallowMultipleComponent, AddComponentMenu("Environment/Town/Sidewalk")]
    public sealed class TownSidewalk : TownAuthoring
    {
        [Min(.1f)] public float Width = 1.5f;
        [Min(.01f)] public float Thickness = .15f;
        public Material Material;
        [Min(.01f)] public float MetresPerRepeat = 2;
        public bool Curbs;
        [Min(.01f), Tooltip("Height above the sidewalk surface, in metres.")] public float CurbHeight = .08f;
        [Tooltip("Empty inherits the sidewalk material.")] public Material CurbMaterial;
        public bool GenerateCollision = true;
    }
}
