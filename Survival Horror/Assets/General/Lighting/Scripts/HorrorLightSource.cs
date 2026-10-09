using UnityEngine;

namespace HorrorGame.Lighting
{
    /// <summary>Optional, inexpensive instability for a practical horror light.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class HorrorLightSource : MonoBehaviour
    {
        [Min(0f)] public float flickerAmount = 0.12f;
        [Min(0f)] public float flickerSpeed = 8f;
        public bool animateFlicker = true;

        private Light source;
        private float baseIntensity;
        private float offset;

        private void Awake()
        {
            source = GetComponent<Light>();
            baseIntensity = source.intensity;
            offset = Random.value * 100f;
        }

        private void OnEnable()
        {
            if (source == null) source = GetComponent<Light>();
            baseIntensity = source.intensity;
        }

        private void Update()
        {
            if (!animateFlicker || flickerAmount <= 0f || source == null) return;

            float noise = Mathf.PerlinNoise(offset, Time.time * flickerSpeed) * 2f - 1f;
            source.intensity = Mathf.Max(0f, baseIntensity * (1f + noise * flickerAmount));
        }
    }
}
