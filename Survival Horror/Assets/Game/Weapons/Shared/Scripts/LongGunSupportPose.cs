using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    [DefaultExecutionOrder(285)]
    public sealed class LongGunSupportPose : MonoBehaviour
    {
        [Tooltip("Support wrist in this weapon's imported Visuals coordinate frame.")]
        public Vector3 ForeEndWrist;
        public Quaternion WristRotation = Quaternion.identity;
        public float GripSeatMetres = .005f;
        private Transform frame, character, upper, lower, hand;
        private ActorStateController states;
        private Vector3 originalPosition;
        private bool seated;
        private float weight;

        private void OnEnable()
        {
            var actor = GetComponentInParent<Actor>();
            var animator = GetComponentInParent<Animator>();
            var weapon = GetComponent<Weapon>();
            if (!actor || !animator || !animator.isHuman || !weapon || !weapon.Visuals) return;
            character = actor.transform; states = actor.StateController;
            frame = weapon.Visuals.transform;
            originalPosition = frame.localPosition;
            // Move the complete visuals/socket hierarchy toward the firing palm.
            // No per-frame weapon-root offsets compete with the rifle bolt solver.
            frame.localPosition += frame.parent.InverseTransformVector(-frame.up * GripSeatMetres);
            seated = true;
            upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            weight = 0;
        }
        private void OnDisable()
        {
            if (seated && frame) frame.localPosition = originalPosition;
            seated = false; weight = 0;
            frame = character = upper = lower = hand = null; states = null;
        }
        private void LateUpdate()
        {
            if (!frame || !states || !hand) return;
            var state = states.m_CurrentState;
            var reload = state as PlayerStateReload;
            bool holding = state is PlayerStateMotion || state is PlayerStateAiming || state is PlayerStateAttack;
            if (reload)
                weight = Mathf.Max(1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .12f, reload.ReloadProgress)), reload.AimReturnBlend);
            else if (holding) weight = Mathf.MoveTowards(weight, 1, Time.deltaTime * 10);
            else { weight = 0; return; }
            ReloadArmIK.Solve(upper, lower, hand, character, frame.TransformPoint(ForeEndWrist),
                frame.rotation * WristRotation, new Vector3(-.38f, 1.10f, .06f), weight);
        }
    }
}
