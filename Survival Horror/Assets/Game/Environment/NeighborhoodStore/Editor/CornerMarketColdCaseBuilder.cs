using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class CornerMarketColdCaseBuilder
    {
        const string Root = "Assets/Game/Environment/NeighborhoodStore/";
        static readonly string[] CaseNames = { "SodaCooler", "DairyCooler", "JuiceCooler", "IceCreamFreezer" };

        [MenuItem("Horror Game/Environment/Create Missing Corner Market Cold Cases")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before creating assets.");

            const string atlasPath = Root + "Textures/CornerMarket_ColdCaseAtlas.png";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(atlasPath) is not TextureImporter textureImporter)
                throw new InvalidOperationException("The cold case atlas is missing.");
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.mipmapEnabled = true;
            textureImporter.anisoLevel = 0;
            textureImporter.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var enamel = GetMaterial("ColdCase_Atlas", "Fridge_Atlas", atlas);
            var glass = GetMaterial("ColdCase_Glass", "Fridge_Glass", atlas);
            var header = GetMaterial("ColdCase_Header", "Fridge_Atlas", atlas);
            header.SetTexture("_EmissionMap", atlas);
            header.SetColor("_EmissionColor", new Color(.28f, .31f, .29f));
            header.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(header);

            foreach (var name in CaseNames)
            {
                string prefabPath = Root + "Prefabs/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) continue;
                string modelPath = Root + "Models/" + name + ".fbx";
                if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
                    throw new InvalidOperationException("Missing cold case model: " + modelPath);
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ColdCase_Atlas"), enamel);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ColdCase_Glass"), glass);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ColdCase_Header"), header);
                importer.SaveAndReimport();

                var root = new GameObject(name);
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    var renderers = model.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length != 2 ||
                        renderers.Count(r => r.sharedMaterials.Length == 1 && r.sharedMaterial == glass) != 1 ||
                        !renderers.SelectMany(r => r.sharedMaterials).Contains(header) ||
                        renderers.SelectMany(r => r.sharedMaterials).Any(m => m != enamel && m != glass && m != header))
                        throw new InvalidOperationException("Cold case requires separate textured body and glass: " + name);
                    foreach (var renderer in renderers)
                    {
                        if (renderer.sharedMaterial != glass) continue;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    AddModelCollision(model, renderers);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        internal static void AddModelCollision(GameObject model, MeshRenderer[] renderers)
        {
            Bounds bounds = GetModelBounds(model.transform, renderers);
            var collider = model.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        internal static Bounds GetModelBounds(Transform model, MeshRenderer[] renderers)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                Matrix4x4 rendererToModel = model.worldToLocalMatrix * renderer.localToWorldMatrix;
                Bounds localBounds = renderer.localBounds;
                Vector3 center = rendererToModel.MultiplyPoint3x4(localBounds.center);
                Vector3 x = rendererToModel.MultiplyVector(Vector3.right * localBounds.extents.x);
                Vector3 y = rendererToModel.MultiplyVector(Vector3.up * localBounds.extents.y);
                Vector3 z = rendererToModel.MultiplyVector(Vector3.forward * localBounds.extents.z);
                Vector3 extents = new Vector3(
                    Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                var rendererBounds = new Bounds(center, extents * 2f);
                if (hasBounds)
                    bounds.Encapsulate(rendererBounds);
                else
                    bounds = rendererBounds;
                hasBounds = true;
            }
            return bounds;
        }

        static Material GetMaterial(string name, string sourceName, Texture2D atlas)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + sourceName + ".mat");
            if (!source) throw new InvalidOperationException("Missing existing fridge material: " + sourceName);
            material = new Material(source) { name = name };
            material.SetTexture("_BaseMap", atlas);
            material.SetTexture("_MainTex", atlas);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
