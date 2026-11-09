using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using LibTessDotNet;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace EnvironmentAuthoring.Editor
{
    // All polygon operations use 0.1 mm precision, finer than the plan's 1 mm tolerance.
    internal static class SlabGeometry
    {
        internal sealed class FloorArea
        {
            public string RoomId, RegionId;
            public PathsD Shape;
        }

        internal static List<FloorArea> FloorAreas(HouseStack.Level level)
        {
            var building = level?.Plan == null ? null : HouseBuilding.For(level.Plan);
            if (building == null) return new List<FloorArea>();
            // Editor callers can encounter malformed external data before output refresh.
            // Fail closed; the build diagnostic owns the explanation and never repairs it.
            if (!HouseSlabLayout.TryPatches(building.Data, item => item.Plan?.Data,
                out var patches, out _, building.Below(level)?.Id)) return new List<FloorArea>();
            return FloorAreas(building.Data, level, item => item.Plan?.Data, patches);
        }

        internal static List<FloorArea> FloorAreas(HouseStack building, HouseStack.Level level,
            Func<HouseStack.Level, FloorPlan> planFor, IReadOnlyList<HouseSlabLayout.Patch> patches = null)
        {
            var result = new List<FloorArea>();
            if (building == null || level == null) return result;
            patches = patches ?? HouseSlabLayout.Patches(building, planFor, building.Below(level)?.Id);
            foreach (var patch in patches)
                if (patch.Upper == level && patch.UpperRoom != null)
                    result.Add(new FloorArea { RoomId = patch.UpperRoom, RegionId = patch.Floor.Id,
                        Shape = patch.Shape });
            return result;
        }

        internal static bool Contains(PathsD shape, Vector2 point)
        {
            int winding = 0;
            foreach (var path in shape)
            {
                var polygon = path.Select(p => new Vector2((float)p.x, (float)p.y)).ToArray();
                for (int i = 0; i < polygon.Length; i++)
                    if (OnSegment(point, polygon[i], polygon[(i + 1) % polygon.Length])) return true;
                if (OutlineGeometry.Contains(polygon, point)) winding += Clipper.Area(path) < 0 ? -1 : 1;
            }
            return winding != 0;
        }

        private static bool OnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            return Mathf.Abs(edge.x * (point.y - a.y) - edge.y * (point.x - a.x)) <= OutlineGeometry.Tolerance * edge.magnitude
                && point.x >= Mathf.Min(a.x, b.x) - OutlineGeometry.Tolerance && point.x <= Mathf.Max(a.x, b.x) + OutlineGeometry.Tolerance
                && point.y >= Mathf.Min(a.y, b.y) - OutlineGeometry.Tolerance && point.y <= Mathf.Max(a.y, b.y) + OutlineGeometry.Tolerance;
        }

        internal static PathsD Shape(IEnumerable<Vector2> points)
        {
            var path = new PathD(points.Select(p => new PointD(p.x, p.y)));
            if (Clipper.Area(path) < 0)
                path.Reverse();
            return new PathsD{path};
        }

        internal static PathsD Intersect(PathsD a, PathsD b)
        {
            // Independent floor/ceiling regions are often disjoint. Reject those
            // pairs before Clipper allocates and quantizes their polygon paths.
            return Clipper.GetBounds(a).Intersects(Clipper.GetBounds(b))
                ? Clipper.Intersect(a, b, FillRule.NonZero, 4) : new PathsD();
        }
        internal static PathsD Subtract(PathsD a, PathsD b) => Clipper.Difference(a, b, FillRule.NonZero, 4);
        internal static PathsD Union(PathsD a) => Clipper.Union(a, new PathsD(), FillRule.NonZero, 4);
        internal static PathsD Cuts(HouseStack.Boundary boundary)
        {
            var paths = new PathsD();
            foreach (var opening in boundary.Openings)
                paths.AddRange(Shape(opening.Points));
            return Union(paths);
        }

        // Source-attached cuts keep the logical face and its finishes. Only its
        // physical realization disappears; no duplicate polygon/handles are saved.
        internal static PathsD VisibleShape(HouseSlabLayout.Patch patch, PathsD cuts = null)
        {
            if (patch.Floor.CutAway) return new PathsD();
            cuts ??= Cuts(patch.Boundary);
            // Patches are already polygon intersections. With no holes, keep that
            // shape instead of solving the same polygons again. Return an owned copy.
            return cuts.Count == 0 ? new PathsD(patch.Shape.Select(path => new PathD(path))) : Subtract(patch.Shape, cuts);
        }

        internal static string Signature(PathsD paths)
        {
            // Stable regardless of the boolean solver's contour starting vertex/order.
            return string.Join(";", paths.Select(p =>
            {
                var values = p.Select(v => v.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "," + v.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                int first = Array.IndexOf(values, values.OrderBy(v => v, StringComparer.Ordinal).First());
                return string.Join("/", values.Skip(first).Concat(values.Take(first)));
            }).OrderBy(v => v, StringComparer.Ordinal));
        }

        internal static Tess Triangulate(PathsD shape)
        {
            var tess = new Tess();
            foreach (var path in shape)
                tess.AddContour(path.Select(p => new ContourVertex{Position = new Vec3((float)p.x, (float)p.y, 0)}).ToArray());
            tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3, normal: new Vec3(0, 0, 1));
            return tess;
        }

        internal static Mesh Extrude(PathsD shape, float bottom, float top, Vector2 textureSize)
        {
            if (shape.Count == 0) return null;
            var tess = Triangulate(shape);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                foreach (var vertex in tess.Vertices)
                {
                    var p = vertex.Position;
                    vertices.Add(new Vector3(p.X, face == 0 ? top : bottom, p.Y));
                    uvs.Add(new Vector2(p.X / textureSize.x, p.Y / textureSize.y));
                }

                for (int i = 0; i < tess.ElementCount; i++)
                {
                    int a = tess.Elements[i * 3], b = tess.Elements[i * 3 + 1], c = tess.Elements[i * 3 + 2];
                    if (a < 0 || b < 0 || c < 0)
                        continue;
                    triangles.Add(start + a);
                    triangles.Add(start + (face == 0 ? c : b));
                    triangles.Add(start + (face == 0 ? b : c));
                }
            }

            foreach (var path in shape)
                for (int i = 0; i < path.Count; i++)
                {
                    var a = path[i];
                    var b = path[(i + 1) % path.Count];
                    int start = vertices.Count;
                    vertices.Add(new Vector3((float)a.x, bottom, (float)a.y));
                    vertices.Add(new Vector3((float)b.x, bottom, (float)b.y));
                    vertices.Add(new Vector3((float)b.x, top, (float)b.y));
                    vertices.Add(new Vector3((float)a.x, top, (float)a.y));
                    float length = Vector3.Distance(vertices[start], vertices[start + 1]);
                    uvs.Add(Vector2.zero);
                    uvs.Add(new Vector2(length / textureSize.x, 0));
                    uvs.Add(new Vector2(length / textureSize.x, (top - bottom) / textureSize.y));
                    uvs.Add(new Vector2(0, (top - bottom) / textureSize.y));
                    triangles.AddRange(new[]{start, start + 2, start + 1, start, start + 3, start + 2});
                }

            var mesh = new Mesh{name = "Floor / ceiling panel", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
