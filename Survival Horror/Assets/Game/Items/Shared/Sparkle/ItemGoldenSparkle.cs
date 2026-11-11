using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Items
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ItemGoldenSparkle : MonoBehaviour
    {
        public bool RequireRoomEntry = true;
        public Color Tint = new Color(1f, .73f, .25f, .5f);
        [Min(.01f)] public float Size = .12f;
        [Tooltip("Minimum billboard height as a fraction of the screen. Keeps the tiny bright centre readable at PSX resolution.")]
        [Range(0, .2f)] public float MinimumScreenSize = .08f;
        [Min(.05f)] public float PulseDuration = .8f;
        [Min(.1f)] public float PulseInterval = 3f;
        [Min(0)] public float FadeStartDistance = 3f;
        [Min(.1f)] public float MaximumDistance = 8f;
        [Tooltip("Optional viewer override. Otherwise uses the registered player.")]
        public Transform Viewer;

        ParticleSystem particles;
        bool roomVisible;
        float nextPulse;
        float previewUntil;
        ParticleSystemRenderer particleRenderer;
        public bool RoomVisible => roomVisible;
        public string VisibilityStatus { get; private set; } = "Enter Play mode to test room activation.";
        readonly Gradient fadeGradient = new Gradient();
        readonly GradientColorKey[] colors = { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) };
        readonly GradientAlphaKey[] alphas = { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) };

        void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            particleRenderer = GetComponent<ParticleSystemRenderer>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startSpeed = 0;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
        }

        void OnEnable()
        {
            if (particles) particles.Play();
        }

        public void SetRoomVisible(bool visible)
        {
            roomVisible = visible;
            nextPulse = Time.time;
            if (!visible && particles)
                particles.Clear(true);
        }

        public void Preview()
        {
            previewUntil = Time.time + 5f;
            nextPulse = Time.time;
        }

        void Update()
        {
            bool preview = Time.time < previewUntil;
            if (particleRenderer) particleRenderer.minParticleSize = MinimumScreenSize;
            if (!preview && RequireRoomEntry && !roomVisible)
            { VisibilityStatus = "Waiting for the trigger's entry timer."; return; }
            if (PauseController.Exists && PauseController.Instance.IsPaused)
            { VisibilityStatus = "Game is paused."; return; }
            Transform viewer = Viewer;
            if (!viewer && GameManager.Exists && GameManager.Instance.Player)
                viewer = GameManager.Instance.Player.transform;
            if (!viewer && !preview) { VisibilityStatus = "No registered player or Viewer."; return; }
            float distance = viewer ? Vector3.Distance(viewer.position, transform.position) : 0;
            float fade = preview ? 1f : 1f - Mathf.InverseLerp(Mathf.Min(FadeStartDistance, MaximumDistance - .01f), MaximumDistance, distance);
            VisibilityStatus = preview ? "Preview active (ignores trigger and distance for 5 seconds)."
                : fade <= 0 ? $"Out of range: {distance:F1} m (limit {MaximumDistance:F1} m)."
                : $"Active: {distance:F1} m away, visibility {fade:P0}.";
            // Update living particles as well, so walking away fades the current pulse.
            var tintModule = particles.colorOverLifetime;
            tintModule.enabled = true;
            alphas[1] = new GradientAlphaKey(fade, .3f);
            fadeGradient.SetKeys(colors, alphas);
            tintModule.color = fadeGradient;
            if (fade <= 0 || Time.time < nextPulse) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = Vector3.zero, velocity = Vector3.zero,
                startColor = Tint, startSize = Size, startLifetime = PulseDuration,
                rotation = Random.Range(-25f, 25f)
            };
            particles.Emit(emit, 1);
            nextPulse = Time.time + Mathf.Max(PulseDuration, PulseInterval);
        }

        void OnDisable()
        {
            roomVisible = false;
            if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
