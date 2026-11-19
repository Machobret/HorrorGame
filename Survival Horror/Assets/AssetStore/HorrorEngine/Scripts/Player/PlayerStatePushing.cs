using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PlayerStatePushing : ActorState
    {
        private static readonly int k_PushingHash = Animator.StringToHash("Pushing");
        private static readonly int k_PushStrengthHash = Animator.StringToHash("PushStrength");
        private static readonly string k_EventStartPush = "StartPush";
        private static readonly int k_PushingLoopHash = Animator.StringToHash("PushingLoop");
        private const float InteractionPushTimeout = 2f;

        [SerializeField] PushDetector m_PushDetector;
        [SerializeField] ActorState m_ExitState;
        [SerializeField] float m_InitialDelay = 0.5f;
        
        private Vector3 m_DirAxis;
        private float m_RotationAngle;
        private PlayerMovement m_Movement;
        private float m_Time;
        private Pushable m_Pushable;
        private Pushable m_RequestedPushable;
        private Vector3 m_RequestedAxis;
        private bool m_InteractionPush;
        private bool m_PushEventReceived;
        private bool m_Entered;
        private Rigidbody m_Rigidbody;
        private Rigidbody m_PushableRB;
        private Vector3 m_ExpectedPosition;
        private Vector3 m_PrevPushablePosition;
        private UnityAction<AnimationEvent> m_OnAnimatorPushEvent;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_OnAnimatorPushEvent = OnAnimatorPushEvent;
            m_Movement = GetComponentInParent<PlayerMovement>();
            m_Rigidbody = GetComponentInParent<Rigidbody>();
        }

        // --------------------------------------------------------------------

        public void StartInteractionPush(Pushable pushable)
        {
            if (Actor.StateController.CurrentState is not PlayerStateInteraction || !pushable.CanBePushed
                || pushable.GetComponent<Rigidbody>().isKinematic)
                return;

            Vector3 toward = pushable.transform.position - Actor.transform.position;
            if (Mathf.Abs(toward.y) > .45f)
                return;
            toward.y = 0;
            if (Vector3.Dot(toward.normalized, Actor.transform.forward) < .45f)
                return;

            Vector3 axis = pushable.GetPushingAxis(toward, out bool found);
            if (!found)
                return;

            m_RequestedPushable = pushable;
            m_RequestedAxis = axis;
            SetState(this);
        }

        public override void StateEnter(IActorState fromState)
        {
            m_InteractionPush = fromState is PlayerStateInteraction;
            m_PushEventReceived = false;
            m_Entered = false;
            m_Pushable = m_InteractionPush ? m_RequestedPushable : m_PushDetector.Pushable;
            m_RequestedPushable = null;
            if (!m_Pushable || !m_Pushable.CanBePushed)
            {
                SetState(m_ExitState);
                return;
            }

            base.StateEnter(fromState);
            m_Entered = true;
            m_PushableRB = m_Pushable.GetComponent<Rigidbody>();
            m_PrevPushablePosition= m_PushableRB.position; 
            m_ExpectedPosition = m_PushableRB.position;

            m_DirAxis = m_InteractionPush ? m_RequestedAxis : m_PushDetector.PushAxis;
            m_RotationAngle = Vector3.Angle(m_DirAxis, Actor.transform.forward);

            m_Movement.Constrain = PlayerMovement.MovementConstrain.Rotation | PlayerMovement.MovementConstrain.MovementToAxis;
            m_Movement.LockedAxis = m_DirAxis;
            m_Movement.enabled = !m_InteractionPush;

            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.AddListener(m_OnAnimatorPushEvent);
#if UNITY_6000_0_OR_NEWER
            Actor.MainAnimator.updateMode = AnimatorUpdateMode.Fixed;
#else
            Actor.MainAnimator.updateMode = AnimatorUpdateMode.AnimatePhysics;
#endif

            UIManager.Get<UIInputListener>().AddBlockingContext(this);

            m_Time = 0;
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            m_Time += Time.deltaTime;

            
            if (!m_Entered || !m_Pushable || !m_Pushable.CanBePushed)
            {
                SetState(m_ExitState);
                return;
            }

            if (m_InteractionPush)
            {
                var animation = Actor.MainAnimator.GetCurrentAnimatorStateInfo(0);
                if (m_Time >= InteractionPushTimeout || (animation.shortNameHash == k_PushingLoopHash && animation.normalizedTime >= 1f))
                    SetState(m_ExitState);
            }
            else if (!m_PushDetector.IsPushing)
            {
                SetState(m_ExitState);
            }

            
        }

        // --------------------------------------------------------------------

        public override void StateFixedUpdate()
        {
            base.StateFixedUpdate();
            if (!m_Entered || !m_Pushable || !m_Pushable.CanBePushed)
                return;

            m_Rigidbody.MoveRotation(Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(m_DirAxis), m_RotationAngle * (Time.deltaTime / m_InitialDelay)));

            if (m_Time > m_InitialDelay)
            {
                Actor.MainAnimator.SetBool(k_PushingHash, true);

                //Rigidbody could move in any direction due to collisions. So the movement is projected on the expected axis and the position forced there
                Vector3 dirToExpected = (m_ExpectedPosition - m_PrevPushablePosition).normalized;
                Vector3 toActual = m_PushableRB.position - m_PrevPushablePosition;
                Vector3 projectionOnAxis = dirToExpected * Vector3.Dot(toActual, dirToExpected.normalized);
                Vector3 position = m_PrevPushablePosition + projectionOnAxis;

                if (m_InteractionPush && m_PushEventReceived)
                {
                    float moved = Mathf.Max(0, Vector3.Dot(projectionOnAxis, m_DirAxis));
                    if (moved > 0)
                        m_Movement.MoveCinematic(m_DirAxis * moved);
                }

                m_PrevPushablePosition = position;
                float push = Actor.MainAnimator.GetFloat(k_PushStrengthHash);

                m_ExpectedPosition = position + (m_DirAxis * Time.deltaTime * push);

                m_Pushable.PushTo(m_ExpectedPosition);
                
            }
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            if (!m_Entered)
                return;
            m_Entered = false;

            base.StateExit(intoState);
            Actor.MainAnimator.updateMode = AnimatorUpdateMode.Normal;
            Actor.MainAnimator.SetBool(k_PushingHash, false);
            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.RemoveListener(m_OnAnimatorPushEvent);

            m_Movement.Constrain = PlayerMovement.MovementConstrain.None;
            m_Movement.enabled = false;

            if (m_InteractionPush)
                m_Movement.StopCinematicMovement();
            if (m_Pushable)
                m_Pushable.StopPush();

            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);
        }

        // --------------------------------------------------------------------

        void OnAnimatorPushEvent(AnimationEvent e)
        {
            if (e.stringParameter != k_EventStartPush || !m_Pushable || (m_InteractionPush && m_PushEventReceived))
                return;

            m_PushEventReceived = true;
            m_Pushable.OnStartPushEvent();
        }

    }
}