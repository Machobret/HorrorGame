using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class CheckoutCounterBuilder
    {
        const string Root = "Assets/Game/Environment/NeighborhoodStore/";
        const string ModelPath = Root + "Models/CheckoutCounter.fbx";
        const string PrefabPath = Root + "Prefabs/CheckoutCounter.prefab";
        [Serializable] public sealed class Surface { public string name, texture; public float roughness; }
        [Serializable] public sealed class Manifest { public Surface[] materials; public float[] size; public int triangles; }

        [MenuItem("Horror Game/Environment/Build Checkout Counter")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building assets.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "CheckoutCounter_Manifest.json"));
            Directory.CreateDirectory(Root + "Materials"); Directory.CreateDirectory(Root + "Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            modelImporter.importAnimation = false; modelImporter.importLights = false; modelImporter.importCameras = false;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var source in manifest.materials)
            {
                // Only change our new textures. Existing pack textures already have the approved PSX settings.
                if (source.texture.StartsWith(Root))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(source.texture);
                    importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Repeat;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = true; importer.anisoLevel = 0; importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
                string path = Root + "Materials/" + source.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new Exception("URP Lit unavailable.");
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source.texture));
                material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", 1 - source.roughness);
                material.SetFloat("_Metallic", source.name == "Checkout_Metal" ? .15f : 0);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }
            modelImporter.SaveAndReimport();
            var root = new GameObject("CheckoutCounter");
            GameObject prefab;
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                // Retain imported FBX scale/axis conversion below an identity root for level placement.
                model.transform.SetParent(root.transform, false); model.name = "CounterMesh";
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = true;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            Validate();
            Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
        }

        [MenuItem("Horror Game/Environment/Validate Checkout Counter")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab || prefab.transform.localScale != Vector3.one || prefab.transform.localRotation != Quaternion.identity)
                throw new Exception("Counter prefab missing or placement root is not identity.");
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0 || filters.Any(f => !f.sharedMesh || f.sharedMesh.uv.Length != f.sharedMesh.vertexCount))
                throw new Exception("Missing counter mesh or UVs.");
            var renderer = prefab.GetComponentInChildren<MeshRenderer>();
            foreach (var material in renderer.sharedMaterials)
                if (!material || !material.GetTexture("_BaseMap") || material.shader.name != "Universal Render Pipeline/Lit")
                    throw new Exception("Missing counter texture/material.");
            var size = renderer.bounds.size;
            if ((size - new Vector3(1.85f, .95f, .72f)).magnitude > .015f)
                throw new Exception("Unexpected checkout size: " + size.ToString("F4"));
            if (!prefab.GetComponentInChildren<MeshCollider>()) throw new Exception("Counter collision missing.");
            if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                throw new Exception("Counter has missing scripts.");
            Directory.CreateDirectory("Exports/Reviews/NeighborhoodStore");
            File.WriteAllText("Exports/Reviews/NeighborhoodStore/Validation.txt", "PASS: one checkout counter prefab, textured UVs, URP materials, static mesh collision, identity root and metre size " + size.ToString("F3") + ".\n");
            Debug.Log("CHECKOUT_COUNTER_READY: one validated prop at " + PrefabPath);
        }
    }
}
