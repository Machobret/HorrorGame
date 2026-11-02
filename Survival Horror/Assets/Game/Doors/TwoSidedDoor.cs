using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Horror Engine/Doors/Two Sided Door")]
    public sealed class TwoSidedDoor : DoorBase
    {
        [Tooltip("The exit on side B. The inherited Exit Point is side A. Place both at a similar distance from the doorway, facing away from it.")]
        public Transform OtherExitPoint;

        private DoorLock m_Lock;
        private bool m_InUse;

        private void Awake()
        {
            m_Lock = GetComponent<DoorLock>();
            MessageBuffer<DoorTransitionEndMessage>.Subscribe(OnTransitionEnd);
        }

        private void OnDestroy()
        {
            MessageBuffer<DoorTransitionEndMessage>.Unsubscribe(OnTransitionEnd);
        }

        public override bool IsLocked()
        {
            return m_Lock && m_Lock.IsLocked;
        }

        public override void Use(IInteractor interactor)
        {
            if (!isActiveAndEnabled || m_InUse)
                return;

            if (!ExitPoint || !OtherExitPoint || ExitPoint == OtherExitPoint ||
                (ExitPoint.position - OtherExitPoint.position).sqrMagnitude < 0.0001f)
            {
                Debug.LogError("TwoSidedDoor requires two distinct exit points on opposite sides of the doorway.", this);
                return;
            }

            if (!(interactor is MonoBehaviour interactorComponent) || !interactorComponent)
            {
                Debug.LogError("TwoSidedDoor requires a MonoBehaviour interactor.", this);
                return;
            }

            // Use the actor root even when the interaction component is on a child.
            var actor = interactorComponent.GetComponentInParent<Actor>();
            Transform user = actor ? actor.transform : interactorComponent.transform;
            Transform destination = SelectDestination(user.position);

            // Guard lock and opened callbacks too: UnityEvents can call Use again.
            m_InUse = true;
            bool transitionStarted = false;
            try
            {
                if (IsLocked())
                {
                    bool open;
                    // Entering from side A uses the normal lock; side B uses its exit behavior.
                    if (destination == OtherExitPoint)
                        m_Lock.OnTryToUnlock(out open);
                    else
                        m_Lock.OnTryToUnlockFromExit(out open);

                    if (!open)
                    {
                        if (IsLocked())
                            OnLocked?.Invoke();
                        return;
                    }
                }

                var controller = DoorTransitionController.Instance;
                if (!controller || !controller.isActiveAndEnabled)
                {
                    Debug.LogError("TwoSidedDoor requires an active DoorTransitionController.", this);
                    return;
                }

                m_Interactor = user;
                OnOpened?.Invoke();
                // Capture the chosen exit before the fade or any transition messages move the player.
                controller.Trigger(this, user.gameObject, () => TransitionRoutine(destination));
                transitionStarted = true;
            }
            finally
            {
                if (!transitionStarted)
                    m_InUse = false;
            }
        }

        private Transform SelectDestination(Vector3 position)
        {
            float distanceA = (ExitPoint.position - position).sqrMagnitude;
            float distanceB = (OtherExitPoint.position - position).sqrMagnitude;
            // An exact tie consistently selects side B.
            return distanceA > distanceB ? ExitPoint : OtherExitPoint;
        }

        private IEnumerator TransitionRoutine(Transform destination)
        {
            if (destination && m_Interactor)
                TeleportInteractor(destination);

            yield return null;
        }

        private void OnTransitionEnd(DoorTransitionEndMessage message)
        {
            if (message.Door == this)
            {
                m_InUse = false;
                m_Interactor = null;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (OtherExitPoint)
            {
                Gizmos.DrawWireCube(OtherExitPoint.position, new Vector3(0.5f, 0f, 0.5f));
                GizmoUtils.DrawArrow(OtherExitPoint.position, OtherExitPoint.forward, 1f);
            }
        }
    }
}
