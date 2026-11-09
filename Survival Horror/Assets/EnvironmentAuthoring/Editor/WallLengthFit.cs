using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static EnvironmentAuthoring.Editor.WallMeshData;

namespace EnvironmentAuthoring.Editor
{
    // Crop inward from both ends, retaining baked details and UV proportions.
    // Ordinary lengths use the native shared mesh and never enter this operation.
    internal static class WallLengthFit
    {
        // Cache identity for the cropped vertices; output interpretation has its own versioned geometry key.
        internal const string Version = "wall-length-crop-v2";
        internal static Mesh Crop(WallModuleDefinition definition, float length)
            => Crop(definition.Body, definition.Width, length);

        // Fences use the same attribute-preserving crop, without fabricating a
        // House definition or giving geometry a second resource/cache owner.
        internal static Mesh Crop(Mesh source, float width, float length)
        {
            if (!float.IsFinite(length) || length <= 0 || length > width)
                throw new System.ArgumentOutOfRangeException(nameof(length), "Crop length must fit within the source width.");
            if (Mathf.Abs(length - width) < .00001f) return source;
            string name = "Wall length fit " + Hash128.Compute(GeneratedMeshResources.SourceKey(source)
                + $":{Version}:{width:R}:{length:R}");
            var retained = GeneratedMeshResources.FindCached(name);
            if (retained != null) return retained;

            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(source);
            var data = snapshot[0];
            var input = ReadVertices(data);
            var vertices = new List<Vertex>();
            var slots = Enumerable.Range(0, source.subMeshCount).Select(_ => new List<int>()).ToArray();
            // Keeping each outer half also keeps full end profiles whenever they
            // fit. Translating the right half does not stretch geometry or its bake.
            float keptEnd = length / 2;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                var indices = Read<int>(data.GetSubMesh(slot).indexCount, a => data.GetIndices(a, slot));
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var triangle = new List<Vertex> { input[indices[i]], input[indices[i + 1]], input[indices[i + 2]] };
                    Append(vertices, slots[slot], Clip(triangle, new Plane(Vector3.right, -keptEnd), false));
                    var right = Clip(triangle, new Plane(Vector3.right, -(width - keptEnd)), true);
                    Translate(right, length - width);
                    Append(vertices, slots[slot], right);
                }
            }
            return CreateMesh(name, vertices, slots);
        }

        private static void Translate(List<Vertex> polygon, float offset)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                var vertex = polygon[i];
                vertex.Position.x += offset;
                polygon[i] = vertex;
            }
        }
    }
}
