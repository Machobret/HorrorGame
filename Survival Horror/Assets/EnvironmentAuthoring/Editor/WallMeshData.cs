using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // Shared polygon operations preserve authored attributes across wall fitting and
    // finish seams. Callers own the planes, material policy and resource cache keys.
    internal static class WallMeshData
    {
        internal struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector4 Tangent;
            public Vector2 UV;
            public Color Color;
            public byte ExtraUVMask;
            public Vector4[] ExtraUV;
            public static Vertex Lerp(Vertex a, Vertex b, float t)
            {
                var vertex = new Vertex{Position = Vector3.LerpUnclamped(a.Position, b.Position, t), Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized, Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t), UV = Vector2.LerpUnclamped(a.UV, b.UV, t), Color = UnityEngine.Color.LerpUnclamped(a.Color, b.Color, t)};
                vertex.ExtraUVMask = (byte)(a.ExtraUVMask | b.ExtraUVMask);
                if (vertex.ExtraUVMask != 0)
                    vertex.ExtraUV = Enumerable.Range(0, 7).Select(i => Vector4.LerpUnclamped(
                        a.ExtraUV == null ? Vector4.zero : a.ExtraUV[i], b.ExtraUV == null ? Vector4.zero : b.ExtraUV[i], t)).ToArray();
                return vertex;
            }
        }

        internal static Mesh CreateMesh(string meshName, List<Vertex> vertices, List<int>[] submeshes)
        {
            var mesh = new Mesh{name = meshName, indexFormat = IndexFormat.UInt32};
            mesh.SetVertices(vertices.Select(v => v.Position).ToArray());
            mesh.SetNormals(vertices.Select(v => v.Normal).ToArray());
            mesh.SetTangents(vertices.Select(v => v.Tangent).ToArray());
            mesh.SetUVs(0, vertices.Select(v => v.UV).ToArray());
            int mask = vertices.Aggregate(0, (value, vertex) => value | vertex.ExtraUVMask);
            for (int channel = 1; channel < 8; channel++)
                if ((mask & (1 << (channel - 1))) != 0)
                    mesh.SetUVs(channel, vertices.Select(v => v.ExtraUV == null ? Vector4.zero : v.ExtraUV[channel - 1]).ToArray());
            mesh.SetColors(vertices.Select(v => v.Color).ToArray());
            mesh.subMeshCount = submeshes.Length;
            for (int slot = 0; slot < submeshes.Length; slot++)
                mesh.SetTriangles(submeshes[slot], slot);
            mesh.RecalculateBounds();
            return GeneratedMeshResources.Cache(meshName, mesh);
        }

        internal static Vertex[] ReadVertices(Mesh.MeshData data)
        {
            int count = data.vertexCount;
            var positions = Read<Vector3>(count, a => data.GetVertices(a));
            var normals = Read<Vector3>(count, a => data.GetNormals(a));
            var uv = Read<Vector2>(count, a => data.GetUVs(0, a));
            var tangents = data.HasVertexAttribute(VertexAttribute.Tangent) ? Read<Vector4>(count, a => data.GetTangents(a)) : Enumerable.Repeat(new Vector4(1, 0, 0, 1), count).ToArray();
            var colors = data.HasVertexAttribute(VertexAttribute.Color) ? Read<Color>(count, a => data.GetColors(a)) : Enumerable.Repeat(Color.white, count).ToArray();
            var result = Enumerable.Range(0, count).Select(i => new Vertex { Position = positions[i], Normal = normals[i],
                Tangent = tangents[i], UV = uv[i], Color = colors[i] }).ToArray();
            for (int channel = 1; channel < 8; channel++)
                if (data.HasVertexAttribute(VertexAttribute.TexCoord0 + channel))
                {
                    var values = Read<Vector4>(count, a => data.GetUVs(channel, a));
                    for (int i = 0; i < count; i++)
                    {
                        result[i].ExtraUV ??= new Vector4[7];
                        result[i].ExtraUV[channel - 1] = values[i];
                        result[i].ExtraUVMask |= (byte)(1 << (channel - 1));
                    }
                }
            return result;
        }

        internal static void Append(List<Vertex> vertices, List<int> triangles, List<Vertex> polygon)
        {
            int start = vertices.Count;
            vertices.AddRange(polygon);
            for (int j = 1; j + 1 < polygon.Count; j++)
                if (Vector3.Cross(polygon[j].Position - polygon[0].Position, polygon[j + 1].Position - polygon[0].Position).sqrMagnitude > 1e-14f)
                    triangles.AddRange(new[] { start, start + j, start + j + 1 });
        }

        internal static List<Vertex> Clip(List<Vertex> input, float height, bool below, List<Vector3> crossings = null)
        {
            var output = new List<Vertex>();
            for (int i = 0; i < input.Count; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Count];
                bool insideA = below ? a.Position.y <= height : a.Position.y >= height;
                bool insideB = below ? b.Position.y <= height : b.Position.y >= height;
                if (insideA) output.Add(a);
                if (insideA == insideB) continue;
                var vertex = Vertex.Lerp(a, b, (height - a.Position.y) / (b.Position.y - a.Position.y));
                vertex.Position.y = height;
                output.Add(vertex);
                crossings?.Add(vertex.Position);
            }
            return output;
        }

        internal static List<Vertex> Clip(List<Vertex> input, Plane plane, bool positive)
        {
            var output = new List<Vertex>();
            for (int i = 0; i < input.Count; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Count];
                float da = plane.GetDistanceToPoint(a.Position), db = plane.GetDistanceToPoint(b.Position);
                bool insideA = positive ? da >= 0 : da <= 0;
                bool insideB = positive ? db >= 0 : db <= 0;
                if (insideA) output.Add(a);
                if (insideA != insideB) output.Add(Vertex.Lerp(a, b, da / (da - db)));
            }
            return output;
        }

        internal static T[] Read<T>(int count, Action<NativeArray<T>> read)
            where T : struct
        {
            using var array = new NativeArray<T>(count, Allocator.Temp);
            read(array);
            return array.ToArray();
        }
    }
}
