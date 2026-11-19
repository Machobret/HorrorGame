using UnityEngine;

namespace HorrorGame.Lighting
{
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer))]
    public sealed class StreetlightBeam : MonoBehaviour
    {
        static readonly int Tint = Shader.PropertyToID("_BeamTint");
        Light source;
        MeshRenderer beam;
        MaterialPropertyBlock properties;
        Color previous;

        void OnEnable()
        {
            source = GetComponentInParent<Light>();
            beam = GetComponent<MeshRenderer>();
            previous = Color.clear;
            LateUpdate();
        }

        void LateUpdate()
        {
            bool visible = source && source.isActiveAndEnabled && source.intensity > 0;
            if (beam.enabled != visible) beam.enabled = visible;
            if (!visible || previous == source.color) return;
            previous = source.color;
            properties ??= new MaterialPropertyBlock();
            beam.GetPropertyBlock(properties);
            properties.SetColor(Tint, previous);
            beam.SetPropertyBlock(properties);
        }

        void OnDisable() { if (beam) beam.enabled = false; }
    }
}
