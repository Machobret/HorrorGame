using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class CashRegisterBuilder
    {
        const string Root = "Assets/Game/Environment/NeighborhoodStore/";
        const string ModelPath = Root + "Models/CashRegister.fbx";
        const string PrefabPath = Root + "Prefabs/CashRegister.prefab";
        [MenuItem("Horror Game/Environment/Build Cash Register")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building assets.");
            const string texturePath = Root + "Textures/CashRegister_Atlas.png";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = true;
            ti.anisoLevel = 0; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport();
            string matPath = Root + "Materials/Register_Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, matPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .2f); material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false; importer.importLights = false; importer.importCameras = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Register_Atlas"), material);
            importer.SaveAndReimport();
            var root = new GameObject("CashRegister");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(root.transform, false); model.name = "RegisterMesh";
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = true;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var renderer = prefab.GetComponentInChildren<MeshRenderer>();
            var filterCheck = prefab.GetComponentInChildren<MeshFilter>();
            if (!renderer || renderer.sharedMaterial != material || !material.GetTexture("_BaseMap") ||
                !filterCheck || !filterCheck.sharedMesh || filterCheck.sharedMesh.uv.Length != filterCheck.sharedMesh.vertexCount ||
                !prefab.GetComponentInChildren<MeshCollider>() ||
                (renderer.bounds.size - new Vector3(.42f, .375f, .4525f)).magnitude > .005f)
                throw new Exception("Cash register validation failed: material, UV, collider or scale.");
            Directory.CreateDirectory("Exports/Reviews/NeighborhoodStore");
            File.WriteAllText("Exports/Reviews/NeighborhoodStore/CashRegister_Validation.txt", "PASS: one cash register prefab, point-filtered atlas, mapped URP material, UVs, mesh collision and metre scale " + renderer.bounds.size.ToString("F3") + ".\n");
            Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            Debug.Log("CASH_REGISTER_READY: " + PrefabPath);
        }
    }
}
