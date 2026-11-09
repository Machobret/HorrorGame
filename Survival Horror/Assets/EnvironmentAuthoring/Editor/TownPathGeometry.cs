using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class TownPathGeometry
    {
        internal const float SurfaceTolerance = .00015f; // 0.1 mm clipping plus float margin.
        internal static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
        internal static Vector3[] Samples(TownPath path, TownPath.Span span)
            => new[] { path.Find(span.Start).Position, path.Find(span.End).Position };

        // Butt-ended strips plus one bevel at each shared point. Union removes
        // all interior seams; dead ends stay open for the road/sidewalk mouths.
        internal static PathsD Footprint(TownPath path, float left, float right)
        {
            var shapes = new PathsD();
            var corners = path.Points.ToDictionary(point => point.Id, _ => new List<Vector2>());
            foreach (var span in path.Spans)
            {
                var a = Flat(path.Find(span.Start).Position); var b = Flat(path.Find(span.End).Position);
                var direction = (b - a).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                var al = a + normal * left; var ar = a - normal * right;
                var bl = b + normal * left; var br = b - normal * right;
                shapes.AddRange(SlabGeometry.Shape(new[] { al, bl, br, ar }));
                corners[span.Start].AddRange(new[] { al, ar });
                corners[span.End].AddRange(new[] { bl, br });
            }
            foreach (var corner in corners.Values.Where(values => values.Count > 2))
                shapes.AddRange(Hull(corner));
            return SlabGeometry.Union(shapes);
        }

        internal static bool JoinCrossings(TownPath path, out string reason)
        {
            if (!path.Valid(out reason)) return false;
            var spans = path.Spans.ToArray();
            var positions = path.Points.ToDictionary(point => point.Id, point => point.Position);
            var samples = spans.ToDictionary(span => span.Id, span => new[] { positions[span.Start], positions[span.End] });
            var bounds = samples.ToDictionary(pair => pair.Key, pair =>
            {
                var box = new Bounds(pair.Value[0], Vector3.zero);
                foreach (var position in pair.Value) box.Encapsulate(position);
                box.Expand(new Vector3(.002f, .1f, .002f)); return box;
            });
            var pointsByX = path.Points.OrderBy(point => point.Position.x).ToArray();
            spans = spans.OrderBy(span => bounds[span.Id].min.x).ToArray();
            var cuts = spans.ToDictionary(span => span.Id, span => new List<(float t, string point)>());
            foreach (var span in spans)
                for (int p = LowerBound(pointsByX, bounds[span.Id].min.x); p < pointsByX.Length && pointsByX[p].Position.x <= bounds[span.Id].max.x; p++)
                {
                    var point = pointsByX[p];
                    if (point.Id == span.Start || point.Id == span.End) continue;
                    if (!bounds[span.Id].Contains(point.Position)) continue;
                    var curve = samples[span.Id];
                    for (int i = 0; i + 1 < curve.Length; i++)
                    {
                        var delta = Flat(curve[i + 1] - curve[i]);
                        float t = Vector2.Dot(Flat(point.Position - curve[i]), delta) / Mathf.Max(.000001f, delta.sqrMagnitude);
                        if (t < 0 || t > 1) continue;
                        var hit = Vector3.Lerp(curve[i], curve[i + 1], t);
                        if (Vector2.Distance(Flat(point.Position), Flat(hit)) < .001f && Mathf.Abs(point.Position.y - hit.y) <= .05f)
                            cuts[span.Id].Add(((i + t) / (curve.Length - 1), point.Id));
                    }
                }
            for (int i = 0; i < spans.Length; i++)
                for (int j = i + 1; j < spans.Length; j++)
                {
                    if (bounds[spans[j].Id].min.x > bounds[spans[i].Id].max.x) break;
                    if (!bounds[spans[i].Id].Intersects(bounds[spans[j].Id])) continue;
                    var a = samples[spans[i].Id]; var b = samples[spans[j].Id];
                    for (int u = 0; u + 1 < a.Length; u++)
                        for (int v = 0; v + 1 < b.Length; v++)
                        {
                            var ab = Flat(a[u + 1] - a[u]); var cd = Flat(b[v + 1] - b[v]);
                            float Cross(Vector2 x, Vector2 y) => x.x * y.y - x.y * y.x;
                            float denominator = Cross(ab, cd);
                            if (Mathf.Abs(denominator) < 1e-7f)
                            {
                                // Coincident runs have ambiguous ownership. Reject
                                // the edit instead of saving stacked duplicate walls.
                                if (ab.sqrMagnitude > .000001f && Mathf.Abs(Cross(Flat(b[v] - a[u]), ab)) < .00001f)
                                {
                                    float from = Vector2.Dot(Flat(b[v] - a[u]), ab) / ab.sqrMagnitude;
                                    float to = Vector2.Dot(Flat(b[v + 1] - a[u]), ab) / ab.sqrMagnitude;
                                    float low = Mathf.Max(0, Mathf.Min(from, to)), high = Mathf.Min(1, Mathf.Max(from, to));
                                    if ((high - low) * ab.magnitude > .001f)
                                    {
                                        var hit = Vector3.Lerp(a[u], a[u + 1], (low + high) / 2);
                                        float along = Vector2.Dot(Flat(hit - b[v]), cd) / cd.sqrMagnitude;
                                        if (Mathf.Abs(hit.y - Mathf.Lerp(b[v].y, b[v + 1].y, along)) <= .05f)
                                        { reason = "Runs may meet or cross, but cannot overlap along the same path. Remove the duplicate span first."; return false; }
                                    }
                                }
                                continue;
                            }
                            var offset = Flat(b[v] - a[u]);
                            float s = Cross(offset, cd) / denominator, t = Cross(offset, ab) / denominator;
                            if (s < -.00001f || s > 1.00001f || t < -.00001f || t > 1.00001f) continue;
                            var first = Vector3.Lerp(a[u], a[u + 1], s); var second = Vector3.Lerp(b[v], b[v + 1], t);
                            if (Mathf.Abs(first.y - second.y) > .05f) continue;
                            float sa = (u + Mathf.Clamp01(s)) / (a.Length - 1), sb = (v + Mathf.Clamp01(t)) / (b.Length - 1);
                            string End(TownPath.Span edge, float value) => value < .0001f ? edge.Start : value > .9999f ? edge.End : null;
                            string one = End(spans[i], sa), two = End(spans[j], sb);
                            string id = one ?? two ?? path.AddPoint((first + second) / 2);
                            if (one != null && two != null && one != two)
                            {
                                path.MergePoint(two, one);
                                foreach (var list in cuts.Values)
                                    for (int c = 0; c < list.Count; c++) if (list[c].point == two) list[c] = (list[c].t, one);
                            }
                            cuts[spans[i].Id].Add((sa, id)); cuts[spans[j].Id].Add((sb, id));
                        }
                }
            foreach (var span in spans) path.Split(span, cuts[span.Id].GroupBy(cut => cut.point).Select(group => group.First()));
            return path.Valid(out reason);
        }
        private static int LowerBound(TownPath.Point[] points, float x)
        {
            int low = 0, high = points.Length;
            while (low < high) { int middle = (low + high) / 2; if (points[middle].Position.x < x) low = middle + 1; else high = middle; }
            return low;
        }

        // One height rule for road, curb, standalone sidewalk and their previews.
        // Each span supplies a linear elevation; nearby spans blend at junctions.
        // This is a surface over X/Z, not a bridge or automatic terrain drape.
        internal static Func<Vector2, float> Heights(TownPath path)
        {
            var points = path.Points.ToDictionary(point => point.Id, point => point.Position);
            // A connected straight run has one height profile. Blending its
            // individual clamped segments would bend a straight slope sideways.
            var runs = new List<List<TownPath.Span>>();
            foreach (var span in path.Spans)
            {
                var direction = Flat(points[span.End] - points[span.Start]).normalized;
                float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
                var matching = runs.Where(run =>
                {
                    var first = run[0]; var other = Flat(points[first.End] - points[first.Start]).normalized;
                    bool touches = run.Any(edge => edge.Start == span.Start || edge.End == span.Start || edge.Start == span.End || edge.End == span.End);
                    return touches && Mathf.Abs(Cross(direction, other)) < .00001f
                        && Mathf.Abs(Cross(direction, Flat(points[first.Start] - points[span.Start]))) < SurfaceTolerance;
                }).ToArray();
                var target = matching.FirstOrDefault();
                if (target == null) { target = new List<TownPath.Span>(); runs.Add(target); }
                foreach (var other in matching.Skip(1)) { target.AddRange(other); runs.Remove(other); }
                target.Add(span);
            }
            var samples = runs.Select(run => run.Select(span => (a: points[span.Start], b: points[span.End])).ToArray()).ToArray();
            float Sample(Vector2 position)
            {
                double weightedHeight = 0, weights = 0;
                foreach (var run in samples)
                {
                    float nearest = float.PositiveInfinity, height = 0;
                    foreach (var segment in run)
                    {
                        var delta = Flat(segment.b - segment.a);
                        float t = Mathf.Clamp01(Vector2.Dot(position - Flat(segment.a), delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                        float distance = (position - Flat(segment.a) - delta * t).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; height = Mathf.Lerp(segment.a.y, segment.b.y, t); }
                    }
                    if (nearest == 0) return height;
                    // Each straight run is continuous, including beyond its ends.
                    // Blend runs by distance; never switch a contribution off at
                    // an endpoint plane, which creates abrupt height jumps.
                    double weight = 1 / ((double)nearest * nearest);
                    weightedHeight += height * weight; weights += weight;
                }
                return weights > 0 ? (float)(weightedHeight / weights) : 0;
            }
            return Sample;
        }

        internal static Mesh Surface(TownPath path, PathsD shape, float thickness, float repeat, float top = 0)
        {
            float elevation = path.Points.Count == 0 ? 0 : path.Points[0].Position.y;
            bool flat = path.Points.All(point => Mathf.Abs(point.Position.y - elevation) < .000001f);
            string Edge(TownPath.Span span)
            {
                string Position(string id) => path.Find(id).Position.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                string a = Position(span.Start), b = Position(span.End);
                return string.CompareOrdinal(a, b) <= 0 ? a + b : b + a;
            }
            string key = "Town-shared-surface-v2-" + Hash128.Compute(SlabGeometry.Signature(shape) + "|" + thickness + "|" + repeat + "|" + top
                + "|" + string.Join(";", path.Spans.Select(Edge).OrderBy(value => value, StringComparer.Ordinal)));
            var cached = GeneratedMeshResources.FindCached(key);
            if (cached != null) return cached;
            Dictionary<Vector2, float> heights = null;
            var mesh = flat ? SlabGeometry.Extrude(shape, top - thickness, top, Vector2.one * repeat)
                : TownSurfaceMesh.Build(path, shape, top - thickness, top, repeat, Heights(path), out heights);
            if (mesh == null) return null;
            AlignSurfaceUVs(mesh, path, repeat);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].y += flat ? elevation : heights[Flat(vertices[i])];
            mesh.vertices = vertices;
            if (!flat) { mesh.RecalculateNormals(); mesh.RecalculateTangents(); }
            mesh.RecalculateBounds();
            mesh.name = key;
            return GeneratedMeshResources.Cache(key, mesh);
        }

        private static void AlignSurfaceUVs(Mesh mesh, TownPath path, float repeat)
        {
            var frames = path.Spans.Select(span =>
            {
                var a = Flat(path.Find(span.Start).Position); var b = Flat(path.Find(span.End).Position);
                var direction = (b - a).normalized;
                // Direction is an axis, not the drawing order. Reversed/collinear
                // spans must retain the same texture orientation and phase.
                if (direction.x < 0 || direction.x == 0 && direction.y < 0) { (a, b) = (b, a); direction = -direction; }
                return (a, b, direction);
            }).OrderBy(frame => frame.a.x).ThenBy(frame => frame.a.y).ThenBy(frame => frame.b.x).ThenBy(frame => frame.b.y).ToArray();
            var source = mesh.vertices; var sourceUV = mesh.uv; var sourceNormals = mesh.normals; var indices = mesh.triangles;
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var center = Flat((source[indices[i]] + source[indices[i + 1]] + source[indices[i + 2]]) / 3);
                float nearest = float.PositiveInfinity; var direction = Vector2.right;
                foreach (var frame in frames)
                {
                    var delta = frame.b - frame.a;
                    float t = Mathf.Clamp01(Vector2.Dot(center - frame.a, delta) / delta.sqrMagnitude);
                    float distance = (center - frame.a - delta * t).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; direction = frame.direction; }
                }
                for (int j = 0; j < 3; j++)
                {
                    int index = indices[i + j]; var p = source[index];
                    // One frame per face avoids compressing UVs across a bend.
                    // Corners may have seams; no extra triangles are generated.
                    var uv = Mathf.Abs(sourceNormals[index].y) > .9f
                        ? new Vector2(Vector2.Dot(Flat(p), direction), Vector2.Dot(Flat(p), new Vector2(-direction.y, direction.x))) / repeat
                        : sourceUV[index];
                    triangles.Add(vertices.Count); vertices.Add(p); normals.Add(sourceNormals[index]); uvs.Add(uv);
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
        }

        internal static PathsD Hull(IEnumerable<Vector2> points)
        {
            var values = points.Distinct().OrderBy(point => point.x).ThenBy(point => point.y).ToArray();
            if (values.Length < 3) return new PathsD();
            var hull = new List<Vector2>();
            void Add(Vector2 value)
            {
                while (hull.Count >= 2)
                {
                    var a = hull[hull.Count - 1] - hull[hull.Count - 2]; var b = value - hull[hull.Count - 1];
                    if (a.x * b.y - a.y * b.x > .000001f) break;
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(value);
            }
            foreach (var value in values) Add(value);
            int lower = hull.Count;
            for (int i = values.Length - 2; i >= 0; i--)
            {
                while (hull.Count > lower)
                {
                    var a = hull[hull.Count - 1] - hull[hull.Count - 2]; var b = values[i] - hull[hull.Count - 1];
                    if (a.x * b.y - a.y * b.x > .000001f) break;
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(values[i]);
            }
            hull.RemoveAt(hull.Count - 1);
            return SlabGeometry.Shape(hull);
        }
    }
}
