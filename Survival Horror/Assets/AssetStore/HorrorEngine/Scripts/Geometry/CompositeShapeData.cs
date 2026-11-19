using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace HorrorEngine
{
    public partial class CompositeShape
    {

        /*
         * Holds data for each shape needed when calculating composite shapes.
         * Source : https://github.com/SebLague/Shape-Editor-Tool
         
            MIT License

            Copyright (c) 2020 Sebastian Lague

            Permission is hereby granted, free of charge, to any person obtaining a copy
            of this software and associated documentation files (the "Software"), to deal
            in the Software without restriction, including without limitation the rights
            to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
            copies of the Software, and to permit persons to whom the Software is
            furnished to do so, subject to the following conditions:

            The above copyright notice and this permission notice shall be included in all
            copies or substantial portions of the Software.

            THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
            IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
            FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
            AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
            LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
            OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
            SOFTWARE.

         */

        public class CompositeShapeData
        {
            public readonly Vector2[] points;
            public readonly Polygon polygon;
            public readonly int[] triangles;

            public List<CompositeShapeData> parents = new List<CompositeShapeData>();
            public List<CompositeShapeData> holes = new List<CompositeShapeData>();
            public bool IsValidShape { get; private set; }

            public CompositeShapeData(Vector3[] points)
            {
                this.points = points.Select(v => v.ToXZ()).ToArray();
                IsValidShape = points.Length >= 3 && !IntersectsWithSelf();

                if (IsValidShape)
                {
                    polygon = new Polygon(this.points);
                    Triangulator t = new Triangulator(polygon);
                    triangles = t.Triangulate();
                }
            }

            // Removes any holes which overlap with another hole
            public void ValidateHoles()
            {
                for (int i = 0; i < holes.Count; i++)
                {
                    for (int j = i + 1; j < holes.Count; j++)
                    {
                        bool overlap = holes[i].OverlapsPartially(holes[j]);

                        if (overlap)
                        {
                            holes[i].IsValidShape = false;
                            break;
                        }
                    }
                }

                for (int i = holes.Count - 1; i >= 0; i--)
                {
                    if (!holes[i].IsValidShape)
                    {
                        holes.RemoveAt(i);
                    }
                }
            }

            // A parent is a shape which fully contains another shape
            public bool IsParentOf(CompositeShapeData otherShape)
            {
                if (otherShape.parents.Contains(this))
                {
                    return true;
                }
                if (parents.Contains(otherShape))
                {
                    return false;
                }

                // Shared walls and corners are boundaries between adjoining solids.
                // Only strictly interior outlines can become holes.
                const float boundaryToleranceSquared = .00000001f;
                foreach (Vector2 point in otherShape.points)
                {
                    bool interior = false;
                    for (int i = 0; i < triangles.Length && !interior; i += 3)
                        interior = Maths2D.PointInTriangle(polygon.points[triangles[i]], polygon.points[triangles[i + 1]], polygon.points[triangles[i + 2]], point);
                    if (!interior) return false;

                    for (int i = 0; i < points.Length; i++)
                    {
                        Vector2 start = points[i];
                        Vector2 edge = points[(i + 1) % points.Length] - start;
                        Vector2 offset = point - start;
                        float lengthSquared = edge.sqrMagnitude;
                        float fraction = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(offset, edge) / lengthSquared) : 0f;
                        if ((offset - fraction * edge).sqrMagnitude <= boundaryToleranceSquared) return false;
                    }
                }

                // Check for intersections between line segs of this shape and otherShape (any intersections will fail the parent test)
                for (int i = 0; i < points.Length; i++)
                {
                    LineSegment parentSeg = new LineSegment(points[i], points[(i + 1) % points.Length]);
                    for (int j = 0; j < otherShape.points.Length; j++)
                    {
                        LineSegment childSeg = new LineSegment(otherShape.points[j], otherShape.points[(j + 1) % otherShape.points.Length]);
                        if (Maths2D.LineSegmentsIntersect(parentSeg.a, parentSeg.b, childSeg.a, childSeg.b))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            // Test if the shapes overlap partially (test will fail if one shape entirely contains other shape, i.e. one is parent of the other).
            public bool OverlapsPartially(CompositeShapeData otherShape)
            {

                // Check for intersections between line segs of this shape and otherShape (any intersection will validate the overlap test)
                for (int i = 0; i < points.Length; i++)
                {
                    LineSegment segA = new LineSegment(points[i], points[(i + 1) % points.Length]);
                    for (int j = 0; j < otherShape.points.Length; j++)
                    {
                        LineSegment segB = new LineSegment(otherShape.points[j], otherShape.points[(j + 1) % otherShape.points.Length]);
                        if (Maths2D.LineSegmentsIntersect(segA.a, segA.b, segB.a, segB.b))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // Checks if any of the line segments making up this shape intersect
            public bool IntersectsWithSelf()
            {

                for (int i = 0; i < points.Length; i++)
                {
                    LineSegment segA = new LineSegment(points[i], points[(i + 1) % points.Length]);
                    for (int j = i + 2; j < points.Length; j++)
                    {
                        if ((j + 1) % points.Length == i)
                        {
                            continue;
                        }
                        LineSegment segB = new LineSegment(points[j], points[(j + 1) % points.Length]);
                        if (Maths2D.LineSegmentsIntersect(segA.a, segA.b, segB.a, segB.b))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public struct LineSegment
            {
                public readonly Vector2 a;
                public readonly Vector2 b;

                public LineSegment(Vector2 a, Vector2 b)
                {
                    this.a = a;
                    this.b = b;
                }
            }
        }
    }
}
