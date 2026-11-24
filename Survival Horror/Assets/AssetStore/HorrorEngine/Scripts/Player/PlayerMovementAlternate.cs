using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System;

namespace HorrorEngine
{
    [Serializable]
    public class CamLockedInput
    {
        private const float k_CutInputOverrideDelay = 0.1f;
        private const float k_StickDirectionAlignment = 0.8660254f; // Cosine of 30 degrees.

        [FormerlySerializedAs("IsLockingEnabled")]
        [FormerlySerializedAs("IsCutBlendEnabled")]
        public bool IsCameraLockEnabled = true;
        public float MinInputMovementThreshold = 0.5f;
        public float MinInputRotationThreshold = 0.15f;

        private Behaviour m_ActiveCamera;
        private Vector3 m_MovementForward;
        private float m_CutTime = float.NegativeInfinity;
        private Vector2 m_InputReferenceDirection;

        public Vector3 CalculateDirFromCamera(Vector2 input, bool inputHeld, bool? buttonPress = null)
        {
            var cameraSystem = CameraSystem.Instance;
            var output = cameraSystem.MainCamera;
            Behaviour activeCamera = cameraSystem.ActiveCamera;
            if (!activeCamera)
                activeCamera = output;

            float now = Time.time;
            bool hadCamera = m_ActiveCamera is not null;
            if (hadCamera && m_ActiveCamera != activeCamera)
                m_CutTime = now;
            m_ActiveCamera = activeCamera;

            bool inCutGrace = now - m_CutTime < k_CutInputOverrideDelay;
            float inputMagnitude = input.magnitude;
            bool moving = inputHeld && inputMagnitude >= MinInputMovementThreshold;
            Vector2 direction = moving ? input.normalized : Vector2.zero;
            bool newDirection = direction != Vector2.zero &&
                Vector2.Dot(m_InputReferenceDirection, direction) < k_StickDirectionAlignment;

            // During the grace period, accept steering on the outgoing axes and track it as held input.
            if (moving && (inCutGrace || newDirection))
                m_InputReferenceDirection = direction;
            else if (!inputHeld || (inputMagnitude > 0f && inputMagnitude < MinInputRotationThreshold))
                m_InputReferenceDirection = Vector2.zero;

            // Only new intent adopts the live camera while moving, even if the shot tracks the player.
            bool newInput = buttonPress ?? newDirection;
            if (!IsCameraLockEnabled || !hadCamera || (!inCutGrace && (!inputHeld || newInput)))
            {
                m_MovementForward = Vector3.ProjectOnPlane(output.transform.forward, Vector3.up).normalized;
                if (m_MovementForward == Vector3.zero)
                    m_MovementForward = Vector3.ProjectOnPlane(output.transform.up, Vector3.up).normalized;
            }

            if (!moving)
                return Vector3.zero;

            Vector3 right = Vector3.Cross(Vector3.up, m_MovementForward);
            return right * input.x + m_MovementForward * input.y;
        }

        public void Clear()
        {
            m_ActiveCamera = null;
            m_CutTime = float.NegativeInfinity;
            m_InputReferenceDirection = Vector2.zero;
        }
    }

    public class PlayerMovementAlternate : MonoBehaviour, IPlayerMovementSettings
    {
        [SerializeField] AnimationCurve m_RotationSpeedOverAngle = AnimationCurve.Linear(0, 0, 360, 1384);
        [SerializeField] float m_AimingRotationSpeed = 180f;
        [SerializeField] List<ActorState> m_TankRotationStates;
        [SerializeField] CamLockedInput m_CameraLockedInput;

        private ActorStateController m_StateController;
        private Vector3 m_MovementDirection;

        private bool UsesTankRotation => m_TankRotationStates.Contains((ActorState)m_StateController.CurrentState);

        private void Awake()
        {
            m_StateController = GetComponent<ActorStateController>();
        }

        public PlayerMovementType GetMovementType()
        {
            return PlayerMovementType.Alternate;
        }

        public float GetFwdRate(PlayerMovement movement)
        {
            Debug.DrawLine(movement.transform.position, movement.transform.position + m_MovementDirection, Color.magenta);
            return Vector3.Dot(movement.transform.forward, m_MovementDirection);
        }

        public float GetRightRate(PlayerMovement movement)
        {
            return 0f;
        }

        internal bool TryGetCameraRelativeMovement(out Vector3 movementDirection)
        {
            movementDirection = m_MovementDirection;
            return !UsesTankRotation;
        }

        public void GetRotation(PlayerMovement movement, out float sign, out float rate)
        {
            if (UsesTankRotation)
            {
                if (Mathf.Abs(movement.InputAxis.x) < m_CameraLockedInput.MinInputRotationThreshold)
                {
                    sign = 0f;
                    rate = 0f;
                    return;
                }

                sign = movement.InputAxis.x;
                rate = m_AimingRotationSpeed;
                return;
            }
            else
            {
                sign = 0;
                rate = 0;

                if (movement.InputAxis.magnitude < m_CameraLockedInput.MinInputRotationThreshold)
                    return;

                float signedAngle = Vector3.SignedAngle(movement.transform.forward, m_MovementDirection, Vector3.up);
                //if (Mathf.Abs(signedAngle) > m_MinAngleRotationThreshold)
                {
                    sign = Mathf.Sign(signedAngle);
                    rate = m_RotationSpeedOverAngle.Evaluate(Mathf.Abs(signedAngle));
                }
            }
        }

        internal void UpdateCameraInput(Vector2 input, bool inputHeld, bool? buttonPress)
        {
            m_MovementDirection = m_CameraLockedInput.CalculateDirFromCamera(Vector2.ClampMagnitude(input, 1f), inputHeld, buttonPress);
        }

        public void ResetMovement()
        {
            m_CameraLockedInput.Clear();
            m_MovementDirection = Vector3.zero;
        }
    }
}
