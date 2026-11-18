using UnityEngine;

namespace HouseToolkit
{
    [SelectionBase, DisallowMultipleComponent]
    public sealed class FixtureAttachment : MonoBehaviour
    {
        [HideInInspector] public FixtureMount mount;
        [HideInInspector] public int wallSide = 1;
        [HideInInspector] public float surfaceOffset;
        [HideInInspector] public bool emitterFitted;
        [SerializeField, HideInInspector] internal FixturePalette lightPalette;
        [SerializeField, HideInInspector] internal GameObject lightPrefab;
        [SerializeField, HideInInspector] internal Light emitter;
        [SerializeField, HideInInspector] internal bool overrideSpread;
        [SerializeField, HideInInspector] internal float localSpread;
        [System.Serializable] internal struct LocalValue { public bool enabled; public float value; }
        [SerializeField, HideInInspector] internal LocalValue intensity, range;
        [SerializeField, HideInInspector] internal bool lightingInitialized;
        [SerializeField, HideInInspector] internal float surfaceRotation;

        internal FixtureChoice LightPreset => lightPalette ? lightPalette.Find(lightPrefab, mount) : null;

        void OnEnable()
        {
            if (emitter && LightPreset is FixtureChoice preset) ApplyLighting(preset);
        }

        internal bool ApplyLighting(FixtureChoice preset)
        {
            float spread = overrideSpread ? localSpread : preset.Spread;
            if (mount == FixtureMount.Floor && emitter.type == LightType.Point)
            {
                float radius = Mathf.Max(.01f, spread);
                if (Mathf.Approximately(emitter.range, radius)) return false;
                emitter.range = radius;
                return true;
            }
            if (emitter.type != LightType.Spot || mount == FixtureMount.Floor) return false;
            bool changed = !lightingInitialized;
            if (!lightingInitialized)
            {
                // An older placement could have been tuned directly on its Light.
                float oldRange = preset.originalRange > 0 ? preset.originalRange : preset.lightRange;
                float oldIntensity = preset.originalRange > 0 ? preset.originalIntensity : preset.lightIntensity;
                range = new LocalValue { enabled = !Mathf.Approximately(emitter.range, oldRange), value = emitter.range };
                intensity = new LocalValue { enabled = !Mathf.Approximately(emitter.intensity, oldIntensity), value = emitter.intensity };
                lightingInitialized = true;
            }
            float reach = Mathf.Max(.01f, range.enabled ? range.value : preset.lightRange);
            float strength = Mathf.Max(0, intensity.enabled ? intensity.value : preset.lightIntensity);
            if (!Mathf.Approximately(emitter.range, reach)) { emitter.range = reach; changed = true; }
            if (!Mathf.Approximately(emitter.intensity, strength)) { emitter.intensity = strength; changed = true; }
            float angle = Mathf.Clamp(spread, 1f, 179f);
            float inner = angle * (100f / 155f);
            if (Mathf.Approximately(emitter.spotAngle, angle) && Mathf.Approximately(emitter.innerSpotAngle, inner)) return changed;
            emitter.spotAngle = angle;
            emitter.innerSpotAngle = inner;
            return true;
        }

        // The local transform stores the editable coordinates along the surface.
        internal Vector3 MountedPosition(Vector3 local)
        {
            if (mount == FixtureMount.Wall && transform.parent.TryGetComponent<HouseWall>(out var wall))
            {
                local.x = Mathf.Clamp(local.x, 0, wall.length);
                local.y = Mathf.Clamp(local.y, 0, wall.Floor.height);
                local.z = wallSide * wall.Thickness * .5f + surfaceOffset;
            }
            else if (transform.parent.TryGetComponent<HouseFloor>(out var floor))
                local.y = mount == FixtureMount.Ceiling ? floor.height : 0;
            return local;
        }
    }
}
