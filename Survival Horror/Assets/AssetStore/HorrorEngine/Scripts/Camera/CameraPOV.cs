using System;
using System.Collections.Generic;
using UnityEngine;

using Unity.Cinemachine;

namespace HorrorEngine 
{

    public class CameraPOV : MonoBehaviour
    {
        public enum PlayerFollowMode
        {
            KeepPlayerCentered,
            KeepPlayerInsideBorder
        }

        [Serializable]
        public class PlayerFollowSettings
        {
            [Tooltip("Enable this type of player follow.")]
            public bool Enabled;

            [Tooltip("Keeps the player near the camera centre, or only moves the camera when the player reaches its visible border.")]
            public PlayerFollowMode FollowMode = PlayerFollowMode.KeepPlayerInsideBorder;

            [Tooltip("After the player leaves the visible camera frame, this is the world-space distance the camera moves them back inside its border.")]
            [Min(0f)]
            public float BorderOffset = 1f;

            [Tooltip("In Center mode, the player may move this many metres from screen centre before the camera follows. Set to 0 to keep the player centred.")]
            [Min(0f)]
            public float CenterFollowDistance = 0f;

            [Tooltip("Allow the camera to follow the player across the screen horizontally.")]
            public bool FollowHorizontal = true;

            [Tooltip("Allow the camera to follow the player across the screen vertically.")]
            public bool FollowVertical = true;

        }

        public int Priority;

        [Header("Player Follow")]
        public PlayerFollowSettings PositionFollow = new PlayerFollowSettings { Enabled = true };
        public PlayerFollowSettings RotationFollow = new PlayerFollowSettings();

        private CinemachineVirtualCameraBase m_VirtualCam;
        private int m_TriggerEnterCount;
        private Dictionary<OnDisableNotifier, Collider> m_TargetTracked = new Dictionary<OnDisableNotifier, Collider>();
        private Action<OnDisableNotifier> m_TargetDisabledCallback;
        private Transform m_FollowTarget;
        private Collider m_FollowCollider;
        private Renderer[] m_FollowRenderers = Array.Empty<Renderer>();
        private float m_FollowVisualRadius;
        private Quaternion m_BaseCameraLocalRotation;
        private FixedCameraRotationFollowExtension m_RotationFollowExtension;

        internal Transform FollowTarget => m_FollowTarget;
        internal Collider FollowCollider => m_FollowCollider;
        internal Renderer[] FollowRenderers => m_FollowRenderers;
        internal float FollowVisualRadius => m_FollowVisualRadius;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_TargetDisabledCallback = OnTargetDisabled;

            m_VirtualCam = GetComponentInChildren<CinemachineVirtualCameraBase>(true);

            if (!m_VirtualCam)
            {
                Debug.LogError($"CameraPOV '{name}' requires a Cinemachine virtual camera in its children.", this);
                enabled = false;
                return;
            }

            m_BaseCameraLocalRotation = m_VirtualCam.transform.localRotation;
            m_RotationFollowExtension = m_VirtualCam.GetComponent<FixedCameraRotationFollowExtension>();
            if (!m_RotationFollowExtension)
                m_RotationFollowExtension = m_VirtualCam.gameObject.AddComponent<FixedCameraRotationFollowExtension>();
            m_RotationFollowExtension.Owner = this;

            var disableNotifier = GetComponentInChildren<OnDisableNotifier>();
            if (disableNotifier) 
            {
                disableNotifier.AddCallback((notif) => 
                {
                    m_TriggerEnterCount = 0;
                    CameraStack.Instance.RemoveCamera(this);
                });
            }

            Deactivate();
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            foreach(var entry in m_TargetTracked)
            {
                var disableNotifier = entry.Key;
                disableNotifier?.RemoveCallback(m_TargetDisabledCallback);
            }
            m_TargetTracked.Clear();
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<CameraPOVTarget>())
                return;

            if (!m_TargetTracked.ContainsValue(other)) 
            {
                var notifier = other.GetComponentInParent<OnDisableNotifier>();
                notifier.AddCallback(m_TargetDisabledCallback);
                m_TargetTracked.Add(notifier, other);
            }

            ++m_TriggerEnterCount;
            SetFollowTarget(GetFollowTransform(other));
            CameraStack.Instance.AddCamera(this, Priority);
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            if (!other.GetComponent<CameraPOVTarget>())
                return;

