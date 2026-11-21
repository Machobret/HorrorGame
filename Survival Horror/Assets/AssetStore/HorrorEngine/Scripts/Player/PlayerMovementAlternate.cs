using UnityEngine;
using System.Collections.Generic;
using System;

namespace HorrorEngine
{
    [Serializable]
    public class CamLockedInput
    {
        public bool IsLockingEnabled = true;
        public float MinInputMovementThreshold = 0.5f;
        public float MinInputRotationThreshold = 0.15f;

        private bool m_GestureInProgress;
        private Behaviour m_GestureCamera;
        private Vector3 m_GestureForward;

        public Vector3 CalculateDirFromCamera(Vector2 input, bool inputHeld)
        {
            if (!inputHeld)
            {
                Clear();
                return Vector3.zero;
            }

            var cameraSystem = CameraSystem.Instance;
            var output = cameraSystem.MainCamera;
            Behaviour activeCamera = cameraSystem.ActiveCamera;
            if (!activeCamera)
                activeCamera = output;

            if (!m_GestureInProgress || !IsLockingEnabled)
            {
                m_GestureInProgress = true;
                m_GestureCamera = activeCamera;
            }

            // Follow a moving camera until it cuts away. Keep the last basis through further cuts.
            if (m_GestureCamera && m_GestureCamera == activeCamera)
                m_GestureForward = Vector3.ProjectOnPlane(output.transform.forward, Vector3.up).normalized;
            else
                m_GestureCamera = null;

            if (input.magnitude < MinInputMovementThreshold)
                return Vector3.zero;

            Vector3 right = Vector3.Cross(Vector3.up, m_GestureForward);
            return right * input.x + m_GestureForward * input.y;
        }

        public void Clear()
        {
            m_GestureInProgress = false;
            m_GestureCamera = null;
            m_GestureForward = Vector3.zero;
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

        public void GetRotation(PlayerMovement movement, out float sign, out float rate)
        {
            if (m_TankRotationStates.Contains((ActorState)m_StateController.CurrentState))
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

        internal void UpdateCameraInput(Vector2 input, bool inputHeld)
        {
            m_MovementDirection = m_CameraLockedInput.CalculateDirFromCamera(Vector2.ClampMagnitude(input, 1f), inputHeld);
        }

        public void ResetMovement()
        {
            m_CameraLockedInput.Clear();
            m_MovementDirection = Vector3.zero;
        }
    }
}
