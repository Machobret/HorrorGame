using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class DrinksFridgeBuilder
    {
        const string Root = "Assets/Game/Environment/NeighborhoodStore/";
        const string ModelPath = Root + "Models/DrinksFridge.fbx";
        const string PrefabPath = Root + "Prefabs/DrinksFridge.prefab";
        [Serializable] public sealed class Manifest { public float[] size; public int triangles, stockCount; }
        [MenuItem("Horror Game/Environment/Build Drinks Fridge")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building assets.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string texturePath = Root + "Textures/DrinksFridge_Atlas.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Clamp; ti.anisoLevel = 0;
            ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = true;
            ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (string name in new[] { "Fridge_Atlas", "Fridge_Glass" })
            {
                string path = Root + "Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
                mat.SetTexture("_BaseMap", texture); mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", .2f);
                bool glass = name == "Fridge_Glass";
                mat.SetColor("_BaseColor", new Color(1, 1, 1, glass ? .12f : 1));
                mat.SetFloat("_Surface", glass ? 1 : 0); mat.SetFloat("_ZWrite", glass ? 0 : 1);
                mat.SetFloat("_SrcBlend", (float)(glass ? BlendMode.SrcAlpha : BlendMode.One));
                mat.SetFloat("_DstBlend", (float)(glass ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)(glass ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                mat.SetOverrideTag("RenderType", glass ? "Transparent" : "Opaque");
                mat.renderQueue = glass ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                if (glass) { mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.SetShaderPassEnabled("ShadowCaster", false); }
                else mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            }
            importer.SaveAndReimport();
            var root = new GameObject("DrinksFridge");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(root.transform, false); model.name = "FridgeModel";
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer.sharedMaterial.name == "Fridge_Glass") renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var record = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "DrinksFridge_Manifest.json"));
            var expected = new Vector3(record.size[0], record.size[1], record.size[2]);
            if ((bounds.size - expected).magnitude > .005f || renderers.Length != 2 || prefab.GetComponentsInChildren<MeshCollider>().Length != 2)
                throw new Exception("Fridge model/collision/scale validation failed.");
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (!mesh || mesh.uv.Length != mesh.vertexCount) throw new Exception("Missing fridge mesh UVs.");
                foreach (var mat in renderer.sharedMaterials)
                    if (!mat || mat.GetTexture("_BaseMap") != texture || mat.shader.name != "Universal Render Pipeline/Lit") throw new Exception("Missing fridge material/texture.");
            }
            var glassMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Fridge_Glass.mat");
            if (!glassMat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || glassMat.GetColor("_BaseColor").a > .2f)
                throw new Exception("Fridge glass must remain transparent.");
            Directory.CreateDirectory("Exports/Reviews/NeighborhoodStore");
            File.WriteAllText("Exports/Reviews/NeighborhoodStore/DrinksFridge_Validation.txt", "PASS: one stocked fridge prefab, two mapped textured meshes, transparent glass, point filtering, collision and metre scale " + bounds.size.ToString("F3") + ".\n");
            Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            Debug.Log("DRINKS_FRIDGE_READY: " + PrefabPath);
        }
    }
}
