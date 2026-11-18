using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Characters
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class PlayerAimStance : MonoBehaviour
    {
        [Tooltip("Minimum height of the aimed pelvis in actor-local metres.")]
        [SerializeField, Min(0)] private float minimumHipHeight = .83f;
        [SerializeField, Range(.5f, 1f)] private float stanceDepth = .76f;

        private readonly Transform[] thighs = new Transform[2];
        private readonly Transform[] shins = new Transform[2];
        private readonly Transform[] feet = new Transform[2];
        private Animator animator;
        private Actor actor;
        private PlayerMovement movement;
        private Transform hips;
        private int weaponLayer;
        private float weight;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            actor = GetComponentInParent<Actor>();
            if (!actor || !animator || !animator.isHuman) { enabled = false; return; }

            movement = actor.GetComponent<PlayerMovement>();
            weaponLayer = animator.GetLayerIndex("Weapon Layer");
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            thighs[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            thighs[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            shins[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            shins[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            feet[0] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            feet[1] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private void OnDisable() => weight = 0;

        private void LateUpdate()
        {
            if (!animator.isActiveAndEnabled || !hips || !feet[0] || !feet[1] || weaponLayer < 0 ||
                !actor.StateController || Time.deltaTime <= 0)
                return;

            var state = actor.StateController.CurrentState;
            bool aiming = state is PlayerStateAiming || (state is PlayerStateAttack && weight > 0);
            bool leavingAim = state is PlayerStateMotion && weight > 0;
            if ((!aiming && !leavingAim) || (movement && movement.IntendedMovement.sqrMagnitude > .000004f))
            {
                weight = 0;
                return;
            }

            weight = Mathf.MoveTowards(weight, aiming ? 1 : 0, Time.deltaTime / .16f);
            float blend = Mathf.Min(weight, animator.GetLayerWeight(weaponLayer));
            if (blend <= 0) return;

            Transform root = actor.transform;
            Vector3 left = root.InverseTransformPoint(feet[0].position);
            Vector3 right = root.InverseTransformPoint(feet[1].position);
            Quaternion leftFacing = feet[0].rotation, rightFacing = feet[1].rotation;
            float middle = (left.z + right.z) * .5f;
            left.z = Mathf.Lerp(left.z, middle, (1 - stanceDepth) * blend);
            right.z = Mathf.Lerp(right.z, middle, (1 - stanceDepth) * blend);

            // The vertical aim poses share one grounded lower-body stance.
            // Keep the authored foot height and solve the knees after raising the pelvis.
            Vector3 pelvis = root.InverseTransformPoint(hips.position);
            pelvis.y += Mathf.Max(0, minimumHipHeight - pelvis.y) * blend;
            hips.position = root.TransformPoint(pelvis);
            ReloadArmIK.Solve(thighs[0], shins[0], feet[0], root, root.TransformPoint(left),
                leftFacing, new Vector3(-.20f, .55f, .45f), 1);
            ReloadArmIK.Solve(thighs[1], shins[1], feet[1], root, root.TransformPoint(right),
                rightFacing, new Vector3(.20f, .55f, .45f), 1);
        }
    }
}
