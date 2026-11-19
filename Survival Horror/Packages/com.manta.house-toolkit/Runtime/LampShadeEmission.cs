using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class LampShadeEmission : MonoBehaviour
    {
        [SerializeField] Light source;
        [SerializeField] Renderer shade;
        [SerializeField, Min(0)] int materialSlot;
        [SerializeField, Min(0), Tooltip("Light intensity at which the diffuser uses its authored brightness. Zero uses fixed brightness.")]
        float referenceIntensity;

        static readonly int emissionColor = Shader.PropertyToID("_EmissionColor");
        internal bool FollowsIntensity => referenceIntensity > 0;
        FixtureAttachment attachment;
        MaterialPropertyBlock block;
        Color lastColor;
        bool applied;

        void OnEnable()
        {
            OnTransformParentChanged();
            Sync();
        }

        void OnTransformParentChanged()
        {
            attachment = GetComponentInParent<FixtureAttachment>();
            applied = false;
        }

        void LateUpdate() => Sync();

        internal void Sync()
        {
            var fitted = attachment ? attachment.emitter : null;
            // The fixture palette drives fixed-brightness diffusers on mounted lights.
            if (fitted && !FollowsIntensity) return;
            Sync(fitted ? fitted : source);
        }

        internal void Sync(Light light)
        {
            if (!light || !shade) return;
            var color = light.isActiveAndEnabled ? light.color : Color.black;
            if (FollowsIntensity)
                color *= Mathf.Max(0, light.intensity) / referenceIntensity;
            if (applied && color == lastColor) return;

            var materials = shade.sharedMaterials;
            if ((uint)materialSlot >= (uint)materials.Length) return;
            var material = materials[materialSlot];
            if (!material || !material.HasProperty(emissionColor)) return;
            block ??= new MaterialPropertyBlock();
            shade.GetPropertyBlock(block, materialSlot);
            block.SetColor(emissionColor, color * material.GetColor(emissionColor).maxColorComponent);
            shade.SetPropertyBlock(block, materialSlot);
            lastColor = color;
            applied = true;
        }
    }
}
