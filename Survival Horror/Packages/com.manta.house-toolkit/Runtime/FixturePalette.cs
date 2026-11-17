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
        [Tooltip("Rotate the authored model into its installed orientation.")] public Vector3 modelRotation;
        [Tooltip("Add a point light to a visual-only prefab. Zero keeps its authored lighting.")] [Min(0)] public float lightRange;
        [Tooltip("Intensity of the point light added by the toolkit.")] [Min(0)] public float lightIntensity = 1f;
    }

    [CreateAssetMenu(menuName = "House Toolkit/Fixture Palette")]
    public sealed class FixturePalette : ScriptableObject
    {
        public FixtureChoice[] fixtures = Array.Empty<FixtureChoice>();
    }
}
