using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "Procedural Environment/Shelf Family")]
    public sealed class ShelfFamily : ScriptableObject
    {
        [Tooltip("Unscaled end frame, centred along X, with feet at Y = 0. Depth runs along Z.")]
        public Mesh frame;
        public Material steel;
        [Tooltip("Deck centred on X/Z with its upper surface at Y = 0. Length runs along X.")]
        public Mesh deck;
        public Material timber;
        [Tooltip("Decorative stock with a unit footprint and bottom-centre pivot.")]
        public GameObject stock;
        [Range(1, 12)] public int levels = 4;
        [Min(0)] public float firstDeckHeight = .18f;
        [Min(0)] public float topInset = .15f;
        public float Depth => frame ? frame.bounds.size.z : .6f;
        public float Height => frame ? frame.bounds.size.y : 2f;
    }

    public interface ISeededStock
    {
        void SetStockSeed(int seed);
    }
}
