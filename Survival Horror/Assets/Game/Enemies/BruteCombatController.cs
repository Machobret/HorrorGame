using HorrorEngine;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Enemies
{
    // Run before the state controller so an unavailable NavMesh cannot enter pursuit.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(ActorStateController), typeof(Health))]
    public sealed class BruteCombatController : MonoBehaviour, IResetable, IDeactivateWithActor
    {
        [Tooltip("Allow this Brute's melee attacks and acid mist to deal damage.")]
        public bool EnableDamage = false;
        [SerializeField] private LayerMask m_BlockerMask = 1;
        private ActorStateController m_States;
        private EnemyStateAttack m_Attack;
        private EnemyStateIdle m_Idle;
        private EnemySensesController m_Senses;
        private NavMeshAgent m_Agent;
        private Health m_Health;
        private float m_NextAttack;

        private void Awake()
        {
            m_States = GetComponent<ActorStateController>();
            m_Attack = GetComponentInChildren<EnemyStateAttack>(true);
            m_Idle = GetComponentInChildren<EnemyStateIdle>(true);
            m_Senses = GetComponent<EnemySensesController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_Health = GetComponent<Health>();
            m_States.OnStateChanged.AddListener(OnStateChanged);
        }

        private void OnStateChanged(IActorState from, IActorState to)
        {
            if (ReferenceEquals(from, m_Attack)) m_NextAttack = Time.time + m_Attack.Cooldown;
        }

        private void Update()
        {
            if (!m_States.isActiveAndEnabled || m_Health.IsDead || !m_Attack || !m_Idle ||
                (PauseController.Exists && PauseController.Instance.IsPaused)) return;

            bool canNavigate = m_Agent && m_Agent.isActiveAndEnabled && m_Agent.isOnNavMesh;
            if (!canNavigate && m_States.CurrentState is EnemyStateAlerted)
                m_States.SetState(m_Idle);

            // Leave queued transitions (including attack exits) alone until they complete.
            var state = m_States.CurrentState;
            if (state != m_States.m_CurrentState ||
                (!(state is EnemyStateIdle) && !(state is EnemyStateAlerted)) ||
                Time.time < m_NextAttack || !m_Senses || !m_Senses.IsPlayerDetected ||
                m_Senses.IsPlayerGrabbed || !GameManager.Exists) return;

            var player = GameManager.Instance.Player;
            if (!player || !player.gameObject.activeInHierarchy) return;
            var playerHealth = player.GetComponent<Health>();
            if (playerHealth && playerHealth.IsDead) return;
            Vector3 delta = player.transform.position - transform.position;
            if (delta.sqrMagnitude > m_Attack.AttackDistance * m_Attack.AttackDistance) return;

            Vector3 start = transform.position + Vector3.up;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude,
                         m_BlockerMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(player.transform)) return;
            }
            delta.y = 0;
            if (delta.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(delta);
            if (canNavigate) m_Agent.isStopped = true;
            m_States.SetState(m_Attack);
        }

        public void OnReset() { m_NextAttack = 0f; }

        private void OnDestroy()
        {
            if (m_States) m_States.OnStateChanged.RemoveListener(OnStateChanged);
        }
    }
}