            if (m_TargetTracked.ContainsValue(other))
            {
                var notifier = other.GetComponentInParent<OnDisableNotifier>();
                RemoveCallback(notifier, other);
            }

            if (m_FollowTarget == GetFollowTransform(other))
                SetFollowTarget(GetTrackedTarget());

            --m_TriggerEnterCount;
            Debug.Assert(m_TriggerEnterCount >= 0, $"Trigger count went negative in CameraPOV {name}");
            if (m_TriggerEnterCount == 0)
            {
                CameraStack.Instance.RemoveCamera(this);
            }
        }

        // --------------------------------------------------------------------

        private void OnTargetDisabled(OnDisableNotifier notifier)
        {
            var collider = m_TargetTracked[notifier];
            RemoveCallback(notifier, collider);
            OnTriggerExit(collider);
        }

        // --------------------------------------------------------------------

        private void RemoveCallback(OnDisableNotifier notifier, Collider collider)
        {
            notifier.RemoveCallback(m_TargetDisabledCallback);
            m_TargetTracked.Remove(notifier);
        }

        // --------------------------------------------------------------------

        private Transform GetTrackedTarget()
        {
            foreach (var trackedTarget in m_TargetTracked.Values)
                return GetFollowTransform(trackedTarget);

            return null;
        }

        // --------------------------------------------------------------------

        private static Transform GetFollowTransform(Collider targetCollider)
        {
            // CameraPOVTarget is the deliberately placed, stable camera-follow marker.
            return targetCollider.transform;
        }

        // --------------------------------------------------------------------

        private void SetFollowTarget(Transform followTarget)
        {
            if (m_FollowTarget == followTarget)
                return;

            m_FollowTarget = followTarget;
            m_FollowCollider = m_FollowTarget ? m_FollowTarget.GetComponent<Collider>() : null;
            var player = m_FollowTarget ? m_FollowTarget.GetComponentInParent<PlayerActor>() : null;
            m_FollowRenderers = player
                ? player.GetComponentsInChildren<Renderer>()
                : Array.Empty<Renderer>();
            m_FollowVisualRadius = 0f;
            foreach (Renderer renderer in m_FollowRenderers)
            {
                if (renderer)
                    m_FollowVisualRadius = Mathf.Max(m_FollowVisualRadius, (renderer.bounds.center - m_FollowTarget.position).magnitude + renderer.bounds.extents.magnitude);
            }
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (GameManager.Exists && GameManager.Instance.Player)
            {
                CameraPOVTarget marker = GameManager.Instance.Player.GetComponentInChildren<CameraPOVTarget>();
                SetFollowTarget(marker ? marker.transform : GameManager.Instance.Player.transform);
            }

            if (!m_VirtualCam || !m_VirtualCam.gameObject.activeInHierarchy || !m_FollowTarget)
                return;

            FollowPlayer();
        }

        // --------------------------------------------------------------------

        private void FollowPlayer()
        {
            Transform cameraTransform = m_VirtualCam.transform;
            cameraTransform.localRotation = m_BaseCameraLocalRotation;
            bool cameraChanged = false;

            // Position follow only slides the camera across its screen plane.
            Quaternion cameraRotation = cameraTransform.rotation;
            Vector3 localPlayerPosition = Quaternion.Inverse(cameraRotation) * (m_FollowTarget.position - cameraTransform.position);

            if (localPlayerPosition.z <= 0.001f || !PositionFollow.Enabled)
            {
                SynchronizeCameraState(cameraTransform, cameraChanged);
                return;
            }

            Vector2 correction = GetFollowCorrection(localPlayerPosition, PositionFollow);
            if (correction.sqrMagnitude > 0f)
            {
                cameraTransform.position += cameraTransform.right * correction.x + cameraTransform.up * correction.y;
                cameraChanged = true;
            }

            SynchronizeCameraState(cameraTransform, cameraChanged);
        }

        // --------------------------------------------------------------------

        private void SynchronizeCameraState(Transform cameraTransform, bool cameraChanged)
        {
            if (cameraChanged && m_VirtualCam is CinemachineCamera cinematicCamera)
                cinematicCamera.ForceCameraPosition(cameraTransform.position, cameraTransform.rotation);
        }

        // --------------------------------------------------------------------

        private Vector2 GetFollowCorrection(Vector3 localPlayerPosition, PlayerFollowSettings settings)
        {
            return settings.FollowMode == PlayerFollowMode.KeepPlayerCentered
                ? GetCenterCorrection(localPlayerPosition, settings)
                : GetBorderCorrection(localPlayerPosition, settings);
        }

        // --------------------------------------------------------------------

        private static Vector2 GetCenterCorrection(Vector3 localPlayerPosition, PlayerFollowSettings settings)
        {
            return new Vector2(
                settings.FollowHorizontal ? OutsideDistance(localPlayerPosition.x, settings.CenterFollowDistance) : 0f,
                settings.FollowVertical ? OutsideDistance(localPlayerPosition.y, settings.CenterFollowDistance) : 0f);
        }

        // --------------------------------------------------------------------

        private Vector2 GetBorderCorrection(Vector3 localPlayerPosition, PlayerFollowSettings settings)
        {
            var cinematicCamera = m_VirtualCam as CinemachineCamera;
            if (cinematicCamera == null)
                return Vector2.zero;

            // The output camera is the authoritative source for what the player can see.
            // WorldToViewportPoint is normalized (0..1), so this check is independent of
            // resolution, aspect ratio, Cinemachine blending, and the POV's local hierarchy.
            Camera outputCamera = CameraSystem.Exists ? CameraSystem.Instance.MainCamera : null;
            if (outputCamera == null)
                return GetVirtualCameraBorderCorrection(localPlayerPosition, cinematicCamera, settings);

            Vector3 playerViewport = outputCamera.WorldToViewportPoint(m_FollowTarget.position);
            if (playerViewport.z <= 0.001f)
                return Vector2.zero;

            float halfHeight = outputCamera.orthographic
                ? outputCamera.orthographicSize
                : playerViewport.z * Mathf.Tan(outputCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * outputCamera.aspect;
            float horizontalMargin = Mathf.Clamp01(settings.BorderOffset / (halfWidth * 2f));
            float verticalMargin = Mathf.Clamp01(settings.BorderOffset / (halfHeight * 2f));

            GetPlayerViewportBounds(outputCamera, playerViewport, out Vector2 viewportMin, out Vector2 viewportMax);
            float horizontalOverflow = GetViewportOverflow(viewportMin.x, viewportMax.x, horizontalMargin, 1f - horizontalMargin);
            float verticalOverflow = GetViewportOverflow(viewportMin.y, viewportMax.y, verticalMargin, 1f - verticalMargin);

            return new Vector2(
                settings.FollowHorizontal ? horizontalOverflow * halfWidth * 2f : 0f,
                settings.FollowVertical ? verticalOverflow * halfHeight * 2f : 0f);
        }

        // --------------------------------------------------------------------

        private void GetPlayerViewportBounds(Camera outputCamera, Vector3 playerViewport, out Vector2 viewportMin, out Vector2 viewportMax)
        {
            viewportMin = new Vector2(playerViewport.x, playerViewport.y);
            viewportMax = viewportMin;

            foreach (Renderer playerRenderer in m_FollowRenderers)
            {
                if (!playerRenderer || !playerRenderer.enabled)
                    continue;

                Bounds bounds = playerRenderer.bounds;
                for (int x = 0; x <= 1; ++x)
                for (int y = 0; y <= 1; ++y)
                for (int z = 0; z <= 1; ++z)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 viewportCorner = outputCamera.WorldToViewportPoint(corner);
                    if (viewportCorner.z <= 0f)
                        continue;

                    viewportMin = Vector2.Min(viewportMin, viewportCorner);
                    viewportMax = Vector2.Max(viewportMax, viewportCorner);
                }
            }
        }

        // --------------------------------------------------------------------

        private static float GetViewportOverflow(float minimum, float maximum, float safeMinimum, float safeMaximum)
        {
            if (minimum < safeMinimum)
                return minimum - safeMinimum;
            if (maximum > safeMaximum)
                return maximum - safeMaximum;

            return 0f;
        }

        // --------------------------------------------------------------------

        private Vector2 GetVirtualCameraBorderCorrection(Vector3 localPlayerPosition, CinemachineCamera cinematicCamera, PlayerFollowSettings settings)
        {
            float halfHeight = cinematicCamera.Lens.Orthographic
                ? cinematicCamera.Lens.OrthographicSize
                : localPlayerPosition.z * Mathf.Tan(cinematicCamera.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * GetCameraAspectRatio();
            float horizontalLimit = Mathf.Max(0f, halfWidth - settings.BorderOffset);
            float verticalLimit = Mathf.Max(0f, halfHeight - settings.BorderOffset);

            return new Vector2(
                settings.FollowHorizontal ? OutsideDistance(localPlayerPosition.x, horizontalLimit) : 0f,
                settings.FollowVertical ? OutsideDistance(localPlayerPosition.y, verticalLimit) : 0f);
        }

        // --------------------------------------------------------------------

        private float GetCameraAspectRatio()
        {
            return CameraSystem.Exists && CameraSystem.Instance.MainCamera
                ? CameraSystem.Instance.MainCamera.aspect
                : 16f / 9f;
        }

        // --------------------------------------------------------------------

        internal static float OutsideDistance(float value, float limit)
        {
            return Mathf.Abs(value) > limit ? value - Mathf.Sign(value) * limit : 0f;
        }

        // --------------------------------------------------------------------

        // --------------------------------------------------------------------

        public void Activate()
        {
            if (m_VirtualCam)
            {
                m_VirtualCam.gameObject.SetActive(true);
                m_VirtualCam.enabled = true;
            }
        }

        // --------------------------------------------------------------------

        public void ResetPlayerFollowState()
        {
            m_RotationFollowExtension?.ResetFollowState();
        }

        // --------------------------------------------------------------------

        public void Deactivate()
        {
            if (m_VirtualCam)
            {
                m_VirtualCam.enabled = false;
                m_VirtualCam.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Runs rotation follow inside Cinemachine's pipeline so its result cannot be
    /// overwritten by the virtual-camera or brain update order.
    /// </summary>
    [DisallowMultipleComponent]
    public class FixedCameraRotationFollowExtension : CinemachineExtension
    {
        [NonSerialized] public CameraPOV Owner;
        private bool m_HasBorderOrientation;
        private Quaternion m_BorderOrientation;
        private Quaternion m_AppliedBorderOrientation;
        private float m_HorizontalBorderDirection;
        private float m_VerticalBorderDirection;
        private const float BorderRotationSpeed = 90f;

        public void ResetFollowState()
        {
            m_HasBorderOrientation = false;
            m_HorizontalBorderDirection = 0f;
            m_VerticalBorderDirection = 0f;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize
                || Owner == null
                || !Owner.RotationFollow.Enabled
                || Owner.FollowTarget == null)
                return;

            bool borderMode = Owner.RotationFollow.FollowMode == CameraPOV.PlayerFollowMode.KeepPlayerInsideBorder;
            if (!borderMode)
            {
                m_HasBorderOrientation = false;
                m_HorizontalBorderDirection = 0f;
                m_VerticalBorderDirection = 0f;
            }

            Vector3 cameraPosition = state.GetFinalPosition();
            Quaternion cameraRotation = borderMode && m_HasBorderOrientation
                ? m_AppliedBorderOrientation
                : state.GetFinalOrientation();
            Vector3 localPlayerPosition = Quaternion.Inverse(cameraRotation) * (Owner.FollowTarget.position - cameraPosition);
            if (localPlayerPosition.z <= 0.001f)
                return;

            // When the player is almost directly below a perspective camera, horizontal
            // screen direction is unstable.  Keep the established border angle instead.
            if (borderMode && localPlayerPosition.z < 0.5f)
            {
                if (m_HasBorderOrientation)
                    state.RawOrientation = m_AppliedBorderOrientation;
                return;
            }

            float aspect = CameraSystem.Exists && CameraSystem.Instance.MainCamera ? CameraSystem.Instance.MainCamera.aspect : 16f / 9f;
            float halfHeight = state.Lens.Orthographic
                ? state.Lens.OrthographicSize
                : localPlayerPosition.z * Mathf.Tan(state.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * aspect;
            float correctionX;
            float correctionY;
            if (!borderMode)
            {
                correctionX = CameraPOV.OutsideDistance(localPlayerPosition.x, Owner.RotationFollow.CenterFollowDistance);
                correctionY = CameraPOV.OutsideDistance(localPlayerPosition.y, Owner.RotationFollow.CenterFollowDistance);
            }
            else
            {
                float horizontalMargin = Mathf.Clamp01(Owner.RotationFollow.BorderOffset / (halfWidth * 2f));
                float verticalMargin = Mathf.Clamp01(Owner.RotationFollow.BorderOffset / (halfHeight * 2f));
                Vector3 playerViewport = ToViewport(cameraPosition, cameraRotation, state.Lens, aspect, Owner.FollowTarget.position);

                // Rotation follows the stable CameraPOVTarget marker rather than the
                // character's 3D bounds.  At close range those bounds can project
                // across both screen borders at once, which creates an impossible
                // side choice and makes a horizontal-only camera oscillate.
                float playerX = playerViewport.x;
                float playerY = playerViewport.y;
                correctionX = Owner.RotationFollow.FollowHorizontal
                    ? GetLockedOverflow(playerX, playerX, playerX, horizontalMargin, 1f - horizontalMargin, ref m_HorizontalBorderDirection) * halfWidth * 2f
                    : 0f;
                correctionY = Owner.RotationFollow.FollowVertical
                    ? GetLockedOverflow(playerY, playerY, playerY, verticalMargin, 1f - verticalMargin, ref m_VerticalBorderDirection) * halfHeight * 2f
                    : 0f;
            }

            float yaw = Owner.RotationFollow.FollowHorizontal ? Mathf.Atan2(correctionX, localPlayerPosition.z) * Mathf.Rad2Deg : 0f;
            float pitch = Owner.RotationFollow.FollowVertical ? Mathf.Atan2(correctionY, localPlayerPosition.z) * Mathf.Rad2Deg : 0f;

            if (Mathf.Approximately(yaw, 0f) && Mathf.Approximately(pitch, 0f))
            {
                if (borderMode && m_HasBorderOrientation)
                    state.RawOrientation = SmoothBorderOrientation(deltaTime);
                return;
            }

            Quaternion orientation = Quaternion.AngleAxis(yaw, state.ReferenceUp) * cameraRotation;
            orientation *= Quaternion.AngleAxis(-pitch, Vector3.right);
            if (borderMode)
            {
                if (!m_HasBorderOrientation)
                    m_AppliedBorderOrientation = state.RawOrientation;

                m_BorderOrientation = orientation;
                m_HasBorderOrientation = true;
                state.RawOrientation = SmoothBorderOrientation(deltaTime);
            }
            else
            {
                state.RawOrientation = orientation;
            }
        }

        private Quaternion SmoothBorderOrientation(float deltaTime)
        {
            float maximumAngleStep = deltaTime < 0f ? 180f : BorderRotationSpeed * deltaTime;
            m_AppliedBorderOrientation = Quaternion.RotateTowards(m_AppliedBorderOrientation, m_BorderOrientation, maximumAngleStep);
            return m_AppliedBorderOrientation;
        }

        private static void GetViewportBounds(Vector3 cameraPosition, Quaternion cameraRotation, LensSettings lens, float aspect, Renderer[] renderers, Vector3 fallbackPosition, out Vector2 minimum, out Vector2 maximum)
        {
            Vector3 fallback = ToViewport(cameraPosition, cameraRotation, lens, aspect, fallbackPosition);
            minimum = new Vector2(fallback.x, fallback.y);
            maximum = minimum;

            foreach (Renderer renderer in renderers)
            {
                if (!renderer || !renderer.enabled)
                    continue;

                Bounds bounds = renderer.bounds;
                for (int x = 0; x <= 1; ++x)
                for (int y = 0; y <= 1; ++y)
                for (int z = 0; z <= 1; ++z)
                {
                    Vector3 point = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 viewport = ToViewport(cameraPosition, cameraRotation, lens, aspect, point);
                    if (viewport.z <= 0f)
                        continue;

                    minimum = Vector2.Min(minimum, viewport);
                    maximum = Vector2.Max(maximum, viewport);
                }
            }
        }

        private static Vector3 ToViewport(Vector3 cameraPosition, Quaternion cameraRotation, LensSettings lens, float aspect, Vector3 point)
        {
            Vector3 localPoint = Quaternion.Inverse(cameraRotation) * (point - cameraPosition);
            float halfHeight = lens.Orthographic ? lens.OrthographicSize : localPoint.z * Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * aspect;
            return new Vector3(0.5f + localPoint.x / (halfWidth * 2f), 0.5f + localPoint.y / (halfHeight * 2f), localPoint.z);
        }

        private static void GetStableViewportBounds(Vector3 cameraPosition, Quaternion cameraRotation, LensSettings lens, float aspect, Vector3 center, float radius, out Vector2 minimum, out Vector2 maximum)
        {
            Vector3 viewportCenter = ToViewport(cameraPosition, cameraRotation, lens, aspect, center);
            minimum = new Vector2(viewportCenter.x, viewportCenter.y);
            maximum = minimum;
            if (radius <= 0f)
                return;

            // A cached enclosing cube stays constant when the model or its animation yaws.
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 viewport = ToViewport(cameraPosition, cameraRotation, lens, aspect, center + new Vector3(x, y, z) * radius);
                if (viewport.z <= 0f)
                    continue;

                minimum = Vector2.Min(minimum, viewport);
                maximum = Vector2.Max(maximum, viewport);
            }
        }

        private static float GetOverflow(float minimum, float maximum, float safeMinimum, float safeMaximum)
        {
            if (minimum < safeMinimum)
                return minimum - safeMinimum;
            if (maximum > safeMaximum)
                return maximum - safeMaximum;
            return 0f;
        }

        private static float GetLockedOverflow(float minimum, float maximum, float playerCenter, float safeMinimum, float safeMaximum, ref float direction)
        {
            float negativeOverflow = minimum < safeMinimum ? minimum - safeMinimum : 0f;
            float positiveOverflow = maximum > safeMaximum ? maximum - safeMaximum : 0f;

            if (Mathf.Approximately(negativeOverflow, 0f) && Mathf.Approximately(positiveOverflow, 0f))
                return 0f;

            if (direction > 0f)
            {
                // Keep following right while the player comes back toward the middle.
                // A momentary opposite overflow is common when the player is close to
                // or below a fixed camera, and must not be allowed to flip the camera.
                if (!Mathf.Approximately(positiveOverflow, 0f) || Mathf.Approximately(negativeOverflow, 0f))
                    return positiveOverflow;

                // Do not hand off merely because a wide character overlaps the
                // opposite edge.  The target marker itself must reach that border.
                // This keeps a chosen side locked through the full middle region.
                if (playerCenter <= safeMinimum)
                {
                    direction = -1f;
                    return negativeOverflow;
                }

                return 0f;
            }

            if (direction < 0f)
            {
                // Symmetric lock for a left-side follow.
                if (!Mathf.Approximately(negativeOverflow, 0f) || Mathf.Approximately(positiveOverflow, 0f))
                    return negativeOverflow;

                if (playerCenter >= safeMaximum)
                {
                    direction = 1f;
                    return positiveOverflow;
                }

                return 0f;
            }

            if (Mathf.Abs(positiveOverflow) >= Mathf.Abs(negativeOverflow))
            {
                direction = 1f;
                return positiveOverflow;
            }

            direction = -1f;
            return negativeOverflow;
        }

        private static void GetColliderViewportBounds(Vector3 cameraPosition, Quaternion cameraRotation, LensSettings lens, float aspect, Collider collider, Vector3 fallbackPosition, out Vector2 minimum, out Vector2 maximum)
        {
            Vector3 fallback = ToViewport(cameraPosition, cameraRotation, lens, aspect, fallbackPosition);
            minimum = new Vector2(fallback.x, fallback.y);
            maximum = minimum;
            if (!collider)
                return;

            Bounds bounds = collider.bounds;
            for (int x = 0; x <= 1; ++x)
            for (int y = 0; y <= 1; ++y)
            for (int z = 0; z <= 1; ++z)
            {
                Vector3 point = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                Vector3 viewport = ToViewport(cameraPosition, cameraRotation, lens, aspect, point);
                if (viewport.z <= 0f)
                    continue;

                minimum = Vector2.Min(minimum, viewport);
                maximum = Vector2.Max(maximum, viewport);
            }
        }
    }
}
