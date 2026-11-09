using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Author each face against its own rooms and the explicitly drawn slab dividers.
    // Opposite-room seams only subdivide output. A cut participates in authoring
    // only when a divider actually terminates on its perimeter.
    internal static class HouseSurfaceLayout
    {
        internal sealed class Face
        {
            internal string RoomId, Key;
            internal bool Ceiling;
            internal PathsD Shape;
            internal HouseStack.SurfaceRegion Record;
        }

        internal static List<Face> Derive(HouseStack.Boundary boundary, FloorPlan lower, FloorPlan upper)
        {
            var result = new List<Face>();
            var lowerRooms = Rooms(lower); var upperRooms = Rooms(upper);
            var allLower = SlabGeometry.Union(new PathsD(lowerRooms.SelectMany(room => room.shape)));
            var allUpper = SlabGeometry.Union(new PathsD(upperRooms.SelectMany(room => room.shape)));
            var dividers = boundary.Dividers.Select(edge => (IReadOnlyList<Vector2>)new[]
                { boundary.DividerPosition(edge.StartId), boundary.DividerPosition(edge.EndId) }).ToList();
            foreach (var opening in boundary.Openings)
                if (boundary.DividerNodes.Any(node => Enumerable.Range(0, opening.Points.Count).Any(index =>
                    OutlineGeometry.PointOnSegment(node.Position, opening.Points[index], opening.Points[(index + 1) % opening.Points.Count]))))
                    // Two strokes can divide visible floor by terminating at a hole.
                    // That host contour closes their graph without adding draggable cut points.
                    dividers.Add(opening.Points.Concat(opening.Points.Take(1)).ToArray());
            foreach (var room in upperRooms) Add(room.id, false, room.shape);
            foreach (var room in lowerRooms) Add(room.id, true, room.shape);
            // Unowned exposed roof tops and ground/balcony undersides still accept finishes.
            // Their outline follows coverage, not individual opposite-room subdivisions.
            Add(null, false, SlabGeometry.Subtract(allLower, allUpper));
            Add(null, true, SlabGeometry.Subtract(allUpper, allLower));
            return result;

            void Add(string room, bool ceiling, PathsD coverage)
            {
                if (coverage.Count == 0) return;
                var shapes = dividers.Count == 0 ? SurfacePartition.Components(coverage) : SurfacePartition.Split(coverage, dividers);
                foreach (var shape in shapes)
                    result.Add(new Face { RoomId = room, Ceiling = ceiling, Shape = shape,
                        Key = (ceiling ? "ceiling:" : "floor:") + room + ":" + SurfacePartition.NormalizedShapeKey(shape) });
            }
        }

        private static List<(string id, PathsD shape)> Rooms(FloorPlan plan)
            => plan == null ? new List<(string, PathsD)>()
                : plan.Cells.Select(room => (room.Id, SlabGeometry.Shape(plan.GetCellBoundary(room)))).ToList();

        internal static bool TryRead(HouseStack.Boundary boundary, FloorPlan lower, FloorPlan upper,
            out List<Face> faces, out string reason)
        {
            faces = null;
            if (!boundary.TryValidateSurfaceData(out reason)) return false;
            foreach (var opening in boundary.Openings)
                if (!OutlineGeometry.Validate(opening.Points, true, out reason)) return false;
            faces = Derive(boundary, lower, upper);
            reason = "The slab face regions do not match their layout. Reconcile the authored edit before building output.";
            if (boundary.Regions.Count != faces.Count) return false;
            var records = boundary.Regions.ToDictionary(record => record.FaceKey);
            foreach (var face in faces)
            {
                if (!records.TryGetValue(face.Key, out var record) || record.Ceiling != face.Ceiling) return false;
                face.Record = record;
            }
            reason = "";
            return true;
        }

        // Creation only. Output readers never repair authored data.
        internal static void Seed(HouseStack.Boundary boundary, FloorPlan lower, FloorPlan upper)
        {
            if (boundary.Regions.Count != 0) throw new System.InvalidOperationException("Only seed a new, empty boundary. Existing values require reconciliation.");
            if (!boundary.TryValidateSurfaceData(out var reason)) throw new System.InvalidOperationException(reason);
            boundary.ReplaceRegions(Derive(boundary, lower, upper).Select(face => new HouseStack.SurfaceRegion(face.Key, face.Ceiling)));
        }
    }
}
