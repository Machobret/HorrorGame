using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Environment.Editor
{
    public static class GarageDoorLibrary
    {
        public const string Folder = "Assets/Game/Environment/GarageDoors";
        public const string AtlasPath = Folder + "/GarageDoors_Atlas.png";
        public const string MaterialPath = Folder + "/GarageDoors_Atlas.mat";
        public const float Width = 2.8f, Height = 2.8f, Thickness = .05f;
        public static readonly string[] Names = { "GarageDoor_GreyRoller", "GarageDoor_BlueSectional", "GarageDoor_WhiteSectional", "GarageDoor_GreenRoller" };

        [MenuItem("Tools/Environment/Garage Doors/Build atlas door meshes")]
        public static void Build()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture == null || texture.width != 256 || texture.height != 256)
                throw new InvalidOperationException("Garage doors need the supplied 256 x 256, 2 x 2 atlas.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/EnvironmentAuthoring/Runtime/HorizontalWall.shader");
                material = new Material(shader) { name = "GarageDoors_Atlas", enableInstancing = true };
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Mapping", 0); // Exact atlas UVs, never world projection.
                material.SetFloat("_AnchorToWallBottom", 0);
                material.SetFloat("_PointSampling", 1);
                material.SetFloat("_UseNormalMap", 0);
                material.SetFloat("_NormalStrength", 0);
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SetLabels(material, new[] { "GarageDoor", "Environment" });
            }
            for (int i = 0; i < Names.Length; i++)
            {
                string name = Names[i], meshPath = Folder + "/" + name + "_Mesh.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                var generated = CreateMesh(i);
                generated.name = name + "_Mesh";
                if (mesh == null) { mesh = generated; AssetDatabase.CreateAsset(mesh, meshPath); }
                else { EditorUtility.CopySerialized(generated, mesh); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
                string prefabPath = Folder + "/" + name + ".prefab";
                // Existing prefab settings remain artist-owned; mesh GUIDs stay stable.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;
                var root = new GameObject(name);
                try
                {
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = root.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0, Height / 2, 0);
                    collider.size = new Vector3(Width, Height, Thickness);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    AssetDatabase.SetLabels(prefab, new[] { "GarageDoor", "Environment" });
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }

        private static Mesh CreateMesh(int variant)
        {
            float x = Width / 2, z = Thickness / 2;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            var tile = new Rect(variant % 2 * .5f, variant < 2 ? .5f : 0, .5f, .5f);
            float inset = .5f / 256;
            void Face(Vector3 normal, params Vector3[] positions)
            {
                int first = vertices.Count;
                foreach (var position in positions)
                {
                    vertices.Add(position); normals.Add(normal);
                    float u, v;
                    if (Mathf.Abs(normal.z) > .5f)
                    {
                        // Each side reads correctly when viewed from that side.
                        u = .5f + position.x / Width * (normal.z > 0 ? -1 : 1);
                        v = position.y / Height;
                    }
                    else if (Mathf.Abs(normal.x) > .5f)
                    {
                        u = normal.x > 0 ? (position.z / Thickness + .5f) / 128 : 1 - (position.z / Thickness + .5f) / 128;
                        v = position.y / Height;
                    }
                    else
                    {
                        u = .5f - position.x / Width;
                        v = normal.y > 0 ? 1 - (position.z / Thickness + .5f) / 128 : (position.z / Thickness + .5f) / 128;
                    }
                    uv.Add(new Vector2(Mathf.Lerp(tile.xMin + inset, tile.xMax - inset, u),
                        Mathf.Lerp(tile.yMin + inset, tile.yMax - inset, v)));
                }
                bool reverse = Vector3.Dot(Vector3.Cross(positions[1] - positions[0], positions[2] - positions[0]), normal) < 0;
                foreach (int index in reverse ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 }) triangles.Add(first + index);
            }
            Face(Vector3.forward, new Vector3(-x,0,z), new Vector3(x,0,z), new Vector3(x,Height,z), new Vector3(-x,Height,z));
            Face(Vector3.back, new Vector3(x,0,-z), new Vector3(-x,0,-z), new Vector3(-x,Height,-z), new Vector3(x,Height,-z));
            Face(Vector3.left, new Vector3(-x,0,-z), new Vector3(-x,0,z), new Vector3(-x,Height,z), new Vector3(-x,Height,-z));
            Face(Vector3.right, new Vector3(x,0,z), new Vector3(x,0,-z), new Vector3(x,Height,-z), new Vector3(x,Height,z));
            Face(Vector3.up, new Vector3(-x,Height,z), new Vector3(x,Height,z), new Vector3(x,Height,-z), new Vector3(-x,Height,-z));
            Face(Vector3.down, new Vector3(-x,0,-z), new Vector3(x,0,-z), new Vector3(x,0,z), new Vector3(-x,0,z));
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
