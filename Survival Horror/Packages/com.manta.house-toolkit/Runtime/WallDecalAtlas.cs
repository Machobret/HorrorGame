using System;
using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "Procedural Environment/Wall Decal Atlas")]
    public sealed class WallDecalAtlas : ScriptableObject
    {
        [Serializable]
        public sealed class Poster
        {
            public string id;
            public string label;
            public string[] themes;
            public Rect uv;
            public Vector2 size = Vector2.one;
        }

        public Material material;
        public Poster[] posters = Array.Empty<Poster>();

        internal Poster Find(string id) => Array.Find(posters, p => p != null && p.id == id);
    }
}
