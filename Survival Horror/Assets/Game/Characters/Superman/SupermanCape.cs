using UnityEngine;

namespace HorrorGame.Characters
{
    /// <summary>Clear cloth inertia when a player is spawned, re-enabled, or teleported.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Cloth))]
    public sealed class SupermanCape : MonoBehaviour
    {
        [SerializeField] Transform movementRoot;
        [SerializeField, Min(.1f)] float teleportDistance = 3f;
        Cloth simulation;
        Vector3 previousPosition;
        Quaternion previousRotation;

        public int ResetCount { get; private set; }

        public void SetMovementRoot(Transform root) => movementRoot = root;

        void OnEnable()
        {
            simulation = GetComponent<Cloth>();
            if (!movementRoot) movementRoot = transform.root;
            ResetSimulation();
        }

        void LateUpdate()
        {
            if ((movementRoot.position - previousPosition).sqrMagnitude > teleportDistance * teleportDistance ||
                Quaternion.Angle(previousRotation, movementRoot.rotation) > 120f)
                ResetSimulation();
            previousPosition = movementRoot.position;
            previousRotation = movementRoot.rotation;
        }

        [ContextMenu("Reset cape simulation")]
        public void ResetSimulation()
        {
            if (!simulation) simulation = GetComponent<Cloth>();
            bool wasEnabled = simulation.enabled;
            simulation.enabled = false;
            simulation.enabled = wasEnabled;
            simulation.ClearTransformMotion();
            previousPosition = movementRoot ? movementRoot.position : transform.position;
            previousRotation = movementRoot ? movementRoot.rotation : transform.rotation;
            ResetCount++;
        }
    }
}
