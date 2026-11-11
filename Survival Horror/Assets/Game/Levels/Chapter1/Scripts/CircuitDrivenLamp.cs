using HorrorGame.Lighting;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    public sealed class CircuitDrivenLamp : CircuitDrivenObject
    {
        public Light[] Lights;
        public Renderer[] EmissiveRenderers;
        [Tooltip("Optional local fog clear volume. It is active only while this lamp is powered.")]
        public FogClearingLampLight FogClearVolume;
        public Color OnEmission = new Color(1f, .62f, .18f);

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Prefabs and placed lamps stay lit in the editor so their pool of
            // light can be composed. Play mode always calls Refresh via the
            // circuit base class, restoring the actual powered state.
            if (Application.isPlaying) return;
            foreach (Light light in Lights) if (light) light.enabled = true;
        }
#endif

        public override void Refresh()
        {
            bool powered = Powered;
            foreach (Light light in Lights) if (light) light.enabled = powered;
            if (FogClearVolume) FogClearVolume.SetPowered(powered);
            foreach (Renderer renderer in EmissiveRenderers)
            {
                if (!renderer || !renderer.material.HasProperty("_EmissionColor")) continue;
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", powered ? OnEmission : Color.black);
            }
        }
    }
}
