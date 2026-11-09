using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Teaching geometry helpers. Current asset creation belongs to WallFamilyImporter.
    public static class WallModuleExamples
    {
        public const string Folder = "Assets/EnvironmentAuthoring/Reference/WallModules";
        public static MeshFilter CreatePart(GameObject root, string name)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(root.transform, false);
            return part.GetComponent<MeshFilter>();
        }

        internal static Mesh BuildCornerBlock()
        {
            var quads=new Quads();
            quads.Box(Vector3.zero,Vector3.one);
            return quads.ToMesh("Square corner block");
        }

        internal static Mesh BuildTrim()
        {
            var quads = new Quads();
            quads.Box(Vector3.zero, new Vector3(1, .2f, .15f));
            return quads.ToMesh("Plain modular trim");
        }

        private sealed class Quads
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                // Choose winding from the intended outward normal instead of memorizing six cases.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0) { var swap = b; b = d; d = swap; }
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uv.AddRange(new[] { Vector2.zero, new Vector2(Vector3.Distance(a, b), 0),
                    new Vector2(Vector3.Distance(a, b), Vector3.Distance(b, c)), new Vector2(0, Vector3.Distance(a, d)) });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public void Face(float l, float b, float r, float t, float z, Vector3 normal)
            {
                Add(new Vector3(l, b, z), new Vector3(r, b, z), new Vector3(r, t, z), new Vector3(l, t, z), normal);
            }

            public void Box(Vector3 min, Vector3 max)
            {
                Face(min.x, min.y, max.x, max.y, min.z, Vector3.back);
                Face(min.x, min.y, max.x, max.y, max.z, Vector3.forward);
                Add(min, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left);
                Add(max, new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.right);
                Add(min, new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down);
                Add(max, new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.up);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
