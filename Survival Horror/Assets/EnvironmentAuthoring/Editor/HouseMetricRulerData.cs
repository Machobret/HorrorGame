using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Geometry is read once when displayed inputs change, never while hovering or painting.
    internal sealed class HouseMetricRulerData
    {
        internal const float PixelsPerMetre = 72, Padding = 12, MinimumHoverHeight = 6;
        internal sealed class Band
        {
            internal HouseSectionData.Row Row;
            internal float Surface;
            internal float Thickness;
            internal bool Ceiling, Reference, Selected;
            internal string BoundaryId, RegionId;
            internal float Bottom => Ceiling ? Surface : Surface - Thickness;
            internal float Top => Ceiling ? Surface + Thickness : Surface;
            internal string Tooltip => HouseSceneUI.LevelName(Row.Level.Plan) + (Ceiling ? " \u00B7 ceiling" : " \u00B7 floor")
                + (Reference ? " default" : "") + "\nThickness: " + HouseSectionData.Metres(Thickness)
                + (Ceiling && Row.OpenSky ? "\nPartly open sky; ceiling height describes the covered area." : "");
        }
        internal sealed class Gap
        {
            internal float Bottom, Top;
            internal float Distance;
            internal bool PartlyOpenSky;
            internal string Tooltip => "Clear gap: " + HouseSectionData.Metres(Distance)
                + (PartlyOpenSky ? "\nMeasured only where an overhead slab covers the floor." : "");
        }
        internal readonly List<Band> Bands = new List<Band>();
        internal readonly List<Gap> Gaps = new List<Gap>();
        internal HouseSectionData Section;
        internal float Minimum, Maximum, FocusHeight;
        internal float Scale { get; private set; } = PixelsPerMetre;
        private float HalfSpan => Mathf.Max(Maximum - FocusHeight, FocusHeight - Minimum);
        internal float Height => HalfSpan * 2 * Scale + Padding * 2;
        internal float Y(float metres) => Height / 2 + (FocusHeight - metres) * Scale;
        internal bool FitHeight(float available)
        {
            // Fit the displayed levels by changing the one shared metre scale, never
            // by enlarging slabs or clipping/scrolling the top of the level away.
            float next = Mathf.Min(PixelsPerMetre, Mathf.Max(1, available - Padding * 2) / Mathf.Max(.01f, HalfSpan * 2));
            if (Mathf.Abs(next - Scale) < .001f) return false;
            Scale = next;
            return true;
        }

        internal static HouseMetricRulerData Read(HousePlan plan, string roomId, string boundaryId = null, string regionId = null)
        {
            var pending = HouseEditorSession.instance.Pending;
            var preview = pending.Preview;
            if (!pending.Valid || preview?.Building == null || preview.SourceBuilding != HouseBuilding.For(plan))
                return ReadLayout(plan, roomId, boundaryId: boundaryId, regionId: regionId);
            var result = ReadLayout(plan, roomId, preview.Building, preview.PlanFor, boundaryId, regionId);
            // Values come from the isolated candidate; interactions keep source IDs
            // and references. Temporary plans must never become an editor selection.
            foreach (var row in result.Section.Rows) row.Level = preview.SourceBuilding.Find(row.Level.Id);
            result.Section.Plan = plan;
            result.Section.Building = preview.SourceBuilding;
            return result;
        }

        private static HouseMetricRulerData ReadLayout(HousePlan plan, string roomId, HouseStack candidate = null,
            System.Func<HouseStack.Level, FloorPlan> planFor = null, string boundaryId = null, string regionId = null)
        {
            var result = new HouseMetricRulerData { Section = HouseSectionData.Read(plan, roomId, candidate, planFor, boundaryId, regionId) };
            var section = result.Section;
            if (section.Building == null || section.Rows.Count == 0) return result;
            int activeIndex = section.Rows.FindIndex(r => r.Level.Plan == plan);
            var active = section.Rows[activeIndex].Level;
            // Anchor the level's default midpoint, not the selected room's offset.
            // Equal space on either side leaves a missing neighbour's side empty.
            result.FocusHeight = section.Stack.FloorElevation(active) + section.Stack.WallHeight(active) / 2;
            // Include only the immediate neighbour on each side. Room replacements
            // and shared-boundary suppression still come from the same section data.
            var displayed = section.Rows.Skip(Mathf.Max(0, activeIndex - 1))
                .Take(activeIndex == 0 ? 2 : 3).ToArray();
            foreach (var row in displayed)
            {
                void Add(bool ceiling, bool reference)
                {
                    float nominal = ceiling ? HouseVerticalLayout.NominalCeiling(section.Stack, row.Level)
                        : section.Stack.FloorElevation(row.Level);
                    result.Bands.Add(new Band { Row = row, Ceiling = ceiling, Reference = reference,
                        Selected = !reference && (ceiling ? row.SelectedCeiling : row.SelectedFloor),
                        Surface = reference ? nominal : ceiling ? row.Ceiling : row.Floor,
                        Thickness = section.Stack.SlabThickness,
                        BoundaryId = !reference && (ceiling ? row.SelectedCeiling : row.SelectedFloor) ? section.BoundaryId : null,
                        RegionId = !reference && (ceiling ? row.SelectedCeiling : row.SelectedFloor) ? section.RegionId : null });
                }
                if (!row.HideFloor) Add(false, false);
                if (row.HasCeiling && !row.HideCeiling) Add(true, false);
                // Defaults remain faint reference positions only when selection displaced/replaced them.
                if (row.HideFloor || Mathf.Abs(row.Floor - section.Stack.FloorElevation(row.Level)) > .0001f) Add(false, true);
                if (row.HasCeiling && (row.HideCeiling || Mathf.Abs(row.Ceiling - HouseVerticalLayout.NominalCeiling(section.Stack, row.Level)) > .0001f)) Add(true, true);
                if (row.HasCeiling && row.HasClearance && row.Ceiling > row.Floor)
                    result.Gaps.Add(new Gap { Bottom = row.Floor, Top = row.Ceiling,
                        Distance = row.Clearance, PartlyOpenSky = row.OpenSky });
            }
            // One physical boundary serves two storeys. Draw it once, preferring
            // the active storey's target; shared bands are not duplicate slabs.
            var kept = new List<Band>();
            foreach (var band in result.Bands.OrderBy(b => b.Reference).ThenByDescending(b => b.Selected)
                .ThenBy(b => b.Row.Level.Plan != plan))
            {
                if (kept.Any(b => b.Reference == band.Reference
                    && Mathf.Abs(b.Bottom - band.Bottom) < .0001f && Mathf.Abs(b.Top - band.Top) < .0001f)) continue;
                kept.Add(band);
            }
            result.Bands.Clear(); result.Bands.AddRange(kept);
            // An open-sky room still needs room for its storey's navigation and ticks.
            // Nominal framing does not create a selectable ceiling or measured gap.
            result.Minimum = Mathf.Min(result.Bands.Min(b => b.Bottom), displayed.Min(row => row.Floor - section.Stack.SlabThickness));
            result.Maximum = Mathf.Max(result.Bands.Max(b => b.Top), displayed.Max(row => row.Ceiling + section.Stack.SlabThickness));
            return result;
        }

        internal Band HitBand(float y)
        {
            Band hit = null;
            float nearest = float.PositiveInfinity;
            foreach (var band in Bands.OrderBy(b => b.Reference).ThenBy(b => b.Row.Level.Plan != Section.Plan))
            {
                float top = Y(band.Top), bottom = Y(band.Bottom);
                float distance = y < top ? top - y : y > bottom ? y - bottom : 0;
                float allowance = Mathf.Max(0, (MinimumHoverHeight - (bottom - top)) / 2);
                if (distance > allowance || distance >= nearest) continue;
                nearest = distance; hit = band;
            }
            return hit;
        }
        internal Gap HitGap(float y) => Gaps.FirstOrDefault(g => y >= Y(g.Top) && y <= Y(g.Bottom));
    }
}
