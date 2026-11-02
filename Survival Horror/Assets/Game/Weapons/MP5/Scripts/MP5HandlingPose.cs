using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    [DefaultExecutionOrder(290), DisallowMultipleComponent]
    public sealed class MP5HandlingPose : MonoBehaviour
    {
        public Transform ModelFrame;
        public Vector3 ForeEndWrist = new Vector3(-.055f, .005f, .175f);
        public Quaternion WristRotation = Quaternion.Euler(0, 0, -85);
        public Vector3 ElbowHint = new Vector3(-.27f, 1.08f, .12f);
        public float BlendSeconds = .20f;
        public Vector3 LoweredWrist = new Vector3(.18f, 1.38f, .22f);
        public Vector3 AimedWrist = new Vector3(.18f, 1.25f, .325f);
        public Vector3 RightElbowHint = new Vector3(.35f, 1.10f, .10f);

        private Transform character, shoulder, upper, lower, hand;
        private Transform rightShoulder, rightUpper, rightLower, rightHand, attachmentParent;
        private Animator animator;
        private ActorStateController states;
        private MP5ReloadVisual reload;
        private float weight, aimWeight, lastLoaded = -1, shotTime = -100;
        private Weapon equippedWeapon;
        private Vector3 attachmentPosition;
        private Quaternion attachmentRotation;
        private bool displaced;
        private Transform hips, leftThigh, leftShin, leftFoot, rightThigh, rightShin, rightFoot;
        private bool wasAiming, firingStance;
        private float aimedHipHeight, stanceOffset;

        // The mobile attack layer only animates the upper body. Keep the height
        // of the preceding aim pose without overriding locomotion's foot targets.
        private void PreserveFiringStance(bool aiming, bool attacking)
        {
            if (!hips || !leftFoot || !rightFoot) return;
            float nativeHeight = character.InverseTransformPoint(hips.position).y;
            if (attacking && wasAiming) firingStance = true;
            if (aiming && !firingStance) aimedHipHeight = nativeHeight;
            if (firingStance && (aiming || attacking))
            {
                stanceOffset = Mathf.Clamp(aimedHipHeight - nativeHeight, -.35f, .35f);
                if (aiming && Mathf.Abs(stanceOffset) < .005f)
                { firingStance = false; stanceOffset = 0; }
            }
            else
            {
                firingStance = false;
                stanceOffset = Mathf.MoveTowards(stanceOffset, 0, Time.deltaTime * 2.5f);
            }
            wasAiming = aiming;
            if (Mathf.Abs(stanceOffset) < .0001f) return;
            Vector3 leftTarget = leftFoot.position, rightTarget = rightFoot.position;
            Quaternion leftRotation = leftFoot.rotation, rightRotation = rightFoot.rotation;
            hips.position += character.TransformVector(Vector3.up * stanceOffset);
            ReloadArmIK.Solve(leftThigh, leftShin, leftFoot, character, leftTarget,
                leftRotation, new Vector3(-.2f, .55f, .45f), 1);
            ReloadArmIK.Solve(rightThigh, rightShin, rightFoot, character, rightTarget,
                rightRotation, new Vector3(.2f, .55f, .45f), 1);
        }

        private void OnEnable()
        {
            var actor = GetComponentInParent<Actor>();
            animator = actor ? actor.MainAnimator : null;
            var weapon = GetComponent<Weapon>(); equippedWeapon = weapon;
            if (!ModelFrame && weapon && weapon.Visuals) ModelFrame = weapon.Visuals.transform;
            if (!actor || !animator || !animator.isHuman || !ModelFrame) return;
            character = actor.transform; states = actor.StateController;
            shoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            wasAiming = firingStance = false; stanceOffset = 0;
            reload = GetComponent<MP5ReloadVisual>(); weight = aimWeight = 0;
        }

        public Vector3 HoldingWrist(float aiming) => character.TransformPoint(Vector3.Lerp(LoweredWrist, AimedWrist, aiming));

        public Quaternion HoldingRotation(float aiming)
        {
            // Anchor both arms to a forward hold, independent of locomotion's arm swing.
            float vertical = animator ? animator.GetFloat(PlayerStateAiming.k_AimVerticalityHash) : 0;
            Quaternion frame = character.rotation * Quaternion.Euler(Mathf.Lerp(16, -vertical * 35, aiming) - 1.2f * Mathf.Exp(-(Time.time - shotTime) * 60), 0, 0);
            Quaternion rootToFrame = Quaternion.Inverse(transform.rotation) * ModelFrame.rotation;
            return frame * Quaternion.Inverse(rootToFrame);
        }
        public void SetReturnPose(bool aimed) { weight = 1; aimWeight = aimed ? 1 : 0; }

        public void RestoreAttachment()
        {
            if (!displaced) return;
            if (transform.parent == attachmentParent)
            { transform.localPosition = attachmentPosition; transform.localRotation = attachmentRotation; }
            displaced = false;
        }
        private void Update()
        {
            RestoreAttachment();
            if (!GameManager.Exists || !equippedWeapon) return;
            var entry = GameManager.Instance.Inventory.GetEquippedWeapon();
            if (entry == null || entry.Item != equippedWeapon.WeaponData) { lastLoaded = -1; return; }
            if (lastLoaded >= 0 && entry.SecondaryCount < lastLoaded) shotTime = Time.time;
            lastLoaded = entry.SecondaryCount;
        }

        private void LateUpdate()
        {
            if (!hand || !rightHand || !states || !ModelFrame) return;
            if (reload && reload.IsReloading)
            { wasAiming = firingStance = false; stanceOffset = 0; return; }
            var state = states.m_CurrentState;
            if (state is PlayerStateMotion || state is PlayerStateAiming || state is PlayerStateAttack)
                PreserveFiringStance(state is PlayerStateAiming, state is PlayerStateAttack);
            else { wasAiming = firingStance = false; stanceOffset = 0; }
            bool holding = state is PlayerStateMotion || state is PlayerStateAiming || state is PlayerStateAttack;
            bool aimed = state is PlayerStateAiming || state is PlayerStateAttack;
            float speed = Time.deltaTime / Mathf.Max(.01f, BlendSeconds);
            weight = Mathf.MoveTowards(weight, holding ? 1 : 0, speed);
            aimWeight = Mathf.MoveTowards(aimWeight, aimed ? 1 : 0, speed);
            if (weight <= 0) return;
            attachmentParent = transform.parent; attachmentPosition = transform.localPosition; attachmentRotation = transform.localRotation;
            Vector3 grip = transform.InverseTransformPoint(rightHand.position);
            Quaternion gripRotation = Quaternion.Inverse(transform.rotation) * rightHand.rotation;
            Quaternion rotation = HoldingRotation(Mathf.SmoothStep(0, 1, aimWeight));
            ReloadArmIK.Solve(rightUpper, rightLower, rightHand, character, HoldingWrist(Mathf.SmoothStep(0, 1, aimWeight)),
                rotation * gripRotation, RightElbowHint, Mathf.SmoothStep(0, 1, weight), rightShoulder, .35f);
            transform.rotation = rightHand.rotation * Quaternion.Inverse(gripRotation);
            transform.position = rightHand.position - transform.TransformVector(grip); displaced = true;
            ReloadArmIK.Solve(upper, lower, hand, character, ModelFrame.TransformPoint(ForeEndWrist),
                ModelFrame.rotation * WristRotation, ElbowHint, Mathf.SmoothStep(0, 1, weight), shoulder, .3f);
        }

        private void OnDisable()
        {
            RestoreAttachment(); weight = aimWeight = 0; wasAiming = firingStance = false; stanceOffset = 0;
            character = shoulder = upper = lower = hand = rightHand = null; states = null; animator = null;
        }
    }
}