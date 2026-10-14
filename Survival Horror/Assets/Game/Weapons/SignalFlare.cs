using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    public class SignalFlare : AttackBase
    {
        public LayerMask CollisionMask = 513;
        public float Radius = .04f;
        public float Gravity = 9.81f;
        public float Lifetime = 12;
        public float BurnDuration = 6;
        public float BurnDamagePerSecond = .25f;
        public bool EnableDamage = true;
        public bool BurnTargetCompletely = true;
        public Light FlareLight;
        public float LightIntensity = 12;
        public float LightRange = 8;
        [Min(1)] public float FlickerFrequency = 12;
        public Material GlowMaterial;
        private Transform shooter;
        private Vector3 velocity;
        private float age, burnTime, burnClock;
        private bool landed;
        private bool launched;
        private Damageable target;
        private Health targetHealth;
        private float effectiveBurnDamage;
        public bool HasLanded => landed;
        public float Age => age;
        public Vector3 Velocity => velocity;

        protected override void Awake()
        {
            base.Awake();
            if (FlareLight)
            {
                FlareLight.color = new Color(1, .008f, .005f);
                FlareLight.range = LightRange;
                FlareLight.intensity = LightIntensity;
                FlareLight.shadows = LightShadows.Hard;
            }
            if (GlowMaterial)
            {
                var glow = new GameObject("Red flare glow");
                glow.transform.SetParent(transform, false);
                var particles = glow.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.startLifetime = Lifetime; main.startSpeed = 0; main.startSize = .36f;
                main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 1;
                var emission = particles.emission; emission.rateOverTime = 0;
                var shape = particles.shape; shape.enabled = false;
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = GlowMaterial;
                particles.Emit(1);
            }
            var trail = GetComponent<TrailRenderer>();
            if (trail) { trail.time = .6f; trail.startWidth = .12f; }
        }

        protected override void OnEnable()
        {
            if (!launched) base.OnEnable();
            StylizedFlareLighting.Register(this);
        }

        private void OnDisable() { StylizedFlareLighting.Remove(this); }
        private void LateUpdate() { StylizedFlareLighting.Publish(); }

        public void Launch(Vector3 initialVelocity, Combatant source)
        {
            velocity = initialVelocity;
            shooter = source ? source.transform : null;
            // AttackBase caches its owner from the parent. Keep the actual shooter
            // as owner after detaching, so SenseDamage pursues the player.
            transform.SetParent(shooter, true);
            base.OnEnable();
            transform.SetParent(null, true);
            launched = true;
        }

        public override void StartAttack() { }
        public override void StopAttack() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            age += dt;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            if (!landed)
            {
                velocity += Vector3.down * Gravity * dt;
                Vector3 step = velocity * dt;
                RaycastHit? nearest = null;
                foreach (var hit in Physics.SphereCastAll(transform.position, Radius, step.normalized, step.magnitude, CollisionMask, QueryTriggerInteraction.Collide))
                {
                    if (hit.transform.IsChildOf(transform) || (shooter && hit.transform.IsChildOf(shooter))) continue;
                    var damageable = hit.collider.GetComponentInParent<Damageable>();
                    if (hit.collider.isTrigger && !damageable) continue;
                    if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
                }
                if (nearest.HasValue) Land(nearest.Value);
                else { transform.position += step; transform.rotation = Quaternion.LookRotation(velocity); }
            }
            if (landed && target && targetHealth && !targetHealth.IsDead && burnTime < BurnDuration)
            {
                float burnStep = Mathf.Min(dt, BurnDuration - burnTime);
                burnTime += burnStep;
                burnClock += burnStep;
                if (burnClock >= .5f || burnTime >= BurnDuration)
                {
                    if (EnableDamage) target.Damage(effectiveBurnDamage * burnClock, new AttackInfo
                    { Attack = this, Damageable = target, ImpactPoint = transform.position, ImpactDir = Vector3.up, SuppressHitReaction = true });
                    burnClock = 0;
                }
            }
            if (FlareLight)
            {
                // Hold discrete brightness levels rather than a smooth modern pulse.
                float frame = Mathf.Floor(age * Mathf.Max(1, FlickerFrequency));
                float flicker = Mathf.Round((1 + Mathf.Sin(frame * 2.3f) * .08f) * 16) / 16;
                float fade = Mathf.Ceil(Mathf.Clamp01((Lifetime - age) / 2) * 4) / 4;
                FlareLight.intensity = LightIntensity * flicker * fade;
            }
        }

        private void Land(RaycastHit hit)
        {
            landed = true;
            Vector3 normal = hit.normal.sqrMagnitude > .01f ? hit.normal : -velocity.normalized;
            Vector3 point = hit.distance > 0 ? hit.point : hit.collider.ClosestPoint(transform.position);
            transform.position = point + normal * Radius;
            target = hit.collider.GetComponentInParent<Damageable>();
            targetHealth = target ? target.GetComponentInParent<Health>() : null;
            var info = new AttackInfo { Attack = this, Damageable = target, ImpactPoint = point, ImpactDir = -normal };
            var impact = target && m_Attack ? m_Attack.GetImpact(target.Type) : null;
            bool allowed = impact != null;
            if (allowed && impact.Filters != null)
                foreach (var filter in impact.Filters) if (filter && !filter.Passes(info)) allowed = false;
            if (!allowed) { target = null; targetHealth = null; }
            if (targetHealth && !targetHealth.IsDead && EnableDamage && m_Attack)
                Process(info);
            effectiveBurnDamage = BurnTargetCompletely && targetHealth
                ? Mathf.Max(BurnDamagePerSecond, (targetHealth.Value + .01f) / Mathf.Max(.01f, BurnDuration))
                : BurnDamagePerSecond;
            transform.SetParent(hit.transform, true);
        }
    }
}
