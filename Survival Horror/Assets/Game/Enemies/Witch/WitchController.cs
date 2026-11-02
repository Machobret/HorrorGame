using HorrorEngine;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Enemies
{
    [RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(Combatant))]
    public sealed class WitchController : AttackBase, IResetable, IDeactivateWithActor
    {
        public enum WitchState { Idle, Warning, Chase, Search, Windup, Lunge, Recover, Dead }
        [Header("Provocation")]
        [Min(0.1f)] public float WarningRadius = 3f;
        [Min(0.1f)] public float WarningDuration = 2.5f;
        [Min(1f)] public float SightDistance = 16f;
        [Min(0.05f)] public float SenseInterval = 0.15f;
        [Tooltip("Optional test target. Normally resolved from GameManager.Player.")]
        public Transform Target;
        [SerializeField] private LayerMask m_Blockers = 1;
        [Header("Pursuit")]
        [Min(0.1f)] public float ChaseSpeed = 3.2f;
        [Min(0.1f)] public float SearchSpeed = 1.8f;
        [Min(0.5f)] public float SearchRadius = 5f;
        [Header("Committed slash")]
        public bool EnableDamage = true;
        [Min(0.1f)] public float AttackRange = 1.7f;
        [Min(0.1f)] public float WindupDuration = 0.7f;
        [Min(0.1f)] public float LungeDuration = 0.3f;
        [Min(0f)] public float LungeDistance = 1.1f;
        [Min(0.1f)] public float RecoveryDuration = 1.1f;
        [Min(0.1f)] public float HitReach = 1.15f;
        [Range(10f, 180f)] public float HitArc = 85f;
        [Header("Prototype voice")]
        [Range(0f, 1f)] public float VoiceVolume = .25f;
        public AudioClip IdleVoice;
        public AudioClip WarningVoice;
        public AudioClip AttackVoice;

        public WitchState State { get; private set; }
        public bool IsProvoked { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        private Health m_Health;
        private NavMeshAgent m_Agent;
        private WitchVisual m_Visual;
        private Collider[] m_Colliders;
        private bool[] m_ColliderEnabled;
        private float m_Age, m_NextSense, m_NextSearch;
        private bool m_Visible, m_Hit, m_WarnedNav;
        private Vector3 m_LungeDirection;
        private AudioSource m_Voice;
        private bool CanNavigate => m_Agent && m_Agent.isActiveAndEnabled && m_Agent.isOnNavMesh;
        private bool Paused => PauseController.Exists && PauseController.Instance.IsPaused;

        protected override void Awake()
        {
            base.Awake();
            m_Health = GetComponent<Health>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_Visual = GetComponent<WitchVisual>();
            m_Voice = gameObject.AddComponent<AudioSource>();
            m_Voice.playOnAwake = false; m_Voice.spatialBlend = 1f;
            m_Voice.minDistance = 2f; m_Voice.maxDistance = 15f;
            if (!IdleVoice) IdleVoice = Resources.Load<AudioClip>("Witch/Witch_Idle");
            if (!WarningVoice) WarningVoice = Resources.Load<AudioClip>("Witch/Witch_Warning");
            if (!AttackVoice) AttackVoice = Resources.Load<AudioClip>("Witch/Witch_Attack");
            m_Colliders = GetComponentsInChildren<Collider>();
            m_ColliderEnabled = new bool[m_Colliders.Length];
            for (int i = 0; i < m_Colliders.Length; ++i) m_ColliderEnabled[i] = m_Colliders[i].enabled;
            m_Health.OnHealthDecreased.AddListener(OnDamaged);
            m_Health.OnDeath.AddListener(OnDeath);
            m_Health.OnLoadedDead.AddListener(OnLoadedDead);
        }

        private void Start() { Enter(m_Health.IsDead ? WitchState.Dead : WitchState.Idle); }

        private void Update()
        {
            if (Paused) { StopMoving(); m_Voice.Pause(); return; }
            m_Voice.UnPause();
            if (m_Health.IsDead) { if (State != WitchState.Dead) Enter(WitchState.Dead); return; }
            if (!Target && GameManager.Exists && GameManager.Instance.Player)
                Target = GameManager.Instance.Player.transform;
            if (!Target || !Target.gameObject.activeInHierarchy ||
                (Target.TryGetComponent<Health>(out var targetHealth) && targetHealth.IsDead) ||
                (Target.TryGetComponent<Actor>(out var actor) && actor.IsDisabled))
            {
                StopMoving();
                if (State == WitchState.Windup || State == WitchState.Lunge) Enter(WitchState.Recover);
                return;
            }
            if (!CanNavigate && !m_WarnedNav)
            {
                m_WarnedNav = true;
                Debug.LogWarning("Witch needs a baked NavMesh and placement on it to pursue the player.", this);
            }
            m_Age += Time.deltaTime;
            if (Time.time >= m_NextSense)
            {
                m_NextSense = Time.time + Mathf.Max(.05f, SenseInterval);
                m_Visible = CanSeeTarget();
                if (IsProvoked && m_Visible) LastKnownPosition = Target.position;
            }
            float distance = Vector3.Distance(transform.position, Target.position);
            switch (State)
            {
                case WitchState.Idle:
                    if (m_Visible && distance <= WarningRadius) Enter(WitchState.Warning);
                    break;
                case WitchState.Warning:
                    Face(Target.position, 90f);
                    if (!m_Visible || distance > WarningRadius + .4f) Enter(WitchState.Idle);
                    else if (m_Age >= WarningDuration) Provoke();
                    break;
                case WitchState.Chase:
                    if (m_Visible && distance <= AttackRange && HasClearLine(Target))
                    {
                        Face(Target.position, 10000f);
                        m_LungeDirection = transform.forward;
                        Enter(WitchState.Windup);
                    }
                    else
                    {
                        MoveTo(LastKnownPosition, ChaseSpeed);
                        if (!m_Visible && AtDestination()) Enter(WitchState.Search);
                    }
                    break;
                case WitchState.Search:
                    if (m_Visible) Enter(WitchState.Chase);
                    else if (CanNavigate && Time.time >= m_NextSearch)
                    {
                        m_NextSearch = Time.time + 3f;
                        Vector2 offset = Random.insideUnitCircle * SearchRadius;
                        if (NavMesh.SamplePosition(LastKnownPosition + new Vector3(offset.x, 0, offset.y), out var hit, 2f, m_Agent.areaMask))
                            MoveTo(hit.position, SearchSpeed);
                    }
                    else if (CanNavigate) m_Agent.isStopped = false;
                    break;
                case WitchState.Windup:
                    // Direction is locked from the first wind-up frame; normal sidestepping can evade it.
                    if (m_Age >= WindupDuration) Enter(WitchState.Lunge);
                    break;
                case WitchState.Lunge:
                    Lunge();
                    TryHit();
                    if (m_Age >= LungeDuration) Enter(WitchState.Recover);
                    break;
                case WitchState.Recover:
                    if (m_Age >= RecoveryDuration) Enter(WitchState.Chase);
                    break;
            }
        }

        public void Provoke()
        {
            if (m_Health.IsDead || IsProvoked) return;
            IsProvoked = true;
            if (!Target && GameManager.Exists && GameManager.Instance.Player) Target = GameManager.Instance.Player.transform;
            LastKnownPosition = Target ? Target.position : transform.position;
            Enter(WitchState.Chase);
        }

        private void OnDamaged(float value) { if (value > 0f) Provoke(); }
        private void OnDeath(Health health) { Enter(WitchState.Dead); }
        private void OnLoadedDead() { Enter(WitchState.Dead); }

        private void Enter(WitchState next)
        {
            State = next; m_Age = 0f;
            StopMoving();
            m_Voice.Stop();
            m_Voice.volume = VoiceVolume;
            m_Voice.loop = next == WitchState.Idle;
            m_Voice.clip = next == WitchState.Idle ? IdleVoice : next == WitchState.Warning ? WarningVoice :
                next == WitchState.Windup ? AttackVoice : null;
            if (m_Voice.clip) m_Voice.Play();
            if (next == WitchState.Search) m_NextSearch = 0f;
            if (next == WitchState.Lunge) m_Hit = false;
            if (next == WitchState.Dead)
            {
                for (int i = 0; i < m_Colliders.Length; ++i) if (m_Colliders[i]) m_Colliders[i].enabled = false;
            }
            if (m_Visual)
            {
                string clip = next == WitchState.Search ? "Chase" : next.ToString();
                if (next == WitchState.Dead) clip = "Death";
                float duration = next == WitchState.Warning ? WarningDuration : next == WitchState.Windup ? WindupDuration :
                    next == WitchState.Lunge ? LungeDuration : next == WitchState.Recover ? RecoveryDuration : 0f;
                m_Visual.Play(clip, duration);
            }
        }

        private bool CanSeeTarget()
        {
            return Target && (Target.position - transform.position).sqrMagnitude <= SightDistance * SightDistance && HasClearLine(Target);
        }

        private bool HasClearLine(Transform target)
        {
            Vector3 from = transform.position + Vector3.up * 1.5f;
            Vector3 delta = target.position + Vector3.up - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, m_Blockers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return false;
            return true;
        }

        private void Face(Vector3 position, float speed)
        {
            Vector3 delta = position - transform.position; delta.y = 0f;
            if (delta.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), speed * Time.deltaTime);
        }

        private void MoveTo(Vector3 position, float speed)
        {
            if (!CanNavigate) return;
            m_Agent.speed = speed; m_Agent.isStopped = false;
            // Throttle path requests while still letting NavMeshAgent follow its current path.
            if (!m_Agent.hasPath || (m_Agent.destination - position).sqrMagnitude > .25f)
                m_Agent.SetDestination(position);
        }

        private bool AtDestination()
        {
            return CanNavigate && !m_Agent.pathPending && (!m_Agent.hasPath || m_Agent.remainingDistance <= .6f);
        }

        private void StopMoving() { if (CanNavigate) m_Agent.isStopped = true; }

        private void Lunge()
        {
            if (!CanNavigate) return;
            float step = LungeDistance / Mathf.Max(.1f, LungeDuration) * Mathf.Min(Time.deltaTime, Mathf.Max(0f, LungeDuration - m_Age + Time.deltaTime));
            Vector3 from = transform.position;
            if (NavMesh.Raycast(from, from + m_LungeDirection * step, out var edge, m_Agent.areaMask))
                step = Mathf.Min(step, Mathf.Max(0f, edge.distance - .05f));
            foreach (var hit in Physics.SphereCastAll(from + Vector3.up, m_Agent.radius, m_LungeDirection, step, m_Blockers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) step = Mathf.Min(step, Mathf.Max(0f, hit.distance - .02f));
            m_Agent.Move(m_LungeDirection * step);
        }

        private void TryHit()
        {
            if (m_Hit || !EnableDamage || !m_Attack || !Target) return;
            Vector3 delta = Target.position - transform.position;
            if (Mathf.Abs(delta.y) > 1.5f) return;
            delta.y = 0f;
            if (delta.magnitude > HitReach || Vector3.Angle(m_LungeDirection, delta) > HitArc * .5f || !HasClearLine(Target)) return;
            Damageable best = null;
            foreach (var damageable in Target.GetComponentsInChildren<Damageable>())
                if (damageable.isActiveAndEnabled && m_Attack.GetImpact(damageable.Type) != null && (!best || damageable.Priority > best.Priority)) best = damageable;
            if (!best) return;
            m_Hit = true;
            Process(new AttackInfo { Attack = this, Damageable = best, ImpactDir = m_LungeDirection, ImpactPoint = Target.position + Vector3.up });
        }

        public void OnReset()
        {
            IsProvoked = false; m_Visible = false; m_NextSense = 0f; m_WarnedNav = false;
            for (int i = 0; i < m_Colliders.Length; ++i) if (m_Colliders[i]) m_Colliders[i].enabled = m_ColliderEnabled[i];
            Enter(WitchState.Idle);
        }

        private void OnDisable() { StopMoving(); if (m_Voice) m_Voice.Stop(); }
        private void OnDestroy()
        {
            if (!m_Health) return;
            m_Health.OnHealthDecreased.RemoveListener(OnDamaged);
            m_Health.OnDeath.RemoveListener(OnDeath);
            m_Health.OnLoadedDead.RemoveListener(OnLoadedDead);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, WarningRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, AttackRange);
        }
    }
}
