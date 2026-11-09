using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseSurfaceEdit
    {
        // Finished knife strokes must separate visible floor, not merely split
        // hidden faces beneath a hole or trace an existing automatic boundary.
        internal static bool StrokePartitionsFloor(HouseStack.Boundary before, HouseStack.Boundary after,
            FloorPlan lower, FloorPlan upper, IReadOnlyList<(Vector2 a, Vector2 b)> stroke)
        {
            var oldFaces = FloorFaces(before);
            var newFaces = FloorFaces(after);
            if (newFaces.Count <= oldFaces.Count) return false;
            var separators = new PathsD();
            for (int first = 0; first < newFaces.Count; first++)
                for (int second = first + 1; second < newFaces.Count; second++)
                    foreach (var edge in SurfacePartition.SharedEdges(newFaces[first], newFaces[second]))
                        separators.Add(Line(edge));

            var coverage = SlabGeometry.Union(new PathsD(newFaces.SelectMany(face => face)));
            foreach (var edge in stroke)
                foreach (var visible in SurfacePartition.ClipLine(edge, coverage))
                {
                    // ClipLine merges collinear overlaps. SharedEdges alone counts
                    // both directions of a two-point contour more than once.
                    float covered = SurfacePartition.ClipLine(visible, separators)
                        .Sum(part => Vector2.Distance(part.a, part.b));
                    // Match the existing polygon solver's 0.1 mm precision. A
                    // valid cut plus an unfinished tail must not save that tail.
                    if (covered + .0002f < Vector2.Distance(visible.a, visible.b)) return false;
                }
            return true;

            List<PathsD> FloorFaces(HouseStack.Boundary boundary)
            {
                if (!HouseSurfaceLayout.TryRead(boundary, lower, upper, out var faces, out _)) return new List<PathsD>();
                return faces.Where(face => !face.Ceiling && !face.Record.CutAway && (upper != null ? face.RoomId != null
                        : lower.Cells.Any(room => !room.IsOutdoor && SlabGeometry.Intersect(face.Shape, SlabGeometry.Shape(lower.GetCellBoundary(room))).Count > 0)))
                    .SelectMany(face => SurfacePartition.Components(SlabGeometry.Subtract(face.Shape, SlabGeometry.Cuts(boundary))))
                    .ToList();
            }
            static PathD Line((Vector2 a, Vector2 b) edge)
                => new PathD { new PointD(edge.a.x, edge.a.y), new PointD(edge.b.x, edge.b.y) };
        }
    }
}
