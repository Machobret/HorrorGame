using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // A compact two-handed stock/handguard hold. Reload owns the support arm
    // exclusively; this solver must not pull it off the charging handle.
    [DefaultExecutionOrder(290), DisallowMultipleComponent]
    public sealed class MP5HandlingPose : MonoBehaviour
    {
        public Transform ModelFrame;
        public Vector3 ForeEndWrist = new Vector3(-.04f, .035f, .235f);
        public Quaternion WristRotation = Quaternion.Euler(0, 0, -85);
        public Vector3 ElbowHint = new Vector3(-.25f, 1.08f, .08f);
        public float BlendSeconds = .20f;
        public Vector3 AimedWrist = new Vector3(.14f, 1.43f, .27f);
        public Vector3 RightElbowHint = new Vector3(.30f, 1.20f, .08f);

        private Transform character, shoulder, upper, lower, hand;
        private ActorStateController states;
        private MP5ReloadVisual reload;
        private float weight, aimWeight;
        private Transform rightShoulder, rightUpper, rightLower, rightHand, attachmentParent;
        private Vector3 attachmentPosition;
        private Quaternion attachmentRotation;
        private bool displaced;

        private void OnEnable()
        {
            var actor = GetComponentInParent<Actor>();
            var animator = actor ? actor.MainAnimator : null;
            var weapon = GetComponent<Weapon>();
            if (!ModelFrame && weapon && weapon.Visuals) ModelFrame = weapon.Visuals.transform;
            if (!actor || !animator || !animator.isHuman || !ModelFrame) return;
            character = actor.transform;
            states = actor.StateController;
            shoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            reload = GetComponent<MP5ReloadVisual>();
            weight = 0;
        }

        public void RestoreAttachment()
        {
            if (!displaced) return;
            if (transform.parent == attachmentParent)
            {
                transform.localPosition = attachmentPosition;
                transform.localRotation = attachmentRotation;
            }
            displaced = false;
        }

        private void Update() { RestoreAttachment(); }

        private void LateUpdate()
        {
            if (!hand || !states || !ModelFrame) return;
            if (reload && reload.IsReloading) { weight = 0; aimWeight = 0; return; }
            var state = states.m_CurrentState;
            bool aimed = state is PlayerStateAiming || state is PlayerStateAttack;
            aimWeight = Mathf.MoveTowards(aimWeight, aimed ? 1 : 0, Time.deltaTime / Mathf.Max(.01f, BlendSeconds));
            if (rightHand && aimWeight > 0)
            {
                attachmentParent = transform.parent;
                attachmentPosition = transform.localPosition;
                attachmentRotation = transform.localRotation;
                Vector3 grip = transform.InverseTransformPoint(rightHand.position);
                Quaternion gripRotation = Quaternion.Inverse(transform.rotation) * rightHand.rotation;
                ReloadArmIK.Solve(rightUpper, rightLower, rightHand, character,
                    character.TransformPoint(AimedWrist), rightHand.rotation, RightElbowHint,
                    Mathf.SmoothStep(0, 1, aimWeight), rightShoulder, .35f);
                transform.rotation = rightHand.rotation * Quaternion.Inverse(gripRotation);
                transform.position = rightHand.position - transform.TransformVector(grip);
                displaced = true;
            }
            bool holding = state is PlayerStateMotion || state is PlayerStateAiming || state is PlayerStateAttack;
            weight = Mathf.MoveTowards(weight, holding ? 1 : 0, Time.deltaTime / Mathf.Max(.01f, BlendSeconds));
            if (weight <= 0) return;
            ReloadArmIK.Solve(upper, lower, hand, character, ModelFrame.TransformPoint(ForeEndWrist),
                ModelFrame.rotation * WristRotation, ElbowHint, Mathf.SmoothStep(0, 1, weight), shoulder, .3f);
        }

        private void OnDisable()
        {
            RestoreAttachment();
            weight = aimWeight = 0;
            character = shoulder = upper = lower = hand = null;
            states = null;
        }
    }
}
