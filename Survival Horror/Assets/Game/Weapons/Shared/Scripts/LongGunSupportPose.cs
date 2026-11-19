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
        private Transform firingUpper, firingLower, firingHand;
        private ActorStateController states;
        private Vector3 carryPosition, carryVelocity;
        private Quaternion carryRotation;
        private bool trackingCarry;
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
            firingUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            firingLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            firingHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            trackingCarry = false;
            weight = 0;
        }
        private void OnDisable()
        {
            if (seated && frame) frame.localPosition = originalPosition;
            seated = trackingCarry = false; weight = 0;
            frame = character = upper = lower = hand = null;
            firingUpper = firingLower = firingHand = null;
            states = null;
        }
        private void LateUpdate()
        {
            if (!frame || !states || !hand) return;
            var state = states.m_CurrentState;
            if (state is PlayerStateMotion && firingUpper && firingLower && firingHand && Time.deltaTime > 0)
                SmoothCarry();
            else
                trackingCarry = false;

            var reload = state as PlayerStateReload;
            bool holding = state is PlayerStateMotion || state is PlayerStateAiming || state is PlayerStateAttack;
            if (reload)
                weight = Mathf.Max(1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .12f, reload.ReloadProgress)), reload.AimReturnBlend);
            else if (holding) weight = Mathf.MoveTowards(weight, 1, Time.deltaTime * 10);
            else { weight = 0; return; }
            ReloadArmIK.Solve(upper, lower, hand, character, frame.TransformPoint(ForeEndWrist),
                frame.rotation * WristRotation, new Vector3(-.38f, 1.10f, .06f), weight);
        }

        private void SmoothCarry()
        {
            // The weapon follows the firing hand. Ease its low-ready motion
            // before solving the support hand so both hands follow one grip.
            Vector3 position = character.InverseTransformPoint(firingHand.position);
            Quaternion rotation = Quaternion.Inverse(character.rotation) * firingHand.rotation;
            if (!trackingCarry)
            {
                trackingCarry = true;
                carryPosition = position;
                carryRotation = rotation;
                carryVelocity = Vector3.zero;
                return;
            }

            const float carryResponse = .09f;
            carryPosition = Vector3.SmoothDamp(carryPosition, position, ref carryVelocity, carryResponse);
            carryRotation = Quaternion.Slerp(carryRotation, rotation, 1 - Mathf.Exp(-Time.deltaTime / carryResponse));
            Vector3 pole = character.InverseTransformPoint(firingLower.position);
            ReloadArmIK.Solve(firingUpper, firingLower, firingHand, character,
                character.TransformPoint(carryPosition), character.rotation * carryRotation, pole, 1);
        }
    }
}
