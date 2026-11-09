using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Polygonization only: finite dividers partition coverage without removing any
    // material. Heights, cuts, identities and room policy belong to their callers.
    internal static class SurfacePartition
    {
        private const double Scale = 10000; // Same 0.1 mm precision as SlabGeometry.

        internal static List<PathsD> Split(PathsD coverage, IEnumerable<IReadOnlyList<Vector2>> dividers)
        {
            var sourceVertices = new List<Vector2>();
            var sourceEdges = new List<Vector2Int>();
            foreach (var contour in coverage)
                for (int i = 0; i < contour.Count; i++) Add(contour[i], contour[(i + 1) % contour.Count]);
            foreach (var divider in dividers)
                for (int i = 1; i < divider.Count; i++)
                {
                    var a = divider[i - 1]; var b = divider[i];
                    Add(new PointD(a.x, a.y), new PointD(b.x, b.y));
                }
            void Add(PointD a, PointD b)
            {
                int first = sourceVertices.Count;
                sourceVertices.Add(new Vector2((float)a.x, (float)a.y));
                sourceVertices.Add(new Vector2((float)b.x, (float)b.y));
                sourceEdges.Add(new Vector2Int(first, first + 1));
            }
            var noded = PlanarNoding.Split(sourceVertices, sourceEdges, weldRoundedEndpoints: true);
            var vertices = noded.Vertices;
            var edges = new List<Vector2Int>();
            var unique = new HashSet<(int, int)>();
            foreach (var piece in noded.Pieces)
                if (unique.Add((Math.Min(piece.Start, piece.End), Math.Max(piece.Start, piece.End))))
                    edges.Add(new Vector2Int(piece.Start, piece.End));

            var faces = new List<PathsD>();
            foreach (var walk in PlanarFaceWalk.Trace(vertices, edges))
            {
                var path = new PathD(walk.Select(edge =>
                {
                    var point = vertices[edge % 2 == 0 ? edges[edge / 2].x : edges[edge / 2].y];
                    return new PointD(point.x, point.y);
                }));
                if (Clipper.Area(path) <= 0) continue; // The unbounded face runs clockwise.
                // A bridge can put an outer ring and a hole in one weakly-simple
                // walk. Clipper normalizes that walk without filling its hole.
                faces.Add(SlabGeometry.Union(new PathsD { path }));
            }
            var result = new List<PathsD>();
            foreach (var face in faces)
            {
                var nested = new PathsD();
                double area = Area(face);
                foreach (var other in faces)
                    if (Area(other) < area - 1e-8 && SlabGeometry.Subtract(other, face).Count == 0)
                        nested.AddRange(other);
                // Disconnected interior rings have separate walks. Removing their
                // enclosed faces from the parent keeps each surface owned once.
                var shape = SlabGeometry.Intersect(SlabGeometry.Subtract(face, nested), coverage);
                result.AddRange(Components(shape));
            }
            return result;
        }

        internal static List<PathsD> Components(PathsD shape)
        {
            var tree = new PolyTreeD();
            Clipper.BooleanOp(ClipType.Union, shape, null, tree, FillRule.NonZero, 4);
            var result = new List<PathsD>();
            Visit(tree);
            return result;
            void Visit(PolyPathD parent)
            {
                for (int i = 0; i < parent.Count; i++)
                {
                    var child = parent[i];
                    if (!child.IsHole)
                    {
                        var component = new PathsD { child.Polygon };
                        for (int j = 0; j < child.Count; j++) component.Add(child[j].Polygon);
                        result.Add(component);
                    }
                    Visit(child); // Islands inside holes are separate connected surfaces.
                }
            }
        }

        internal static string ShapeKey(PathsD shape) => NormalizedShapeKey(SlabGeometry.Union(shape));

        // Components already resolves overlap, holes and 0.1 mm precision. Its
        // callers can key that result without running the same union a second time.
        internal static string NormalizedShapeKey(PathsD shape)
        {
            var contours = shape.Select(path =>
            {
                var points = path.Select(Quantize).ToList();
                for (int i = points.Count - 1; i >= 0 && points.Count > 3; i--)
                {
                    var a = points[(i + points.Count - 1) % points.Count];
                    var b = points[i]; var c = points[(i + 1) % points.Count];
                    if ((double)(b.X - a.X) * (c.Y - b.Y) == (double)(b.Y - a.Y) * (c.X - b.X)) points.RemoveAt(i);
                }
                var values = points.Select(point => point.X.ToString(CultureInfo.InvariantCulture) + ","
                    + point.Y.ToString(CultureInfo.InvariantCulture)).ToArray();
                string Sequence(IEnumerable<string> sequence)
                {
                    var items = sequence.ToArray();
                    int first = Array.IndexOf(items, items.OrderBy(value => value, StringComparer.Ordinal).First());
                    return string.Join("/", items.Skip(first).Concat(items.Take(first)));
                }
                var forward = Sequence(values); var reverse = Sequence(values.Reverse());
                return (Clipper.Area(path) < 0 ? "hole:" : "face:")
                    + (string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse);
            });
            return Hash128.Compute(string.Join(";", contours.OrderBy(value => value, StringComparer.Ordinal))).ToString();
        }

        private static Point64 Quantize(PointD point) => new Point64(point, Scale);
        internal static double Area(PathsD shape) => shape.Sum(path => Clipper.Area(path));

        internal static IEnumerable<(Vector2 a, Vector2 b)> ClipLine((Vector2 a, Vector2 b) edge, PathsD shape)
        {
            var direction = edge.b - edge.a;
            float length = direction.magnitude;
            if (length <= .0001f) yield break;
            direction /= length;
            var line = new PathD { new PointD(edge.a.x, edge.a.y), new PointD(edge.b.x, edge.b.y) };
            var clipper = new ClipperD(4);
            clipper.AddOpenSubject(line);
            clipper.AddClip(shape);
            var open = new PathsD();
            clipper.Execute(ClipType.Intersection, FillRule.NonZero, new PathsD(), open);
            var portions = new List<(Vector2 a, Vector2 b)>();
            foreach (var path in open)
                for (int i = 1; i < path.Count; i++)
                    portions.Add((new Vector2((float)path[i - 1].x, (float)path[i - 1].y), new Vector2((float)path[i].x, (float)path[i].y)));
            // Open-path clipping need not include a coincident polygon edge.
            // Construction bands and retained separators include that boundary.
            portions.AddRange(SharedEdges(new PathsD { line }, shape));
            var ranges = portions.Select(portion =>
            {
                float a = Vector2.Dot(portion.a - edge.a, direction), b = Vector2.Dot(portion.b - edge.a, direction);
                return (min: Mathf.Clamp(Mathf.Min(a, b), 0, length), max: Mathf.Clamp(Mathf.Max(a, b), 0, length));
            }).OrderBy(range => range.min);
            float start = -1, end = -1;
            foreach (var range in ranges)
            {
                if (start >= 0 && range.min > end + .0001f)
                { yield return (edge.a + direction * start, edge.a + direction * end); start = -1; }
                if (start < 0) start = range.min;
                end = Mathf.Max(end, range.max);
            }
            if (start >= 0 && end > start + .0001f) yield return (edge.a + direction * start, edge.a + direction * end);
        }

        // Positive-length contour overlap. Point contact is not adjacency.
        internal static IEnumerable<(Vector2 a, Vector2 b)> SharedEdges(PathsD first, PathsD second)
        {
            foreach (var path in first)
                for (int i = 0; i < path.Count; i++)
                {
                    var a = new Vector2((float)path[i].x, (float)path[i].y);
                    var b = new Vector2((float)path[(i + 1) % path.Count].x, (float)path[(i + 1) % path.Count].y);
                    var edge = b - a; float length = edge.magnitude;
                    if (length == 0) continue;
                    foreach (var other in second)
                        for (int j = 0; j < other.Count; j++)
                        {
                            var c = new Vector2((float)other[j].x, (float)other[j].y);
                            var d = new Vector2((float)other[(j + 1) % other.Count].x, (float)other[(j + 1) % other.Count].y);
                            float Cross(Vector2 point) => Mathf.Abs(edge.x * (point.y - a.y) - edge.y * (point.x - a.x));
                            if (Cross(c) > .0001f * length || Cross(d) > .0001f * length) continue;
                            float t = Vector2.Dot(c - a, edge) / edge.sqrMagnitude, u = Vector2.Dot(d - a, edge) / edge.sqrMagnitude;
                            float min = Mathf.Max(0, Mathf.Min(t, u)), max = Mathf.Min(1, Mathf.Max(t, u));
                            if ((max - min) * length > .0001f) yield return (a + edge * min, a + edge * max);
                        }
                }
        }
    }
}
