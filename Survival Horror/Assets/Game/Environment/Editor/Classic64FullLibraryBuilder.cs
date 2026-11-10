using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    /// <summary>Imports the complete approved Classic64 catalog while preserving the pilot.</summary>
    public static class Classic64FullLibraryBuilder
    {
        const string Source = "ArtSource/Environment/Classic64/";
        const string Folder = "Assets/Sandbox/Classic64";
        const string ScenePath = Folder + "/Classic64_LibraryReview.unity";
        const string Output = "Exports/Reviews/Classic64/Library";
        [Serializable] public class ModelSource { public string source, status; public string[] runtime_paths; }
        [Serializable] public class ImageSource { public string source, texture_path, material_path; }
        [Serializable] public class Counts { public int source_blend_files, source_image_files, new_prefabs, existing_pilot_prefabs, materials, textures; }
        [Serializable] public class FullManifest
        {
            public string batch;
            public Classic64LibraryBuilder.AssetRecord[] assets;
            public Classic64LibraryBuilder.MaterialRecord[] materials;
            public Classic64LibraryBuilder.TextureRecord[] textures;
            public Classic64LibraryBuilder.SurfaceRecord[] surfaces;
            public ModelSource[] source_models;
            public ImageSource[] source_images;
            public Counts counts;
        }
        [Serializable] public class FileRecord { public string path, guid, sha256, meta_sha256; public bool preserve_existing; }
        [Serializable] public class Receipt
        {
            public string batch, unity_version, completed_utc, review_scene, manifest_sha256;
            public int source_models, source_images, new_prefabs, environment_prefabs, example_prefabs, existing_pilot_prefabs, materials, textures, mesh_parts, excluded_source_models;
            public bool verified;
            public FileRecord[] files;
            public string[] checks, captures, authored_unfinished, untextured_sources;
        }
        static readonly List<Camera> Cameras = new List<Camera>();
        static Receipt receipt;
        static bool batch;
        static int frames;

        [MenuItem("Tools/Horror Game/Classic64/Open Complete Library Review")]
        public static void OpenReview()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Horror Game/Classic64/Rebuild Complete Library and Captures")]
        public static void Rebuild()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Start(false);
        }

        public static void RunBatch() { Start(true); }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void Start(bool isBatch)
        {
            batch = isBatch;
            try
            {
                Directory.CreateDirectory(Output);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string manifestPath = Source + "LibraryManifest.json";
                var full = JsonUtility.FromJson<FullManifest>(File.ReadAllText(manifestPath));
                if (full.batch != "Classic64_Library" || full.source_models.Length != 130 || full.source_images.Length != 732
                    || full.assets.Length != full.counts.new_prefabs || full.source_models.Select(s => s.source).Distinct().Count() != 130)
                    throw new InvalidOperationException("Incomplete source coverage in full-library manifest");
                if (full.materials.Select(m => m.name).Distinct().Count() != full.materials.Length
                    || full.materials.Select(m => m.path).Distinct().Count() != full.materials.Length)
                    throw new InvalidOperationException("Material names and paths must be unique across the library");
                if (full.assets.Any(a=>a.authored_unfinished) || full.materials.Any(m=>m.texture==null||string.IsNullOrEmpty(m.texture.path)))
                    throw new InvalidOperationException("Only finished models with painted textures are in the approved library scope");
                var preservedPaths = new HashSet<string>(JsonUtility.FromJson<Classic64LibraryBuilder.Receipt>(File.ReadAllText(Source + "PilotImportReceipt.json")).files.Select(f => f.path));
                var preservedHashes = preservedPaths.ToDictionary(p => p, p => new[] { Hash(p), Hash(p + ".meta") });
                var previousPath = Source + "LibraryImportReceipt.json";
                var previous = File.Exists(previousPath) ? JsonUtility.FromJson<Receipt>(File.ReadAllText(previousPath)) : null;
                foreach (var texture in full.textures)
                    if (Hash(texture.path) != texture.sha256) throw new InvalidOperationException("Texture bytes differ from source: " + texture.path);
                Classic64LibraryBuilder.Import(new Classic64LibraryBuilder.Manifest {
                    batch = full.batch, assets = full.assets, materials = full.materials, surfaces = full.surfaces
                });
                foreach (var image in full.source_images)
                    if (!AssetDatabase.LoadAssetAtPath<Texture2D>(image.texture_path) || !AssetDatabase.LoadAssetAtPath<Material>(image.material_path))
                        throw new InvalidOperationException("Source image not represented: " + image.source);
                foreach (var source in full.source_models)
                {
                    if (source.status == "ExistingPilot" || source.status.StartsWith("ExcludedByUser",StringComparison.Ordinal)) continue;
                    if (source.runtime_paths == null || source.runtime_paths.Length == 0 || source.runtime_paths.Any(p => !AssetDatabase.LoadAssetAtPath<GameObject>(p)))
                        throw new InvalidOperationException("Source model not represented: " + source.source);
                }
                // Preserve the approved pilot byte-for-byte, including metadata.
                foreach (var pair in preservedHashes)
                    if (Hash(pair.Key) != pair.Value[0] || Hash(pair.Key + ".meta") != pair.Value[1])
                        throw new InvalidOperationException("Approved pilot changed: " + pair.Key);
                BuildGallery(full);
                var paths = full.assets.SelectMany(a => new[] { a.model_path, a.prefab_path })
                    .Concat(full.materials.Select(m => m.path)).Concat(full.textures.Select(t => t.path))
                    .Concat(new[] { ScenePath, Folder + "/Classic64_LibraryPlinth.mat" }).Distinct().OrderBy(p => p).ToArray();
                receipt = new Receipt {
                    batch = full.batch, unity_version = Application.unityVersion, completed_utc = DateTime.UtcNow.ToString("o"),
                    review_scene = ScenePath, manifest_sha256 = Hash(manifestPath), source_models = 130, source_images = 732,
                    new_prefabs = full.assets.Length, environment_prefabs = full.assets.Count(a => !a.sandbox_example),
                    example_prefabs = full.assets.Count(a => a.sandbox_example), existing_pilot_prefabs = full.counts.existing_pilot_prefabs,
                    excluded_source_models = full.source_models.Count(s => s.status.StartsWith("ExcludedByUser",StringComparison.Ordinal)),
                    materials = full.materials.Length, textures = full.textures.Length, mesh_parts = full.assets.Sum(a => a.geometry.Length),
                    authored_unfinished = full.assets.Where(a => a.authored_unfinished).Select(a => a.source).Distinct().OrderBy(p => p).ToArray(),
                    untextured_sources = full.materials.Where(m => m.texture == null || string.IsNullOrEmpty(m.texture.path)).Select(m => m.path).ToArray(),
                    checks = new[] { "All 130 source Blender files accounted for as imported or explicitly excluded; all 732 images represented", "Texture SHA-256 hashes match source bytes",
                        "All mesh parts, UV channels, dimensions and ground pivots verified", "Only finished source models; all materials resolve to painted source textures",
                        "Pilot assets and metadata unchanged", "No Blender runtime dependencies or inferred gameplay behaviors",
                        "Examples and library gallery are excluded from enabled build scenes", "Previous import GUIDs retained" },
                    files = paths.Select(p => new FileRecord { path = p, guid = AssetDatabase.AssetPathToGUID(p), preserve_existing = preservedPaths.Contains(p) }).ToArray()
                };
                if (receipt.files.Any(f => string.IsNullOrEmpty(f.guid))) throw new InvalidOperationException("Missing file GUID");
                if (previous != null)
                    foreach (var old in previous.files)
                    {
                        var current = receipt.files.FirstOrDefault(f => f.path == old.path);
                        if (current != null && current.guid != old.guid) throw new InvalidOperationException("Existing GUID changed: " + old.path);
                    }
                if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path.StartsWith(Folder + "/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Classic64 review content enabled in Build Settings");
                frames = 0;
                EditorApplication.update += Finish;
            }
            catch (Exception e) { Fail(e); }
        }

        static void Label(Transform parent, string name, Vector3 position, float size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>(); text.text = name; text.fontSize = 48; text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.8f, .87f, .9f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        }

        static void BuildGallery(FullManifest manifest)
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.25f, .27f, .3f);
            RenderSettings.skybox = null; RenderSettings.fog = false;
            var key = new GameObject("ReviewKey").AddComponent<Light>(); key.type = LightType.Directional;
            key.intensity = 1.1f; key.color = new Color(1, .95f, .87f); key.transform.rotation = Quaternion.Euler(45, -35, 0);
            var fill = new GameObject("ReviewFill").AddComponent<Light>(); fill.type = LightType.Directional;
            fill.intensity = .55f; fill.color = new Color(.65f, .8f, 1); fill.transform.rotation = Quaternion.Euler(25, 140, 0);
            var plinthPath = Folder + "/Classic64_LibraryPlinth.mat";
            var plinth = AssetDatabase.LoadAssetAtPath<Material>(plinthPath);
            if (!plinth) { plinth = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(plinth, plinthPath); }
            plinth.SetColor("_BaseColor", new Color(.08f, .1f, .12f)); plinth.SetFloat("_Smoothness", 0); EditorUtility.SetDirty(plinth);
            var pilot = JsonUtility.FromJson<Classic64LibraryBuilder.Manifest>(File.ReadAllText(Source + "PilotManifest.json"));
            var allAssets = pilot.assets.Concat(manifest.assets).ToArray();
            Cameras.Clear();
            int familyIndex = 0;
            foreach (var group in allAssets.GroupBy(a => a.sandbox_example ? "Examples" : a.family).OrderBy(g => g.Key))
            {
                var root = new GameObject(group.Key + " / Display copies").transform;
                root.position = new Vector3(familyIndex++ * 40, 0, 0);
                var assets = group.OrderBy(a => a.name).ToArray();
                int columns = Mathf.Min(6, assets.Length), rows = Mathf.CeilToInt(assets.Length / (float)columns);
                float width = columns * 3, height = rows * 3.3f;
                Label(root, "CLASSIC64 / " + group.Key.ToUpperInvariant(), new Vector3((width - 3) / 2, 2.9f, 1), .05f);
                for (int index = 0; index < assets.Length; index++)
                {
                    var asset = assets[index];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset.prefab_path));
                    go.transform.SetParent(root, false);
                    var bounds = Classic64LibraryBuilder.BoundsOf(go);
                    float displayScale = 1.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    go.transform.localScale = Vector3.one * displayScale;
                    go.transform.localRotation = Quaternion.Euler(0, -20, 0);
                    bounds = Classic64LibraryBuilder.BoundsOf(go);
                    Vector3 location = new Vector3(index % columns * 3, 0, -(index / columns) * 3.3f);
                    go.transform.localPosition += root.TransformPoint(location + new Vector3(0, .1f, 0)) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    var baseObject = GameObject.CreatePrimitive(PrimitiveType.Cube); baseObject.name = asset.name + "_DisplayBase";
                    baseObject.transform.SetParent(root, false); baseObject.transform.localPosition = location;
                    baseObject.transform.localScale = new Vector3(2.4f, .12f, 2.4f);
                    baseObject.GetComponent<MeshRenderer>().sharedMaterial = plinth; Object.DestroyImmediate(baseObject.GetComponent<Collider>());
                    string label = asset.name.Replace("Classic64_", "") + (asset.authored_unfinished ? " [UNFINISHED]" : "");
                    Label(root, label, location + new Vector3(0, -.2f, -1.25f), .018f);
                    Label(root, $"{asset.bounds_m[0]:0.##} x {asset.bounds_m[1]:0.##} x {asset.bounds_m[2]:0.##} m", location + new Vector3(0, -.42f, -1.25f), .014f);
                }
                var camera = new GameObject(group.Key + "_Models").AddComponent<Camera>();
                camera.transform.SetParent(root, false);
                Vector3 center = new Vector3((width - 3) / 2, .7f, -(height - 3.3f) / 2);
                camera.transform.localPosition = center + new Vector3(0, 16, -18);
                camera.transform.LookAt(root.TransformPoint(center));
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(width / 3.15f, height / 2.15f) + 1.2f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 80; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .035f, .045f); camera.enabled = Cameras.Count == 0;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                if (camera.enabled) camera.tag = "MainCamera";
                Cameras.Add(camera);
            }
            // All standalone textures/materials are browsable in the Project folders;
            // swatches here provide a quick visual catalog without duplicating assets.
            int materialFamily = 0;
            foreach (var group in manifest.materials.Where(m => m.texture != null && !string.IsNullOrEmpty(m.texture.path))
                .GroupBy(m => m.path.Split('/')[3]).OrderBy(g => g.Key))
            {
                var root = new GameObject(group.Key + " / Material swatches").transform;
                root.position = new Vector3(materialFamily++ * 40, 0, 80);
                var records = group.OrderBy(m => m.name).ToArray();
                const int columns = 12;
                for (int i = 0; i < records.Length; i++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = records[i].name;
                    quad.transform.SetParent(root, false); quad.transform.localPosition = new Vector3(i % columns * 1.4f, -(i / columns) * 1.6f, 0);
                    quad.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(records[i].path);
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    Label(root, records[i].name.Replace("Classic64_", ""), quad.transform.localPosition + new Vector3(0, -.65f, -.02f), .009f);
                }
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Library gallery save failed");
        }

        static void Finish()
        {
            if (++frames < 12 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Finish;
            var graphics = GraphicsSettings.defaultRenderPipeline; var quality = QualitySettings.renderPipeline;
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            bool success = false;
            try
            {
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                foreach (var camera in Cameras)
                {
                    var target = new RenderTexture(1600, 1100, 24); var old = RenderTexture.active;
                    var image = new Texture2D(1600, 1100, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 1600, 1100), 0, 0); image.Apply();
                        File.WriteAllBytes(Output + "/" + camera.name + ".png", image.EncodeToPNG());
                    }
                    finally { camera.targetTexture = null; RenderTexture.active = old; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
                }
                foreach (var file in receipt.files) { file.sha256 = Hash(file.path); file.meta_sha256 = Hash(file.path + ".meta"); }
                receipt.captures = Cameras.Select(c => Output + "/" + c.name + ".png").ToArray();
                receipt.verified = true;
                File.WriteAllText(Source + "LibraryImportReceipt.json", JsonUtility.ToJson(receipt, true));
                Debug.Log("CLASSIC64_LIBRARY_VERIFIED new_prefabs=" + receipt.new_prefabs + " materials=" + receipt.materials + " textures=" + receipt.textures);
                success = true;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                QualitySettings.renderPipeline = quality; GraphicsSettings.defaultRenderPipeline = graphics;
                Object.DestroyImmediate(pipeline); Object.DestroyImmediate(renderer);
            }
            if (batch) EditorApplication.Exit(success ? 0 : 1);
        }

        static void Fail(Exception e)
        {
            EditorApplication.update -= Finish; Debug.LogException(e);
            if (batch) EditorApplication.Exit(1);
        }
    }
}
