using System;
using UnityEngine;

namespace HouseToolkit
{
    public enum FixtureMount { Wall, Floor, Ceiling }

    [Serializable]
    public sealed class FixtureChoice
    {
        public string label;
        public GameObject prefab;
        public FixtureMount mount;
        [Tooltip("Allow this decoration to mount on window and door insert faces.")] public bool allowOpeningSurface;
        [Tooltip("Clearance from the mount surface in metres.")] [Min(0)] public float surfaceClearance;
        [Tooltip("Allow this wall fixture to be placed on a ceiling as well.")] public bool wallAndCeiling;
        [Tooltip("Also mount this fixture upright on walls with half its depth recessed.")] public bool recessedWallMount;
        [Tooltip("Rotate the authored model into its installed orientation.")] public Vector3 modelRotation;
        [Tooltip("Add a light suited to the mounting surface. Zero keeps its authored lighting.")] [Min(0)] public float lightRange;
        [HideInInspector] public float previousLightIntensity;
        [Tooltip("Intensity of the light added by the toolkit.")] [Min(0)] public float lightIntensity = 1f;
        public bool initiallyOff;
        public Color lightColor = Color.white;
        [Tooltip("Material whose emissive pixels follow the light.")] public Material diffuserMaterial;
        [Range(0,1)] public float emitterOffset;
        [Tooltip("Offset direction in fixture-local space when freely placed.")] public Vector3 freeOffsetAxis = Vector3.up;
        [Tooltip("Outer beam angle of a generated wall or ceiling light.")] [Range(1, 179)] public float beamAngle = 155f;

        internal float Spread => mount == FixtureMount.Floor ? lightRange : beamAngle;
        internal bool Supports(FixtureMount surface) => surface == mount ||
            (recessedWallMount && surface == FixtureMount.Wall) ||
            (mount == FixtureMount.Wall && wallAndCeiling && surface == FixtureMount.Ceiling);
        internal FixtureMount HorizontalMount => wallAndCeiling ? FixtureMount.Ceiling : mount;
        [HideInInspector] public float originalRange, originalIntensity;
    }

    [CreateAssetMenu(menuName = "House Toolkit/Fixture Palette")]
    public sealed class FixturePalette : ScriptableObject
    {
        public FixtureChoice[] fixtures = Array.Empty<FixtureChoice>();

        internal FixtureChoice Find(GameObject prefab, FixtureMount mount)
        {
            if (!prefab || fixtures == null) return null;
            foreach (var entry in fixtures)
                if (entry != null && entry.prefab == prefab && entry.Supports(mount)) return entry;
            return null;
        }
    }
}
