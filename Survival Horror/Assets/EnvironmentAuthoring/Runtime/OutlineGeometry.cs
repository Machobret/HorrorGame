using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public static class OutlineGeometry
    {
        // One millimetre in plan coordinates. Snapping is a separate, user-facing setting.
        public const float Tolerance = 0.001f;

        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[j]; var b = polygon[i];
                if (PointOnSegment(point, a, b)) return true;
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static bool Validate(IReadOnlyList<Vector2> points, bool closed, out string reason)
        {
            reason = "";
            if (closed && points.Count < 3)
            {
                reason = "Place at least three corners before closing.";
                return false;
            }

            if (!ValidatePoints(points, out reason))
                return false;

            int edgeCount = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < edgeCount; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                for (int j = i + 1; j < edgeCount; j++)
                {
                    // Neighbouring walls share a corner on purpose; test their overlap below.
                    if (j == i + 1 || (closed && i == 0 && j == edgeCount - 1))
                        continue;
                    if (SegmentsTouch(a, b, points[j], points[(j + 1) % points.Count]))
                    {
                        reason = "Walls cannot cross or touch another wall yet.";
                        return false;
                    }
                }
            }

            int first = closed ? 0 : 1;
            int end = closed ? points.Count : points.Count - 1;
            for (int i = first; i < end; i++)
            {
                Vector2 previous = points[(i + points.Count - 1) % points.Count];
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                // A straight material-transition point is valid. Doubling back along a wall is not.
                if (PointOnSegment(next, previous, current) || PointOnSegment(previous, current, next))
                {
                    reason = "Neighbouring walls cannot fold back over each other.";
                    return false;
                }
            }

            if (closed && Math.Abs(SignedArea(points)) <= Tolerance * Tolerance)
            {
                reason = "The outline must enclose an area.";
                return false;
            }
            return true;
        }

        public static double SignedArea(IReadOnlyList<Vector2> points)
        {
            double twiceArea = 0;
            // Triangles relative to the first point avoid subtracting large world coordinates.
            for (int i = 1; i + 1 < points.Count; i++)
                twiceArea += Cross(points[0], points[i], points[i + 1]);
            return twiceArea * 0.5;
        }

        public static bool ValidatePoints(IReadOnlyList<Vector2> points, out string reason)
        {
            reason = "";
            for (int i = 0; i < points.Count; i++)
            {
                if (float.IsNaN(points[i].x) || float.IsNaN(points[i].y)
                    || float.IsInfinity(points[i].x) || float.IsInfinity(points[i].y))
                {
                    reason = "Corner coordinates must be finite numbers.";
                    return false;
                }
                for (int j = 0; j < i; j++)
                {
                    if (DistanceSquared(points[i], points[j]) <= Tolerance * Tolerance)
                    {
                        reason = "Keep corners more than 1 mm apart.";
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool TryTriangulate(IReadOnlyList<Vector2> points, out List<int> triangles)
        {
            triangles = new List<int>();
            if (!Validate(points, true, out _))
                return false;

            var remaining = new List<int>();
            for (int i = 0; i < points.Count; i++)
                remaining.Add(i);
            if (SignedArea(points) < 0)
                remaining.Reverse();

            // Ear clipping: remove a convex corner whose triangle contains no other corner.
            // Unlike a triangle fan, this also fills concave outlines such as an L-shaped room.
            while (remaining.Count > 3)
            {
                bool removed = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    double turn = Cross(points[a], points[b], points[c]);
                    if (turn == 0)
                    {
                        // Only the preview drops this straight corner. Authoring data keeps its ID.
                        remaining.RemoveAt(i);
                        removed = true;
                        break;
                    }
                    if (turn < 0)
                        continue;

                    bool containsCorner = false;
                    foreach (int other in remaining)
                    {
                        if (other == a || other == b || other == c)
                            continue;
                        if (Cross(points[a], points[b], points[other]) >= 0
                            && Cross(points[b], points[c], points[other]) >= 0
                            && Cross(points[c], points[a], points[other]) >= 0)
                        {
                            containsCorner = true;
                            break;
                        }
                    }
                    if (containsCorner)
                        continue;

                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    remaining.RemoveAt(i);
                    removed = true;
                    break;
                }
                if (!removed)
                {
                    triangles.Clear();
                    return false;
                }
            }
            if (Cross(points[remaining[0]], points[remaining[1]], points[remaining[2]]) > 0)
                triangles.AddRange(remaining);
            return triangles.Count > 0;
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            // Positive means c is to the left of a -> b. Double arithmetic reduces rounding noise.
            return ((double)b.x - a.x) * ((double)c.y - a.y)
                - ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        internal static bool ContainsPoint(IReadOnlyList<Vector2> boundary, Vector2 point)
        {
            bool inside = false;
            for (int i = 0; i < boundary.Count; i++)
            {
                Vector2 a = boundary[i];
                Vector2 b = boundary[(i + 1) % boundary.Count];
                if (PointOnSegment(point, a, b))
                    return false; // Boundary contact is checked separately from containment.
                if ((a.y > point.y) != (b.y > point.y))
                {
                    // Count intersections of a horizontal ray, taking each vertex only once.
                    double x = a.x + ((double)point.y - a.y) * ((double)b.x - a.x) / ((double)b.y - a.y);
                    if (point.x < x)
                        inside = !inside;
                }
            }
            return inside;
        }

        private static double DistanceSquared(Vector2 a, Vector2 b)
        {
            double x = (double)b.x - a.x;
            double y = (double)b.y - a.y;
            return x * x + y * y;
        }

        public static bool PointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            double length = Math.Sqrt(DistanceSquared(a, b));
            return Math.Abs(Cross(a, b, point)) <= Tolerance * length
                && point.x >= Math.Min(a.x, b.x) - Tolerance
                && point.x <= Math.Max(a.x, b.x) + Tolerance
                && point.y >= Math.Min(a.y, b.y) - Tolerance
                && point.y <= Math.Max(a.y, b.y) + Tolerance;
        }

        public static bool TryInteriorIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            out float secondFraction)
        {
            secondFraction = 0f;
            double rx = (double)b.x - a.x;
            double ry = (double)b.y - a.y;
            double sx = (double)d.x - c.x;
            double sy = (double)d.y - c.y;
            double denominator = rx * sy - ry * sx;
            if (denominator == 0)
                return false; // Parallel or overlapping lines have no single crossing point.
            double qx = (double)c.x - a.x;
            double qy = (double)c.y - a.y;
            // Solve a + t(b-a) = c + u(d-c). Each fraction must be inside its segment.
            double t = (qx * sy - qy * sx) / denominator;
            double u = (qx * ry - qy * rx) / denominator;
            double firstLength = Math.Sqrt(rx * rx + ry * ry);
            double secondLength = Math.Sqrt(sx * sx + sy * sy);
            // Near an endpoint, this would duplicate a corner or create an ambiguous junction.
            if (t * firstLength <= Tolerance || (1 - t) * firstLength <= Tolerance
                || u * secondLength <= Tolerance || (1 - u) * secondLength <= Tolerance)
                return false;
            secondFraction = (float)u;
            return true;
        }

        internal static bool SegmentsTouch(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (PointOnSegment(a, c, d) || PointOnSegment(b, c, d)
                || PointOnSegment(c, a, b) || PointOnSegment(d, a, b))
                return true;
            return Cross(a, b, c) * Cross(a, b, d) < 0
                && Cross(c, d, a) * Cross(c, d, b) < 0;
        }
    }
}
