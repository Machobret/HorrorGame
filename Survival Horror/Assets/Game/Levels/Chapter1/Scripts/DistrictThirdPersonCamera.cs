using System.Collections;
using HorrorEngine;
using Unity.Cinemachine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Scene-local third-person target; follows the registered player, including respawns.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineThirdPersonFollow))]
    public sealed class DistrictThirdPersonCamera : MonoBehaviour
    {
        public Transform FollowPivot;
        [Min(.5f)] public float TargetHeight = 1.35f;
        [Range(0, 45)] public float DownwardPitch = 12f;
        [Min(0)] public float TurnSmoothTime = .2f;

        PlayerActor player;
        GameManager manager;
        CinemachineCamera virtualCamera;
        float yaw, turnVelocity;

        IEnumerator Start()
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            while (!GameManager.Exists) yield return null;
            manager = GameManager.Instance;
            manager.OnPlayerRegistered.AddListener(Bind);
            if (manager.Player) Bind(manager.Player);
        }

        void Bind(PlayerActor actor)
        {
            player = actor;
            if (!player || !FollowPivot) return;
            yaw = player.transform.eulerAngles.y;
            turnVelocity = 0;
            UpdatePivot();
            virtualCamera.Follow = FollowPivot;
            virtualCamera.PreviousStateIsValid = false;
        }

        void LateUpdate()
        {
            if (!player || !FollowPivot) return;
            // Keep the camera still while inventory, dialogs or a closeup pauses gameplay.
            if (PauseController.Exists && PauseController.Instance.IsPaused) return;
            yaw = Mathf.SmoothDampAngle(yaw, player.transform.eulerAngles.y,
                ref turnVelocity, TurnSmoothTime, Mathf.Infinity, Time.deltaTime);
            UpdatePivot();
        }

        void UpdatePivot()
        {
            FollowPivot.SetPositionAndRotation(player.transform.position + Vector3.up * TargetHeight,
                Quaternion.Euler(DownwardPitch, yaw, 0));
        }

        void OnDestroy()
        {
            if (manager) manager.OnPlayerRegistered.RemoveListener(Bind);
        }
    }
}
