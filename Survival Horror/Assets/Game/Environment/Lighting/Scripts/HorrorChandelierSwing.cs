using UnityEngine;

namespace HorrorGame.Lighting
{
    /// <summary>Subtle pendulum motion for hanging lights; intended for a chandelier's root object.</summary>
    public sealed class HorrorChandelierSwing : MonoBehaviour
    {
        [Min(0f)] public float swingAngle = 9f;
        [Min(0f)] public float swingFrequency = 0.42f;
        [Min(0f)] public float swingRadius = 0.22f;
        [Range(0f, 1f)] public float secondarySway = 0.28f;
        public bool randomizePhase = true;

        private Quaternion restRotation;
        private Vector3 restPosition;
        private float phase;

        private void Awake()
        {
            restRotation = transform.localRotation;
            restPosition = transform.localPosition;
            phase = randomizePhase ? Random.value * Mathf.PI * 2f : 0f;
        }

        private void OnEnable()
        {
            restRotation = transform.localRotation;
            restPosition = transform.localPosition;
        }

        private void Update()
        {
            float time = Time.time * swingFrequency * Mathf.PI * 2f + phase;
            float x = Mathf.Sin(time) * swingAngle;
            float z = Mathf.Sin(time * 1.73f + .8f) * swingAngle * secondarySway;
            // The chandelier prefab's emitter is centered on this transform. Moving its
            // pivot as well as rotating it makes the sway visible and moves the light pool.
            transform.localPosition = restPosition + new Vector3(
                Mathf.Sin(time) * swingRadius,
                -Mathf.Abs(Mathf.Cos(time)) * swingRadius * .08f,
                Mathf.Sin(time * 1.73f + .8f) * swingRadius * secondarySway);
            transform.localRotation = restRotation * Quaternion.Euler(x, 0f, z);
        }
    }
}
