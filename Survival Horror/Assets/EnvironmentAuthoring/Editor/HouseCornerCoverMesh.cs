using System.Linq;
using System.Collections.Generic;
using Clipper2Lib;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // The layout owns the footprint. This adapter only supplies a shared closed
    // unit-height mesh for rendering and collision; it never cuts a wall module.
    internal static class HouseCornerCoverMesh
    {
        [System.Serializable] private struct ShapeInputs { public Vector2[] Points; }

        internal static Mesh For(HouseWallLayout.CornerCover cover)
        {
            string key = cover.Profile == null ? "Shared square corner cube v1"
                : "Angled corner cover v1:" + Hash128.Compute(JsonUtility.ToJson(new ShapeInputs { Points = cover.Profile }));
            var mesh = GeneratedMeshResources.FindCached(key);
            if (mesh != null) { PrepareMapping(mesh); return mesh; }
            if (cover.Profile == null) mesh = WallModuleExamples.BuildCornerBlock();
            else
            {
                // Reuse the existing closed extrusion, without any polygon Boolean.
                var path = new PathD(cover.Profile.Select(point => new PointD(point.x, point.y)));
                mesh = SlabGeometry.Extrude(new PathsD { path }, 0, 1, Vector2.one);
            }
            mesh.name = key;
            PrepareMapping(mesh);
            return GeneratedMeshResources.Cache(key, mesh);
        }

        internal static void PrepareRetainedMapping(MeshFilter filter)
        {
            var mesh = filter.sharedMesh;
            var ownership = filter.GetComponent<GeneratedMeshOwnership>();
            // Never edit an imported or substituted painted mesh. Creation records,
            // rather than a mesh name, establish that this is our derived base mesh.
            if (mesh != null && !EditorUtility.IsPersistent(mesh) && ownership != null
                && ownership.Meshes.Contains(mesh)) PrepareMapping(mesh);
        }

        private static void PrepareMapping(Mesh mesh)
        {
            var mapping = new List<Vector2>();
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord3))
            {
                mesh.GetUVs(3, mapping);
                if (mapping.Count == mesh.vertexCount && mapping.All(value => value.x == 3)) return;
                mapping.Clear();
            }
            // The shader needs only a cover tag to choose each side's direction.
            // Physical V no longer requires generated UVs; preserve UV0/tangents.
            mapping.AddRange(Enumerable.Repeat(new Vector2(3,0), mesh.vertexCount));
            mesh.SetUVs(3, mapping);
        }
    }
}
