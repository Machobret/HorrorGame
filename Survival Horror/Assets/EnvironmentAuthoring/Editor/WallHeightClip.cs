using System;
using static EnvironmentAuthoring.Editor.WallMeshData;
using System.Collections.Generic;
using System.Linq;
using LibTessDotNet;
using UnityEditor;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace EnvironmentAuthoring.Editor
{
    // Vertical fitting only. Opening geometry stays fixed while the solid head
    // of the last wall band can cover the slab above it. No general CSG or shader deformation.
    internal static class WallHeightClip
    {
        internal static Mesh Fit(Mesh source, float originalTop, float targetTop, float openingHead)
        {
            if (source == null || targetTop <= originalTop + .00001f)
                return Trim(source, originalTop);
            originalTop = Mathf.Round(originalTop * 10000) / 10000;
            targetTop = Mathf.Round(targetTop * 10000) / 10000;
            openingHead = Mathf.Round(openingHead * 10000) / 10000;
            if (originalTop <= openingHead)
                throw new ArgumentException("Extending a wall needs solid geometry above the opening head.");
            string meshName = "Wall head fit " + Hash128.Compute("split-head-v1:" + GeneratedMeshResources.SourceKey(source)
                + $":{originalTop:R}:{targetTop:R}:{openingHead:R}");
            var retained = GeneratedMeshResources.FindCached(meshName);
            if (retained != null) return retained;

            // Imported FBX meshes need not have Read/Write enabled. Read a snapshot
            // and create one shared fitted mesh for each distinct source and height.
            // Key the final result from the authored source, not the temporary trim:
            // the resource owner can release that intermediate mesh between rebuilds.
            var trimmed = Trim(source, originalTop);
            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(trimmed);
            var data = snapshot[0];
            var input = ReadVertices(data);
            var vertices = new List<Vertex>();
            var submeshes = Enumerable.Range(0, trimmed.subMeshCount).Select(_ => new List<int>()).ToArray();
            float scale = (targetTop - openingHead) / (originalTop - openingHead);
            for (int slot = 0; slot < submeshes.Length; slot++)
            {
                var indices = Read<int>(data.GetSubMesh(slot).indexCount, a => data.GetIndices(a, slot));
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var triangle = new List<Vertex> { input[indices[i]], input[indices[i + 1]], input[indices[i + 2]] };
                    bool above = triangle.Any(v => v.Position.y > openingHead);
                    bool below = triangle.Any(v => v.Position.y < openingHead);
                    // Splitting is essential: moving only the original upper vertices
                    // would distort sloping faces and UVs below the fixed opening head.
                    if (above && below)
                        Append(vertices, submeshes[slot], Clip(triangle, openingHead, true));
                    var polygon = above && below ? Clip(triangle, openingHead, false) : triangle;
                    if (above)
                        for (int j = 0; j < polygon.Count; j++)
                        {
                            var vertex = polygon[j];
                            vertex.Position.y = openingHead + (vertex.Position.y - openingHead) * scale;
                            vertex.Normal = new Vector3(vertex.Normal.x, vertex.Normal.y / scale, vertex.Normal.z).normalized;
                            var direction = new Vector3(vertex.Tangent.x, vertex.Tangent.y * scale, vertex.Tangent.z).normalized;
                            vertex.Tangent = new Vector4(direction.x, direction.y, direction.z, vertex.Tangent.w);
                            polygon[j] = vertex;
                        }
                    Append(vertices, submeshes[slot], polygon);
                }
            }
            return CreateMesh(meshName, vertices, submeshes);
        }

        internal static Mesh Trim(Mesh source, float height)
            => Trim(source, height, false, false);

        internal static Mesh TrimWidth(Mesh source, float from, float to)
            => Trim(Trim(source, to, true, false), -from, true, true);

        // Revisit only retained trims whose effective cuts used the old rounding.
        // Uncut and exact-grid attachments can keep their geometry and paint.
        internal static string WidthInputs(Mesh source, float from, float to)
        {
            // Float noise alone must not discard a retained painted attachment.
            bool Rounded(float value) => Mathf.Abs(value - Mathf.Round(value * 10000) / 10000) > .0000001f;
            return to < source.bounds.max.x - .00001f && Rounded(to)
                || from > source.bounds.min.x + .00001f && Rounded(from) ? "|width-crop-v3" : "";
        }

        private static Mesh Trim(Mesh source, float height, bool width, bool reverse)
        {
            if (source == null || height >= (width ? reverse ? -source.bounds.min.x : source.bounds.max.x : source.bounds.max.y) - .00001f)
                return source;
            // Width cuts meet an exact adjacent repeat. Rounding them independently
            // opens a gap or overlap at the join; retain height fitting's tolerance.
            if (!width) height = Mathf.Round(height * 10000) / 10000;
            string sourceKey = GeneratedMeshResources.SourceKey(source);
            string key = Hash128.Compute("side-slots-v2:" + (width ? "width-v3:" + reverse + ":" : "") + sourceKey + ":" + height.ToString("R")).ToString();
            string meshName = "Wall height cut " + key;
            var existing = GeneratedMeshResources.FindCached(meshName);
            if (existing != null) return existing;

            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(source);
            var data = snapshot[0];
            var inputVertices = ReadVertices(data);
            // Rotate width cuts into the existing horizontal cutter. A proper
            // rotation preserves winding and lets the same cap solver close ends.
            var rotation = width ? Quaternion.Euler(0, 0, reverse ? -90 : 90) : Quaternion.identity;
            Vertex Rotate(Vertex v, Quaternion q)
            {
                v.Position = q * v.Position; v.Normal = q * v.Normal;
                var tangent = q * new Vector3(v.Tangent.x, v.Tangent.y, v.Tangent.z);
                v.Tangent = new Vector4(tangent.x, tangent.y, tangent.z, v.Tangent.w);
                return v;
            }
            if (width) inputVertices = inputVertices.Select(v => Rotate(v, rotation)).ToArray();
            var vertices = new List<Vertex>();
            var submeshes = Enumerable.Range(0, source.subMeshCount).Select(_ => new List<int>()).ToArray();
            var segments = new List<(Vector3 A, Vector3 B)>();
            for (int slot = 0; slot < source.subMeshCount; slot++)
            {
                var indices = Read<int>(data.GetSubMesh(slot).indexCount, a => data.GetIndices(a, slot));
                var triangles = submeshes[slot];
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var input = new List<Vertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        input.Add(inputVertices[indices[i + j]]);
                    }

                    var crossings = new List<Vector3>();
                    Append(vertices, triangles, Clip(input, height, true, crossings));
                    if (crossings.Count == 2 && (crossings[0] - crossings[1]).sqrMagnitude > 1e-10f)
                        segments.Add((crossings[0], crossings[1]));
                }
            }

            // Stitch the cut contour by position, because hard normals/UV seams duplicate vertices.
            var loops = new List<List<Vector3>>();
            while (segments.Count > 0)
            {
                var first = segments[0];
                segments.RemoveAt(0);
                var loop = new List<Vector3>{first.A, first.B};
                bool closed = false;
                while (segments.Count > 0)
                {
                    int index = segments.FindIndex(s => (s.A - loop.Last()).sqrMagnitude < 1e-8f || (s.B - loop.Last()).sqrMagnitude < 1e-8f);
                    if (index < 0)
                        break;
                    var segment = segments[index];
                    segments.RemoveAt(index);
                    var next = (segment.A - loop.Last()).sqrMagnitude < 1e-8f ? segment.B : segment.A;
                    if ((next - loop[0]).sqrMagnitude < 1e-8f)
                    {
                        closed = true;
                        break;
                    }

                    loop.Add(next);
                }

                // Only a closed cut contour can create a thickness face. Do not guess missing edges.
                if (closed && loop.Count >= 3)
                    loops.Add(loop);
            }

            if (loops.Count > 0)
            {
                var tess = new Tess();
                foreach (var loop in loops)
                    tess.AddContour(loop.Select(p => new ContourVertex{Position = new Vec3(p.x, p.z, 0)}).ToArray());
                tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3, normal: new Vec3(0, 0, 1));
                Vertex CapVertex(int index)
                {
                    var v = tess.Vertices[index];
                    return new Vertex{Position = new Vector3(v.Position.X, height, v.Position.Y), Normal = Vector3.up, Tangent = new Vector4(1, 0, 0, -1), UV = new Vector2(v.Position.X, v.Position.Y), Color = Color.white};
                }

                for (int i = 0; i < tess.ElementCount; i++)
                {
                    int a = tess.Elements[3 * i], b = tess.Elements[3 * i + 1], c = tess.Elements[3 * i + 2];
                    if (a < 0 || b < 0 || c < 0)
                        continue;
                    var input = new List<Vertex>{CapVertex(a), CapVertex(c), CapVertex(b)};
                    for (int slot = 0; slot < submeshes.Length; slot++)
                    {
                        // Newly exposed tops inherit the finish of their half of the wall.
                        var polygon = submeshes.Length == 1 ? input : CentrelineHalf(input, slot == 0);
                        int start = vertices.Count;
                        vertices.AddRange(polygon);
                        for (int j = 1; j + 1 < polygon.Count; j++)
                            if (Vector3.Cross(polygon[j].Position - polygon[0].Position, polygon[j + 1].Position - polygon[0].Position).sqrMagnitude > 1e-14f)
                                submeshes[slot].AddRange(new[]{start, start + j, start + j + 1});
                    }
                }
            }

            if (width) vertices = vertices.Select(v => Rotate(v, Quaternion.Inverse(rotation))).ToList();
            return CreateMesh(meshName, vertices, submeshes);
        }

        private static List<Vertex> CentrelineHalf(List<Vertex> input, bool positive)
        {
            var output = new List<Vertex>();
            float sign = positive ? 1 : -1;
            for (int i = 0; i < input.Count; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Count];
                bool insideA = a.Position.z * sign >= 0, insideB = b.Position.z * sign >= 0;
                if (insideA)
                    output.Add(a);
                if (insideA != insideB)
                    output.Add(Vertex.Lerp(a, b, -a.Position.z / (b.Position.z - a.Position.z)));
            }

            return output;
        }

    }
}
