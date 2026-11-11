using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class PSXShelvesPropsBuilder
    {
        const string Root = "Assets/Game/Environment/PSXShelvesProps/";
        [Serializable] public sealed class MaterialRecord
        {
            public string name, texture;
            public float roughness, metallic, alpha, emissionStrength;
            public float[] emission;
        }
        [Serializable] public sealed class PropRecord
        {
            public string name, variant, category, model;
            public string[] materials;
            public float[] size, pivotSource;
        }
        [Serializable] public sealed class Manifest
        {
            public MaterialRecord[] materials;
            public PropRecord[] props;
        }

        [MenuItem("Horror Game/Environment/Build PSX Shelves Props")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building the shelf assets.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "PackManifest.json"));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("URP Lit unavailable.");
            Directory.CreateDirectory(Root + "Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in manifest.materials.Select(m => Root + "Textures/" + m.texture).Distinct())
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!ti) throw new Exception("Missing pack texture: " + path);
                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = true; ti.anisoLevel = 0;
                ti.maxTextureSize = 2048; ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
            var materials = new Dictionary<string, Material>();
            foreach (var source in manifest.materials)
            {
                string path = Root + "Materials/" + source.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + source.texture);
                mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", source.metallic);
                mat.SetFloat("_Smoothness", 1 - source.roughness);
                if (source.emissionStrength > 0)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", texture);
                    mat.SetColor("_EmissionColor", new Color(source.emission[0], source.emission[1], source.emission[2]) * source.emissionStrength);
                }
                else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
                EditorUtility.SetDirty(mat); materials.Add(source.name, mat);
            }
            foreach (var source in manifest.props)
            {
                var importer = AssetImporter.GetAtPath(source.model) as ModelImporter;
                if (!importer) throw new Exception("Missing separated model: " + source.model);
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (string name in source.materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[name]);
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(source.model);
                // Preserve FBX axis/unit conversion on the child. Level designers place a clean identity root.
                var instance = new GameObject(source.name + "_" + source.variant);
                var modelChild = (GameObject)PrefabUtility.InstantiatePrefab(model);
                modelChild.transform.SetParent(instance.transform, false);
                try
                {
                    instance.name = source.name + "_" + source.variant;
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                        // Exact static mesh collision leaves shelf openings and scattered stock correctly shaped.
                        if (!source.name.StartsWith("Display_Label"))
                        {
                            var collider = renderer.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                            collider.convex = false;
                        }
                    }
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                    if (source.category == "Lighting")
                    {
                        var lightObject = new GameObject("CeilingLight_Source");
                        lightObject.transform.SetParent(instance.transform, false);
                        lightObject.transform.localPosition = Vector3.down * .08f;
                        lightObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        var light = lightObject.AddComponent<Light>();
                        light.type = LightType.Spot; light.range = 5; light.spotAngle = 110; light.innerSpotAngle = 70;
                        light.color = new Color(.86f, .91f, 1); light.intensity = 1.5f; light.shadows = LightShadows.None;
                        int number = int.Parse(source.name.Substring(source.name.Length - 2));
                        light.enabled = source.variant == "Normal" || number % 2 == 1;
                    }
                    string directory = Root + "Prefabs/" + source.variant + "/" + source.category;
                    Directory.CreateDirectory(directory);
                    PrefabUtility.SaveAsPrefabAsset(instance, directory + "/" + instance.name + ".prefab");
                }
                finally { Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Horror Game/Environment/Validate PSX Shelves Props")]
        public static void Validate()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "PackManifest.json"));
            var report = new List<string>();
            foreach (var source in manifest.props)
            {
                string path = Root + "Prefabs/" + source.variant + "/" + source.category + "/" + source.name + "_" + source.variant + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new Exception("Missing prefab: " + path);
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0) throw new Exception("Empty prefab: " + path);
                var bounds = new Bounds(); bool first = true;
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(renderer.gameObject) > 0)
                        throw new Exception("Missing script: " + path);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (!material || !source.materials.Contains(material.name) || !material.GetTexture("_BaseMap"))
                            throw new Exception("Unmapped material: " + path);
                        var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")));
                        if (ti.filterMode != FilterMode.Point) throw new Exception("Non-PSX texture filtering: " + path);
                    }
                }
                var expected = new Vector3(source.size[0], source.size[1], source.size[2]);
                if ((bounds.size - expected).magnitude > .01f) throw new Exception("Model scale mismatch: " + path + " " + bounds.size + " expected " + expected);
                if (source.category != "DisplayGeometry" && !prefab.GetComponentInChildren<Collider>(true)) throw new Exception("Missing collision: " + path);
                report.Add(path + " | size=" + bounds.size.ToString("F3") + " | meshes=" + filters.Length);
            }
            string output = "Exports/Reviews/PSXShelvesProps/Validation.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, "PASS: " + report.Count + " prefabs; materials/textures, PSX filtering, metre scale and static colliders.\n" + string.Join("\n", report));
            Debug.Log("PSX_SHELVES_READY: " + report.Count + " validated prefabs in " + Root + "Prefabs (Normal/Horror). No scene changes.");
        }
    }
}
