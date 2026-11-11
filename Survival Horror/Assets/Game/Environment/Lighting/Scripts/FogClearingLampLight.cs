using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Lighting
{
    /// <summary>Registers a powered lamp as a small local area where distance fog is reduced.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Horror Game/Lighting/Fog-Clearing Lamp Light")]
    public sealed class FogClearingLampLight : MonoBehaviour
    {
        private const int MaximumLights = 8;
        private static readonly List<FogClearingLampLight> ActiveLights = new();
        private static readonly Vector4[] Positions = new Vector4[MaximumLights];
        private static readonly Vector4[] Settings = new Vector4[MaximumLights];
        private static readonly int CountId = Shader.PropertyToID("_HorrorFogClearCount");
        private static readonly int PositionsId = Shader.PropertyToID("_HorrorFogClearPositions");
        private static readonly int SettingsId = Shader.PropertyToID("_HorrorFogClearSettings");
        private static readonly int ColorId = Shader.PropertyToID("_HorrorFogClearColor");
        private static readonly int DensityId = Shader.PropertyToID("_HorrorFogClearDensity");

        [Tooltip("The light whose circuit state drives this clear-fog volume.")]
        public Light SourceLight;
        [Min(.1f), Tooltip("World-space radius of the clearer air around this lamp.")]
        public float ClearRadius = 5f;
        [Range(0f, 1f), Tooltip("How much of the built-in distance fog is removed at the lamp.")]
        public float ClearStrength = .82f;

        private bool powered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobals()
        {
            ActiveLights.Clear();
            Shader.SetGlobalInt(CountId, 0);
        }

        private void Awake()
        {
            if (!SourceLight) SourceLight = GetComponentInChildren<Light>();
        }

        private void OnEnable()
        {
            if (!ActiveLights.Contains(this)) ActiveLights.Add(this);
            Publish();
        }

        private void OnDisable()
        {
            ActiveLights.Remove(this);
            Publish();
        }

        private void LateUpdate() => Publish();

        /// <summary>Called by the circuit lamp whenever its actual light state changes.</summary>
        public void SetPowered(bool value)
        {
            powered = value;
            Publish();
        }

        private static void Publish()
        {
            int count = 0;
            for (int i = ActiveLights.Count - 1; i >= 0 && count < MaximumLights; --i)
            {
                var clearLight = ActiveLights[i];
                if (!clearLight || !clearLight.isActiveAndEnabled || !clearLight.powered ||
                    !clearLight.SourceLight || !clearLight.SourceLight.enabled) continue;

                Vector3 position = clearLight.SourceLight.transform.position;
                Positions[count] = new Vector4(position.x, position.y, position.z, clearLight.ClearRadius);
                Settings[count] = new Vector4(clearLight.ClearStrength, 0f, 0f, 0f);
                ++count;
            }

            Shader.SetGlobalVectorArray(PositionsId, Positions);
            Shader.SetGlobalVectorArray(SettingsId, Settings);
            Shader.SetGlobalInt(CountId, count);
            Shader.SetGlobalColor(ColorId, RenderSettings.fogColor);
            Shader.SetGlobalFloat(DensityId, RenderSettings.fogDensity);
        }
    }
}
