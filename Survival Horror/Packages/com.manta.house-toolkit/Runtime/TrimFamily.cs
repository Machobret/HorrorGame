using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "House Toolkit/Trim Family")]
    public sealed class TrimFamily : ScriptableObject
    {
        public bool omitTrim;
        [Min(.001f)] public float height = .12f;
        [Min(.001f)] public float depth = .018f;
        public Material material;
        [Tooltip("Optional profile mesh. X runs along the wall, Y is up.")]
        public Mesh profile;
    }
}
