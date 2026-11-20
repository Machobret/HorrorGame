using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Environment
{
    /// <summary>Distance-driven animation for a two-handed crate drag.</summary>
    internal sealed class CrateDragAnimation
    {
        // The swing phases share a short double-support interval. A load stays
        // braced even at low analog speed and each planted shoe keeps its world position.
        private const float SwingFraction = 0.76f;
        private const float StopDuration = 0.12f;
        private const float MinFootGap = 0.14f;

        private static readonly AnimationCurve Lift = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.20f, 0.55f),
            new Keyframe(0.46f, 1f), new Keyframe(0.73f, 0.62f), new Keyframe(1f, 0f));

        private static readonly AnimationCurve Contact = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 0.13f),
            new Keyframe(0.70f, 0.84f), new Keyframe(1f, 1f));

        private static readonly AnimationCurve ToeRoll = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.18f, 0.8f),
            new Keyframe(0.62f, -0.6f), new Keyframe(1f, 0f));

        private sealed class Foot
        {
            public readonly Transform Upper;
            public readonly Transform Lower;
            public readonly Transform Ankle;
            public readonly int Side;

            public Vector3 Rest;
            public float Height;
            public Quaternion RestRotation;
            public Vector3 Plant;

            public Foot(Animator animator, bool left)
            {
                Side = left ? -1 : 1;
                Upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                Lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                Ankle = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            }

            public bool IsValid => Upper && Lower && Ankle;
        }

        private readonly Transform m_Actor;
        private readonly Transform m_Hips;
        private readonly Foot[] m_Feet;
        private readonly Collider m_PlayerCollider;
        private readonly Rigidbody m_CrateBody;
        private readonly float m_LowCrate;
        private readonly RaycastHit[] m_GroundHits = new RaycastHit[12];

        private Vector3 m_Displacement;
        private bool m_Moving;
        private bool m_Initialized;
        private int m_Swing = -1;
        private int m_NextFoot = -1;
        private Vector2 m_Effort;
        private Vector3 m_StepDirection;
        private Vector3 m_StepFrom;
        private Vector3 m_StepTo;
        private float m_StepDistance;
        private float m_LiftHeight;
        private float m_StepFloorFrom;
        private float m_ToeDegrees;
        private float m_Progress;

        private Vector3 m_SettleFrom;
        private Vector3 m_SettleTo;
        private float m_Settle;

        private Vector3 m_HipsBeforeOffset;
        private Vector3 m_HipsAfterOffset;
        private bool m_HipsAdjusted;

        public float SpinePitch => (m_Effort.y >= 0f ? 8f : 4f) * m_Effort.y + 4f * Mathf.Abs(m_Effort.x);
        public float SpineRoll => -3.5f * m_Effort.x;

        public CrateDragAnimation(Actor actor, CharacterController controller, Rigidbody crateBody, float crateHeight)
        {
            m_Actor = actor.transform;
            m_PlayerCollider = controller;
            m_CrateBody = crateBody;
            m_LowCrate = 1f - Mathf.InverseLerp(1f, 1.5f, crateHeight);
            Animator animator = actor.MainAnimator;
            if (!animator.isHuman)
                return;

            m_Hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            m_Feet = new[] { new Foot(animator, true), new Foot(animator, false) };
        }

        public void RecordMovement(Vector3 displacement)
        {
            m_Displacement += displacement;
            m_Moving = displacement.sqrMagnitude > 0.00000025f;
        }

        public void Apply(float weight, bool attached)
        {
            ClearPose();
            if (!m_Hips || !m_Feet[0].IsValid || !m_Feet[1].IsValid)
                return;

            if (!m_Initialized)
            {
                if (weight < 0.75f) return;
                float ankleHeight = float.PositiveInfinity;
                foreach (var foot in m_Feet)
                    ankleHeight = Mathf.Min(ankleHeight, foot.Ankle.position.y - Floor(foot.Ankle.position));
                ankleHeight = Mathf.Clamp(ankleHeight, 0.035f, 0.14f);
                foreach (var foot in m_Feet)
                {
                    var rest = m_Actor.InverseTransformPoint(foot.Ankle.position);
                    rest.x = foot.Side * Mathf.Clamp(Mathf.Abs(rest.x), 0.145f, 0.19f);
                    rest.z = Mathf.Clamp(rest.z, -0.075f, 0.06f);
                    foot.Rest = rest;
                    foot.RestRotation = Quaternion.Inverse(m_Actor.rotation) * foot.Ankle.rotation;
                    foot.Height = ankleHeight;
                    foot.Plant = Grounded(foot, m_Actor.TransformPoint(rest));
                }
                m_Initialized = true;
            }

            if (attached)
                UpdateSteps();
            else
                m_Moving = false;

            var localDirection = m_Moving ? m_Actor.InverseTransformDirection(m_StepDirection) : Vector3.zero;
            m_Effort = Vector2.MoveTowards(m_Effort, new Vector2(localDirection.x, localDirection.z), Time.deltaTime * 6f);
            PoseHips(weight);

            for (int i = 0; i < m_Feet.Length; i++)
            {
                var foot = m_Feet[i];
                Vector3 target = i == m_Swing ? SwingPosition() : foot.Plant;
                float toe = i == m_Swing && m_Moving ? ToeRoll.Evaluate(SwingPhase) * m_ToeDegrees : 0f;
                Quaternion rotation = Quaternion.AngleAxis(toe, m_Actor.right) * m_Actor.rotation * foot.RestRotation;
                ReloadArmIK.Solve(foot.Upper, foot.Lower, foot.Ankle, m_Actor, target, rotation,
                    new Vector3(foot.Side * 0.23f, 0.47f, 0.67f), weight);
            }
        }

        private float SwingPhase => Mathf.Clamp01(m_Progress / SwingFraction);
        private Vector3 SwingPosition()
        {
            if (m_Settle > 0f)
                return Vector3.Lerp(m_SettleFrom, m_SettleTo, Mathf.SmoothStep(0f, 1f, m_Settle / StopDuration));

            float phase = SwingPhase;
            float progress = Contact.Evaluate(phase);
            Vector3 position = Vector3.Lerp(m_StepFrom, m_StepTo, progress);
            float floor = Mathf.Lerp(m_StepFloorFrom, m_StepTo.y, progress);
            position.y = Mathf.Max(position.y, floor + Lift.Evaluate(phase) * m_LiftHeight);
            return position;
        }

        private void UpdateSteps()
        {
            Vector3 displacement = m_Displacement;
            m_Displacement = Vector3.zero;
            float distance = displacement.magnitude;
            if (!m_Moving)
            {
                if (m_Swing >= 0)
                {
                    if (m_Settle <= 0f)
                    {
                        m_SettleFrom = SwingPosition();
                        m_SettleTo = Grounded(m_Feet[m_Swing], m_SettleFrom);
                    }

                    m_Settle += Time.deltaTime;
                    if (m_Settle >= StopDuration)
                    {
                        m_Feet[m_Swing].Plant = m_SettleTo;
                        m_Swing = m_NextFoot = -1;
                        m_Settle = 0f;
                    }
                }
                return;
            }

            // Render frames between fixed updates keep their existing contact pose.
            if (distance < 0.0001f)
                return;

            Vector3 direction = displacement / distance;
            if (m_Swing < 0)
            {
                if (Vector3.Dot(direction, m_StepDirection) < 0.45f)
                    m_NextFoot = -1;
                StartStep(direction);
            }
            else if (m_Settle > 0f || Vector3.Dot(direction, m_StepDirection) < 0.45f)
            {
                m_Feet[m_Swing].Plant = SwingPosition();
                m_NextFoot = m_Swing;
                StartStep(direction);
            }

            m_Progress += distance / m_StepDistance;
            if (m_Progress >= 1f)
            {
                m_Feet[m_Swing].Plant = m_StepTo;
                m_NextFoot = 1 - m_Swing;
                m_Swing = -1;
            }
        }

        private void StartStep(Vector3 direction)
        {
            float across = Vector3.Dot(direction, m_Actor.right);
            float forward = Vector3.Dot(direction, m_Actor.forward);
            bool lateral = Mathf.Abs(across) > Mathf.Abs(forward);
            if (m_NextFoot < 0)
            {
                if (lateral)
                    m_NextFoot = across < 0f ? 0 : 1;
                else
                    m_NextFoot = Vector3.Dot(m_Feet[0].Plant - m_Feet[1].Plant, direction) <= 0f ? 0 : 1;
            }

            m_Swing = m_NextFoot;
            var foot = m_Feet[m_Swing];
            var support = m_Feet[1 - m_Swing];
            m_StepDirection = direction;
            m_StepDistance = lateral ? 0.24f : forward < 0f ? 0.26f : 0.31f;
            m_LiftHeight = lateral ? 0.048f : forward < 0f ? 0.059f : 0.052f;
            m_ToeDegrees = lateral ? 4f : forward < 0f ? 9f : -8f;
            m_StepFrom = foot.Plant;
            m_StepFloorFrom = Floor(m_StepFrom) + foot.Height;
            m_StepTo = m_Actor.TransformPoint(foot.Rest) + direction * (m_StepDistance * 0.88f);

            // The leading foot opens the stance. The other gathers without
            // crossing it, including when a keyboard direction reverses abruptly.
            Vector3 outside = m_Actor.right * foot.Side;
            float gap = Vector3.Dot(m_StepTo - support.Plant, outside);
            m_StepTo = Grounded(foot, m_StepTo + outside * Mathf.Max(0f, MinFootGap - gap));
            m_Progress = m_Settle = 0f;
        }

        private void PoseHips(float weight)
        {
            m_HipsBeforeOffset = m_Hips.localPosition;
            float loaded = Mathf.Clamp01(m_Effort.magnitude);
            float compression = m_Swing >= 0 ? 1f - Mathf.Sin(SwingPhase * Mathf.PI) : 1f;
            float crouch = Mathf.Lerp(0.025f, 0.043f, m_LowCrate)
                + loaded * (0.014f + 0.009f * compression) + Mathf.Max(0f, -m_Effort.y) * 0.011f;
            Vector3 shift = m_Actor.right * (0.047f * m_Effort.x)
                + m_Actor.forward * (0.038f * m_Effort.y) - Vector3.up * crouch;
            m_Hips.localPosition += m_Hips.parent.InverseTransformVector(shift * weight);
            m_HipsAfterOffset = m_Hips.localPosition;
            m_HipsAdjusted = true;
        }

        private Vector3 Grounded(Foot foot, Vector3 point)
        {
            point.y = Floor(point) + foot.Height;
            return point;
        }

        private float Floor(Vector3 point)
        {
            float floor = m_Actor.position.y;
            Vector3 origin = new Vector3(point.x, floor + 0.22f, point.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, m_GroundHits, 0.44f, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = m_GroundHits[i];
                var collider = hit.collider;
                if (!collider || hit.normal.y < 0.6f || hit.distance >= closest
                    || collider.transform.IsChildOf(m_Actor) || collider.attachedRigidbody == m_CrateBody
                    || Physics.GetIgnoreLayerCollision(m_PlayerCollider.gameObject.layer, collider.gameObject.layer)
                    || Physics.GetIgnoreCollision(m_PlayerCollider, collider))
                    continue;

                closest = hit.distance;
                floor = hit.point.y;
            }
            return floor;
        }

        public void ClearPose()
        {
            // A render frame can reuse the preceding fixed-step bone pose.
            if (m_HipsAdjusted && m_Hips && (m_Hips.localPosition - m_HipsAfterOffset).sqrMagnitude < 0.00000001f)
                m_Hips.localPosition = m_HipsBeforeOffset;
            m_HipsAdjusted = false;
        }
    }
}
