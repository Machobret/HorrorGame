using System.Collections;
using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MutantJumpOutZone : MonoBehaviour
    {
        [SerializeField] private MutantPursuit mutant;
        [SerializeField] private Transform landingPoint;
        [SerializeField, Min(.1f)] private float jumpTime = .38f;
        [SerializeField, Min(0f)] private float jumpHeight = .65f;
        [SerializeField, Min(.1f)] private float recoveryTime = .26f;

        private bool triggered;
        public bool HasTriggered => triggered;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void Awake()
        {
            if (mutant && mutant.gameObject != gameObject && !transform.IsChildOf(mutant.transform))
                mutant.gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;
            var player = other.GetComponentInParent<PlayerActor>();
            if (!player) return;
            if (!mutant || !landingPoint || mutant.gameObject == gameObject || transform.IsChildOf(mutant.transform))
            {
                Debug.LogWarning("Assign a separate scene mutant and a landing marker to the jump-out zone.", this);
                return;
            }

            var agent = mutant.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(landingPoint.position, out var floor, .45f, filter))
            {
                Debug.LogWarning("The mutant's landing marker must be on its NavMesh.", landingPoint);
                return;
            }

            triggered = true;
            StartCoroutine(Jump(floor.position, landingPoint.eulerAngles.y, agent, player.transform));
        }

        private IEnumerator Jump(Vector3 destination, float facing, NavMeshAgent agent, Transform player)
        {
            var root = mutant.transform;
            var controller = mutant.GetComponent<ActorStateController>();
            var animator = mutant.GetComponent<Actor>().MainAnimator;
            var start = root.position;
            var endRotation = Quaternion.Euler(0f, facing, 0f);
            Vector3 travel = Vector3.ProjectOnPlane(destination - start, Vector3.up);
            float side = Vector3.Dot(endRotation * Vector3.right, travel) < 0f ? -1f : 1f;

            agent.enabled = false;
            controller.enabled = false;
            mutant.enabled = false;
            var disabledColliders = new List<Collider>();
            foreach (var collider in mutant.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled) continue;
                collider.enabled = false;
                disabledColliders.Add(collider);
            }

            // MutantPursuit caches its rig in Awake. Disable the Animator after activation
            // so the procedural entrance can pose the same skeleton independently.
            root.gameObject.SetActive(true);
            // Pose the entrance from the same animated stance it returns to.
            PlayIdlePose(animator);
            var pose = new EntrancePose(animator ? animator.transform : root);
            if (animator) animator.enabled = false;

            try
            {
                float elapsed = 0f;
                while (elapsed < jumpTime + recoveryTime)
                {
                    float flight = Mathf.Clamp01(elapsed / jumpTime);
                    float recovery = Mathf.Clamp01((elapsed - jumpTime) / recoveryTime);
                    float crouch = flight < 1f ? Mathf.Lerp(.18f, 1f, flight * flight)
                        : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, 1f, recovery));
                    root.SetPositionAndRotation(
                        Vector3.Lerp(start, destination, flight) + Vector3.up * (4f * flight * (1f - flight) * jumpHeight),
                        endRotation * Quaternion.Euler(0f, side * 24f * (1f - flight), 0f));
                    pose.Apply(root, crouch, side * Mathf.Sin(flight * Mathf.PI));
                    yield return null;
                    if (!PauseController.Exists || !PauseController.Instance.IsPaused)
                        elapsed += Time.deltaTime;
                }
            }
            finally
            {
                if (root)
                {
                    root.SetPositionAndRotation(destination, endRotation);
                    pose.Restore();
                    if (animator)
                    {
                        animator.enabled = true;
                        // Evaluate the first idle frame now so the imported bind pose is never displayed.
                        PlayIdlePose(animator);
                    }
                    foreach (var collider in disabledColliders)
                        if (collider) collider.enabled = true;
                    agent.enabled = true;
                    if (agent.isOnNavMesh) agent.Warp(destination);
                    controller.enabled = true;
                    mutant.enabled = true;
                    if (player && agent.isOnNavMesh &&
                        mutant.TryGetComponent<EnemySensesController>(out var senses))
                    {
                        var alerted = mutant.GetComponentInChildren<EnemyStateAlerted>();
                        if (alerted)
                        {
                            senses.TrackScriptedTarget(player);
                            controller.SetState(alerted);
                        }
                    }
                }
            }
        }

        private static void PlayIdlePose(Animator animator)
        {
            int idleHash = Animator.StringToHash("Base Layer.EnemyIdle");
            if (!animator || !animator.HasState(0, idleHash)) return;
            animator.Play(idleHash, 0, 0f);
            animator.Update(0f);
        }

        private sealed class EntrancePose
        {
            private readonly Transform hips;
            private readonly Vector3 hipPosition;
            private readonly Transform[] joints;
            private readonly Quaternion[] rotations;

            public EntrancePose(Transform visual)
            {
                hips = visual.Find("Monster_Rig/Root/Hips");
                if (hips) hipPosition = hips.localPosition;
                joints = new[]
                {
                    hips?.Find("LeftUpperLeg"), hips?.Find("RightUpperLeg"),
                    hips?.Find("LeftUpperLeg/LeftLowerLeg"), hips?.Find("RightUpperLeg/RightLowerLeg"),
                    hips?.Find("Spine"),
                    hips?.Find("Spine/Chest/UpperChest/LeftShoulder/LeftUpperArm"),
                    hips?.Find("Spine/Chest/UpperChest/RightShoulder/RightUpperArm")
                };
                rotations = new Quaternion[joints.Length];
                for (int i = 0; i < joints.Length; i++)
                    if (joints[i]) rotations[i] = joints[i].localRotation;
            }

            public void Apply(Transform root, float crouch, float sideways)
            {
                Restore();
                if (!hips) return;
                hips.position -= root.up * (.34f * crouch);
                Bend(0, root.right, -46f * crouch - 14f * sideways);
                Bend(1, root.right, -46f * crouch + 14f * sideways);
                Bend(2, root.right, 76f * crouch);
                Bend(3, root.right, 76f * crouch);
                Bend(4, root.right, 22f * crouch);
                Bend(4, root.forward, -19f * sideways);
                Bend(5, root.forward, -26f - 20f * sideways);
                Bend(6, root.forward, 26f - 20f * sideways);
            }

            private void Bend(int index, Vector3 axis, float angle)
            {
                if (joints[index])
                    joints[index].rotation = Quaternion.AngleAxis(angle, axis) * joints[index].rotation;
            }

            public void Restore()
            {
                if (hips) hips.localPosition = hipPosition;
                for (int i = 0; i < joints.Length; i++)
                    if (joints[i]) joints[i].localRotation = rotations[i];
            }
        }
    }
}
