using UnityEngine;

namespace HorrorGame.Chapter1
{
    public sealed class CircuitDrivenLamp : CircuitDrivenObject
    {
        public Light[] Lights;
        public Renderer[] EmissiveRenderers;
        public Color OnEmission = new Color(1f, .62f, .18f);

        public override void Refresh()
        {
            bool powered = Powered;
            foreach (Light light in Lights) if (light) light.enabled = powered;
            foreach (Renderer renderer in EmissiveRenderers)
            {
                if (!renderer || !renderer.material.HasProperty("_EmissionColor")) continue;
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", powered ? OnEmission : Color.black);
            }
        }
    }
}
