using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // A sloping path needs one shared planar topology. Editing separate top,
    // bottom and side triangles can create vertical slivers at rounded cap edges.
    internal static class TownSurfaceMesh
    {
        private const float Tolerance = TownPathGeometry.SurfaceTolerance;
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static (int a, int b) Edge(int a, int b) => a < b ? (a, b) : (b, a);
        private static int Opposite((int a, int b, int c) triangle, (int a, int b) edge)
            => triangle.a != edge.a && triangle.a != edge.b ? triangle.a
                : triangle.b != edge.a && triangle.b != edge.b ? triangle.b : triangle.c;
        private static Dictionary<(int a, int b), List<int>> Edges(List<(int a, int b, int c)> triangles)
        {
            var result = new Dictionary<(int a, int b), List<int>>();
            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                foreach (var edge in new[] { Edge(t.a, t.b), Edge(t.b, t.c), Edge(t.c, t.a) })
                { if (!result.TryGetValue(edge, out var faces)) result.Add(edge, faces = new List<int>()); faces.Add(i); }
            }
            return result;
        }

        internal static Mesh Build(TownPath path, PathsD shape, float bottom, float top, float repeat,
            Func<Vector2, float> sample, out Dictionary<Vector2, float> heights)
        {
            heights = new Dictionary<Vector2, float>();
            // Remove rounding-sized outline kinks before triangulation, not faces
            // afterward: deleting 3D slivers would leave holes in the surface.
            // Boolean subtraction can leave cap debris thinner than clipping
            // precision. Remove those narrow regions as closed polygons, then
            // restore the offset; never delete triangles from an open shell.
            var clean = Clipper.InflatePaths(shape, -Tolerance, JoinType.Miter, EndType.Polygon, precision: 6);
            clean = Clipper.InflatePaths(clean, Tolerance, JoinType.Miter, EndType.Polygon, precision: 6);
            var tess = SlabGeometry.Triangulate(Clipper.SimplifyPaths(clean, Tolerance));
            if (tess.ElementCount == 0) return null;
            var points = new List<Vector2>(); var lookup = new Dictionary<Vector2, int>(); var map = new int[tess.VertexCount];
            for (int i = 0; i < tess.VertexCount; i++)
            {
                var p = new Vector2(tess.Vertices[i].Position.X, tess.Vertices[i].Position.Y);
                if (!lookup.TryGetValue(p, out int index))
                { lookup.Add(p, index = points.Count); points.Add(p); heights.Add(p, sample(p)); }
                map[i] = index;
            }
            var triangles = new List<(int a, int b, int c)>();
            for (int i = 0; i < tess.ElementCount; i++)
            {
                int a = tess.Elements[i * 3], b = tess.Elements[i * 3 + 1], c = tess.Elements[i * 3 + 2];
                if (a >= 0 && b >= 0 && c >= 0 && map[a] != map[b] && map[b] != map[c] && map[c] != map[a])
                    triangles.Add(Triangle(points, map[a], map[b], map[c]));
            }
            var connected = new HashSet<string>(path.Spans.SelectMany(span => new[] { span.Start, span.End }));
            // Reinsert original contour stations on shared edges. Geometrically
            // collinear stations may still carry a crest or change of slope.
            var stations = shape.SelectMany(contour => contour).Select(p => new Vector2((float)p.x, (float)p.y)).Distinct()
                .Select(p => (position: p, height: sample(p)))
                .Concat(path.Points.Where(node => connected.Contains(node.Id))
                    .Select(node => (position: TownPathGeometry.Flat(node.Position), height: node.Position.y)));
            foreach (var node in stations) InsertStation(points, triangles, heights, node.position, node.height);
            ImproveTriangles(points, triangles);
            return Extrude(points, triangles, bottom, top, repeat);
        }

        private static (int a, int b, int c) Triangle(List<Vector2> points, int a, int b, int c)
            => Cross(points[b] - points[a], points[c] - points[a]) > 0 ? (a, b, c) : (a, c, b);

        private static void InsertStation(List<Vector2> points, List<(int a, int b, int c)> triangles,
            Dictionary<Vector2, float> heights, Vector2 p, float elevation)
        {
            int vertex = points.FindIndex(value => (value - p).sqrMagnitude <= Tolerance * Tolerance);
            if (vertex >= 0) { heights[points[vertex]] = elevation; return; }
            var edges = Edges(triangles); (int a, int b) onEdge = (-1, -1);
            float nearest = Tolerance * Tolerance; var projected = p;
            foreach (var edge in edges.Keys)
            {
                var a = points[edge.a]; var delta = points[edge.b] - a;
                float along = Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
                var q = a + delta * along; float distance = (p - q).sqrMagnitude;
                if (distance <= nearest) { nearest = distance; onEdge = edge; projected = q; }
            }
            if (onEdge.a >= 0)
            {
                vertex = points.FindIndex(value => (value - projected).sqrMagnitude <= Tolerance * Tolerance);
                if (vertex >= 0) { heights[points[vertex]] = elevation; return; }
                vertex = points.Count; points.Add(projected); heights[projected] = elevation;
                // Split every face using this edge together. Its boundary cap
                // will later be derived from exactly the same edges.
                foreach (int i in edges[onEdge].OrderByDescending(i => i))
                {
                    int other = Opposite(triangles[i], onEdge);
                    triangles.RemoveAt(i); triangles.Add(Triangle(points, onEdge.a, vertex, other)); triangles.Add(Triangle(points, vertex, onEdge.b, other));
                }
                return;
            }
            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i]; var a = points[t.a]; var b = points[t.b]; var c = points[t.c];
                if (Cross(b - a, p - a) < 0 || Cross(c - b, p - b) < 0 || Cross(a - c, p - c) < 0) continue;
                vertex = points.Count; points.Add(p); heights[p] = elevation;
                triangles.RemoveAt(i); triangles.Add((t.a, t.b, vertex)); triangles.Add((t.b, t.c, vertex)); triangles.Add((t.c, t.a, vertex));
                return;
            }
        }

        private static void ImproveTriangles(List<Vector2> points, List<(int a, int b, int c)> triangles)
        {
            // Flip poor internal diagonals instead of adding polygons or moving
            // points. A near-edge insertion must not leave a needle-thin face.
            float Quality((int a, int b, int c) t)
            {
                var ab = points[t.b] - points[t.a]; var ac = points[t.c] - points[t.a]; var bc = points[t.c] - points[t.b];
                float area = Cross(ab, ac), sum = ab.sqrMagnitude + ac.sqrMagnitude + bc.sqrMagnitude;
                // Normalize by edge length so this compares shape, not size.
                return area * area / Mathf.Max(1e-20f, sum * sum);
            }
            for (int pass = 0; pass < triangles.Count * 2; pass++)
            {
                var changed = new HashSet<int>();
                foreach (var pair in Edges(triangles).Where(pair => pair.Value.Count == 2))
                {
                    int i = pair.Value[0], j = pair.Value[1]; if (changed.Contains(i) || changed.Contains(j)) continue;
                    var edge = pair.Key; var one = triangles[i]; var two = triangles[j];
                    int c = Opposite(one, edge), d = Opposite(two, edge);
                    if (Cross(points[d] - points[c], points[edge.a] - points[c]) * Cross(points[d] - points[c], points[edge.b] - points[c]) >= 0) continue;
                    var a = Triangle(points, c, d, edge.a); var b = Triangle(points, d, c, edge.b);
                    if (Mathf.Min(Quality(a), Quality(b)) <= Mathf.Min(Quality(one), Quality(two)) * 1.0001f + 1e-14f) continue;
                    triangles[i] = a; triangles[j] = b; changed.Add(i); changed.Add(j);
                }
                if (changed.Count == 0) break;
            }

        }

        private static Mesh Extrude(List<Vector2> points, List<(int a, int b, int c)> triangles,
            float bottom, float top, float repeat)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            foreach (float y in new[] { top, bottom }) foreach (var p in points)
            { vertices.Add(new Vector3(p.x, y, p.y)); uv.Add(p / repeat); }
            foreach (var t in triangles)
            {
                indices.AddRange(new[] { t.a, t.c, t.b, t.a + points.Count, t.b + points.Count, t.c + points.Count });
            }
            foreach (var edge in Edges(triangles).Where(pair => pair.Value.Count == 1))
            {
                var t = triangles[edge.Value[0]]; int a = edge.Key.a, b = edge.Key.b;
                // The top winding is reversed from the planar X/Z winding.
                if (t.a == a && t.b == b || t.b == a && t.c == b || t.c == a && t.a == b) (a, b) = (b, a);
                int start = vertices.Count; var pa = points[a]; var pb = points[b];
                vertices.AddRange(new[] { new Vector3(pa.x, top, pa.y), new Vector3(pa.x, bottom, pa.y), new Vector3(pb.x, bottom, pb.y), new Vector3(pb.x, top, pb.y) });
                float length = Vector2.Distance(pa, pb) / repeat, height = (top - bottom) / repeat;
                uv.AddRange(new[] { new Vector2(0, height), Vector2.zero, new Vector2(length, 0), new Vector2(length, height) });
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = "Path surface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
