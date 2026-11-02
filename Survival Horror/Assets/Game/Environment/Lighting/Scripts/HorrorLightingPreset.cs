using UnityEngine;

namespace HorrorGame.Lighting
{
    [CreateAssetMenu(menuName = "Horror Game/Lighting Preset", fileName = "HorrorLightingPreset")]
    public sealed class HorrorLightingPreset : ScriptableObject
    {
        [Header("Mood")]
        public Color ambientSky = new Color(0.025f, 0.035f, 0.055f);
        public Color ambientEquator = new Color(0.015f, 0.018f, 0.024f);
        public Color ambientGround = new Color(0.004f, 0.004f, 0.006f);
        [Range(0f, 2f)] public float ambientIntensity = 0.32f;
        public Color fogColor = new Color(0.09f, 0.11f, 0.13f);
        [Range(0f, 0.1f)] public float fogDensity = 0.018f;

        [Header("Key light")]
        public Color keyColor = new Color(0.32f, 0.46f, 0.75f);
        [Range(0f, 3f)] public float keyIntensity = 0.28f;
        public Vector3 keyEuler = new Vector3(42f, -32f, 0f);
    }
}
