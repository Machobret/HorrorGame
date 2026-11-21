using HorrorEngine;
using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ControlButtonReach : MonoBehaviour, IInteractionReach
    {
        [SerializeField] private LayerMask blockingLayers = 1;
        private BoxCollider housing;

        private void Awake() => housing = GetComponent<BoxCollider>();

        public bool IsReachableFrom(Vector3 position)
        {
            if (transform.InverseTransformPoint(position).z <= 0f)
                return false;

            // Aim just clear of the housing's front face so the housing cannot obstruct its own button.
            var face = housing.center + Vector3.forward * (housing.size.z * .5f);
            Vector3 target = transform.TransformPoint(face) + transform.forward * .02f;
            return !Physics.Linecast(position + Vector3.up * 1.2f, target, blockingLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
