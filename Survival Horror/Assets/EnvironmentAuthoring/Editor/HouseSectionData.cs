using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Uniform level measurements, with footprint coverage for open-sky rooms.
    internal sealed class HouseSectionData
    {
        internal readonly struct Range
        {
            internal readonly float Min, Max;
            internal bool Uniform => Mathf.Abs(Max - Min) < .0001f;
            internal Range(float min, float max) { Min = min; Max = max; }
            internal string Text => Uniform ? Metres(Min) : Metres(Min) + "â€“" + Metres(Max);
        }

        internal sealed class Row
        {
            internal HouseStack.Level Level;
            internal float Floor, Ceiling, Clearance;
            internal float NominalFloorSpacing;
            internal bool SelectedFloor, SelectedCeiling, HideFloor, HideCeiling;
            // Nominal ranges still frame the ruler when there is no overhead slab.
            // They must not be presented as an actual ceiling or measured clearance.
            internal bool HasCeiling = true, HasClearance = true, OpenSky;
        }

        internal readonly List<Row> Rows = new List<Row>();
        internal HouseBuilding Building;
        internal HouseStack Stack;
        internal HousePlan Plan;
        internal string RoomId, BoundaryId, RegionId;
        internal string Problem = "";

        internal static HouseSectionData Read(HousePlan plan, string roomId, HouseStack candidate = null,
            System.Func<HouseStack.Level, FloorPlan> planFor = null, string boundaryId = null, string regionId = null)
        {
            var data = new HouseSectionData { Plan = plan, Building = HouseBuilding.For(plan), RoomId = roomId };
            if (data.Building == null) return data;
            var building = data.Stack = candidate ?? data.Building.Data;
            planFor = planFor ?? (level => level.Plan.Data);
            var active = building.Find(plan);
            var graph = planFor(active);
            var room = graph.Cells.FirstOrDefault(r => r.Id == roomId);
            data.RoomId = room?.Id;
            // Room or segment selection needs polygon interpretation. Plain level browsing
            // stays cheap; the view retains this snapshot until authoring data changes.
            List<HouseSlabLayout.Patch> patches = null;
            bool surfaceSelected = boundaryId != null && regionId != null;
            if ((room != null || surfaceSelected) && !HouseSlabLayout.TryPatches(building, planFor, out patches, out data.Problem)) return data;
            var selectedSurface = surfaceSelected ? patches?.FirstOrDefault(p => p.Boundary.Id == boundaryId && p.Floor.Id == regionId) : null;
            if (selectedSurface != null) { data.BoundaryId = boundaryId; data.RegionId = regionId; }
            var selectedSurfaces = selectedSurface == null ? new HouseSlabLayout.Patch[0] : new[] { selectedSurface };
            var selectedFootprint = selectedSurface?.Shape;
            if (selectedSurface?.Upper == active)
            {
                var boundary = selectedSurface.Boundary;
                var lower = building.Find(boundary.LowerId);
                if (!HouseSurfaceAreas.TryRead(boundary, lower == null ? null : planFor(lower), planFor(active), out var areas, out data.Problem)) return data;
                var area = areas.FirstOrDefault(item => item.RegionId == regionId);
                if (area != null)
                {
                    selectedSurfaces = patches.Where(patch => patch.Boundary.Id == boundaryId && area.RegionId == patch.Floor.Id).ToArray();
                    selectedFootprint = area.Shape;
                }
            }
            foreach (var level in building.Levels.Where(l => l.Plan != null))
            {
                int index = building.Levels.IndexOf(level);
                var row = new Row { Level = level,
                    NominalFloorSpacing = building.Elevation(level) != 0 && index > 0
                        ? building.Elevation(level) - building.Elevation(building.Levels[index - 1]) : 0,
                    Floor = building.FloorElevation(level),
                    Ceiling = HouseVerticalLayout.NominalCeiling(building, level),
                    Clearance = building.WallHeight(level) };
                PathsD footprint = null;
                HouseSlabLayout.Patch[] ceilings = null;
                if (room != null)
                {
                    footprint = SlabGeometry.Shape(graph.GetCellBoundary(room));
                    if (level == active)
                    {
                        row.SelectedFloor = row.SelectedCeiling = true;
                        ceilings = patches.Where(p => p.Lower == level && p.LowerRoom == room.Id).ToArray();
                    }
                    else
                    {
                        row.SelectedCeiling = row.HideCeiling = patches.Any(p => p.Lower == level && p.Upper == active && p.UpperRoom == room.Id);
                        row.SelectedFloor = row.HideFloor = patches.Any(p => p.Lower == active && p.LowerRoom == room.Id && p.Upper == level);
                        if (row.HideFloor)
                            ceilings = patches.Where(p => p.Lower == level && p.LowerRoom != null
                                && SlabGeometry.Intersect(p.Shape, footprint).Count > 0).ToArray();
                    }
                }
                if (selectedSurface != null && (level == active || selectedSurface.Lower == level || selectedSurface.Upper == level))
                {
                    footprint = selectedFootprint;
                    ceilings = selectedSurface.Lower == level ? selectedSurfaces
                        : patches.Where(p => p.Lower == level && p.LowerRoom != null
                            && SlabGeometry.Intersect(p.Shape, footprint).Count > 0).ToArray();
                    row.SelectedFloor = selectedSurface.Upper == level;
                    row.SelectedCeiling = selectedSurface.Lower == level;
                    row.HideCeiling = selectedSurface.Lower == level && selectedSurface.Upper == active;
                    row.HideFloor = selectedSurface.Upper == level && selectedSurface.Lower == active;
                }
                if (ceilings != null)
                    ReadCoverage(row, ceilings, footprint, building);
                data.Rows.Add(row);
            }
            return data;
        }

        private static void ReadCoverage(Row row, HouseSlabLayout.Patch[] ceilings, PathsD footprint, HouseStack building)
        {
            row.HasCeiling = row.HasClearance = ceilings.Length > 0;
            if (row.HasCeiling)
            {
                // Every patch over this level shares one plane; only its coverage varies.
                row.Ceiling = HouseVerticalLayout.SlabAt(building, ceilings[0].WalkingHeight).Bottom;
                row.Clearance = row.Ceiling - row.Floor;
            }
            var coverage = SlabGeometry.Union(new PathsD(ceilings.SelectMany(patch => patch.Shape)));
            row.OpenSky = SlabGeometry.Subtract(footprint, coverage).Any(path => System.Math.Abs(Clipper.Area(path)) > .000001);
        }

        internal static string Metres(float value) => value.ToString("0.##", CultureInfo.InvariantCulture) + " m";
        internal float Minimum => Rows.Count == 0 ? 0 : Rows.Min(r => r.Floor - Stack.SlabThickness);
        internal float Maximum => Rows.Count == 0 ? 1 : Rows.Max(r => r.Ceiling + Stack.SlabThickness);
        internal float Y(float height, float panelHeight) => 32 + (Maximum - height) * Scale(panelHeight);
        internal float Scale(float panelHeight) => (panelHeight - 64) / Mathf.Max(.01f, Maximum - Minimum);
    }
}
