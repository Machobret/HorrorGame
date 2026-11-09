using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    // Logical surface targets need no collider. Explicit editor controls bypass
    // this query; ordinary surface targets must respect visible scene geometry.
    internal sealed class HouseSceneVisibility
    {
        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly Dictionary<Mesh, MeshShape> shapes = new Dictionary<Mesh, MeshShape>();
        private readonly Camera camera;

        private struct Surface
        {
            internal Mesh Mesh;
            internal Bounds Bounds;
            internal Matrix4x4 Inverse;
            internal Transform Transform;
        }

        private sealed class MeshShape
        {
            internal Vector3[] Vertices;
            internal int[] Triangles;
        }

        internal HouseSceneVisibility(Scene scene, Camera camera = null)
        {
            this.camera = camera;
            Add(scene);
            if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene))
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i) != scene) Add(SceneManager.GetSceneAt(i));
        }

        private void Add(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null || !renderer.enabled || renderer.forceRenderingOff
                        || !renderer.gameObject.activeInHierarchy || SceneVisibilityManager.instance.IsHidden(renderer.gameObject)
                        || (camera != null && (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)) continue;
                    // Read native component state once per GUI event, not once per handle ray.
                    surfaces.Add(new Surface { Mesh = filter.sharedMesh, Bounds = renderer.bounds,
                        Inverse = filter.transform.worldToLocalMatrix, Transform = filter.transform });
                }
        }

        internal bool Visible(Ray ray, float distance, Transform ownSurface = null, Bounds? handleSupport = null,
            Transform[] handleSurfaces = null)
        {
            foreach (var surface in surfaces)
            {
                // A wall's own decorations/reveals still belong to that selectable surface.
                // They must not exempt other walls or slabs from blocking the candidate.
                if (ownSurface != null && surface.Transform.IsChildOf(ownSurface)) continue;
                if (!surface.Bounds.IntersectRay(ray, out float boundDistance) || boundDistance > distance) continue;
                var shape = Shape(surface.Mesh);
                var inverse = surface.Inverse;
                // Keep the transformed direction unnormalised: t remains world-ray metres,
                // including nonuniformly stretched modules and mirrored authored meshes.
                var origin = inverse.MultiplyPoint3x4(ray.origin);
                var direction = inverse.MultiplyVector(ray.direction);
                for (int i = 0; i < shape.Triangles.Length; i += 3)
                {
                    if (!Triangle(origin, direction, shape.Vertices[shape.Triangles[i]], shape.Vertices[shape.Triangles[i + 1]],
                        shape.Vertices[shape.Triangles[i + 2]], out float hit) || hit >= distance - .002f) continue;
                    // A junction handle lives inside wall thickness. Its local support box
                    // permits only contact at that junction, never a remote wall in front.
                    if (handleSupport.HasValue && handleSupport.Value.Contains(ray.GetPoint(hit))
                        && (handleSurfaces == null || System.Array.Exists(handleSurfaces, owner =>
                            owner != null && surface.Transform.IsChildOf(owner)))) continue;
                    return false;
                }
            }
            return true;
        }

        internal bool TryHit(Ray ray, out Transform surface, out float distance)
        {
            surface = null;
            distance = float.PositiveInfinity;
            foreach (var candidate in surfaces)
            {
                if (!candidate.Bounds.IntersectRay(ray, out float boundDistance) || boundDistance > distance) continue;
                var shape = Shape(candidate.Mesh);
                var origin = candidate.Inverse.MultiplyPoint3x4(ray.origin);
                var direction = candidate.Inverse.MultiplyVector(ray.direction);
                for (int i = 0; i < shape.Triangles.Length; i += 3)
                    if (Triangle(origin, direction, shape.Vertices[shape.Triangles[i]], shape.Vertices[shape.Triangles[i + 1]],
                        shape.Vertices[shape.Triangles[i + 2]], out float hit) && hit < distance)
                    {
                        distance = hit;
                        surface = candidate.Transform;
                    }
            }
            return surface != null;
        }

        private MeshShape Shape(Mesh mesh)
        {
            if (shapes.TryGetValue(mesh, out var shape)) return shape;
            // Read imported non-readable meshes without changing import settings or
            // creating hidden physics objects. Acquire only after the bounds rejection.
            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
            var data = snapshot[0];
            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            data.GetVertices(vertices);
            var triangles = new List<int>();
            for (int slot = 0; slot < data.subMeshCount; slot++)
            {
                var submesh = data.GetSubMesh(slot);
                if (submesh.topology != MeshTopology.Triangles) continue;
                using var indices = new NativeArray<int>(submesh.indexCount, Allocator.Temp);
                data.GetIndices(indices, slot);
                triangles.AddRange(indices.ToArray());
            }
            shape = new MeshShape { Vertices = vertices.ToArray(), Triangles = triangles.ToArray() };
            shapes.Add(mesh, shape);
            return shape;
        }

        private static bool Triangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0;
            var ab = b - a; var ac = c - a;
            var cross = Vector3.Cross(direction, ac);
            float determinant = Vector3.Dot(ab, cross);
            // Both sides block: thin authored wall/slab surfaces are still barriers.
            if (Mathf.Abs(determinant) < .0000001f) return false;
            float inverse = 1 / determinant;
            var offset = origin - a;
            float u = Vector3.Dot(offset, cross) * inverse;
            if (u < 0 || u > 1) return false;
            var q = Vector3.Cross(offset, ab);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0 || u + v > 1) return false;
            distance = Vector3.Dot(ac, q) * inverse;
            return distance >= 0;
        }
    }
}
