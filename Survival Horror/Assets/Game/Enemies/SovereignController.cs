using HorrorEngine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Enemies
{
    /// <summary>
    /// A scripted flying arrival enemy. Sovereign samples the NavMesh around the player before
    /// entering, then uses normal NavMesh pursuit after he has landed.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SovereignController : MonoBehaviour
    {
        private enum State { Dormant, FlyingIn, Descending, Chasing }

        [Header("Target")]
        [Tooltip("Normally resolved from GameManager.Player. Set this only for isolated tests.")]
        public Transform Target;
        [Header("Landing search")]
        [Min(1f)] public float MinimumLandingDistance = 1.75f;
        [Min(1f)] public float MaximumLandingDistance = 2.5f;
        [Min(1)] public int LandingSamples = 18;
        [Min(.1f)] public float NavMeshSampleRadius = 2f;
        [Min(0f), Tooltip("Extra space required between Sovereign's landing point and a NavMesh edge.")]
        public float LandingEdgePadding = .2f;
        [Header("Fly-in")]
        [Min(1f)] public float EntryDistance = 16f;
        [Min(1f)] public float EntryHeight = 6f;
        [Min(1f)] public float FlightHeight = 3.5f;
        [Min(.1f)] public float FlightSpeed = 14f;
        [Range(0f, 1f), Tooltip("A phasing arrival ignores roof and wall blockers while descending to the chosen NavMesh point.")]
        public float PhaseThroughGeometryChance = .45f;
        [SerializeField] private LayerMask m_Blockers = 1;
        [Header("Pursuit")]
        [Min(.1f)] public float ChaseSpeed = 1f;
        [Min(.1f)] public float StoppingDistance = 1.5f;
        [Min(.05f)] public float RepathInterval = .15f;
        [Min(.1f), Tooltip("How far from the player Sovereign may search for a valid chase destination.")]
        public float TargetNavMeshSampleRadius = 4f;

        public bool IsActive => m_State != State.Dormant;
        public bool IsPhasingArrival => m_PhaseArrival;
        public Vector3 LandingPosition => m_LandingPosition;

        private NavMeshAgent m_Agent;
        private CapsuleCollider m_Capsule;
        private WitchVisual m_Visual;
        private State m_State;
        private Vector3 m_LandingPosition;
        private Vector3 m_FlightTarget;
        private bool m_PhaseArrival;
        private float m_NextRepath;
        private float m_VisibilityWait;
        private readonly HashSet<Collider> m_BrokenSurfaces = new HashSet<Collider>();

        private void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            m_Capsule = GetComponent<CapsuleCollider>();
            if (m_Capsule) { m_Capsule.isTrigger = false; m_Capsule.enabled = false; }
            var body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            m_Visual = GetComponent<WitchVisual>();
            m_Agent.enabled = false;
        }

        private void Update()
        {
            if (PauseController.Exists && PauseController.Instance.IsPaused)
            {
                if (m_Agent.enabled && m_Agent.isOnNavMesh) m_Agent.isStopped = true;
                return;
            }
            if (!Target && GameManager.Exists && GameManager.Instance.Player)
                Target = GameManager.Instance.Player.transform;

            if (m_State == State.FlyingIn) FlyIn();
            else if (m_State == State.Descending) Descend();
            else if (m_State == State.Chasing) Chase();
        }

        /// <summary>Safe to call from a UnityEvent, a script, or SovereignSpawnTrigger.</summary>
        public bool Trigger(Transform target = null)
        {
            if (target) Target = target;
            if (!Target && GameManager.Exists && GameManager.Instance.Player)
                Target = GameManager.Instance.Player.transform;
            MatchPlayerFootprint();
            m_PhaseArrival = Random.value < PhaseThroughGeometryChance;
            if (!Target || !TryFindLandingPosition(Target.position, out m_LandingPosition))
            {
                Debug.LogWarning("Sovereign needs a clear, camera-visible landing point on the player's reachable NavMesh. No suitable point was found.", this);
                return false;
            }

            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector2.right;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.y);
            // Normal arrivals select an exposed surface and descend into it. Phasing arrivals
            // deliberately enter from the side, allowing the straight route to cut a wall or roof.
            Vector3 entry = m_PhaseArrival
                ? m_LandingPosition + horizontal * EntryDistance + Vector3.up * EntryHeight
                : m_LandingPosition + Vector3.up * EntryHeight;
            transform.SetPositionAndRotation(entry, Quaternion.LookRotation(-horizontal));
            m_Agent.enabled = false;
            if (m_Capsule) m_Capsule.enabled = false;
            m_BrokenSurfaces.Clear();
            m_VisibilityWait = 0f;
            m_FlightTarget = m_LandingPosition + Vector3.up * FlightHeight;
            m_State = State.FlyingIn;
            m_Visual?.Play("Chase");
            return true;
        }

        private bool TryFindLandingPosition(Vector3 playerPosition, out Vector3 landing)
        {
            landing = default;
            if (!NavMesh.SamplePosition(playerPosition, out var playerHit, NavMeshSampleRadius, NavMesh.AllAreas)) return false;

            float min = Mathf.Min(MinimumLandingDistance, MaximumLandingDistance);
            float max = Mathf.Max(MinimumLandingDistance, MaximumLandingDistance);
            for (int i = 0; i < Mathf.Max(64, LandingSamples); ++i)
            {
                Vector2 circle = Random.insideUnitCircle.normalized;
                if (circle.sqrMagnitude < .01f) circle = Vector2.right;
                Vector3 candidate = playerHit.position + new Vector3(circle.x, 0f, circle.y) * Random.Range(min, max);
                if (!NavMesh.SamplePosition(candidate, out var hit, NavMeshSampleRadius, NavMesh.AllAreas)) continue;
                if (!VisibleLanding(hit.position)) continue;
                if (Vector3.Distance(hit.position, playerPosition) < Mathf.Max(StoppingDistance, min)) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * (m_Agent.radius + .05f),
                    hit.position + Vector3.up * (m_Agent.height - m_Agent.radius), m_Agent.radius,
                    m_Blockers, QueryTriggerInteraction.Ignore)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(playerHit.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                if (NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas) &&
                    edge.distance < m_Agent.radius + LandingEdgePadding)
                    continue;
                if (!m_PhaseArrival && Physics.Raycast(hit.position + Vector3.up * .1f, Vector3.up,
                        EntryHeight + .2f, m_Blockers, QueryTriggerInteraction.Ignore))
                    continue;
                landing = hit.position;
                return true;
            }
            return false;
        }

        private void FlyIn()
        {
            MoveFlying(m_FlightTarget);
            if ((transform.position - m_FlightTarget).sqrMagnitude > .01f) return;

            // Commit to descent once; never steer back to the airborne waypoint.
            m_State = State.Descending;
        }

        private void Descend()
        {
            // A fixed-camera transition can happen during arrival. Retarget before touching down.
            if (!VisibleLanding(m_LandingPosition))
            {
                if (!Target || !TryFindLandingPosition(Target.position, out m_LandingPosition))
                {
                    m_VisibilityWait += Time.deltaTime;
                    if (m_VisibilityWait > 5f)
                    {
                        Debug.LogWarning("Sovereign arrival cancelled: camera has no visible landing space.", this);
                        Destroy(gameObject);
                    }
                    return;
                }
            }
            MoveFlying(m_LandingPosition);
            if ((transform.position - m_LandingPosition).sqrMagnitude > .01f) return;

            // Finish on the selected walkable surface before starting corner-based pursuit.
            if (m_Agent.enabled) m_Agent.enabled = false;
            transform.position = m_LandingPosition;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (m_Capsule) m_Capsule.enabled = true;
            PlaceAgentOnNavMesh(m_LandingPosition);
            SovereignDebris.Burst(m_LandingPosition + Vector3.up * .05f, Vector3.up);
            m_State = State.Chasing;
        }

        private void MoveFlying(Vector3 destination)
        {
            Vector3 delta = destination - transform.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (horizontal.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(horizontal), 540f * Time.deltaTime);
            Vector3 next = Vector3.MoveTowards(transform.position, destination, FlightSpeed * Time.deltaTime);
            Vector3 travel = next - transform.position;
            if (travel.sqrMagnitude > .000001f)
            {
                // Sweep the body, including reverse hits for exits from thick walls.
                Vector3 center = transform.position + Vector3.up * (m_Agent.height * .5f);
                EmitBreaks(Physics.SphereCastAll(center, m_Agent.radius, travel.normalized,
                    travel.magnitude, m_Blockers, QueryTriggerInteraction.Ignore));
                EmitBreaks(Physics.SphereCastAll(center + travel, m_Agent.radius, -travel.normalized,
                    travel.magnitude, m_Blockers, QueryTriggerInteraction.Ignore));
            }
            transform.position = next;
        }

        private void Chase()
        {
            if (!Target || !m_Agent.enabled || !m_Agent.isOnNavMesh) return;
            m_Agent.speed = ChaseSpeed;
            m_Agent.acceleration = 4f;
            m_Agent.stoppingDistance = Mathf.Max(StoppingDistance, m_Agent.radius * 2f + .2f);
            m_Agent.isStopped = Vector3.Distance(transform.position, Target.position) <= m_Agent.stoppingDistance;
            if (Time.time < m_NextRepath) return;
            m_NextRepath = Time.time + RepathInterval;
            if (NavMesh.SamplePosition(Target.position, out var hit, TargetNavMeshSampleRadius, m_Agent.areaMask))
                m_Agent.SetDestination(hit.position);
        }

        private bool VisibleLanding(Vector3 point)
        {
            var camera = Camera.main; // Cinemachine's rendered output, including blends.
            if (!camera || !camera.isActiveAndEnabled) return false;
            foreach (float height in new[] { .15f, m_Agent.height * .5f, m_Agent.height })
            {
                Vector3 sample = point + Vector3.up * height;
                Vector3 viewport = camera.WorldToViewportPoint(sample);
                if (viewport.z <= camera.nearClipPlane || viewport.x < .08f || viewport.x > .92f ||
                    viewport.y < .08f || viewport.y > .92f) return false;
                foreach (var hit in Physics.RaycastAll(camera.transform.position, (sample - camera.transform.position).normalized,
                    Vector3.Distance(sample, camera.transform.position), m_Blockers, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform) && (!Target || !hit.transform.IsChildOf(Target))) return false;
            }
            return true;
        }

        private void EmitBreaks(RaycastHit[] hits)
        {
            foreach (var hit in hits)
            {
                if (!hit.collider || hit.transform.IsChildOf(transform) ||
                    (Target && hit.transform.IsChildOf(Target)) || hit.point == Vector3.zero ||
                    !m_BrokenSurfaces.Add(hit.collider)) continue;
                SovereignDebris.Burst(hit.point, hit.normal);
            }
        }


        private bool PlaceAgentOnNavMesh(Vector3 position)
        {
            if (!m_Agent.enabled) m_Agent.enabled = true;
            if (!NavMesh.SamplePosition(position, out var hit, NavMeshSampleRadius + m_Agent.radius, m_Agent.areaMask))
                return false;
            if (!m_Agent.isOnNavMesh) return m_Agent.Warp(hit.position);
            return m_Agent.Warp(hit.position);
        }

        private void MatchPlayerFootprint()
        {
            if (!Target) return;
            var controller = Target.GetComponentInChildren<CharacterController>();
            if (controller)
            {
                ApplyFootprint(controller.radius, controller.height);
                return;
            }
            var capsule = Target.GetComponentInChildren<CapsuleCollider>();
            if (capsule) ApplyFootprint(capsule.radius, capsule.height);
        }

        private void ApplyFootprint(float radius, float height)
        {
            radius = Mathf.Max(.05f, radius);
            height = Mathf.Max(radius * 2f, height);
            m_Agent.radius = radius;
            m_Agent.height = height;
            if (!m_Capsule) return;
            m_Capsule.radius = radius;
            m_Capsule.height = height;
            m_Capsule.center = Vector3.up * (height * .5f);
            // Solid once grounded; flight temporarily disables the capsule for phasing.
            m_Capsule.isTrigger = false;
        }

        private void OnDisable()
        {
            if (m_Agent && m_Agent.isActiveAndEnabled) m_Agent.isStopped = true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Target) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Target.position, MinimumLandingDistance);
            Gizmos.DrawWireSphere(Target.position, MaximumLandingDistance);
            if (m_State != State.Dormant) { Gizmos.color = Color.magenta; Gizmos.DrawSphere(m_LandingPosition, .2f); }
        }
    }
}
