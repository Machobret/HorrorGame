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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    /// <summary>Builds selected exterior prefab libraries from normalized FBXs without saving scenes.</summary>
    public static class ExteriorBuildingLibraryBuilder
    {
        static string SelectionPath = "ArtSource/Environment/Urban/Selection.json";
        static string ReceiptPath = "ArtSource/Environment/Urban/ImportReceipt.json";
        static string Output = "Exports/Reviews/Urban";
        [Serializable] public class Model
        {
            public string name, source, source_sha256, model_path, prefab_path, pivot, model_sha256;
            public float[] bounds_m;
            public Part[] parts;
            public int triangles, uv_corners, mesh_parts, expected_triangles;
        }
        [Serializable] public class Selection
        {
            public string batch, folder, texture_path, material_path, atlas_sha256, stage_root;
            public Model[] models;
            public Surface[] materials;
        }
        [Serializable] public class FileRecord { public string path, guid, sha256, meta_sha256; }
        [Serializable] public class Receipt
        {
            public bool verified;
            public string batch, unity_version, completed_utc, selection_sha256;
            public Model[] models;
            public FileRecord[] files;
            public string[] checks, captures;
        }
        static string title = "URBAN / SIX APARTMENT EXTERIORS", labelPrefix = "Urban_Apartment";
        static void Configure(string plan, string receiptFile, string output, string heading, string prefix)
        { SelectionPath = plan; ReceiptPath = receiptFile; Output = output; title = heading; labelPrefix = prefix; }
        public static void BuildLibrary(string plan, string receiptFile, string output, string heading, string prefix)
        { Configure(plan, receiptFile, output, heading, prefix); Build(); }
        public static void RunLibraryBatch(string plan, string receiptFile, string output, string heading, string prefix)
        { Configure(plan, receiptFile, output, heading, prefix); RunBatch(); }
        [Serializable] public class Surface { public string path, texture_path, atlas_sha256; }
        [Serializable] public class Part { public string name, material_path; public int triangles; }
        static Surface[] Surfaces(Selection value) => value.materials != null && value.materials.Length > 0 ? value.materials
            : new[] { new Surface { path = value.material_path, texture_path = value.texture_path, atlas_sha256 = value.atlas_sha256 } };
        static Selection selection;
        static Receipt receipt;
        static int frames;
        static RenderPipelineAsset previousGraphics, previousQuality;
        static UniversalRendererData captureRenderer;
        static UniversalRenderPipelineAsset capturePipeline;
        static bool previousAsyncCompilation;

        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static void Build()
        {
            selection = JsonUtility.FromJson<Selection>(File.ReadAllText(SelectionPath));
            if (selection.models.Length != 6
                || selection.models.Select(m => m.name).Distinct().Count() != 6)
                throw new InvalidOperationException("Expected six distinct selected apartment buildings.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var surfaces = Surfaces(selection);
            var materials = surfaces.ToDictionary(surface => surface.path, ImportSurface);

            foreach (var row in selection.models)
            {
                if (Hash(row.model_path) != row.model_sha256) throw new InvalidOperationException("Normalized model changed: " + row.name);
                var importer = (ModelImporter)AssetImporter.GetAtPath(row.model_path);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false; importer.importLights = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importNormals = ModelImporterNormals.Import;
                importer.isReadable = false;
                importer.SaveAndReimport();
                var preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject(row.name + "_Exterior");
                SceneManager.MoveGameObjectToScene(root, preview);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(row.model_path), preview);
                    model.name = row.name + "_Model";
                    model.transform.SetParent(root.transform, false);
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        filter.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(row, filter, materials);
                        var collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh; collider.convex = false;
                        GameObjectUtility.SetStaticEditorFlags(filter.gameObject, StaticEditorFlags.BatchingStatic
                            | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
                    }
                    Verify(root, row, materials);
                    PrefabUtility.SaveAsPrefabAsset(root, row.prefab_path);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var row in selection.models) Verify(Required<GameObject>(row.prefab_path), row, materials);
            var paths = selection.models.SelectMany(m => new[] { m.model_path, m.prefab_path })
                .Concat(surfaces.SelectMany(m => new[] { m.path, m.texture_path })).Distinct().OrderBy(p => p).ToArray();
            receipt = new Receipt
            {
                verified = true, batch = selection.batch, unity_version = Application.unityVersion,
                completed_utc = DateTime.UtcNow.ToString("o"), selection_sha256 = Hash(SelectionPath), models = selection.models,
                files = paths.Select(p => new FileRecord { path = p, guid = AssetDatabase.AssetPathToGUID(p), sha256 = Hash(p), meta_sha256 = Hash(p + ".meta") }).ToArray(),
                checks = new[] { "Six complete exterior prefabs with unit roots and ground-centred pivots",
                    "Source triangle counts, UV channels, normals, material slots and metre bounds verified",
                    "Byte-identical source atlases and double-sided opaque URP Lit materials",
                    "Static non-convex mesh colliders match the visual meshes",
                    "No runtime behaviours, interiors, missing dependencies or scene writes" },
                captures = Array.Empty<string>()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ReceiptPath));
            File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
            Debug.Log(selection.batch + "_VERIFIED prefabs=6 materials=" + surfaces.Length + " textures=" + surfaces.Length);
        }

        static Material ImportSurface(Surface surface)
        {
            if (Hash(surface.texture_path) != surface.atlas_sha256)
                throw new InvalidOperationException("Apartment atlas does not match the supplied source.");
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(surface.texture_path);
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.sRGBTexture = true;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Repeat;
            textureImporter.mipmapEnabled = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.maxTextureSize = 8192;
            textureImporter.alphaSource = TextureImporterAlphaSource.None;
            textureImporter.anisoLevel = 1;
            textureImporter.SaveAndReimport();
            var texture = Required<Texture2D>(surface.texture_path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(surface.path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("URP Lit has shader compilation errors.");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, surface.path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_Cull", (float)CullMode.Off); // Supplied glTF material is double-sided.
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            material.doubleSidedGI = true;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            return material;
        }

        static Material MaterialFor(Model row, MeshFilter filter, Dictionary<string, Material> materials)
        {
            if (row.parts == null || row.parts.Length == 0) return materials[selection.material_path];
            var part = row.parts.SingleOrDefault(p => p.name == filter.sharedMesh.name);
            if (part == null) throw new InvalidOperationException("Unmapped building mesh: " + filter.sharedMesh.name);
            return materials[part.material_path];
        }

        static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing apartment dependency: " + path);

        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Apartment has no renderers.");
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }

        static void Verify(GameObject root, Model row, Dictionary<string, Material> expectedMaterials)
        {
            if (root.transform.localScale != Vector3.one || root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Apartment must be an art-only unit-scale prefab: " + row.name);
            var bounds = BoundsOf(root);
            var expected = new Vector3(row.bounds_m[0], row.bounds_m[1], row.bounds_m[2]);
            if ((bounds.size - expected).magnitude > .025f || Mathf.Abs(bounds.min.y) > .005f
                || Mathf.Abs(bounds.center.x) > .005f || Mathf.Abs(bounds.center.z) > .005f)
                throw new InvalidOperationException("Apartment bounds or ground pivot differ: " + row.name + " " + bounds);
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            long triangles = 0;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) || !mesh.HasVertexAttribute(VertexAttribute.Normal))
                    throw new InvalidOperationException("Apartment mesh is missing UVs or normals: " + row.name);
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i) / 3;
                var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                var material = MaterialFor(row, filter, expectedMaterials);
                var collider = filter.GetComponent<MeshCollider>();
                if (materials.Length != mesh.subMeshCount || materials.Any(m => m != material)
                    || collider == null || collider.sharedMesh != mesh || collider.convex || !collider.enabled)
                    throw new InvalidOperationException("Apartment material or collision mismatch: " + row.name);
            }
            if (triangles != row.triangles || filters.Length != row.mesh_parts)
                throw new InvalidOperationException("Apartment geometry count differs from source: " + row.name);
        }

        // Captures are isolated, unsaved scenes. Existing scene objects/settings are restored.
        public static void RunBatch()
        {
            try
            {
                var plan = JsonUtility.FromJson<Selection>(File.ReadAllText(SelectionPath));
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');
                if (!Application.isBatchMode || !string.Equals(project, Path.GetFullPath(plan.stage_root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Capture batch must run in the isolated validation project.");
                Build();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                previousGraphics = GraphicsSettings.defaultRenderPipeline; previousQuality = QualitySettings.renderPipeline;
                previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
                // Persistent assets keep the batch renderer alive across scene/editor refreshes.
                const string folder = "Assets/ExteriorCaptureValidation";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "ExteriorCaptureValidation");
                captureRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(folder + "/CaptureRenderer.asset");
                if (captureRenderer == null)
                {
                    captureRenderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(captureRenderer, folder + "/CaptureRenderer.asset");
                }
                capturePipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(folder + "/CapturePipeline.asset");
                if (capturePipeline == null)
                {
                    capturePipeline = UniversalRenderPipelineAsset.Create(captureRenderer);
                    AssetDatabase.CreateAsset(capturePipeline, folder + "/CapturePipeline.asset");
                }
                AssetDatabase.SaveAssets();
                GraphicsSettings.defaultRenderPipeline = capturePipeline; QualitySettings.renderPipeline = capturePipeline;
                ShaderUtil.allowAsyncCompilation = false;
                // Let Unity activate URP on subsequent editor frames before Camera.Render.
                frames = 0; EditorApplication.update += FinishCaptures;
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void FinishCaptures()
        {
            if (++frames < 12 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= FinishCaptures;
            var preview = SceneManager.GetActiveScene();
            var success = false;
            try
            {
                var shader = Required<Material>(Surfaces(selection)[0].path).shader;
                Debug.Log("EXTERIOR_CAPTURE_SHADER supported=" + shader.isSupported + " errors=" + ShaderUtil.ShaderHasError(shader)
                    + " graphicsPipeline=" + GraphicsSettings.currentRenderPipeline
                    + " activePipeline=" + RenderPipelineManager.currentPipeline?.GetType().Name);
                Directory.CreateDirectory(Output);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.42f, .45f, .5f);
                var light = new GameObject("Apartment review light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.4f;
                light.transform.rotation = Quaternion.Euler(45, -35, 0);
                var camera = new GameObject("Apartment review camera").AddComponent<Camera>();
                camera.orthographic = true; camera.enabled = false;
                camera.nearClipPlane = .01f; camera.farClipPlane = 500;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .11f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Vector3 direction = new Vector3(1, .72f, -1).normalized;
                camera.transform.rotation = Quaternion.LookRotation(-direction);
                var roots = selection.models.Select(row => (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(row.prefab_path), preview)).ToArray();
                foreach (var root in roots) root.SetActive(false);
                var captures = new List<string>();
                for (int i = 0; i < roots.Length; i++)
                {
                    var root = roots[i]; root.SetActive(true);
                    var bounds = BoundsOf(root); var projected = ProjectedSize(bounds, camera.transform);
                    camera.transform.position = bounds.center + direction * 100;
                    camera.orthographicSize = Mathf.Max(projected.y / 2, projected.x / 2) * 1.18f;
                    string path = Output + "/" + selection.models[i].name + ".png";
                    Capture(camera, path, 1000, 1000); captures.Add(path);
                    root.SetActive(false);
                }
                for (int i = 0; i < roots.Length; i++)
                {
                    var root = roots[i]; root.SetActive(true);
                    var bounds = BoundsOf(root); var projected = ProjectedSize(bounds, camera.transform);
                    float scale = Mathf.Min(5.3f / projected.x, 4.8f / projected.y);
                    Vector3 cell = camera.transform.right * ((i % 3 - 1) * 6.4f) + camera.transform.up * (2.85f - i / 3 * 6.1f);
                    root.transform.localScale = Vector3.one * scale;
                    root.transform.position = cell - bounds.center * scale;
                    Label(selection.models[i].name.Replace(labelPrefix, ""), cell - camera.transform.up * 2.75f, camera.transform, .19f);
                    var size = selection.models[i].bounds_m;
                    Label(size[0].ToString("0.#") + " x " + size[1].ToString("0.#") + " x " + size[2].ToString("0.#") + " m", cell - camera.transform.up * 3.1f, camera.transform, .13f);
                }
                camera.transform.position = direction * 100; camera.orthographicSize = 7.1f;
                Label(title, camera.transform.up * 6.3f, camera.transform, .3f);
                Label("Source geometry / ground-centred pivots / shared atlas / static colliders", camera.transform.up * 5.85f, camera.transform, .15f);
                string overview = Output + "/BuildingExteriors_Overview.png";
                Capture(camera, overview, 1800, 1200); captures.Add(overview);
                receipt.captures = captures.ToArray();
                // URP can attach editor material-version metadata during first import.
                // Record finalized bytes after capture, then prove the next build is stable.
                AssetDatabase.SaveAssets();
                foreach (var file in receipt.files)
                { file.sha256 = Hash(file.path); file.meta_sha256 = Hash(file.path + ".meta"); }
                var renderedReceipt = receipt;
                Build();
                var renderedFiles = renderedReceipt.files.ToDictionary(f => f.path);
                foreach (var file in receipt.files)
                {
                    var prior = renderedFiles[file.path];
                    if (file.guid != prior.guid || file.sha256 != prior.sha256 || file.meta_sha256 != prior.meta_sha256)
                        throw new InvalidOperationException("Repeat build changed a rendered asset: " + file.path);
                }
                receipt.captures = renderedReceipt.captures;
                receipt.checks = receipt.checks.Concat(new[] { "Repeat build preserves all rendered asset GUIDs, bytes and metadata" }).ToArray();
                File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
                success = true;
                Debug.Log(selection.batch + "_CAPTURED captures=" + captures.Count);
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = previousGraphics; QualitySettings.renderPipeline = previousQuality;
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
            }
            EditorApplication.Exit(success ? 0 : 1);
        }

        static Vector2 ProjectedSize(Bounds bounds, Transform camera)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var point = new Vector2(Vector3.Dot(offset, camera.right), Vector3.Dot(offset, camera.up));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return max - min;
        }

        static void Label(string text, Vector3 position, Transform camera, float size)
        {
            var label = new GameObject("Review label").AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.characterSize = size * .2f; label.fontSize = 48; label.color = new Color(.83f, .88f, .9f);
            label.transform.SetPositionAndRotation(position - camera.forward * 5, camera.rotation);
        }

        static void Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = (float)width / height; camera.targetTexture = target;
                camera.Render(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                int magenta = image.GetPixels32().Count(p => p.r > 220 && p.b > 220 && p.g < 30);
                if (magenta > width * height / 100) throw new InvalidOperationException("Preview contains a missing-shader surface: " + path);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
    }
}
