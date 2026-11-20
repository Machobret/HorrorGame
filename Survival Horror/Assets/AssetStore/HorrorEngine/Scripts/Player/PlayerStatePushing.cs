using HorrorGame.Environment;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [DefaultExecutionOrder(500)]
    public class PlayerStatePushing : ActorState
    {
        private static readonly int k_PushingHash = Animator.StringToHash("Pushing");
        private static readonly int k_PushStrengthHash = Animator.StringToHash("PushStrength");
        private static readonly string k_EventStartPush = "StartPush";
        private const float GrabBlendTime = 0.25f;
        private const float ReleaseBlendTime = 0.16f;
        private const float AlignTimeout = 0.65f;

        [SerializeField] PushDetector m_PushDetector;
        [SerializeField] ActorState m_ExitState;
        [SerializeField] float m_InitialDelay = 0.5f;
        
        private Vector3 m_DirAxis;
        private float m_RotationAngle;
        private PlayerMovement m_Movement;
        private IPlayerInput m_Input;
        private int m_EnteredFrame;
        private float m_Time;
        private Pushable m_Pushable;
        private Pushable m_RequestedPushable;
        private Vector3 m_RequestedAxis;
        private CrateGrab m_Grab;
        private CrateGrab m_ReleasingGrab;
        private float m_ReleaseBlend;
        private bool m_GrabAligned;
        private bool m_Entered;
        private Rigidbody m_Rigidbody;
        private Rigidbody m_PushableRB;
        private Vector3 m_ExpectedPosition;
        private Vector3 m_PrevPushablePosition;
        private UnityAction<AnimationEvent> m_OnAnimatorPushEvent;

        // --------------------------------------------------------------------

        private bool CanContinuePushing => m_Entered && m_Pushable && m_Pushable.CanBePushed
            && (m_Grab == null || m_Grab.IsValid);

        protected override void Awake()
        {
            base.Awake();

            m_OnAnimatorPushEvent = OnAnimatorPushEvent;
            m_Movement = GetComponentInParent<PlayerMovement>();
            m_Input = GetComponentInParent<IPlayerInput>();
            m_Rigidbody = GetComponentInParent<Rigidbody>();
        }

        // --------------------------------------------------------------------

        public void StartCrateGrab(Pushable pushable)
        {
            if (Actor.StateController.CurrentState is not PlayerStateInteraction ||
                !pushable.TryGetPushAxis(Actor.transform, out var axis))
                return;

            m_RequestedPushable = pushable;
            m_RequestedAxis = axis;
            SetState(this);
        }

        public override void StateEnter(IActorState fromState)
        {
            ClearReleasedPose();

            bool grabbing = fromState is PlayerStateInteraction;
            m_Grab = null;
            m_GrabAligned = false;
            m_Entered = false;
            m_Pushable = grabbing ? m_RequestedPushable : m_PushDetector.Pushable;
            m_RequestedPushable = null;
            if (!m_Pushable || !m_Pushable.CanBePushed)
            {
                SetState(m_ExitState);
                return;
            }

            m_DirAxis = grabbing ? m_RequestedAxis : m_PushDetector.PushAxis;
            if (grabbing)
            {
                m_Grab = CrateGrab.TryBegin(m_Pushable, Actor, m_Movement, m_DirAxis);
                if (m_Grab == null)
                {
                    SetState(m_ExitState);
                    return;
                }
            }

            base.StateEnter(fromState);
            m_Entered = true;
            m_EnteredFrame = Time.frameCount;
            m_PushableRB = m_Pushable.GetComponent<Rigidbody>();
            m_PrevPushablePosition= m_PushableRB.position; 
            m_ExpectedPosition = m_PushableRB.position;

            m_RotationAngle = Vector3.Angle(m_DirAxis, Actor.transform.forward);

            m_Movement.Constrain = PlayerMovement.MovementConstrain.Rotation | PlayerMovement.MovementConstrain.MovementToAxis;
            m_Movement.LockedAxis = m_DirAxis;
            m_Movement.enabled = !grabbing;

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

            
            if (!CanContinuePushing)
            {
                SetState(m_ExitState);
                return;
            }

            if (m_Grab != null)
            {
                bool releaseRequested = Time.frameCount != m_EnteredFrame && m_Input.IsInteractingDown();
                if (releaseRequested || (!m_GrabAligned && m_Time >= AlignTimeout))
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
            if (!CanContinuePushing)
                return;

            if (m_Grab != null)
            {
                MoveGrabbedCrate();
                return;
            }

            m_Rigidbody.MoveRotation(Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(m_DirAxis), m_RotationAngle * (Time.deltaTime / m_InitialDelay)));

            if (m_Time > m_InitialDelay)
            {
                Actor.MainAnimator.SetBool(k_PushingHash, true);

                //Rigidbody could move in any direction due to collisions. So the movement is projected on the expected axis and the position forced there
                Vector3 dirToExpected = (m_ExpectedPosition - m_PrevPushablePosition).normalized;
                Vector3 toActual = m_PushableRB.position - m_PrevPushablePosition;
                Vector3 projectionOnAxis = dirToExpected * Vector3.Dot(toActual, dirToExpected.normalized);
                Vector3 position = m_PrevPushablePosition + projectionOnAxis;

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

            if (m_Grab != null)
            {
                m_Grab.Release();
                if (intoState is PlayerStateMotion)
                {
                    m_ReleasingGrab = m_Grab;
                    m_ReleaseBlend = Mathf.Clamp01(m_Time / GrabBlendTime);
                }
                else
                    m_Grab.FinishPose();
                m_Grab = null;
                m_Movement.StopCinematicMovement();
            }
            else if (m_Pushable)
                m_Pushable.StopPush();

            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);
        }

        // --------------------------------------------------------------------

        private void MoveGrabbedCrate()
        {
            float deltaTime = Time.fixedDeltaTime;
            if (!m_GrabAligned || m_Time < GrabBlendTime)
            {
                m_Rigidbody.MoveRotation(Quaternion.RotateTowards(m_Rigidbody.rotation,
                    Quaternion.LookRotation(m_DirAxis), 540f * deltaTime));
                m_GrabAligned = m_Grab.Align(deltaTime);
                return;
            }

            if (!m_Grab.Move(m_Input.GetPrimaryAxis(), deltaTime))
                SetState(m_ExitState);
        }

        private void LateUpdate()
        {
            if (m_Grab != null && m_Grab.IsValid)
                m_Grab.ApplyPose(Mathf.SmoothStep(0f, 1f, m_Time / GrabBlendTime), true);

            if (m_ReleasingGrab == null)
                return;

            m_ReleaseBlend = Mathf.MoveTowards(m_ReleaseBlend, 0f, Time.deltaTime / ReleaseBlendTime);
            if (m_ReleaseBlend > 0f && Actor.StateController.CurrentState is PlayerStateMotion)
                m_ReleasingGrab.ApplyPose(Mathf.SmoothStep(0f, 1f, m_ReleaseBlend), false);
            else
                ClearReleasedPose();
        }

        private void OnDisable() => ClearReleasedPose();

        private void ClearReleasedPose()
        {
            m_ReleasingGrab?.FinishPose();
            m_ReleasingGrab = null;
        }

        private void OnAnimatorPushEvent(AnimationEvent e)
        {
            if (m_Grab == null && e.stringParameter == k_EventStartPush && CanContinuePushing)
                m_Pushable.OnStartPushEvent();
        }

    }
}