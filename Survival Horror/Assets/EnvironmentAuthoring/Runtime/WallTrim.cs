using System;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // One optional exterior attachment per logical wall. The source runs along +X,
    // with +Y up and +Z outward; its bounds provide the authored repeat width.
    [Serializable]
    public sealed class WallTrim
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField] private float height = .2f;
        [SerializeField] private float depth = .15f;
        [SerializeField] private float offset = -.2f;
        public Mesh Mesh { get => mesh; internal set => mesh = value; }
        public Material Material { get => material; internal set => material = value; }
        public float Height { get => height; internal set => height = value; }
        public float Depth { get => depth; internal set => depth = value; }
        public float Offset { get => offset; internal set => offset = value; }
        [SerializeField] private float patternOffset;
        public float PatternOffset => patternOffset;
        public WallTrim Copy() => (WallTrim)MemberwiseClone();
        internal void AdvancePattern(float distance) => patternOffset += distance;
        internal bool SameProfile(WallTrim other) => other != null && mesh == other.mesh && material == other.material
            && height == other.height && depth == other.depth && offset == other.offset;
    }
}
