using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Environment.Editor
{
    /// <summary>Creates and verifies the approved downloaded environment library from the Blender export manifest.</summary>
    public static class EnvironmentAssetImportBuilder
    {
        [Serializable] public sealed class AssetEntry
        {
            public string id, name, category, source, source_asset, model_path, mesh_name, prefab_path;
            public string[] source_objects, material_names;
            public float[] dimensions_m;
            public int triangles, vertices, source_material_count, target_texels_per_metre;
        }
        [Serializable] public sealed class AtlasEntry
        {
            public string name, category, mode, base_color, mask, emission;
            public float metallic, smoothness;
        }
        [Serializable] public sealed class TextureEntry { public string path, source, family, role; }
        [Serializable] public sealed class Manifest
        {
            public string batch, status, texel_density;
            public AssetEntry[] assets;
            public AtlasEntry[] atlases;
            public TextureEntry[] textures;
        }
        [Serializable] public sealed class ImportedAsset
        {
            public string id, name, category, source, prefab_path, prefab_guid, model_path, model_guid;
            public string[] material_paths, material_guids;
            public float[] bounds_m;
            public string status = "ImportedAndVerified";
        }
        [Serializable] public sealed class Receipt
        {
            public string batch, completed_utc, unity_version;
            public ImportedAsset[] imported;
            public string[] material_paths, texture_paths;
            public int prefab_count, atlas_count;
            public bool verified;
            public ImportedFile[] files;
        }
        [Serializable] public sealed class ImportedFile { public string path, guid, type; }

        public static void RunBatch()
        {
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-assetImportManifest");
                if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -assetImportManifest <export_manifest.json>");
                Import(args[index + 1]);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void ConfigureTexture(string path, bool srgb, bool repeat, bool alpha, bool normal = false, bool alphaData = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing texture importer: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = alpha && !alphaData;
            importer.alphaTestReferenceValue = .5f;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha && !alphaData;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture && (texture.width < 4 || texture.height < 4)) importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        static Material Material(string path, string mode, string colorPath, string maskPath = null, string emissionPath = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("URP Lit is unavailable");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
            material.SetFloat("_Smoothness", .2f);
            material.SetFloat("_Metallic", 0);
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            material.enableInstancing = true;
            // Source FBX materials generally include thin double-sided surfaces.
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.doubleSidedGI = true;
            bool transparent = mode == "Transparent", cutout = mode == "Cutout";
            material.SetFloat("_Surface", transparent ? 1 : 0);
            material.SetFloat("_AlphaClip", cutout ? 1 : 0);
            material.SetFloat("_Cutoff", .5f);
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            SetKeyword(material, "_ALPHATEST_ON", cutout);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            if (!string.IsNullOrEmpty(maskPath))
            {
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
                material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (!string.IsNullOrEmpty(emissionPath))
            {
                material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath));
                material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetKeyword(Material m, string keyword, bool enabled)
        { if (enabled) m.EnableKeyword(keyword); else m.DisableKeyword(keyword); }

        static void Import(string manifestPath)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest?.assets == null || manifest.assets.Length == 0) throw new InvalidOperationException("Empty export manifest");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var materials = new Dictionary<string, Material>();
            var texturePaths = new HashSet<string>();
            foreach (var atlas in manifest.atlases)
            {
                ConfigureTexture(atlas.base_color, true, false, atlas.mode != "Opaque");
                if (!string.IsNullOrEmpty(atlas.mask)) { ConfigureTexture(atlas.mask, false, false, true, false, true); texturePaths.Add(atlas.mask); }
                texturePaths.Add(atlas.base_color);
                if (!string.IsNullOrEmpty(atlas.emission))
                { ConfigureTexture(atlas.emission, true, false, false); texturePaths.Add(atlas.emission); }
                string path = "Assets/Game/Environment/" + atlas.category + "/" + atlas.name + ".mat";
                var atlasMaterial = Material(path, atlas.mode, atlas.base_color, atlas.mask, atlas.emission);
                if (string.IsNullOrEmpty(atlas.mask)) { atlasMaterial.SetFloat("_Metallic", atlas.metallic); atlasMaterial.SetFloat("_Smoothness", atlas.smoothness); }
                materials.Add(atlas.name, atlasMaterial);
            }
            foreach (var texture in manifest.textures)
            {
                bool foliage = texture.family == "Vegetation";
                ConfigureTexture(texture.path, texture.role == "base_color", true, foliage && texture.path.EndsWith(".png"), texture.role == "normal");
                texturePaths.Add(texture.path);
                if (foliage && texture.role == "base_color")
                {
                    string path = Path.ChangeExtension(texture.path, ".mat").Replace('\\', '/');
                    materials.Add("Foliage/" + texture.path, Material(path, texture.path.EndsWith(".png") ? "Cutout" : "Opaque", texture.path));
                }
            }
            foreach (string family in new[] { "RoadSurfaces", "MasonrySurfaces" })
            {
                var maps = manifest.textures.Where(t => t.family == family).ToArray();
                var color = maps.FirstOrDefault(t => t.role == "base_color");
                if (color == null) continue;
                string path = "Assets/Game/Environment/" + family + "/" + Path.GetFileNameWithoutExtension(color.path).Replace("_diff_1k", "_Surface") + ".mat";
                var material = Material(path, "Opaque", color.path);
                var normal = maps.FirstOrDefault(t => t.role == "normal");
                if (normal != null)
                { material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal.path)); material.EnableKeyword("_NORMALMAP"); }
                material.SetFloat("_Smoothness", .1f);
                EditorUtility.SetDirty(material);
                materials.Add(family, material);
            }
            foreach (string model in manifest.assets.Select(a => a.model_path).Distinct())
            {
                var importer = AssetImporter.GetAtPath(model) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing FBX importer: " + model);
                importer.importAnimation = false;
                importer.importCameras = false; importer.importLights = false;
                importer.importBlendShapes = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.globalScale = 1; importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.isReadable = false;
                importer.generateSecondaryUV = false;
                var names = manifest.assets.Where(a => a.model_path == model).SelectMany(a => a.material_names).Distinct().ToArray();
                foreach (var remap in importer.GetExternalObjectMap())
                    if (remap.Key.type == typeof(Material) && !names.Contains(remap.Key.name)) importer.RemoveRemap(remap.Key);
                foreach (var name in names)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[name]);
                importer.SaveAndReimport();
            }
            var imported = new List<ImportedAsset>();
            foreach (var asset in manifest.assets)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(asset.model_path);
                var source = model.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(m => m.gameObject.name == asset.mesh_name);
                if (!source || !source.sharedMesh) throw new InvalidOperationException("Missing model child: " + asset.name);
                var go = new GameObject(asset.name);
                try
                {
                    var visual = new GameObject(asset.name + "_Visual"); visual.transform.SetParent(go.transform, false);
                    visual.transform.localPosition = source.transform.position;
                    visual.transform.localRotation = source.transform.rotation;
                    visual.transform.localScale = source.transform.lossyScale;
                    visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    var renderer = visual.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = asset.material_names.Select(n => materials[n]).ToArray();
                    if (renderer.sharedMaterials.Length != source.sharedMesh.subMeshCount)
                        throw new InvalidOperationException("Material/submesh mismatch: " + asset.name);
                    // No gameplay scripts, colliders or interaction behavior are inferred by an art import.
                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, asset.prefab_path);
                    if (!prefab) throw new InvalidOperationException("Could not save prefab: " + asset.prefab_path);
                    var prefabImporter = AssetImporter.GetAtPath(asset.prefab_path);
                    prefabImporter.userData = JsonUtility.ToJson(asset);
                    prefabImporter.SaveAndReimport();
                    Vector3 size = renderer.bounds.size;
                    Vector3 expected = new Vector3(asset.dimensions_m[0], asset.dimensions_m[2], asset.dimensions_m[1]);
                    if ((size - expected).magnitude > Mathf.Max(.015f, expected.magnitude * .015f))
                        throw new InvalidOperationException("Imported scale/orientation mismatch for " + asset.name + ": " + size + " expected " + expected);
                    imported.Add(new ImportedAsset {
                        id = asset.id, name = asset.name, category = asset.category, source = asset.source,
                        prefab_path = asset.prefab_path, prefab_guid = AssetDatabase.AssetPathToGUID(asset.prefab_path),
                        model_path = asset.model_path, model_guid = AssetDatabase.AssetPathToGUID(asset.model_path),
                        material_paths = renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray(),
                        material_guids = renderer.sharedMaterials.Select(m => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))).ToArray(),
                        bounds_m = new[] { size.x, size.y, size.z }
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
            foreach (var asset in imported)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset.prefab_path);
                if (!prefab || prefab.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterials.Any(m => !m || !m.mainTexture)))
                    throw new InvalidOperationException("Prefab verification failed: " + asset.prefab_path);
                foreach (string dependency in AssetDatabase.GetDependencies(asset.prefab_path, true))
                    if (dependency.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Runtime depends on Blender source: " + dependency);
            }
            var receipt = new Receipt {
                batch = manifest.batch, completed_utc = DateTime.UtcNow.ToString("o"), unity_version = Application.unityVersion,
                imported = imported.ToArray(), material_paths = materials.Values.Select(AssetDatabase.GetAssetPath).Distinct().ToArray(),
                texture_paths = texturePaths.OrderBy(p => p).ToArray(), prefab_count = imported.Count, atlas_count = manifest.atlases.Length, verified = true
            };
            receipt.files = imported.Select(a => a.prefab_path).Concat(imported.Select(a => a.model_path))
                .Concat(receipt.material_paths).Concat(receipt.texture_paths).Distinct().OrderBy(p => p)
                .Select(p => new ImportedFile { path = p, guid = AssetDatabase.AssetPathToGUID(p), type = AssetDatabase.GetMainAssetTypeAtPath(p)?.Name }).ToArray();
            if (receipt.files.Any(f => string.IsNullOrEmpty(f.guid))) throw new InvalidOperationException("Imported asset has no GUID");
            string previousPath = Path.Combine(Path.GetDirectoryName(manifestPath), "previous_import_receipt.json");
            if (File.Exists(previousPath))
            {
                var previous = JsonUtility.FromJson<Receipt>(File.ReadAllText(previousPath));
                if (previous.batch != manifest.batch || !previous.imported.Select(a => a.id).OrderBy(i => i).SequenceEqual(imported.Select(a => a.id).OrderBy(i => i)))
                    throw new InvalidOperationException("Cleanup ownership does not match the current batch");
                var active = new HashSet<string>(receipt.files.Select(f => f.path));
                var stale = previous.files.Where(f => !active.Contains(f.path) && (f.type == "Material" || f.type == "Texture2D")).ToArray();
                var stalePaths = new HashSet<string>(stale.Select(f => f.path));
                // Only remove this batch's replaced generated atlases/materials. Check
                // references from every surviving serialized consumer first.
                foreach (string consumer in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !stalePaths.Contains(p)))
                {
                    string ext = Path.GetExtension(consumer).ToLowerInvariant();
                    if (ext != ".mat" && ext != ".prefab" && ext != ".unity" && ext != ".asset" && ext != ".fbx") continue;
                    if (AssetDatabase.GetDependencies(consumer, false).Any(stalePaths.Contains))
                        throw new InvalidOperationException("Replaced atlas still referenced by " + consumer);
                }
                foreach (var file in stale.OrderBy(f => f.type == "Material" ? 0 : 1))
                {
                    if (!File.Exists(file.path) && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(file.path))) continue;
                    if (!file.path.StartsWith("Assets/Game/Environment/", StringComparison.Ordinal) || AssetDatabase.AssetPathToGUID(file.path) != file.guid)
                        throw new InvalidOperationException("Replaced atlas ownership/GUID mismatch: " + file.path);
                    if (!AssetDatabase.DeleteAsset(file.path)) throw new InvalidOperationException("Could not remove replaced generated atlas: " + file.path);
                }
                Debug.Log("REPLACED_GENERATED_ATLAS_FILES_REMOVED " + stale.Length);
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(manifestPath), "unity_import_receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("ENVIRONMENT_IMPORT_VERIFIED " + imported.Count + " prefabs, " + manifest.atlases.Length + " atlas pages");
        }
    }
}
