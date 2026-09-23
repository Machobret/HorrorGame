using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Enemies
{
    [RequireComponent(typeof(Combatant))]
    public sealed class BruteAcidPool : AttackBase
    {
        [SerializeField] private AttackType m_BurstAttack;
        [SerializeField] private Material m_AcidMaterial;
        [SerializeField, Min(0.1f)] private float m_Radius = 2f;
        [SerializeField, Min(0.1f)] private float m_BurstRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float m_Lifetime = 8f;
        [SerializeField, Min(0.05f)] private float m_DamageInterval = 0.5f;
        [SerializeField] private LayerMask m_DamageMask = 512;
        [SerializeField] private LayerMask m_BlockerMask = 1;

        private readonly DamageableSorting m_Sorting = new DamageableSorting();
        private List<Damageable> m_Targets = new List<Damageable>();
        private BruteCombatController m_DamageSettings;
        public bool DamageEnabled => m_DamageSettings && m_DamageSettings.EnableDamage;
        private float m_Age;
        private float m_NextDamage;
        private bool m_Initialized;
        private Transform m_SourceRoot;

        public void Initialize(Vector3 burstPosition, CombatantFaction faction, Transform sourceRoot)
        {
            if (m_Initialized) return;
            m_Initialized = true;
            m_SourceRoot = sourceRoot;
            m_DamageSettings = sourceRoot ? sourceRoot.GetComponent<BruteCombatController>() : null;
            GetComponent<Combatant>().Faction = faction;
            base.OnEnable();
            CreateVisuals(burstPosition);
            var poolAttack = m_Attack;
            m_Attack = m_BurstAttack;
            ApplyDamage(burstPosition, m_BurstRadius);
            m_Attack = poolAttack;
            m_NextDamage = m_DamageInterval;
        }

        private void Update()
        {
            if (!m_Initialized || (PauseController.Exists && PauseController.Instance.IsPaused)) return;
            m_Age += Time.deltaTime;
            if (m_Age >= m_Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (m_Age >= m_NextDamage)
            {
                ApplyDamage(transform.position + Vector3.up * 1.1f, m_Radius);
                m_NextDamage = m_Age + m_DamageInterval;
            }

        }

        private void ApplyDamage(Vector3 center, float radius)
        {
            if (!m_Attack || !DamageEnabled) return;
            m_Targets.Clear();
            m_Sorting.Clear();
            foreach (var collider in Physics.OverlapSphere(center, radius, m_DamageMask, QueryTriggerInteraction.Collide))
            {
                if (!collider.TryGetComponent<Damageable>(out var damageable) || damageable.Owner == Owner)
                    continue;
                Vector3 point = collider.ClosestPoint(center + Vector3.up * 0.4f);
                if (IsBlocked(center + Vector3.up * 0.15f, point))
                    continue;
                m_Targets.Add(damageable);
            }
            // A character can have several damageable body regions; hit it once per tick.
            m_Sorting.SortAndGetImpacted(ref m_Targets, m_Attack);
            foreach (var target in m_Targets)
            {
                var health = target.GetComponentInParent<Health>();
                float previousHealth = health ? health.Value : 0f;
                Process(new AttackInfo
                {
                    Attack = this,
                    SuppressHitReaction = true,
                    Damageable = target,
                    ImpactPoint = target.transform.position,
                    ImpactDir = (target.transform.position - center).normalized
                });
                if (health && !health.IsDead && health.Value < previousHealth)
                {
                    var player = target.GetComponentInParent<PlayerActor>();
                    if (player)
                    {
                        var reaction = player.GetComponent<AcidDamageReaction>();
                        if (!reaction) reaction = player.gameObject.AddComponent<AcidDamageReaction>();
                        reaction.Play(m_AcidMaterial);
                    }
                }
            }
        }

        private bool IsBlocked(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude,
                         m_BlockerMask, QueryTriggerInteraction.Ignore))
            {
                if (!m_SourceRoot || !hit.transform.IsChildOf(m_SourceRoot)) return true;
            }
            return false;
        }

        private void CreateVisuals(Vector3 burstPosition)
        {
            CreateParticles("Acid mist burst", burstPosition, true);
            CreateParticles("Lingering acid mist", transform.position + Vector3.up * 1.1f, false);
        }

        private void CreateParticles(string label, Vector3 position, bool burst)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = burst ? 1f : Mathf.Max(0.1f, m_Lifetime - 2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(burst ? 0.6f : 0.02f, burst ? 1.2f : 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.58f, 1f, 0.12f, burst ? 0.75f : 0.55f);
            main.gravityModifier = 0f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = burst ? 0.35f : m_Radius * 0.65f;
            shape.scale = burst ? Vector3.one : new Vector3(1f, 0.45f, 1f);
            var emission = particles.emission;
            emission.rateOverTime = burst ? 0f : 36f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(burst ? 60 : 36)) });
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = m_AcidMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }
    }
}
