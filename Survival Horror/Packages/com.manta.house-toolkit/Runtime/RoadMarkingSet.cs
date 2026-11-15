using System;
using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "House Toolkit/Road Marking Set")]
    public sealed class RoadMarkingSet : ScriptableObject
    {
        [Serializable]
        public class Marking
        {
            public string name;
            public bool crosswalk;
            [Tooltip("Normalized texture rectangle, measured from the bottom left.")]
            public Rect uv = new Rect(0, 0, 1, 1);
            [Tooltip("Width and length in metres. A crosswalk repeats across the street.")]
            public Vector2 size = Vector2.one;
            internal bool Valid => !string.IsNullOrEmpty(name) && size.x > .01f && size.y > .01f &&
                uv.width > 0 && uv.height > 0 && uv.xMin >= 0 && uv.yMin >= 0 && uv.xMax <= 1 && uv.yMax <= 1;
        }

        [Tooltip("Road Surface material with the transparent atlas as its Base Map.")]
        public Material atlas;
        public Marking[] markings = Array.Empty<Marking>();

        internal Marking Find(string key, bool crossing)
        {
            if (!atlas || string.IsNullOrEmpty(key) || markings == null) return null;
            return Array.Find(markings, m => m != null && m.Valid && m.name == key && m.crosswalk == crossing);
        }
    }
}
