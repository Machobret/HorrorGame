using HorrorEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Enemies
{
    // Add a visual flinch after locomotion animation, without changing the player's state.
    [DefaultExecutionOrder(100)]
    public sealed class AcidDamageReaction : MonoBehaviour, IResetable
    {
        private const float Duration = 0.45f;
        private PlayerActor m_Player;
        private Health m_Health;
        private Transform m_Torso;
        private Quaternion m_BasePose;
        private Quaternion m_AppliedPose;
        private bool m_PoseApplied;
        private float m_Time = Duration;
        private ParticleSystem m_Splashes;

        private void Awake()
        {
            m_Player = GetComponent<PlayerActor>();
            m_Health = GetComponent<Health>();
            var animator = m_Player ? m_Player.MainAnimator : null;
            if (!animator) return;
            if (animator.isHuman)
                m_Torso = animator.GetBoneTransform(HumanBodyBones.Chest) ??
                    animator.GetBoneTransform(HumanBodyBones.Spine);
            else
                foreach (var bone in animator.GetComponentsInChildren<Transform>())
                    if (bone.name.ToLowerInvariant().Contains("spine"))
                    {
                        m_Torso = bone;
                        break;
                    }
        }

        public void Play(Material material)
        {
            if (!m_Player || m_Player.IsDisabled || !m_Health || m_Health.IsDead) return;
            m_Time = 0f;
            if (!m_Splashes && material) CreateSplashes(material);
            if (m_Splashes)
            {
                m_Splashes.Play();
                m_Splashes.Emit(18);
            }
        }

        private void Update()
        {
            RestorePose();
            if (PauseController.Exists && PauseController.Instance.IsPaused) return;
            m_Time += Time.deltaTime;
        }

        private void LateUpdate()
        {
            if (!m_Torso || m_Time >= Duration || !m_Player || m_Player.IsDisabled ||
                !m_Health || m_Health.IsDead || !m_Player.MainAnimator.isActiveAndEnabled) return;
            float phase = Mathf.Clamp01(m_Time / Duration);
            float strength = Mathf.Sin(phase * Mathf.PI);
            m_BasePose = m_Torso.localRotation;
            m_AppliedPose = m_BasePose * Quaternion.Euler(
                12f * strength, 0f, 5f * Mathf.Sin(phase * Mathf.PI * 4f) * strength);
            m_Torso.localRotation = m_AppliedPose;
            m_PoseApplied = true;
        }

        private void RestorePose()
        {
            // Do not overwrite a newer pose supplied by the Animator or another system.
            if (m_PoseApplied && m_Torso && Quaternion.Angle(m_Torso.localRotation, m_AppliedPose) < 0.01f)
                m_Torso.localRotation = m_BasePose;
            m_PoseApplied = false;
        }

        private void CreateSplashes(Material material)
        {
            var go = new GameObject("Acid damage splashes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 1.2f;
            m_Splashes = go.AddComponent<ParticleSystem>();
            m_Splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = m_Splashes.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new Color(0.65f, 1f, 0.12f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var emission = m_Splashes.emission;
            emission.rateOverTime = 0f;
            var shape = m_Splashes.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var fade = m_Splashes.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = m_Splashes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void OnReset()
        {
            RestorePose();
            m_Time = Duration;
            if (m_Splashes) m_Splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable() { OnReset(); }
        private void OnDestroy()
        {
            RestorePose();
            if (m_Splashes) Destroy(m_Splashes.gameObject);
        }
    }
}
