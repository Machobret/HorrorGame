using HorrorEngine;
using UnityEngine;
namespace HorrorGame.Weapons
{
    [DefaultExecutionOrder(250)]
    public sealed class HandgunSupportPose : MonoBehaviour
    {
        [Tooltip("Support wrist offset in the firing hand bone frame, in world metres.")]
        public Vector3 SupportOffset = new Vector3(.06f, .005f, 0);
        public float BlendSpeed = 10;
        public Vector3 SupportRotation = Vector3.zero;
        private Animator animator;
        private ActorStateController states;
        private Transform upper, lower, hand, firingHand, character;
        private float weight;
        private void OnEnable()
        {
            animator = GetComponentInParent<Animator>();
            var actor = GetComponentInParent<Actor>();
            if (!animator || !animator.isHuman || !actor) return;
            character = actor.transform; states = actor.GetComponentInChildren<ActorStateController>();
            upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            firingHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            weight = 0;
        }
        private void OnDisable()
        {
            animator = null; states = null;
            upper = lower = hand = firingHand = character = null;
            weight = 0;
        }
        private void LateUpdate()
        {
            if (!upper || !lower || !hand || !firingHand || !states) return;
            bool aiming = states.m_CurrentState is PlayerStateAiming || states.m_CurrentState is PlayerStateAttack;
            var reload = states.m_CurrentState as PlayerStateReload;
            float returnWeight = reload ? reload.AimReturnBlend : 0;
            // Start the aiming support pose under the fading reload IK so the
            // state transition does not abruptly introduce a new elbow solution.
            if (!aiming && returnWeight <= 0) { weight = 0; return; }
            weight = aiming ? Mathf.MoveTowards(weight, 1, Time.deltaTime * BlendSpeed) : returnWeight;
            if (weight <= 0) return;
            // Align the support palm beside the firing palm; neither hand is a platform under the gun.
            Vector3 target = Vector3.Lerp(hand.position, firingHand.position + firingHand.rotation * SupportOffset, weight);
            Quaternion wristRotation = Quaternion.Slerp(hand.rotation, firingHand.rotation * Quaternion.Euler(SupportRotation), weight);
            Vector3 shoulder = upper.position, elbow = lower.position;
            float first = Vector3.Distance(shoulder, elbow), second = Vector3.Distance(elbow, hand.position);
            Vector3 direction = target - shoulder;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(first - second) + .001f, first + second - .001f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(elbow - shoulder, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.Cross(direction, character.forward);
            float along = (first * first - second * second + distance * distance) / (2 * distance);
            Vector3 solvedElbow = shoulder + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, solvedElbow - shoulder) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            hand.rotation = wristRotation;
        }
    }
}
