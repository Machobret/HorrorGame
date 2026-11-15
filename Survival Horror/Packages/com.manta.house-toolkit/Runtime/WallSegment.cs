using UnityEngine;

namespace HouseToolkit
{
    // The prototype segment prefab uses axis-aligned box parts in segment-local metres.
    // Origin is the lower left corner on the wall centreline.
    [System.Serializable]
    public struct SegmentPart
    {
        public Bounds bounds;
        public bool glass;
        public SegmentPart(Vector3 center, Vector3 size, bool glass = false)
        {
            bounds = new Bounds(center, size);
            this.glass = glass;
        }
    }

    public sealed class WallSegment : MonoBehaviour
    {
        [Tooltip("Imported mesh. Frame first, optional glass second. Measured in metres.")]
        public Mesh mesh;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("size")] Vector2 legacySize = new Vector2(2, 2.8f);
        public Vector2 size { get => mesh ? (Vector2)mesh.bounds.size : legacySize; set => legacySize = value; }
        public OpeningKind kind;
        public Rect aperture;
        public SegmentPart[] parts = System.Array.Empty<SegmentPart>();
    }
}
