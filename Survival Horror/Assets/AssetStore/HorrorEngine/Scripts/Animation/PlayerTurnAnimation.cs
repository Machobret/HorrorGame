using UnityEngine;

namespace HorrorEngine
{
    // A small procedural pivot cycle on top of the authored idle pose. World-space
    // planted feet keep rotation from looking like the whole model is sliding.
    [DefaultExecutionOrder(120), DisallowMultipleComponent]
    public sealed class PlayerTurnAnimation : MonoBehaviour
    {
        [Min(10)] public float DegreesPerStep = 38;
        [Min(0)] public float StepHeight = .05f;
        [Min(.01f)] public float BlendDuration = .12f;
        public float TurningWeight => weight;
        public bool IsStepping => active;
        public int SwingFoot => swing;
        public float StepProgress => phase;
        public float AngularSpeed => yawRate;

        private sealed class Foot
        {
            public Transform Upper, Lower, Bone;
            public Vector3 Rest, Plant, From, To, Output;
            public Quaternion RestRotation, PlantRotation, FromRotation, ToRotation, OutputRotation;
        }
        private readonly Foot[] feet = { new Foot(), new Foot() };
        private Transform actor;
        private PlayerMovement movement;
        private ActorStateController states;
        private float previousYaw, yawRate, weight, phase;
        private Vector3 previousPosition;
        private int swing, direction;
        private bool active, initialized;

        private void OnEnable()
        {
            var owner = GetComponentInParent<Actor>();
            var animator = GetComponent<Animator>();
            if (!owner || !animator || !animator.isHuman) return;
            actor = owner.transform; movement = owner.GetComponent<PlayerMovement>(); states = owner.StateController;
            Bind(feet[0], animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            Bind(feet[1], animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            initialized = movement && feet[0].Bone && feet[1].Bone;
            previousYaw = actor.eulerAngles.y; previousPosition = actor.position;
            active = false; phase = weight = yawRate = 0;
        }
        private static void Bind(Foot foot, Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones bone)
        { foot.Upper = animator.GetBoneTransform(upper); foot.Lower = animator.GetBoneTransform(lower); foot.Bone = animator.GetBoneTransform(bone); }

        private void LateUpdate()
        {
            if (!initialized || Time.deltaTime <= 0) return;
            float dt = Time.deltaTime;
            float delta = Mathf.DeltaAngle(previousYaw, actor.eulerAngles.y);
            float displacement = Vector3.Distance(previousPosition, actor.position);
            previousYaw = actor.eulerAngles.y; previousPosition = actor.position;
            if (Mathf.Abs(delta) > 45 || displacement > .5f) { ResetCycle(); return; }
            yawRate = Mathf.Lerp(yawRate, delta / dt, 1 - Mathf.Exp(-18 * dt));
            var state = states ? states.m_CurrentState : null;
            bool allowedState = state is PlayerStateMotion || state is PlayerStateAiming || state is PlayerStateAttack || state is PlayerStateReload;
            if (!allowedState) { ResetCycle(); return; }
            float speed = movement.IntendedMovement.magnitude / Mathf.Max(.001f, Time.fixedDeltaTime);
            bool turning = movement.enabled && !movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Rotation)
                && Mathf.Abs(movement.InputAxis.x) > .15f && Mathf.Abs(yawRate) > 8 && speed < .12f && displacement < .025f;
            for (int i = 0; i < 2; ++i)
            {
                feet[i].Rest = actor.InverseTransformPoint(feet[i].Bone.position);
                feet[i].RestRotation = Quaternion.Inverse(actor.rotation) * feet[i].Bone.rotation;
            }
            if (turning)
            {
                int sign = Mathf.Abs(delta) > .01f ? (delta > 0 ? 1 : -1) : direction;
                if (sign == 0) sign = movement.InputAxis.x > 0 ? 1 : -1;
                if (!active || sign != direction)
                {
                    for (int i = 0; i < 2; ++i)
                    {
                        feet[i].Plant = weight > .5f ? feet[i].Output : feet[i].Bone.position;
                        feet[i].PlantRotation = weight > .5f ? feet[i].OutputRotation : feet[i].Bone.rotation;
                    }
                    direction = sign; swing = sign > 0 ? 1 : 0; phase = 0; active = true; StartStep();
                }
                phase += Mathf.Abs(delta) / Mathf.Max(10, DegreesPerStep);
            }
            else if (active)
            {
                // Complete the last small step after key release, then fade into
                // idle. The character controller still stops rotating immediately.
                phase += dt / .18f;
                feet[swing].To = Vector3.Lerp(feet[swing].To, actor.TransformPoint(feet[swing].Rest), 1 - Mathf.Exp(-20 * dt));
                feet[swing].ToRotation = Quaternion.Slerp(feet[swing].ToRotation, actor.rotation * feet[swing].RestRotation, 1 - Mathf.Exp(-20 * dt));
            }
            if (active && phase >= 1)
            {
                feet[swing].Plant = feet[swing].To; feet[swing].PlantRotation = feet[swing].ToRotation;
                if (turning) { phase %= 1; swing = 1 - swing; StartStep(); }
                else { phase = 1; active = false; }
            }
            // Walking uses its existing locomotion cycle, with a short handoff.
            if (speed > .12f || !movement.enabled || movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Rotation)) active = false;
            weight = Mathf.MoveTowards(weight, active ? 1 : 0, dt / Mathf.Max(.01f, BlendDuration));
            if (weight <= 0) { return; }
            for (int i = 0; i < 2; ++i)
            {
                var foot = feet[i]; Vector3 target = foot.Plant; Quaternion rotation = foot.PlantRotation;
                if (i == swing && active)
                {
                    float t = Mathf.SmoothStep(0, 1, phase);
                    target = Vector3.Lerp(foot.From, foot.To, t) + actor.up * (Mathf.Sin(phase * Mathf.PI) * StepHeight);
                    rotation = Quaternion.Slerp(foot.FromRotation, foot.ToRotation, t);
                }
                ReloadArmIK.Solve(foot.Upper, foot.Lower, foot.Bone, actor, target, rotation,
                    new Vector3(i == 0 ? -.20f : .20f, .55f, .45f), Mathf.SmoothStep(0, 1, weight));
                foot.Output = foot.Bone.position; foot.OutputRotation = foot.Bone.rotation;
            }

        }
        private void StartStep()
        {
            var foot = feet[swing]; foot.From = foot.Plant; foot.FromRotation = foot.PlantRotation;
            Quaternion lead = Quaternion.AngleAxis(direction * DegreesPerStep * .85f, actor.up);
            foot.To = actor.position + lead * actor.TransformVector(foot.Rest);
            foot.ToRotation = lead * actor.rotation * foot.RestRotation;
        }
        private void ResetCycle() { active = false; weight = phase = yawRate = 0; }
        private void OnDisable() { ResetCycle(); initialized = false; }
    }
}