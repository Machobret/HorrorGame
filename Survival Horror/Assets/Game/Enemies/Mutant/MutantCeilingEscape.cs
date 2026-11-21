using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Enemies
{
    // Plays on a visual copy, with no enemy AI, collision or save registration.
    public sealed class MutantCeilingEscape : MonoBehaviour
    {
        private const float JumpDuration = .55f;
        private const float LatchDuration = .28f;
        private const float ScrambleDuration = .42f;
        private const float PullThroughDuration = .46f;
        private const float ScrambleStart = JumpDuration + LatchDuration;
        private const float PullStart = ScrambleStart + ScrambleDuration;
        private Vector3 start;
        private Vector3 opening;
        private float ceiling;
        private float latchHeight;
        private float elapsed;
        private const float EscapeDuration = PullStart + PullThroughDuration;
        private const float ResidueLightDuration = 1.1f;
        private Light spotlight;
        private float originalIntensity;
        private Material dustMaterial;
        private bool effectsPlayed;
        private MutantShutterAftermath aftermath;
        private Transform hips, spine, leftArm, rightArm, leftElbow, rightElbow, leftLeg, rightLeg;
        private Transform head, leftKnee, rightKnee;

        public static MutantCeilingEscape Stage(GameObject prefab, Transform marker, Light spotlight,
            Material dustMaterial, Material floorBlood, GameObject dropSplash)
        {
            var actor = prefab.GetComponent<Actor>();
            var source = actor ? actor.MainAnimator : null;
            if (!source)
            {
                Debug.LogWarning("The shutter escape needs a monster prefab with a model Animator.", marker);
                return null;
            }

            var visual = Instantiate(source.gameObject, marker.position, marker.rotation);
            visual.name = "Mutant ceiling escape";
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            var animator = visual.GetComponent<Animator>();
            int idle = Animator.StringToHash("Base Layer.EnemyIdle");
            if (animator.HasState(0, idle))
            {
                animator.Play(idle, 0, 0f);
                animator.Update(0f);
            }
            var sequence = visual.AddComponent<MutantCeilingEscape>();
            sequence.enabled = false;
            sequence.spotlight = spotlight;
            if (spotlight) sequence.originalIntensity = spotlight.intensity;
            sequence.dustMaterial = dustMaterial;
            sequence.Initialize(marker.position);
            sequence.aftermath = MutantShutterAftermath.Create(marker, floorBlood, dropSplash);
            return sequence;
        }

        public void BeginClimb()
        {
            int baseColor = Shader.PropertyToID("_BaseColor");
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (!material || !material.HasProperty(baseColor)) continue;
                    renderer.GetPropertyBlock(properties, slot);
                    Color color = material.GetColor(baseColor);
                    properties.SetColor(baseColor, new Color(color.r * .78f, color.g * .78f, color.b * .78f, color.a));
                    renderer.SetPropertyBlock(properties, slot);
                }
            }
            GetComponent<Animator>().enabled = false;
            enabled = true;
        }

        private void Initialize(Vector3 position)
        {
            start = position;
            opening = spotlight ? spotlight.transform.position : start;
            Vector3 probe = opening;
            probe.y = start.y + 1.15f;
            ceiling = Physics.Raycast(probe, Vector3.up, out var hit, 4f, 1, QueryTriggerInteraction.Ignore)
                ? hit.point.y : spotlight ? opening.y + .15f : start.y + 2.7f;
            latchHeight = (spotlight ? Mathf.Min(opening.y, ceiling) : ceiling) - .27f;
            opening.y = ceiling - .12f;

            hips = transform.Find("Monster_Rig/Root/Hips");
            if (!hips) return;
            spine = hips.Find("Spine");
            head = hips.Find("Spine/Chest/UpperChest/Neck/Head");
            const string shoulders = "Spine/Chest/UpperChest/";
            leftArm = hips.Find(shoulders + "LeftShoulder/LeftUpperArm");
            rightArm = hips.Find(shoulders + "RightShoulder/RightUpperArm");
            leftElbow = leftArm ? leftArm.Find("LeftLowerArm") : null;
            rightElbow = rightArm ? rightArm.Find("RightLowerArm") : null;
            leftLeg = hips.Find("LeftUpperLeg");
            rightLeg = hips.Find("RightUpperLeg");
            leftKnee = hips.Find("LeftUpperLeg/LeftLowerLeg");
            rightKnee = hips.Find("RightUpperLeg/RightLowerLeg");
        }

        private void Update()
        {
            if (PauseController.Exists && PauseController.Instance.IsPaused) return;
            elapsed += Time.deltaTime;
            if (spotlight && elapsed >= JumpDuration && !effectsPlayed)
            {
                float flickerTime = elapsed - JumpDuration;
                float flicker = Mathf.Sin(flickerTime * 37f) + .55f * Mathf.Sin(flickerTime * 71f);
                spotlight.intensity = originalIntensity * (flicker < -.25f ? .05f : 1f);
            }
            float jump = Mathf.Clamp01(elapsed / JumpDuration);
            float latch = Mathf.Clamp01((elapsed - JumpDuration) / LatchDuration);
            float scramble = Mathf.Clamp01((elapsed - ScrambleStart) / ScrambleDuration);
            float pull = Mathf.Clamp01((elapsed - PullStart) / PullThroughDuration);
            if (pull >= 1f)
            {
                if (!effectsPlayed)
                {
                    effectsPlayed = true;
                    foreach (var renderer in GetComponentsInChildren<Renderer>())
                        renderer.enabled = false;
                    // Start below the fixture so the debris is inside its downward beam.
                    Vector3 releasePoint = opening;
                    if (spotlight)
                        releasePoint.y = Mathf.Min(releasePoint.y, spotlight.transform.position.y - .35f);
                    EmitResidue(releasePoint, false);
                    EmitResidue(releasePoint, true);
                    if (aftermath) aftermath.ScheduleDrop(opening);
                }
                float residueTime = elapsed - EscapeDuration;
                if (spotlight)
                    spotlight.intensity = originalIntensity *
                        (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, ResidueLightDuration, residueTime)));
                if (residueTime >= ResidueLightDuration)
                {
                    if (spotlight)
                    {
                        spotlight.intensity = originalIntensity;
                        spotlight.enabled = false;
                    }
                    Destroy(gameObject);
                }
                return;
            }

            float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .85f, jump));
            float recoil = Mathf.Sin(latch * Mathf.PI * 3f) * Mathf.Pow(1f - latch, 2f);
            float rake = Mathf.Sin(scramble * Mathf.PI * 2f);
            float leftStep = Mathf.Max(0f, rake);
            float rightStep = Mathf.Max(0f, -rake);
            float hangHeight = Mathf.Max(start.y + .25f, latchHeight - 1.95f);
            float height = Mathf.Lerp(start.y, hangHeight, jump * (2f - jump)) +
                .13f * Mathf.SmoothStep(0f, 1f, scramble) - .21f * recoil +
                Mathf.SmoothStep(0f, 1f, pull) * (ceiling - hangHeight + .22f);
            Vector3 position = Vector3.Lerp(start, opening, Mathf.SmoothStep(0f, 1f, jump));
            position.y = height;
            transform.position = position;

            float tuck = Mathf.Sin(jump * Mathf.PI) * 24f;
            Pose(spine, spineRest, -11f * reach + 18f * recoil + 14f * pull, 10f * rake);
            Pose(head, headRest, -32f * reach + 9f * recoil, 5f * rake);
            Pose(leftLeg, leftLegRest, -tuck - 52f * leftStep - 20f * pull + 12f * recoil);
            Pose(rightLeg, rightLegRest, -.65f * tuck - 52f * rightStep - 12f * pull + 12f * recoil);
            Pose(leftKnee, leftKneeRest, tuck + 72f * leftStep + 17f * pull);
            Pose(rightKnee, rightKneeRest, .65f * tuck + 72f * rightStep + 23f * pull);
            Reach(leftArm, leftElbow, reach, leftStep, -1f);
            Reach(rightArm, rightElbow, reach, rightStep, 1f);
        }

        private void OnDestroy()
        {
            if (aftermath && !effectsPlayed) Destroy(aftermath.gameObject);
        }

        private void EmitResidue(Vector3 opening, bool grit)
        {
            if (!dustMaterial) return;
            var effect = new GameObject(grit ? "Falling ceiling grit" : "Ceiling dust");
            effect.transform.position = opening;
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.duration = .2f;
            main.startLifetime = grit ? 1.35f : 1.85f;
            main.gravityModifier = grit ? .85f : .08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMaterial;
            if (!grit)
            {
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.7f));
            }

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .35f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            particles.Play();
            for (int i = 0; i < (grit ? 16 : 10); i++)
            {
                Vector2 scatter = Random.insideUnitCircle;
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = opening + new Vector3(scatter.x, 0f, scatter.y) * .22f,
                    velocity = new Vector3(scatter.x, grit ? -.9f : -.6f, scatter.y) * (grit ? 1.4f : .9f),
                    startSize = grit ? Random.Range(.045f, .10f) : Random.Range(.28f, .54f),
                    startColor = grit ? new Color(.65f, .51f, .36f, .9f) : new Color(.84f, .76f, .63f, .5f)
                }, 1);
            }
        }

        private Quaternion spineRest, leftLegRest, rightLegRest;
        private Quaternion headRest, leftKneeRest, rightKneeRest;

        private void Start()
        {
            if (spine) spineRest = spine.localRotation;
            if (leftLeg) leftLegRest = leftLeg.localRotation;
            if (rightLeg) rightLegRest = rightLeg.localRotation;
            if (head) headRest = head.localRotation;
            if (leftKnee) leftKneeRest = leftKnee.localRotation;
            if (rightKnee) rightKneeRest = rightKnee.localRotation;
        }

        private void Pose(Transform bone, Quaternion rest, float pitch, float yaw = 0f)
        {
            if (!bone) return;
            bone.localRotation = rest;
            bone.rotation = Quaternion.AngleAxis(yaw, transform.up) *
                Quaternion.AngleAxis(pitch, transform.right) * bone.rotation;
        }

        private void Reach(Transform arm, Transform elbow, float jump, float rake, float side)
        {
            if (!arm || !elbow) return;
            Vector3 reach = transform.up + transform.forward * (.18f + .8f * rake) +
                transform.right * (.25f * side);
            Vector3 desired = Vector3.Lerp(transform.forward, reach, jump);
            arm.rotation = Quaternion.FromToRotation(elbow.position - arm.position, desired) * arm.rotation;
        }
    }
}
