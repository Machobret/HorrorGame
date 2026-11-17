using System;
using UnityEngine;

namespace HouseToolkit
{
    public enum RoadOverride { Inherit, On, Off }
    public enum RoadLines { None, Solid, Dashed }

    [Serializable]
    public class RoadCurb
    {
        public RoadOverride enabled;
        [Min(0)] public float width;
        public float height = -1;
        public Material material;
    }

    [CreateAssetMenu(menuName = "House Toolkit/Road Family")]
    public sealed class RoadFamily : ScriptableObject
    {
        [Min(.2f)] public float width = 6;
        [Min(0), Tooltip("Lanes travelling from the span start to its end.")] public int forwardLanes = 1;
        [Min(0), Tooltip("Lanes travelling from the end back to the start.")] public int reverseLanes = 1;
        internal int LaneCount => forwardLanes + reverseLanes;
        void OnValidate()
        {
            forwardLanes = Mathf.Max(0, forwardLanes);
            reverseLanes = Mathf.Max(0, reverseLanes);
            if (LaneCount == 0) forwardLanes = 1;
        }

        [Min(.1f), Tooltip("Repeat length for UV-mapped custom materials. World-space asphalt uses its material's Repeat metres.")] public float repeatLength = 6;
        public Material road, bareAsphalt, tJunction, xJunction;
        [ColorUsage(false), Tooltip("White leaves the asphalt unchanged. Used by the Road Surface shader.")]
        public Color wheelPathTint = new Color(.82f, .83f, .85f);
        [Min(0), Tooltip("Fade wheel paths to plain asphalt at open road ends over this distance. Zero disables the fade.")]
        public float wheelPathEndFade = 2;
        [Range(0, 1), Tooltip("Zero is fresh paint. Higher values expose more asphalt.")]
        public float paintWear = .5f;

        // The road shader reads linear tint from RGB and paint wear from alpha.
        internal Color32 SurfaceStyle
        {
            get
            {
                Color style = QualitySettings.activeColorSpace == ColorSpace.Linear ? wheelPathTint.linear : wheelPathTint;
                style.a = Mathf.Clamp01(paintWear);
                return style;
            }
        }

        public bool leftSidewalk = true, rightSidewalk = true;
        [Min(.1f)] public float sidewalkWidth = 1.5f;
        [Min(0)] public float sidewalkHeight = .15f;
        public Material sidewalk;
        public bool curbs = true;
        [Min(.01f)] public float curbWidth = .15f;
        [Tooltip("Negative inherits sidewalk height.")] public float curbHeight = -1;
        public Material curbMaterial, paint;
        [Tooltip("Material for crosswalks and stop lines. Empty uses white road paint.")]
        public Material crossingPaint;
        public bool edgeLines;
        [Min(.02f)] public float edgeLineWidth = .12f;
        [Min(0), Tooltip("Distance from the road boundary to the outside of the edge line, in metres.")]
        public float edgeLineInset = .3f;
        [Tooltip("Material for edge lines. Empty uses crosswalk / stop line paint.")]
        public Material edgePaint;
        public RoadMarkingSet markingSet;
        public RoadLines centerLine;
        [Min(.02f)] public float lineWidth = .12f;
        [Min(.1f)] public float dash = 2, gap = 2;

        static Material defaultYellow, defaultWhite, defaultAsphalt;
        internal Material Asphalt => road ? road : DefaultMaterial(ref defaultAsphalt, "HouseToolkitRoadAsphalt");

        internal Material CenterLinePaint => paint ? paint : DefaultMaterial(ref defaultYellow, "HouseToolkitRoadYellow");
        internal Material CrossingPaint => crossingPaint ? crossingPaint : DefaultMaterial(ref defaultWhite, "HouseToolkitRoadWhite");
        internal Material EdgePaint => edgePaint ? edgePaint : CrossingPaint;

        static Material DefaultMaterial(ref Material cached, string resource)
        {
            if (!cached) cached = Resources.Load<Material>(resource);
            return cached;
        }

        static RoadFamily fallback;
        public static RoadFamily Default
        {
            get
            {
                if (!fallback)
                {
                    fallback = CreateInstance<RoadFamily>();
                    fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallback;
            }
        }
    }
}
