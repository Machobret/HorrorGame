using HorrorEngine;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Enemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(ActorStateController))]
    public sealed class MutantPursuit : MonoBehaviour
    {
        private const float MaxHeadYaw = 60f;
        private static readonly int Speed = Animator.StringToHash("Speed");
        [SerializeField] private float runSpeed = 3.1f;
        [SerializeField] private float walkSpeed = .95f;
        [SerializeField] private float walkDistance = 2.6f;
        [SerializeField] private float walkTurnSpeed = 85f;

        private NavMeshAgent agent;
        private ActorStateController states;
        private EnemySensesController senses;
        private Animator animator;
        private Transform head;
        private Transform spine;
        private bool walking;
        private float runBlend;
        private float headYaw;
        private float idleBlend;
        private Quaternion animatedHead;
        private Quaternion animatedSpine;
        private bool posedHead;
        private bool posedSpine;
        private bool originalUpdateRotation;
        private EnemyStateGrab[] grabs;
        private float staggerTime;
        private bool posedStagger;
        private Vector3 animatedModelPosition;
        private Quaternion animatedModelRotation;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            states = GetComponent<ActorStateController>();
            senses = GetComponent<EnemySensesController>();
            grabs = GetComponentsInChildren<EnemyStateGrab>(true);
            originalUpdateRotation = agent.updateRotation;
            animator = GetComponent<Actor>().MainAnimator;
            if (!animator) return;
            if (animator.TryGetComponent<AnimatorSpeedSetter>(out var speedSetter))
                speedSetter.enabled = false;
            head = animator.transform.Find("Monster_Rig/Root/Hips/Spine/Chest/UpperChest/Neck/Head");
            spine = animator.transform.Find("Monster_Rig/Root/Hips/Spine");
        }

        private void Update()
        {
            RestorePose();
            if (!animator || !agent.isOnNavMesh) return;
            if (PauseController.Exists && PauseController.Instance.IsPaused) return;
            float groundSpeed = Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude;
            animator.SetFloat(Speed, groundSpeed, .06f, Time.deltaTime);

            if (!(states.CurrentState is EnemyStateAlerted)) return;
            foreach (var grab in grabs)
            {
                if (!grab.isActiveAndEnabled || !grab.CanEnter()) continue;
                states.SetState(grab);
                break;
            }
        }

        private void RestorePose()
        {
            if (posedStagger && animator)
                animator.transform.SetLocalPositionAndRotation(animatedModelPosition, animatedModelRotation);
            posedStagger = false;
            if (posedHead && head) head.localRotation = animatedHead;
            if (posedSpine && spine) spine.localRotation = animatedSpine;
            posedHead = posedSpine = false;
        }

        private void OnDisable()
        {
            RestorePose();
            if (animator) animator.speed = 1f;
            if (agent) agent.updateRotation = originalUpdateRotation;
        }

        private void LateUpdate()
        {
            if (!animator || (PauseController.Exists && PauseController.Instance.IsPaused)) return;
            bool pursuing = states.CurrentState is EnemyStateAlerted;
            agent.updateRotation = originalUpdateRotation && !(states.CurrentState is EnemyStateGrab);
            var player = GameManager.Exists ? GameManager.Instance.Player : null;
            if (pursuing && player)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                walking = distance < walkDistance + (walking ? .4f : 0f);
                agent.speed = walking ? walkSpeed : runSpeed;
                agent.angularSpeed = walking ? walkTurnSpeed : 360f;
            }

            var animationState = animator.GetCurrentAnimatorStateInfo(0);
            bool locomotion = animationState.IsName("EnemyMotion");
            bool resting = animationState.IsName("EnemyIdle") ||
                (locomotion && agent.velocity.sqrMagnitude < .01f);
            idleBlend = Mathf.MoveTowards(idleBlend, resting ? 1f : 0f, 4f * Time.deltaTime);
            float running = pursuing && !walking && locomotion && agent.velocity.sqrMagnitude > 1f ? 1f : 0f;
            runBlend = Mathf.MoveTowards(runBlend, running, Time.deltaTime * 5f);
            if (states.CurrentState is EnemyStateKnockback)
            {
                animator.speed = .8f;
                ApplyPushbackStagger();
            }
            else
            {
                staggerTime = 0f;
                animator.speed = locomotion ? Mathf.Lerp(1f, 1.8f, runBlend) : 1f;
            }
            if (spine && (locomotion || resting))
            {
                animatedSpine = spine.localRotation;
                posedSpine = true;
                // Hold a forward hunch with an uneven breathing rhythm and a slow side lean.
                float breath = (12f + Mathf.Sin(Time.time * 2.2f) * 4f +
                    Mathf.Sin(Time.time * 4.4f) * 1.25f) * idleBlend;
                float sway = (5f + Mathf.Sin(Time.time * 1.35f) * 4f) * idleBlend;
                spine.rotation = Quaternion.AngleAxis(sway, transform.forward) *
                    Quaternion.AngleAxis(runBlend * 18f + breath, transform.right) * spine.rotation;
            }

            if (!head) return;
            float desiredYaw = 0f;
            if (pursuing && player && senses.IsPlayerDetected)
            {
                Vector3 direction = player.transform.position - head.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .001f)
                    desiredYaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up), -MaxHeadYaw, MaxHeadYaw);
            }
            headYaw = Mathf.Lerp(headYaw, desiredYaw, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (pursuing)
            {
                animatedHead = head.localRotation;
                posedHead = true;
                head.rotation = Quaternion.AngleAxis(headYaw, Vector3.up) * head.rotation;
            }
        }

        private void ApplyPushbackStagger()
        {
            staggerTime += Time.deltaTime;
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.14f, .54f, staggerTime));
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.70f, 1.12f, staggerTime));
            float down = fall * (1f - rise);
            float stumble = Mathf.Sin(staggerTime * 24f) * (1f - fall);
            Transform model = animator.transform;
            animatedModelPosition = model.localPosition;
            animatedModelRotation = model.localRotation;
            posedStagger = true;

            // Roll around the torso so the body drops toward the floor as it topples.
            Vector3 pivot = transform.position + transform.up * .85f;
            Quaternion tumble = Quaternion.AngleAxis(72f * down + 9f * stumble, transform.forward) *
                Quaternion.AngleAxis(-18f * down, transform.right);
            model.SetPositionAndRotation(
                pivot + tumble * (model.position - pivot) - transform.up * (.48f * down),
                tumble * model.rotation);
        }
    }
}
