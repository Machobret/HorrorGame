using UnityEngine;

namespace HorrorEngine
{
    public class InteractionColliderDetector : InteractionDetector
    {
        private Collider mCollider;
        private Transform m_Player;

        public override Interactive FocusedInteractive
        {
            get
            {
                if (!m_Player) return base.FocusedInteractive;
                Interactive best = null;
                int bestPriority = int.MinValue;
                float bestDistance = float.PositiveInfinity;
                bool preferPush = false;
                foreach (var interaction in m_Interactives)
                {
                    if (!interaction || !interaction.isActiveAndEnabled) continue;
                    int priority = interaction.FocusPriority;
                    if (priority < bestPriority) continue;
                    if (!interaction.IsReachableFrom(m_Player.position)) continue;
                    var crate = interaction.GetComponentInParent<Pushable>();
                    bool isPush = crate && crate.PushInteraction == interaction;
                    if (isPush && !crate.TryGetPushAxis(m_Player, out _)) continue;
                    Vector3 offset = interaction.transform.position - m_Player.position;
                    offset.y = 0;
                    float facing = Vector3.Dot(m_Player.forward, offset.normalized);
                    float distance = offset.sqrMagnitude + (1f - facing);
                    if (priority == bestPriority &&
                        ((!isPush && preferPush) || (isPush == preferPush && distance >= bestDistance))) continue;
                    best = interaction;
                    bestPriority = priority;
                    bestDistance = distance;
                    preferPush = isPush;
                }
                return best;
            }
        }

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            mCollider = GetComponent<Collider>();
            var actor = GetComponentInParent<Actor>();
            m_Player = actor ? actor.transform : transform;
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            mCollider.enabled = true;
        }

        // --------------------------------------------------------------------

        protected override void OnDisable()
        {
            base.OnDisable();
            mCollider.enabled = false;
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            Interactive interactive = other.GetComponent<Interactive>();
            if (interactive && interactive.isActiveAndEnabled)
            {
                Debug.Assert(!m_Interactives.Contains(interactive), "Trying to re-add an interactive to the InteractionDetector list", gameObject);

                AddInteractive(interactive);
            }
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            RemoveInteractive(other.gameObject);
        }

        private void LateUpdate() => RefreshFocusIfChanged();
    }
}
