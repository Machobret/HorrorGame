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

        private ActorStateController states;
        private Transform upper, lower, hand, firingHand, firingUpper, firingLower, character;
        private float weight;

        private void OnEnable()
        {
            var animator = GetComponentInParent<Animator>();
            var actor = GetComponentInParent<Actor>();
            if (!animator || !animator.isHuman || !actor) return;
            character = actor.transform;
            states = actor.StateController;
            upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            firingHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            firingUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            firingLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            weight = 0;
        }

        private void OnDisable()
        {
            states = null;
            upper = lower = hand = firingHand = firingUpper = firingLower = character = null;
            weight = 0;
        }

        private void LateUpdate()
        {
            if (!upper || !lower || !hand || !firingHand || !firingUpper || !firingLower || !states) return;
            bool aiming = states.CurrentState is PlayerStateAiming || states.CurrentState is PlayerStateAttack;
            var reload = states.CurrentState as PlayerStateReload;
            float returnWeight = reload ? reload.AimReturnBlend : 0;
            if (!aiming && returnWeight <= 0) { weight = 0; return; }

            weight = aiming ? Mathf.MoveTowards(weight, 1, Time.deltaTime * BlendSpeed) : returnWeight;
            float blend = Mathf.SmoothStep(0, 1, weight);
            if (blend <= 0) return;

            // Each elbow bends out and down in character space, even when the
            // source animation nearly straightens the arm at high or low aim.
            Vector3 firingPole = ElbowPole(firingUpper, 1);
            if (aiming) SoftenFiringArm(firingPole, blend);
            Vector3 support = firingHand.position + firingHand.rotation * SupportOffset;
            ReloadArmIK.Solve(upper, lower, hand, character, support,
                firingHand.rotation * Quaternion.Euler(SupportRotation), ElbowPole(upper, -1), blend);
        }

        private Vector3 ElbowPole(Transform shoulder, int side) =>
            character.InverseTransformPoint(shoulder.position) + new Vector3(side * .27f, -.37f, -.05f);

        private void SoftenFiringArm(Vector3 pole, float blend)
        {
            float first = Vector3.Distance(firingUpper.position, firingLower.position);
            float second = Vector3.Distance(firingLower.position, firingHand.position);
            Vector3 reach = firingHand.position - firingUpper.position;
            float maxReach = Mathf.Sqrt(first * first + second * second -
                2 * first * second * Mathf.Cos(151 * Mathf.Deg2Rad));
            float strength = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(maxReach - .035f, maxReach, reach.magnitude));
            Vector3 target = firingUpper.position + Vector3.ClampMagnitude(reach, maxReach);
            ReloadArmIK.Solve(firingUpper, firingLower, firingHand, character, target,
                firingHand.rotation, pole, blend * strength);
        }
    }
}
