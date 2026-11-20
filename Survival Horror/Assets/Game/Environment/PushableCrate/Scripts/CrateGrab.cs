using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Environment
{
    // One grab session. The crate keeps its own Rigidbody and pressure-plate contacts.
    internal sealed class CrateGrab
    {
        private const float PushSpeed = 0.9f;
        private const float PullSpeed = 0.7f;
        private const float SideSpeed = 0.65f;
        private const float Clearance = 0.015f;
        private const float AlignSpeed = 1.5f;
        private const float MaxSeparation = 0.12f;

        private readonly Pushable m_Crate;
        private readonly Rigidbody m_Body;
        private readonly BoxCollider m_Box;
        private readonly CharacterController m_Controller;
        private readonly PlayerMovement m_Movement;
        private readonly Transform m_Player;
        private readonly Vector3 m_Forward;
        private readonly Vector3 m_Right;
        private readonly Vector3 m_BoxCenterOffset;
        private readonly Quaternion m_BoxRotationOffset;
        private readonly Vector3 m_HalfSize;
        private readonly Vector3 m_StanceOffset;
        private readonly float m_FaceDepth;
        private readonly float m_HandOffset;
        private readonly float m_GripHeight;
        private readonly float m_OriginalStepOffset;
        private readonly float m_InitialHeightDifference;
        private readonly RaycastHit[] m_Hits = new RaycastHit[32];
        private readonly Arm m_Left;
        private readonly Arm m_RightArm;
        private readonly Transform m_Spine;
        private readonly CrateDragAnimation m_Drag;
        private readonly Renderer[] m_HiddenWeaponRenderers;

        private Quaternion m_SpineBeforeLean;
        private Quaternion m_SpineAfterLean;
        private bool m_LeanApplied;

        private float m_ScrapeTimer;
        private bool m_HasMoved;

        private sealed class Arm
        {
            private readonly Transform m_Upper;
            private readonly Transform m_Lower;
            private readonly Transform m_Hand;
            private Quaternion m_Wrist;
            private bool m_CapturedWrist;

            public Arm(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
            {
                if (!animator.isHuman)
                    return;

                m_Upper = animator.GetBoneTransform(upper);
                m_Lower = animator.GetBoneTransform(lower);
                m_Hand = animator.GetBoneTransform(hand);
            }

            public void Plant(Transform player, Vector3 target, float side, float weight)
            {
                if (!m_Upper || !m_Lower || !m_Hand)
                    return;

                if (!m_CapturedWrist)
                {
                    m_Wrist = Quaternion.Inverse(player.rotation) * m_Hand.rotation;
                    m_CapturedWrist = weight >= 0.85f;
                }

                ReloadArmIK.Solve(m_Upper, m_Lower, m_Hand, player, target, player.rotation * m_Wrist,
                    new Vector3(side * 0.72f, 0.85f, 0.12f), weight);
            }
        }

        public static CrateGrab TryBegin(Pushable crate, Actor player, PlayerMovement movement, Vector3 forward)
        {
            var body = crate.GetComponent<Rigidbody>();
            var controller = player.GetComponent<CharacterController>();
            if (!body || body.isKinematic || !controller || !controller.enabled)
                return null;

            foreach (var box in crate.GetComponentsInChildren<BoxCollider>())
            {
                if (box.enabled && !box.isTrigger && box.attachedRigidbody == body)
                    return new CrateGrab(crate, body, box, player, controller, movement, forward);
            }

            return null;
        }

        private CrateGrab(Pushable crate, Rigidbody body, BoxCollider box, Actor actor,
            CharacterController controller, PlayerMovement movement, Vector3 forward)
        {
            m_Crate = crate;
            m_Body = body;
            m_Box = box;
            m_Controller = controller;
            m_Movement = movement;
            m_Player = actor.transform;
            m_Forward = forward;
            m_Right = Vector3.Cross(Vector3.up, forward);
            Quaternion fromBody = Quaternion.Inverse(body.transform.rotation);
            m_BoxCenterOffset = fromBody * (box.transform.TransformPoint(box.center) - body.transform.position);
            m_BoxRotationOffset = fromBody * box.transform.rotation;
            Vector3 scale = box.transform.lossyScale;
            m_HalfSize = Vector3.Scale(box.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            m_Drag = new CrateDragAnimation(actor, controller, body, m_HalfSize.y * 2f);
            m_FaceDepth = ExtentAlong(forward, box.transform.rotation);
            float halfWidth = ExtentAlong(m_Right, box.transform.rotation);
            m_HandOffset = Mathf.Min(halfWidth * 0.4f, 0.21f);
            m_GripHeight = Mathf.Min(m_HalfSize.y * 2f - 0.035f, 1.12f) - m_HalfSize.y;
            float gripLimit = Mathf.Max(0f, halfWidth - m_HandOffset - 0.08f);
            float across = Mathf.Clamp(Vector3.Dot(m_Player.position - BoxCenter, m_Right), -gripLimit, gripLimit);
            float stanceDistance = m_FaceDepth + controller.radius + controller.skinWidth + 0.045f;
            m_StanceOffset = across * m_Right - stanceDistance * m_Forward;
            m_InitialHeightDifference = m_Player.position.y - m_Body.position.y;
            m_OriginalStepOffset = controller.stepOffset;
            controller.stepOffset = 0f;
            m_Left = new Arm(actor.MainAnimator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            m_RightArm = new Arm(actor.MainAnimator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            m_Spine = actor.MainAnimator.isHuman ? actor.MainAnimator.GetBoneTransform(HumanBodyBones.Spine) : null;
            var equipment = actor.GetComponentInChildren<PlayerEquipment>();
            var weapon = equipment ? equipment.GetWeaponInstance(EquipmentSlot.Primary) : null;
            m_HiddenWeaponRenderers = weapon
                ? System.Array.FindAll(weapon.GetComponentsInChildren<Renderer>(true), renderer => renderer.enabled)
                : System.Array.Empty<Renderer>();
            foreach (var renderer in m_HiddenWeaponRenderers)
                renderer.enabled = false;
            crate.PushInteraction.SetPromptText("Let go");
        }

        public bool IsValid => m_Crate && m_Crate.isActiveAndEnabled && m_Crate.CanBePushed
            && m_Box && m_Box.enabled && m_Controller && m_Controller.enabled
            && m_Body && !m_Body.isKinematic
            && Mathf.Abs(m_Player.position.y - m_Body.position.y - m_InitialHeightDifference) < MaxSeparation;

        private Vector3 BoxCenter => m_Body.position + m_Body.rotation * m_BoxCenterOffset;
        private Vector3 StanceError => Vector3.ProjectOnPlane(BoxCenter + m_StanceOffset - m_Player.position, Vector3.up);

        public bool Align(float deltaTime)
        {
            Vector3 step = Vector3.ClampMagnitude(StanceError, AlignSpeed * deltaTime);
            if (step.sqrMagnitude > 0.000001f)
            {
                Vector3 direction = step.normalized;
                step = direction * PlayerClearance(direction, step.magnitude);
            }

            MovePlayer(step);
            return StanceError.sqrMagnitude <= 0.025f * 0.025f &&
                Vector3.Dot(m_Player.forward, m_Forward) > 0.995f;
        }

        public bool Move(Vector2 input, float deltaTime)
        {
            if (StanceError.sqrMagnitude > MaxSeparation * MaxSeparation)
                return false;

            m_ScrapeTimer -= deltaTime;
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 desired = (m_Right * (input.x * SideSpeed)
                + m_Forward * (input.y * (input.y >= 0f ? PushSpeed : PullSpeed))) * deltaTime;
            if (desired.sqrMagnitude > 0.000001f)
            {
                Vector3 direction = desired.normalized;
                float distance = Mathf.Min(PlayerClearance(direction, desired.magnitude),
                    CrateClearance(direction, desired.magnitude));
                desired = direction * distance;
                if (!CrateHasSupport(desired))
                    desired = Vector3.zero;
            }

            Vector3 moved = MovePlayer(desired);
            m_Drag.RecordMovement(moved);
            if (moved.sqrMagnitude > 0.000001f)
            {
                if (m_ScrapeTimer <= 0f)
                {
                    m_HasMoved = true;
                    m_Crate.OnStartPushEvent();
                    m_ScrapeTimer = 0.55f;
                }

                m_Crate.PushTo(m_Body.position + moved);
            }
            return true;
        }

        private Vector3 MovePlayer(Vector3 step)
        {
            Vector3 start = m_Player.position;
            if (step != Vector3.zero && !ClimbClearance.HasSupport(m_Controller, start + step, m_Body.transform))
                step = Vector3.zero;

            // Maintain floor contact while using the cinematic movement path.
            m_Movement.MoveCinematic(step + Vector3.down * 0.025f);
            return Vector3.ProjectOnPlane(m_Player.position - start, Vector3.up);
        }

        private float PlayerClearance(Vector3 direction, float distance)
        {
            var scale = m_Controller.transform.lossyScale;
            float radius = m_Controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(radius, m_Controller.height * Mathf.Abs(scale.y) * 0.5f);
            var center = m_Controller.transform.TransformPoint(m_Controller.center);
            Vector3 segment = Vector3.up * (height - radius);
            int count = Physics.CapsuleCastNonAlloc(center + segment, center - segment,
                radius - Clearance, direction, m_Hits, distance + Clearance, ~0, QueryTriggerInteraction.Ignore);
            return LimitDistance(m_Controller, direction, distance, count);
        }

        private float CrateClearance(Vector3 direction, float distance)
        {
            var halfSize = m_HalfSize - Vector3.up * Clearance;
            int count = Physics.BoxCastNonAlloc(BoxCenter, halfSize, direction, m_Hits,
                m_Body.rotation * m_BoxRotationOffset, distance + Clearance, ~0, QueryTriggerInteraction.Ignore);
            return LimitDistance(m_Box, direction, distance, count);
        }

        private float LimitDistance(Collider source, Vector3 direction, float distance, int count)
        {
            if (count == m_Hits.Length)
                return 0f;

            for (int i = 0; i < count; i++)
            {
                var hit = m_Hits[i];
                if (Blocks(source, hit.collider) && Vector3.Dot(direction, hit.normal) < -0.01f)
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - Clearance));
            }
            return distance;
        }

        private bool CrateHasSupport(Vector3 step)
        {
            Vector3 foot = BoxCenter + step - Vector3.up * m_HalfSize.y;
            int count = Physics.RaycastNonAlloc(foot + Vector3.up * 0.10f, Vector3.down, m_Hits,
                0.24f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (m_Hits[i].normal.y > 0.55f && Blocks(m_Box, m_Hits[i].collider))
                    return true;
            }
            return false;
        }

        private bool Blocks(Collider source, Collider other)
        {
            return other && !other.transform.IsChildOf(m_Player) && other.attachedRigidbody != m_Body
                && !Physics.GetIgnoreLayerCollision(source.gameObject.layer, other.gameObject.layer)
                && !Physics.GetIgnoreCollision(source, other);
        }

        private float ExtentAlong(Vector3 axis, Quaternion rotation)
        {
            Vector3 local = Quaternion.Inverse(rotation) * axis;
            return Vector3.Dot(new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)), m_HalfSize);
        }

        public void ApplyPose(float weight, bool attached)
        {
            ClearPose();
            m_Drag.Apply(weight, attached);
            if (!m_Box || !m_Controller)
                return;

            if (m_Spine)
            {
                m_SpineBeforeLean = m_Spine.localRotation;
                Quaternion pitch = Quaternion.AngleAxis(m_Drag.SpinePitch * weight, m_Player.right);
                Quaternion roll = Quaternion.AngleAxis(m_Drag.SpineRoll * weight, m_Player.forward);
                m_Spine.rotation = roll * pitch * m_Spine.rotation;
                m_SpineAfterLean = m_Spine.localRotation;
                m_LeanApplied = true;
            }

            float across = Vector3.Dot(m_StanceOffset, m_Right);
            Vector3 grip = m_Box.transform.TransformPoint(m_Box.center) + across * m_Right
                - (m_FaceDepth + 0.032f) * m_Forward + Vector3.up * m_GripHeight;

            // Follow the rendered crate so hand placement also follows Rigidbody interpolation.
            m_Left.Plant(m_Player, grip - m_HandOffset * m_Right, -1f, weight);
            m_RightArm.Plant(m_Player, grip + m_HandOffset * m_Right, 1f, weight);
        }

        private void ClearPose()
        {
            // A render update can occur without a new fixed-step animator pose.
            if (m_LeanApplied && m_Spine && Quaternion.Angle(m_Spine.localRotation, m_SpineAfterLean) < 0.05f)
                m_Spine.localRotation = m_SpineBeforeLean;
            m_LeanApplied = false;
        }

        public void FinishPose()
        {
            ClearPose();
            m_Drag.ClearPose();
            foreach (var renderer in m_HiddenWeaponRenderers)
            {
                if (renderer)
                    renderer.enabled = true;
            }
        }

        public void Release()
        {
            if (m_Controller)
                m_Controller.stepOffset = m_OriginalStepOffset;
            if (!m_Crate)
                return;

            if (m_Crate.PushInteraction)
                m_Crate.PushInteraction.SetPromptText(null);
            if (m_HasMoved)
                m_Crate.StopPush();
        }
    }
}
