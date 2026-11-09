using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public static partial class HouseWallLayout
    {
        internal sealed class CornerCover
        {
            internal string Id, WallA, WallB;
            internal Vector2 Node, Center, Along;
            internal Vector2 Size;
            internal float TopInset;
            internal string CoveringWall;
            // Only angled covers need a profile. Coordinates are in the unit
            // bounding rectangle, so height/size/placement do not prevent sharing.
            internal Vector2[] Profile;
            internal Vector2[] Footprint()
            {
                if (Profile != null)
                {
                    var points = new Vector2[Profile.Length];
                    var normal = Perpendicular(Along);
                    for (int i = 0; i < points.Length; i++)
                        points[i] = Center + Along * ((Profile[i].x - .5f) * Size.x)
                            + normal * ((Profile[i].y - .5f) * Size.y);
                    return points;
                }
                var x = Along * Size.x / 2;
                var z = Perpendicular(Along) * Size.y / 2;
                return new[] { Center - x - z, Center + x - z, Center + x + z, Center - x + z };
            }
        }


        internal static bool UsesCornerCover(Vector2 a, Vector2 b)
        {
            float dot = Vector2.Dot(a, b);
            // Cosine decreases as the smaller angle increases: cos(110)..cos(70).
            return dot >= -.34202015f - .000001f && dot <= .34202015f + .000001f;
        }

        // Fit the missing sector from the two end seams and their face intersection.
        // A rectangle is exact only at right angles; angled covers retain these four
        // points instead of adding bounding-box corners outside the adjoining faces.
        internal static CornerCover FitCornerCover(string id, Vector2 node, string wallA, string wallB,
            Vector2 a, Vector2 b, Vector2 depthsA, Vector2 depthsB)
        {
            float cross = Cross(a, b);
            var p = Perpendicular(a) * (cross > 0 ? depthsA.x : depthsA.y);
            var q = Perpendicular(b) * (cross > 0 ? depthsB.y : depthsB.x);
            var outer = p + a * (Cross(q - p, b) / cross);
            var normal = Perpendicular(a);
            bool angled = Mathf.Abs(Vector2.Dot(a, b)) > .000001f;
            var localCorners = new[] { Vector2.zero,
                new Vector2(Vector2.Dot(p, a), Vector2.Dot(p, normal)),
                new Vector2(Vector2.Dot(outer, a), Vector2.Dot(outer, normal)),
                new Vector2(Vector2.Dot(q, a), Vector2.Dot(q, normal)) };
            if (angled)
            {
                // Derive in one local frame. Quantizing the cosine removes tiny
                // pose-dependent dot-product differences before they reach the cache.
                float cosine = Mathf.Round(Vector2.Dot(a,b)*1000000)/1000000;
                var localB = new Vector2(cosine, Mathf.Sign(cross)*Mathf.Sqrt(1-cosine*cosine));
                var localNormalB = Perpendicular(localB);
                float faceA = cross > 0 ? depthsA.x : depthsA.y;
                float faceB = cross > 0 ? depthsB.y : depthsB.x;
                var localP = Vector2.up*faceA;
                var localQ = localNormalB*faceB;
                var localOuter = localP + Vector2.right*(Cross(localQ-localP,localB)/localB.y);
                localCorners = new[] { Vector2.zero, localP, localOuter, localQ };
                // With very unequal depths the face intersection can lie inside
                // a wall. Keep the missing triangle, not a self-crossing extra lobe.
                float firstFraction = faceB/(cosine*faceA);
                float secondFraction = faceA/(cosine*faceB);
                if (firstFraction > 0 && firstFraction < 1 && localOuter.x*localQ.x < 0)
                    localCorners = new[] { Vector2.zero, Vector2.up*(faceB/cosine), localQ };
                else if (secondFraction > 0 && secondFraction < 1
                    && Vector2.Dot(localOuter,localB)*Vector2.Dot(localP,localB) < 0)
                    localCorners = new[] { Vector2.zero, localP, localNormalB*(faceA/cosine) };
            }
            Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (var local in localCorners)
            {
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            var center = (min + max) / 2;
            var size = max - min;
            // Round-off at a right turn must not replace an unchanged painted
            // square. Keep its existing exact scalar geometry/signature.
            if (Mathf.Abs(size.x - size.y) < .000001f)
                size = Vector2.one * Mathf.Max(size.x, size.y);
            Vector2[] profile = null;
            if (angled && size.x > .0001f && size.y > .0001f)
            {
                var rounded = new List<Vector2>();
                for (int i = 0; i < localCorners.Length; i++)
                {
                    var local = localCorners[i];
                    // Round unit-profile coordinates themselves, not just the key,
                    // so equal cache keys always mean equal generated meshes.
                    var uv = new Vector2((local.x - min.x) / size.x, (local.y - min.y) / size.y);
                    var point = new Vector2(Mathf.Round(uv.x * 1000000) / 1000000,
                        Mathf.Round(uv.y * 1000000) / 1000000);
                    if (rounded.Count == 0 || !rounded[rounded.Count-1].Equals(point)) rounded.Add(point);
                }
                if (rounded.Count > 1 && rounded[0].Equals(rounded[rounded.Count-1])) rounded.RemoveAt(rounded.Count-1);
                // A zero-depth side or exact triangle transition can collapse an
                // edge. Never give extrusion/collision a repeated or zero-area edge.
                if (rounded.Count < 3 || System.Math.Abs(OutlineGeometry.SignedArea(rounded)) < 1e-12)
                    size = Vector2.zero;
                else
                {
                    profile = rounded.ToArray();
                    if (OutlineGeometry.SignedArea(profile) < 0) System.Array.Reverse(profile);
                }
            }
            return new CornerCover
            {
                Id = id, WallA = wallA, WallB = wallB, Node = node, Along = a,
                Center = node + a * center.x + normal * center.y,
                Size = size, Profile = profile
            };
        }

        private static void CheckCornerClearances(List<PhysicalRun> runs, Result result)
        {
            foreach (var cover in result.Corners)
            {
                var footprint = cover.Footprint();
                for (int i = 0; i < runs.Count; i++)
                {
                    var run = runs[i];
                    var span = result.Spans[i];
                    bool atStart = run.StartId == cover.Id, atEnd = run.EndId == cover.Id;
                    if (atStart || atEnd) ReserveOpeningEnd(span, atStart, footprint);
                    else if (PolygonsOverlap(footprint, Rectangle(span)))
                        result.Reject("A corner cover overlaps an unrelated wall. Move the walls apart or reduce their thickness.", cover.Node);
                }
            }
        }
    }
}
