using UnityEngine;

namespace HorrorEngine
{
    public class AnimatorLocalAxisSpeedSetter : AnimatorFloatSetter
    {
        [SerializeField] Vector3 m_LocalAxis;
        [SerializeField] Transform OptionalForwardReference;
        private Vector3 m_PrevPos;

        [Tooltip("If the displacement is greater than this number the speed is set to 0")]
        [SerializeField] float m_TeleportationThreshold = 1f;

        [Tooltip("Displacements below this speed are treated as idle to ignore physics jitter.")]
        [SerializeField] float m_IdleSpeedThreshold = 0.05f;

        // --------------------------------------------------------------------

        private PlayerMovement m_PlayerMovement;

        protected override void Awake()
        {
            base.Awake();
            m_PlayerMovement = GetComponentInParent<PlayerMovement>();
        }

        // --------------------------------------------------------------------

        protected override void OnEnable()
        {
            base.OnEnable();
            m_PrevPos = transform.position;
            Set(0, true);
        }

        // --------------------------------------------------------------------

        public override void OnReset()
        {
            base.OnReset();
            m_PrevPos = transform.position;
            Set(0, true);
        }

        // --------------------------------------------------------------------

        void FixedUpdate()
        {
            // Player characters can have root motion.  Measuring their transform here
            // feeds the walk animation back into its own blend parameter, preventing
            // the Animator from returning to idle.  Use the controller's intended
            // movement when it is available.
            Vector3 disp = m_PlayerMovement ? m_PlayerMovement.IntendedMovement : transform.position - m_PrevPos;
            Transform refTransform = OptionalForwardReference ? OptionalForwardReference : transform;
            
            Vector3 localDisp = refTransform.InverseTransformDirection(disp);
            localDisp.x *= m_LocalAxis.x;
            localDisp.y *= m_LocalAxis.y;
            localDisp.z *= m_LocalAxis.z;

            bool isTeleport = disp.magnitude > m_TeleportationThreshold;

            float sign = Mathf.Sign(Vector3.Dot(localDisp, m_LocalAxis));
            
            float speed = localDisp.magnitude / Time.deltaTime * sign;
            if (isTeleport || Mathf.Abs(speed) < m_IdleSpeedThreshold)
                Set(0f, true);
            else
                Set(speed);

            m_PrevPos = transform.position;
        }
    }
}
