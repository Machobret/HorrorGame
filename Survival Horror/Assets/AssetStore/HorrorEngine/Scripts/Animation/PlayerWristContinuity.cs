using UnityEngine;

namespace HorrorEngine
{
    // Runs after the weapon arm solvers. Only wrist rotations are corrected;
    // elbow/hand positions, animation events and weapon timings stay authored.
    // Actor-relative rotations preserve body turns while catching parent-arm
    // handoffs; local wrist angles alone can jump as the elbow moves beneath it.
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    public sealed class PlayerWristContinuity : MonoBehaviour
    {
        public float BlendDuration = .14f;
        private Animator animator;
        private ActorStateController states;
        private Transform character;
        private IActorState lastState;
        private int lastAnimation;
        private bool pending, initialized;
        private readonly Wrist[] wrists = { new Wrist(), new Wrist() };

        private sealed class Wrist
        {
            public Transform Bone, Upper, Lower;
            public Vector3 PreviousPositionTarget, PreviousPositionOutput, PositionOffset;
            public Vector3 PreviousElbow, ElbowOffset;
            public Quaternion PreviousTarget, PreviousOutput, Offset;
            public float Elapsed;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            var actor = GetComponentInParent<Actor>();
            if (!animator || !animator.isHuman || !actor) { enabled = false; return; }
            states = actor.StateController;
            character = actor.transform;
            wrists[0].Bone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            wrists[1].Bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            wrists[0].Upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            wrists[0].Lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            wrists[1].Upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            wrists[1].Lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        // Equipment changes can rebind the Animator without changing actor state.
        public void BeginTransition() { pending = true; }
        private void OnDisable() { initialized = pending = false; }

        private void LateUpdate()
        {
            if (!animator || !states) return;
            if (!animator.isActiveAndEnabled) { initialized = false; return; }
            if (Time.deltaTime <= 0) return;
            var state = states.m_CurrentState;
            int animation = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).fullPathHash
                : animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            bool transition = pending || state != lastState || animation != lastAnimation;
            float duration = Mathf.Max(.01f, BlendDuration);
            foreach (var wrist in wrists)
            {
                if (!wrist.Bone || !wrist.Upper || !wrist.Lower) continue;
                Vector3 position = character.InverseTransformPoint(wrist.Bone.position);
                Vector3 elbow = character.InverseTransformPoint(wrist.Lower.position);
                Quaternion target = Quaternion.Inverse(character.rotation) * wrist.Bone.rotation;
                if (!initialized)
                {
                    wrist.PreviousOutput = wrist.PreviousTarget = target;
                    wrist.Offset = Quaternion.identity;
                    wrist.PreviousPositionTarget = wrist.PreviousPositionOutput = position;
                    wrist.PreviousElbow = elbow;
                    wrist.PositionOffset = wrist.ElbowOffset = Vector3.zero;
                    wrist.Elapsed = duration;
                }
                else
                {
                    // Catch abrupt changes inside a state too (e.g. an Animator
                    // override or an IK owner releasing the hand). Ordinary
                    // recoil and loading motion keep their original timing.
                    bool discontinuity = Quaternion.Angle(wrist.PreviousTarget, target) > 1200 * Time.deltaTime;
                    discontinuity |= Vector3.Distance(wrist.PreviousPositionTarget, position) > 3.6f * Time.deltaTime;
                    if (transition || discontinuity)
                    {
                        wrist.Offset = wrist.PreviousOutput * Quaternion.Inverse(target);
                        wrist.PositionOffset = wrist.PreviousPositionOutput - position;
                        wrist.ElbowOffset = wrist.PreviousElbow - elbow;
                        wrist.Elapsed = 0;
                    }
                    float t = Mathf.Clamp01(wrist.Elapsed / duration);
                    Quaternion correction = Quaternion.Slerp(wrist.Offset, Quaternion.identity, Mathf.SmoothStep(0, 1, t));
                    Quaternion rotation = character.rotation * correction * target;
                    if (t < 1 && wrist.PositionOffset.sqrMagnitude > .0000001f)
                    {
                        float remaining = 1 - Mathf.SmoothStep(0, 1, t);
                        Vector3 handTarget = character.TransformPoint(position + wrist.PositionOffset * remaining);
                        Vector3 pole = elbow + wrist.ElbowOffset * remaining;
                        ReloadArmIK.Solve(wrist.Upper, wrist.Lower, wrist.Bone, character, handTarget, rotation, pole, 1);
                    }
                    else wrist.Bone.rotation = rotation;
                    wrist.PreviousTarget = target;
                    wrist.PreviousPositionTarget = position;
                    wrist.PreviousPositionOutput = character.InverseTransformPoint(wrist.Bone.position);
                    wrist.PreviousElbow = character.InverseTransformPoint(wrist.Lower.position);
                    wrist.PreviousOutput = correction * target;
                    wrist.Elapsed += Time.deltaTime;
                }
            }
            pending = false; initialized = true; lastState = state; lastAnimation = animation;
        }
    }
}