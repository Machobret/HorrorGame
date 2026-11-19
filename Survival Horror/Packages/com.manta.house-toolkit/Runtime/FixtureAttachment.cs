using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    public sealed class FixtureAttachment : MonoBehaviour
    {
        [HideInInspector] public FixtureMount mount;
        [HideInInspector] public bool allowOpeningSurface;
        [HideInInspector] public int wallSide = 1;
        [HideInInspector] public float surfaceOffset;
        [HideInInspector] public bool allowOpeningSurface;
        [HideInInspector] public bool emitterFitted;
        [SerializeField, HideInInspector] internal FixturePalette lightPalette;
        [SerializeField, HideInInspector] internal GameObject lightPrefab;
        [SerializeField, HideInInspector] internal Light emitter;
        public bool HasLight => emitter != null;
        [SerializeField, HideInInspector] internal bool overrideSpread;
        [SerializeField, HideInInspector] internal float localSpread;
        [System.Serializable] internal struct LocalValue { public bool enabled; public float value; }
        [SerializeField, HideInInspector] internal LocalValue intensity, range;
        [SerializeField, HideInInspector] internal bool lightingInitialized;
        [SerializeField, HideInInspector] internal float surfaceRotation;
        [SerializeField, HideInInspector] internal float lightOffset;
        [SerializeField, HideInInspector] internal Vector3 appliedOffsetAxis;
        [SerializeField, HideInInspector] internal bool freePlacement, overrideColor, overridePower, overrideOffset, localOn = true;
        [SerializeField, HideInInspector] internal Color localColor = Color.white;
        [SerializeField, HideInInspector] internal int facadeEdge, storey;
        [SerializeField, HideInInspector] internal float edgeDistance, storeyHeight;
        [SerializeField, HideInInspector] bool offsetMigrated;
        static readonly int emissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock emissionBlock;
        bool hasEmissionState, lastOn;
        Color lastTint;
        bool powered=true;

        internal Vector3 LightOffsetDirection
        {
            get
            {
                if (freePlacement && LightPreset is FixtureChoice preset)
                    return preset.freeOffsetAxis.sqrMagnitude > .0001f ? preset.freeOffsetAxis.normalized : Vector3.up;
                return mount == FixtureMount.Wall ? Vector3.forward : mount == FixtureMount.Ceiling ? Vector3.down : Vector3.up;
            }
        }

        internal FixtureChoice LightPreset => lightPalette ? lightPalette.Find(lightPrefab, mount) : null;

        void OnEnable()
        {
            hasEmissionState = false;
            if (emitter && LightPreset is FixtureChoice preset)
            {
                FitLegacyPoint(out _);
                ApplyLighting(preset);
            }
        }

        // Older placements may have circuit references to their generated Light.
        internal bool FitLegacyPoint(out Light diffuser)
        {
            diffuser = null;
            if (!emitter || emitter.transform.parent != transform || emitter.name != "Light" || !lightPrefab) return false;
            var source = lightPrefab.GetComponentInChildren<Light>(true);
            if (!source || source.type != LightType.Point) return false;
            foreach (Transform child in transform)
            {
                if (child == emitter.transform) continue;
                diffuser = child.GetComponentInChildren<Light>(true);
                if (!diffuser || diffuser.type != LightType.Point || diffuser.name != source.name) continue;
                var center = transform.InverseTransformPoint(diffuser.transform.position + transform.TransformDirection(LightOffsetDirection) * lightOffset);
                if (!diffuser.enabled && emitter.type == LightType.Point && emitter.transform.localPosition == center &&
                    emitter.shadows == diffuser.shadows) return false;
                diffuser.enabled = false;
                emitter.type = LightType.Point;
                emitter.shadows = diffuser.shadows;
                emitter.transform.localPosition = center;
                appliedOffsetAxis=LightOffsetDirection;
                return true;
            }
            return false;
        }

        internal bool ApplyLighting(FixtureChoice preset)
        {
            float spread = overrideSpread ? localSpread : preset.beamAngle;
            bool changed = !lightingInitialized;
            if (!offsetMigrated)
            {
                overrideOffset |= lightOffset != 0;
                offsetMigrated = true;
                changed = true;
            }
            float desiredOffset = overrideOffset ? lightOffset : preset.emitterOffset;
            if (desiredOffset != lightOffset || lightOffset!=0 && appliedOffsetAxis!=LightOffsetDirection)
            {
                MoveEmitter(desiredOffset);
                changed = true;
            }
            var color = overrideColor ? localColor : preset.lightColor;
            bool on = powered && (overridePower ? localOn : !preset.initiallyOff);
            if (emitter.enabled != on) { emitter.enabled = on; changed = true; }
            if (emitter.color != color) { emitter.color = color; changed = true; }
            if (!lightingInitialized)
            {
                // An older placement could have been tuned directly on its Light.
                float oldRange = preset.originalRange > 0 ? preset.originalRange : preset.lightRange;
                float oldIntensity = preset.originalRange > 0 ? preset.originalIntensity : preset.lightIntensity;
                range = new LocalValue { enabled = !Mathf.Approximately(emitter.range, oldRange), value = emitter.range };
                intensity = new LocalValue { enabled = !Mathf.Approximately(emitter.intensity, oldIntensity), value = emitter.intensity };
                lightingInitialized = true;
            }
            float reach = Mathf.Max(.01f, range.enabled ? range.value :
                mount == FixtureMount.Floor && overrideSpread && emitter.type == LightType.Point ? localSpread : preset.lightRange);
            float strength = Mathf.Max(0, intensity.enabled ? intensity.value : preset.lightIntensity);
            if (!Mathf.Approximately(emitter.range, reach)) { emitter.range = reach; changed = true; }
            if (!Mathf.Approximately(emitter.intensity, strength)) { emitter.intensity = strength; changed = true; }
            var diffuser = emitter.GetComponentInParent<LampShadeEmission>();
            if (diffuser && diffuser.FollowsIntensity && diffuser.isActiveAndEnabled) diffuser.Sync(emitter);
            else ApplyEmission(on, color, preset.diffuserMaterial);
            if (emitter.type != LightType.Spot) return changed;
            float angle = Mathf.Clamp(spread, 1f, 179f);
            float inner = angle * (100f / 155f);
            if (Mathf.Approximately(emitter.spotAngle, angle) && Mathf.Approximately(emitter.innerSpotAngle, inner)) return changed;
            emitter.spotAngle = angle;
            emitter.innerSpotAngle = inner;
            return true;
        }

        internal void MoveEmitter(float offset)
        {
            var axis=LightOffsetDirection;
            var previous=appliedOffsetAxis.sqrMagnitude>.0001f ? appliedOffsetAxis : axis;
            emitter.transform.position += transform.TransformDirection(axis*offset-previous*lightOffset);
            appliedOffsetAxis=axis;
            lightOffset = offset;
        }

        public static void SetPower(Light light, bool value)
        {
            var fixture=light.GetComponentInParent<FixtureAttachment>();
            if (fixture && fixture.emitter==light && fixture.LightPreset is FixtureChoice preset)
            {
                fixture.powered=value;
                fixture.ApplyLighting(preset);
            }
            else light.enabled=value;
        }

        void ApplyEmission(bool on, Color tint, Material diffuser)
        {
            if (hasEmissionState && lastOn==on && lastTint==tint) return;
            hasEmissionState=true;
            lastOn=on;
            lastTint=tint;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (!mat || mat!=diffuser || !mat.HasProperty(emissionColor)) continue;
                    emissionBlock ??= new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(emissionBlock, i);
                    float luminance = mat.GetColor(emissionColor).maxColorComponent;
                    // A dark-by-default diffuser still has an authored mask when switched on.
                    emissionBlock.SetColor(emissionColor, on ? tint * (luminance > 0 ? luminance : .6f) : Color.black);
                    renderer.SetPropertyBlock(emissionBlock, i);
                }
            }
        }

        internal void FitFacade(FillerBuilding building, GroundEdge edge)
        {
            var delta = edge.b-edge.a;
            var tangent = delta.normalized;
            var p = edge.a + tangent * Mathf.Clamp(edgeDistance,0,delta.magnitude);
            var normal = new Vector3(tangent.y,0,-tangent.x)*wallSide;
            int level = Mathf.Clamp(storey,0,Mathf.Max(0,building.floors-1));
            float y = Mathf.Min(building.Elevation(level)+storeyHeight,building.Elevation(level+1));
            transform.SetLocalPositionAndRotation(new Vector3(p.x,y,p.y),Quaternion.LookRotation(normal) * Quaternion.AngleAxis(surfaceRotation,Vector3.forward));
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
