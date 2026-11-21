using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Lighting
{
    [RequireComponent(typeof(FixtureAttachment))]
    public sealed class FixtureFlicker : MonoBehaviour
    {
        [SerializeField] private bool flickerDiffuser;
        [SerializeField] private bool stayDark;
        [SerializeField] private Vector2 quietSeconds = new Vector2(.2f, .75f);
        [SerializeField] private Vector2Int pulsesPerBurst = new Vector2Int(1, 3);
        [SerializeField, Range(0f, 1f)] private float dimLevel = .08f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Light emitter;
        private MeshRenderer diffuser;
        private MaterialPropertyBlock properties;
        private Color normalColor, normalEmission;
        private float normalIntensity, nextChange;
        private int pulsesLeft;
        private bool dimmed;
        private System.Random random;

        private void Start()
        {
            emitter = GetComponent<FixtureAttachment>().Emitter;
            if (!emitter) return;

            normalIntensity = emitter.intensity;
            if (flickerDiffuser)
            {
                diffuser = emitter.GetComponentInParent<MeshRenderer>();
                var material = diffuser ? diffuser.sharedMaterial : null;
                if (material && material.HasProperty(BaseColor) && material.HasProperty(EmissionColor))
                {
                    normalColor = material.GetColor(BaseColor);
                    normalEmission = material.GetColor(EmissionColor);
                    properties = new MaterialPropertyBlock();
                }
            }

            if (stayDark)
            {
                emitter.enabled = false;
                ApplyBrightness(0f);
                return;
            }

            random = new System.Random(GetInstanceID());
            ScheduleQuiet();
        }

        private void Update()
        {
            if (!emitter || stayDark || Time.time < nextChange) return;
            if (dimmed)
            {
                ApplyBrightness(1f);
                dimmed = false;
                if (pulsesLeft == 0) ScheduleQuiet();
                else nextChange = Time.time + Sample(.025f, .065f);
                return;
            }

            if (pulsesLeft == 0)
                pulsesLeft = random.Next(pulsesPerBurst.x, pulsesPerBurst.y + 1);
            pulsesLeft--;
            dimmed = true;
            ApplyBrightness(dimLevel);
            nextChange = Time.time + Sample(.035f, .085f);
        }

        private void ScheduleQuiet() => nextChange = Time.time + Sample(quietSeconds.x, quietSeconds.y);

        private float Sample(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void ApplyBrightness(float brightness)
        {
            emitter.intensity = normalIntensity * brightness;
            if (!diffuser || properties == null) return;
            diffuser.GetPropertyBlock(properties, 0);
            var color = normalColor * Mathf.Lerp(.23f, 1f, brightness);
            color.a = normalColor.a;
            properties.SetColor(BaseColor, color);
            properties.SetColor(EmissionColor, normalEmission * brightness);
            diffuser.SetPropertyBlock(properties, 0);
        }

        private void OnDisable()
        {
            if (!emitter || stayDark) return;
            ApplyBrightness(1f);
            dimmed = false;
            pulsesLeft = 0;
            ScheduleQuiet();
        }
    }
}
