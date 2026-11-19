using System;
using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "Procedural Environment/Scatter Preset")]
    public sealed class ScatterPreset : ScriptableObject
    {
        public enum Pieces { All, Flat, Solid }

        [Serializable]
        public sealed class Entry
        {
            public string label;
            [Min(0)] public float weight = 1;
            public bool solid;
            public Material material;
            public Mesh[] variants = Array.Empty<Mesh>();

            internal bool Available(Pieces pieces) => weight > 0 && float.IsFinite(weight) && material &&
                variants != null && variants.Length > 0 && (pieces == Pieces.All || solid == (pieces == Pieces.Solid));
        }

        public Entry[] entries = Array.Empty<Entry>();

        internal Entry Pick(System.Random random, Pieces pieces, Entry[] choices = null)
        {
            choices ??= entries;
            double total = 0;
            foreach (var entry in choices)
                if (entry != null && entry.Available(pieces)) total += entry.weight;
            double choice = random.NextDouble() * total;
            foreach (var entry in choices)
            {
                if (entry == null || !entry.Available(pieces)) continue;
                choice -= entry.weight;
                if (choice < 0) return entry;
            }
            return null;
        }
    }
}
