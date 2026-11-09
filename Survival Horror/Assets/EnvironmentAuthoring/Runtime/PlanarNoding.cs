using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Finite segment arrangement at the polygon solver's 0.1 mm precision. Callers
    // assign authored identities; this primitive owns neither rooms nor saved state.
    internal static class PlanarNoding
    {
        private const double Scale = 10000;
        private sealed class Line
        {
            internal (long x, long y) A, B;
            internal readonly List<(long x, long y)> Points = new List<(long, long)>();
            internal Line((long, long) a, (long, long) b) { A = a; B = b; Points.Add(a); Points.Add(b); }
        }
        internal readonly struct Piece
        {
            internal readonly int Source, Start, End;
            internal Piece(int source, int start, int end) { Source = source; Start = start; End = end; }
        }
        internal sealed class Result
        {
            internal readonly List<Vector2> Vertices = new List<Vector2>();
            internal readonly List<Piece> Pieces = new List<Piece>();
            internal int[] OriginalVertices;
        }

        // Authored graphs keep exact incidence/identities. Floor polygonization
        // explicitly opts into repairing rounded endpoint connections.
        internal static Result Split(IReadOnlyList<Vector2> vertices, IReadOnlyList<Vector2Int> edges, bool weldRoundedEndpoints = false)
        {
            var result = new Result { OriginalVertices = new int[vertices.Count] };
            var indices = new Dictionary<(long, long), int>();
            int Index((long x, long y) point)
            {
                if (!indices.TryGetValue(point, out int index))
                {
                    indices.Add(point, index = result.Vertices.Count);
                    result.Vertices.Add(new Vector2((float)(point.x / Scale), (float)(point.y / Scale)));
                }
                return index;
            }
            for (int i = 0; i < vertices.Count; i++) result.OriginalVertices[i] = Index(Quantize(vertices[i]));
            var lines = edges.Select(edge => new Line(Quantize(vertices[edge.x]), Quantize(vertices[edge.y]))).ToArray();
            for (int i = 0; i < lines.Length; i++)
                for (int j = i + 1; j < lines.Length; j++) Node(lines[i], lines[j], weldRoundedEndpoints);
            for (int source = 0; source < lines.Length; source++)
            {
                var line = lines[source];
                double dx = line.B.x - line.A.x, dy = line.B.y - line.A.y;
                var points = line.Points.Distinct().OrderBy(point => (point.x - line.A.x) * dx + (point.y - line.A.y) * dy).ToArray();
                for (int i = 1; i < points.Length; i++)
                {
                    int a = Index(points[i - 1]), b = Index(points[i]);
                    if (a != b) result.Pieces.Add(new Piece(source, a, b));
                }
            }
            return result;
        }

        private static void Node(Line first, Line second, bool weldRoundedEndpoints)
        {
            double rx = first.B.x - first.A.x, ry = first.B.y - first.A.y;
            double sx = second.B.x - second.A.x, sy = second.B.y - second.A.y;
            if ((rx == 0 && ry == 0) || (sx == 0 && sy == 0)) return;
            double qx = second.A.x - first.A.x, qy = second.A.y - first.A.y;
            double cross = rx * sy - ry * sx;
            if (cross != 0)
            {
                double t = (qx * sy - qy * sx) / cross, u = (qx * ry - qy * rx) / cross;
                double ix = first.A.x + t * rx, iy = first.A.y + t * ry;
                // Repair only an almost-endpoint intersection at solver precision.
                // Parallel neighbours and genuine interior crossings stay distinct.
                (long x, long y)? endpoint = null;
                double nearest = double.PositiveInfinity;
                if (weldRoundedEndpoints)
                {
                    JoinEndpoint(first.A, second); JoinEndpoint(first.B, second);
                    JoinEndpoint(second.A, first); JoinEndpoint(second.B, first);
                }
                if (endpoint.HasValue)
                {
                    first.Points.Add(endpoint.Value); second.Points.Add(endpoint.Value);
                    return;
                }
                if (t < 0 || t > 1 || u < 0 || u > 1) return;
                var point = (Round(ix), Round(iy));
                first.Points.Add(point); second.Points.Add(point);
                return;

                void JoinEndpoint((long x, long y) point, Line line)
                {
                    double distance = (point.x - ix) * (point.x - ix) + (point.y - iy) * (point.y - iy);
                    if (distance > 1 || distance > nearest) return;
                    double dx = line.B.x - line.A.x, dy = line.B.y - line.A.y;
                    double px = point.x - line.A.x, py = point.y - line.A.y;
                    double lengthSquared = dx * dx + dy * dy;
                    double along = px * dx + py * dy, perpendicular = px * dy - py * dx;
                    if (along <= 0 || along >= lengthSquared || perpendicular * perpendicular > lengthSquared) return;
                    if (distance == nearest && endpoint.HasValue && (point.x > endpoint.Value.x
                        || (point.x == endpoint.Value.x && point.y >= endpoint.Value.y))) return;
                    endpoint = point; nearest = distance;
                }
            }
            if (qx * ry - qy * rx != 0) return;
            Add(first, second.A); Add(first, second.B); Add(second, first.A); Add(second, first.B);
            void Add(Line line, (long x, long y) point)
            {
                if (point.x >= Math.Min(line.A.x, line.B.x) && point.x <= Math.Max(line.A.x, line.B.x)
                    && point.y >= Math.Min(line.A.y, line.B.y) && point.y <= Math.Max(line.A.y, line.B.y)) line.Points.Add(point);
            }

        }

        // Match the pinned Clipper Point64 rounding, including half-grid coordinates.
        internal static Vector2 AtPrecision(Vector2 point)
        {
            var value = Quantize(point);
            return new Vector2((float)(value.x / Scale), (float)(value.y / Scale));
        }
        private static long Round(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
        private static (long x, long y) Quantize(Vector2 point) => (Round(point.x * Scale), Round(point.y * Scale));
    }
}
