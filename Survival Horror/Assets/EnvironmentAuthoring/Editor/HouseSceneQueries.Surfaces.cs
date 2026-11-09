using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseSceneQueries
    {
        internal readonly struct FloorBand
        {
            internal readonly HouseEditSelection.Kind Kind;
            internal readonly string Id, BoundaryId;
            internal readonly Vector3 Start, End;
            internal FloorBand(HouseEditSelection.Kind kind, string id, string boundary, Vector3 start, Vector3 end)
            { Kind = kind; Id = id; BoundaryId = boundary; Start = start; End = end; }
        }

        private static PathsD FloorCoverage(HousePlan plan, HouseEditPreview preview = null)
        {
            bool candidate = preview?.Valid == true;
            if (!candidate && geometry != null && geometry.FloorCoverage.TryGetValue(plan, out var cached)) return cached;
            var building = HouseBuilding.For(plan);
            var stack = candidate ? preview.Building : building?.Data;
            var level = candidate ? preview.Level : building?.Find(plan);
            var boundary = level == null ? null : stack.Below(level);
            if (boundary == null) return new PathsD();
            var patches = candidate ? preview.SurfacePatches : BoundaryPatches(building, boundary.Id);
            // A lower-only patch is a roof, not coverage of the floor being edited.
            var coverage = SlabGeometry.Subtract(SlabGeometry.Union(new PathsD(patches
                .Where(patch => patch.Upper == level && patch.UpperRoom != null && !patch.Floor.CutAway).SelectMany(patch => patch.Shape))), SlabGeometry.Cuts(boundary));
            if (!candidate) geometry?.FloorCoverage.Add(plan, coverage);
            return coverage;
        }

        internal static bool OnEditableFloor(HousePlan plan, Vector2 point, HouseEditPreview preview = null)
            => OnFloor(FloorCoverage(plan, preview), point);

        // Selected source boundaries remain editable after their floor disappears.
        // This does not expose arbitrary control points inside independent holes.
        internal static bool DividerHandleOnFloor(HousePlan plan, HouseStack.Boundary boundary, Vector2 point, HouseEditPreview preview = null)
        {
            if (OnEditableFloor(plan, point, preview)) return true;
            var patches = preview?.Valid == true ? preview.SurfacePatches : BoundaryPatches(HouseBuilding.For(plan), boundary.Id);
            return patches.Any(patch => patch.Boundary.Id == boundary.Id && patch.Floor.CutAway
                && SlabGeometry.Contains(patch.Shape, point));
        }

        internal static IEnumerable<SlabEdge> ConstructionPieces(HousePlan plan, Vector2 start, Vector2 end, HouseEditPreview preview = null)
        {
            foreach (var piece in SurfacePartition.ClipLine((start, end), FloorCoverage(plan, preview)))
                foreach (var edge in RoomBoundaryPieces(plan, piece.a, piece.b)) yield return edge;
        }

        // A room outline can establish new coverage. Its unfinished path must not
        // disappear merely because it is outside the floor it will eventually enclose.
        internal static IEnumerable<SlabEdge> RoomBoundaryPieces(HousePlan plan, Vector2 start, Vector2 end)
        {
            yield return new SlabEdge { Start = plan.ToWorld(start, 0), End = plan.ToWorld(end, 0), Height = 0 };
        }

        // Draw and pick the same uniform floor lines. Dividers clip to usable slab;
        // room boundaries may establish new coverage. No duplicate derived seam targets.
        internal static IReadOnlyList<FloorBand> FloorBands(HousePlan plan)
        {
            if (geometry != null && geometry.Bands.TryGetValue(plan, out var cached)) return cached;
            var result = new List<FloorBand>();
            var building = HouseBuilding.For(plan);
            var boundary = building?.Below(building.Find(plan));
            if (boundary != null)
            {
                result.AddRange(ExplicitDividerBands(plan, boundary));
            }
            foreach (var wall in plan.Data.Walls.Where(w => !w.IsPhysical))
                foreach (var piece in RoomBoundaryPieces(plan, plan.Data.GetPosition(wall.StartId), plan.Data.GetPosition(wall.EndId)))
                    result.Add(new FloorBand(HouseEditSelection.Kind.Wall, wall.Id, null, piece.Start, piece.End));
            geometry?.Bands.Add(plan, result);
            return result;
        }

        // Navigation needs authored guides, not derived seam grouping or controls.
        internal static IEnumerable<FloorBand> ExplicitDividerBands(HousePlan plan, HouseStack.Boundary boundary)
        {
            foreach (var divider in boundary.Dividers)
                foreach (var piece in ConstructionPieces(plan, boundary.DividerPosition(divider.StartId), boundary.DividerPosition(divider.EndId)))
                    yield return new FloorBand(HouseEditSelection.Kind.Divider, divider.Id, boundary.Id, piece.Start, piece.End);
        }

        internal static HouseSceneHit? PickFloorBand(HousePlan plan, Vector2 mouse)
        {
            HouseSceneHit? result = null;
            float nearest = 7;
            var cursors = new Dictionary<float, bool>();
            foreach (var band in FloorBands(plan))
            {
                float height = plan.transform.InverseTransformPoint(band.Start).y;
                bool roomBoundary = band.Kind == HouseEditSelection.Kind.Wall;
                if (!cursors.TryGetValue(height, out bool onFloor) && !roomBoundary)
                {
                    onFloor = PointOnEditPlane(plan, mouse, height, out var point) && OnFloor(FloorCoverage(plan), point);
                    cursors.Add(height, onFloor);
                }
                if (!roomBoundary && !onFloor) continue;
                var world = ScreenSegmentPoint(band.Start, band.End, mouse);
                float pixels = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(world));
                if (pixels >= nearest || !VisibleWorld(plan, world)) continue;
                nearest = pixels;
                var local = plan.transform.InverseTransformPoint(world);
                var wall = band.Kind == HouseEditSelection.Kind.Wall ? plan.Data.Walls.First(w => w.Id == band.Id) : null;
                result = new HouseSceneHit { Plan = plan, Kind = band.Kind, Id = band.Id, BoundaryId = band.BoundaryId,
                    Height = local.y, Along = wall == null ? 0 : Vector2.Dot(new Vector2(local.x, local.z) - plan.Data.GetPosition(wall.StartId), Direction(plan, wall)) };
            }
            return result;
        }

        private static bool OnFloor(PathsD shape, Vector2 point)
        {
            int winding = 0;
            foreach (var path in shape)
            {
                var contains = Clipper.PointInPolygon(new PointD(point.x, point.y), path, 4);
                if (contains == PointInPolygonResult.IsOn) return true;
                if (contains == PointInPolygonResult.IsInside) winding += Clipper.Area(path) < 0 ? -1 : 1;
            }
            return winding != 0;
        }
    }
}
