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
        [Tooltip("Allow this surface decoration to mount on window and door insert faces.")] public bool allowOpeningSurface;
        [Tooltip("Small gap between the mounting surface and the model, in metres.")] [Min(0)] public float surfaceClearance;
        [Tooltip("Allow this wall fixture to be placed on a ceiling as well.")] public bool wallAndCeiling;
        [Tooltip("Rotate the authored model into its installed orientation.")] public Vector3 modelRotation;
        [Tooltip("Add a light suited to the mounting surface. Zero keeps its authored lighting.")] [Min(0)] public float lightRange;
        [HideInInspector] public float previousLightIntensity;
        [Tooltip("Intensity of the light added by the toolkit.")] [Min(0)] public float lightIntensity = 1f;
        [Tooltip("Outer beam angle of a generated wall or ceiling light.")] [Range(1, 179)] public float beamAngle = 155f;

        internal float Spread => mount == FixtureMount.Floor ? lightRange : beamAngle;
        internal bool Supports(FixtureMount surface) => surface == mount ||
            (mount == FixtureMount.Wall && wallAndCeiling && surface == FixtureMount.Ceiling);
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
