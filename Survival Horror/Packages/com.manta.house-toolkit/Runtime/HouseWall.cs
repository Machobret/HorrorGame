using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [Serializable]
    public sealed class PlacedOpening
    {
        public string id;
        public HouseInsert insert;
        public float distance;
        public float sill;
        public bool reversed;
        public bool empty;
        public bool HasPrefab => insert && insert.prefab && (insert.kind == OpeningKind.Panel || !empty);
        public Vector2 clearSize;
        public int finishTile;
        public Vector2 Size => insert is GarageDoorInsert garage ? garage.RoughSize(clearSize) : insert.OpeningSize;
        public PlacedOpening Copy() => (PlacedOpening)MemberwiseClone();
    }

    public readonly struct FittedOpening
    {
        public readonly PlacedOpening placement;
        public readonly WallSegment segment;
        public readonly float left, width, sill;
        public float Right => left + width;

        public FittedOpening(PlacedOpening placement, WallSegment segment)
        {
            this.placement = placement;
            this.segment = segment;
            width = segment ? segment.size.x : placement.Size.x;
            float center = segment && segment.aperture.size != Vector2.zero ? segment.aperture.center.x : width * 0.5f;
            left = placement.distance - center;
            sill = segment ? segment.aperture.y : placement.insert.kind == OpeningKind.Panel ? 0 : placement.sill;
        }
    }

    public enum PanelLayout { BestFit, EvenSpacing }
    // Values 0 and 1 preserve the choices serialized by earlier toolkit versions.
    public enum PanelLayoutOverride { Inherit = -1, BestFit = 0, EvenSpacing = 1 }

    public sealed class HouseWall : MonoBehaviour
    {
        [Min(0.01f)] public float length = 4;
        [Min(0)] public float thicknessOverride;
        public WallFamily familyOverride;
        [UnityEngine.Serialization.FormerlySerializedAs("panelLayout")]
        public PanelLayoutOverride panelLayoutOverride = PanelLayoutOverride.Inherit;
        public PanelLayout panelLayout
        {
            get => panelLayoutOverride == PanelLayoutOverride.Inherit
                ? (Family ? Family.defaultPanelLayout : PanelLayout.EvenSpacing)
                : (PanelLayout)panelLayoutOverride;
            set => panelLayoutOverride = (PanelLayoutOverride)value;
        }
        [HideInInspector] public int startCorner, endCorner;
        [HideInInspector] public bool interior;
        [HideInInspector] public float startPostInset, endPostInset;
        [HideInInspector] public float startCornerClearance, endCornerClearance;
        [HideInInspector] public Vector2[] slabSpans = Array.Empty<Vector2>();
        public bool boundaryOnly;
        public TrimFamily trimOverride;
        public bool suppressTrim;
        public TrimFamily Trim
        {
            get
            {
                var chosen = trimOverride ? trimOverride : Floor && Floor.trim ? Floor.trim : Floor && Floor.House ? Floor.House.trim : null;
                return suppressTrim || (chosen && chosen.omitTrim) ? null : chosen;
            }
        }
        public Material surfaceOverride;
        public Material positiveFinish, negativeFinish;
        public Material glassOverride;
        [HideInInspector] public List<PlacedOpening> openings = new List<PlacedOpening>();
        public HouseFloor Floor => GetComponentInParent<HouseFloor>();
        public WallFamily Family => familyOverride ? familyOverride : Floor ? Floor.FamilyFor(interior) : null;

        public float Thickness => ResolveThickness(Family,Floor,interior,thicknessOverride);

        internal static float ResolveThickness(WallFamily family,HouseFloor floor,bool interior,float local)
        {
            if(!family)return 0;
            if(family.IsComplex)return family.thickness;
            if(local>0)return local;
            if(floor)
            {
                float value=interior ? floor.interiorThickness : floor.exteriorThickness;
                if(value>0)return value;
                var house=floor.House;
                if(house)
                {
                    value=interior ? house.interiorThickness : house.exteriorThickness;
                    if(value>0)return value;
                }
            }
            return family.thickness;
        }

        public bool TryLayout(float height, List<FittedOpening> result, out string reason, PlacedOpening candidate = null)
        {
            return Validate(Family, length, height, openings, result, out reason, candidate, Thickness);
        }

        public static bool Validate(WallFamily family, float length, float height, List<PlacedOpening> openings,
            List<FittedOpening> result, out string reason, PlacedOpening candidate = null, float? resolvedThickness = null)
        {
            float depth=resolvedThickness ?? (family ? family.thickness : 0);
            result.Clear();
            if (!family || !float.IsFinite(length) || length <= 0 ||
                !float.IsFinite(height) || height <= 0 || !float.IsFinite(depth) || depth <= 0)
                return Fail("Choose a family and positive wall dimensions.", out reason);
            if (family.IsComplex && !family.panel) return Fail("Assign a repeating panel to the Complex family.", out reason);
            if (family.IsComplex)
            {
                foreach (var panel in family.panels)
                {
                    if (!panel || !ValidSegment(panel) || panel.aperture.size != Vector2.zero)
                        return Fail("Each repeating panel needs valid geometry and no opening.", out reason);
                    if (Mathf.Abs(panel.size.y - family.panel.size.y) > .0005f ||
                        (panel.mesh && Mathf.Abs(panel.mesh.bounds.size.z - family.thickness) > .0005f))
                        return Fail("Repeating panels in a family must share a height and thickness.", out reason);
                }
            }

            for (int i = 0; i < openings.Count + (candidate == null ? 0 : 1); i++)
            {
                var placement = i < openings.Count ? openings[i] : candidate;
                var insert = placement?.insert;
                if (!insert || !Positive(insert.OpeningSize) || !float.IsFinite(placement.distance))
                    return Fail("The insert needs a valid position and positive opening dimensions.", out reason);
                if (!family.SupportsInsertAsset(insert))
                    return Fail($"{family.name} does not offer {insert.name} as a placeable insert.", out reason);
                if (insert is GarageDoorInsert garage && (family.IsComplex || !garage.ValidSize(placement.clearSize) || placement.finishTile < 0 || placement.finishTile >= 16))
                    return Fail("Garage doors require a plain wall, positive clear dimensions and a valid finish.", out reason);
                if (depth < insert.minimumThickness ||
                    (insert.maximumThickness > 0 && depth > insert.maximumThickness))
                    return Fail($"{insert.name} at {placement.distance:0.##} m does not support a wall thickness of {depth:0.###} m.", out reason);
                var segment = family.FindSurround(insert);
                if (family.panel && !segment && insert.kind != OpeningKind.Panel)
                    return Fail("This family has no surround with a matching opening kind and size.", out reason);
                if (segment && (!ValidSegment(segment) || segment.aperture.xMin < 0 || segment.aperture.yMin < 0 ||
                    segment.aperture.xMax > segment.size.x + 0.0005f || segment.aperture.yMax > segment.size.y + 0.0005f))
                    return Fail("The surround opening must be inside its segment.", out reason);
                var fit = new FittedOpening(placement, segment);
                if (!float.IsFinite(fit.sill) || fit.sill < 0 || fit.sill + placement.Size.y > height + 0.0005f)
                    return Fail("The opening exceeds the floor height at this sill.", out reason);
                if (fit.left < -0.0005f || fit.Right > length + 0.0005f)
                    return Fail("The opening or its surround extends past the end of the wall.", out reason);
                result.Add(fit);
            }
            result.Sort((a, b) => a.left.CompareTo(b.left));
            for (int i = 1; i < result.Count; i++)
                if (result[i].left < result[i - 1].Right - 0.0005f)
                    return Fail("The opening overlaps another opening or its surround.", out reason);
            reason = null;
            return true;
        }

        static bool Positive(Vector2 size) => float.IsFinite(size.x) && float.IsFinite(size.y) && size.x > 0 && size.y > 0;
        static bool ValidSegment(WallSegment segment)
        {
            if (segment.mesh)
                return Positive(segment.size) && segment.mesh.isReadable && segment.mesh.subMeshCount >= 1 && segment.mesh.subMeshCount <= 2;
            if (!Positive(segment.size) || segment.parts == null || segment.parts.Length == 0) return false;
            foreach (var part in segment.parts)
            {
                var s = part.bounds.size;
                if (!Positive(s) || !float.IsFinite(s.z) || s.z <= 0) return false;
            }
            return true;
        }
        static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
