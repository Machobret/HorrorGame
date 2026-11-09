using UnityEngine;

namespace EnvironmentAuthoring
{
    // Native storage/Undo and world placement. FloorPlan owns planar interpretation.
    [SelectionBase, DisallowMultipleComponent]
    public sealed partial class HousePlan : MonoBehaviour
    {
        [SerializeField] private FloorPlan data = new FloorPlan();
        public FloorPlan Data => data;

        // Authoring uses metres on an upright plane; tilt/scale invalidates fitting.
        public bool HasSupportedTransform => Vector3.Dot(transform.up, Vector3.up) > 0.99999f
            && (transform.lossyScale - Vector3.one).sqrMagnitude < 0.000001f;
        public Vector3 ToWorld(Vector2 position, float height)
            => transform.TransformPoint(new Vector3(position.x, height, position.y));

    }
}
