using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // Share active flare positions with the localized lighting composite.
    public static class StylizedFlareLighting
    {
        const int MaximumLights = 8;
        static readonly List<SignalFlare> active = new List<SignalFlare>();
        static readonly Vector4[] lights = new Vector4[MaximumLights];
        static readonly Vector4[] settings = new Vector4[MaximumLights];
        static readonly int CountId = Shader.PropertyToID("_StylizedFlareCount");
        static readonly int LightsId = Shader.PropertyToID("_StylizedFlarePositions");
        static readonly int SettingsId = Shader.PropertyToID("_StylizedFlareSettings");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { active.Clear(); Shader.SetGlobalInt(CountId, 0); }
        public static void Register(SignalFlare flare) { if (!active.Contains(flare)) active.Add(flare); }
        public static void Remove(SignalFlare flare) { active.Remove(flare); Publish(); }
        public static void Publish()
        {
            int count = 0;
            for (int i = active.Count - 1; i >= 0 && count < MaximumLights; --i)
            {
                var flare = active[i];
                if (!flare || !flare.isActiveAndEnabled || !flare.FlareLight || !flare.FlareLight.enabled) continue;
                var p = flare.FlareLight.transform.position;
                lights[count] = new Vector4(p.x, p.y, p.z, Mathf.Min(5.5f, flare.LightRange));
                settings[count] = new Vector4(Mathf.Clamp01(flare.FlareLight.intensity / 12), 0, 0, 0);
                ++count;
            }
            Shader.SetGlobalVectorArray(LightsId, lights);
            Shader.SetGlobalVectorArray(SettingsId, settings);
            Shader.SetGlobalInt(CountId, count);
        }
    }
}
