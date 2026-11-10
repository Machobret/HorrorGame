using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    /// <summary>Imports the reviewed Classic64 manifest and builds its isolated art review scene.</summary>
    public static class Classic64LibraryBuilder
    {
        const string ManifestPath = "ArtSource/Environment/Classic64/PilotManifest.json";
        const string ReceiptPath = "ArtSource/Environment/Classic64/PilotImportReceipt.json";
        const string ReviewFolder = "Assets/Sandbox/Classic64";
        public const string ScenePath = ReviewFolder + "/Classic64_PilotReview.unity";
        const string Output = "Exports/Reviews/Classic64";

        [Serializable] public class TextureRecord { public string path, source, sha256; public int width, height; public bool cutout, preserve_existing; }
        [Serializable] public class MaterialRecord { public string name, path, source_material; public TextureRecord texture; public bool cutout, double_sided, preserve_existing; public float[] base_color; }
        [Serializable] public class GeometryRecord { public string source_object, exported_object; public int vertices, polygons, uv_corners; }
        [Serializable] public class AssetRecord
        {
            public string name, source, family, model_path, prefab_path, pivot;
            public string[] material_names;
            public GeometryRecord[] geometry;
            public float[] bounds_m, original_bounds_m, pivot_source;
            public float scale_correction;
            public bool authored_unfinished, sandbox_example;
        }
        [Serializable] public class SurfaceRecord { public string name, material_path; }
        [Serializable] public class Manifest { public string batch; public AssetRecord[] assets; public MaterialRecord[] materials; public SurfaceRecord[] surfaces; }
        [Serializable] public class ImportedFile { public string path, guid; }
        [Serializable] public class Receipt
        {
            public string batch, unity_version, completed_utc, review_scene, render_pipeline, capture_note;
            public int prefabs, materials, textures, mesh_parts;
            public bool verified;
            public ImportedFile[] files;
            public string[] checks, captures;
        }
        static readonly List<Camera> CaptureCameras = new List<Camera>();
        static Receipt receipt;
        static bool batch;
        static int warmup;

        [MenuItem("Tools/Horror Game/Classic64/Open Pilot Review")]
        public static void OpenReview()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Horror Game/Classic64/Rebuild Pilot and Captures")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Start(false);
        }

        public static void RunBatch() { Start(true); }

        static void Start(bool isBatch)
        {
            batch = isBatch;
            try
            {
                Directory.CreateDirectory(Output);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                if (manifest?.assets == null || manifest.assets.Length != 11 || manifest.surfaces.Length != 6)
                    throw new InvalidOperationException("Current pilot scope is 11 props and six surfaces; standalone building pieces are excluded.");
                var previous = File.Exists(ReceiptPath) ? JsonUtility.FromJson<Receipt>(File.ReadAllText(ReceiptPath)) : null;
                Import(manifest);
                BuildReview(manifest);
                var paths = manifest.assets.SelectMany(a => new[] { a.model_path, a.prefab_path })
                    .Concat(manifest.materials.SelectMany(m => new[] { m.path, m.texture.path }))
                    .Concat(new[] { ScenePath, ReviewFolder + "/Classic64_ReviewFloor.mat", ReviewFolder + "/Classic64_ReviewPlinth.mat" })
                    .Distinct().OrderBy(p => p).ToArray();
                receipt = new Receipt {
                    batch = manifest.batch, unity_version = Application.unityVersion, completed_utc = DateTime.UtcNow.ToString("o"),
                    review_scene = ScenePath, render_pipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),
                    capture_note = "Neutral captures use a temporary plain URP renderer for asset inspection; horror captures use the game's original render pipeline and retro effects.",
                    prefabs = manifest.assets.Length, materials = manifest.materials.Length,
                    textures = manifest.materials.Select(m => m.texture.path).Distinct().Count(),
                    mesh_parts = manifest.assets.Sum(a => a.geometry.Length), verified = false,
                    files = paths.Select(p => new ImportedFile { path = p, guid = AssetDatabase.AssetPathToGUID(p) }).ToArray(),
                    checks = new[] { "Imported bounds match exported dimensions", "All authored mesh parts and UV channels present",
                        "Material slots resolve to textured URP materials", "Original texture sizes preserved; point filtering and mipmaps",
                        "Cutout alpha and coverage mipmaps enabled where required", "Visual prefabs contain no gameplay scripts or colliders",
                        "No runtime dependency on Blender sources", "Existing receipt GUIDs preserved", "Review scene is excluded from enabled build scenes" }
                };
                if (receipt.files.Any(f => string.IsNullOrEmpty(f.guid))) throw new InvalidOperationException("Missing GUID");
                if (previous != null)
                    foreach (var file in previous.files)
                    {
                        var current = receipt.files.FirstOrDefault(f => f.path == file.path);
                        if (current != null && current.guid != file.guid) throw new InvalidOperationException("GUID changed: " + file.path);
                    }
                if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                    throw new InvalidOperationException("Review scene must not be enabled in Build Settings");
                warmup = 0;
                EditorApplication.update += FinishCaptures;
            }
            catch (Exception e) { Fail(e); }
        }

        internal static void Import(Manifest manifest)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("URP Lit unavailable");
            var materials = new Dictionary<string, Material>();
            var configuredTextures = new HashSet<string>();
            foreach (var record in manifest.materials)
            {
                Texture2D texture = null;
                bool hasTexture = record.texture != null && !string.IsNullOrEmpty(record.texture.path);
                if (hasTexture)
                {
                    var importer = AssetImporter.GetAtPath(record.texture.path) as TextureImporter;
                    if (!importer) throw new InvalidOperationException("Missing texture: " + record.texture.path);
                    if (!record.texture.preserve_existing && configuredTextures.Add(record.texture.path))
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.sRGBTexture = true; importer.filterMode = FilterMode.Point;
                        importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(record.texture.width, record.texture.height)));
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.alphaSource = record.cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                        importer.alphaIsTransparency = record.cutout; importer.mipMapsPreserveCoverage = record.cutout;
                        importer.alphaTestReferenceValue = .5f; importer.isReadable = false;
                        importer.SaveAndReimport();
                    }
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(record.texture.path);
                    if (!texture || texture.width != record.texture.width || texture.height != record.texture.height)
                        throw new InvalidOperationException("Texture dimensions changed: " + record.texture.path);
                }
                var mat = AssetDatabase.LoadAssetAtPath<Material>(record.path);
                if (record.preserve_existing)
                {
                    if (!mat || mat.shader != shader || mat.mainTexture != texture)
                        throw new InvalidOperationException("Preserved pilot material differs: " + record.path);
                    materials.Add(record.name, mat);
                    continue;
                }
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, record.path); }
                mat.shader = shader; mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", record.base_color != null && record.base_color.Length == 4
                    ? new Color(record.base_color[0], record.base_color[1], record.base_color[2], record.base_color[3]) : Color.white);
                mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", .1f); mat.SetFloat("_Surface", 0);
                mat.SetFloat("_AlphaClip", record.cutout ? 1 : 0); mat.SetFloat("_Cutoff", .5f);
                mat.SetFloat("_Cull", (float)(record.double_sided ? CullMode.Off : CullMode.Back));
                mat.SetFloat("_SrcBlend", (float)BlendMode.One); mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1); mat.SetFloat("_AlphaToMask", record.cutout ? 1 : 0);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (record.cutout) mat.EnableKeyword("_ALPHATEST_ON"); else mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", record.cutout ? "TransparentCutout" : "Opaque");
                mat.renderQueue = record.cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                mat.enableInstancing = true; mat.doubleSidedGI = record.double_sided;
                EditorUtility.SetDirty(mat); materials.Add(record.name, mat);
            }
            foreach (var asset in manifest.assets)
            {
                var importer = AssetImporter.GetAtPath(asset.model_path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing FBX: " + asset.model_path);
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.importBlendShapes = false; importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.preserveHierarchy = true;
                importer.isReadable = false; importer.generateSecondaryUV = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var name in asset.material_names)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[name]);
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(asset.model_path);
                var go = new GameObject(asset.name);
                try
                {
                    var visual = Object.Instantiate(model, go.transform, false);
                    visual.name = asset.name + "_Model";
                    var parts = visual.GetComponentsInChildren<MeshFilter>();
                    if (parts.Length != asset.geometry.Length) throw new InvalidOperationException("Missing mesh parts: " + asset.name);
                    foreach (var part in parts)
                    {
                        var source = asset.geometry.SingleOrDefault(p => p.exported_object == part.name);
                        var mesh = part.sharedMesh;
                        var renderer = part.GetComponent<MeshRenderer>();
                        if (source == null || !mesh || mesh.vertexCount == 0 || !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                            throw new InvalidOperationException("Missing mesh or UV channel: " + part.name);
                        if (!renderer || renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => !m || !materials.Values.Contains(m)))
                            throw new InvalidOperationException("Unresolved material slots: " + part.name);
                    }
                    var bounds = BoundsOf(go);
                    var expected = new Vector3(asset.bounds_m[0], asset.bounds_m[1], asset.bounds_m[2]);
                    if ((bounds.size - expected).magnitude > Mathf.Max(.002f, expected.magnitude * .002f) || Mathf.Abs(bounds.min.y) > .002f)
                        throw new InvalidOperationException("Scale/pivot mismatch: " + asset.name + " got " + bounds + " expected size " + expected);
                    if (go.GetComponentsInChildren<MonoBehaviour>().Length != 0 || go.GetComponentsInChildren<Collider>().Length != 0)
                        throw new InvalidOperationException("Art import added behavior");
                    if (!PrefabUtility.SaveAsPrefabAsset(go, asset.prefab_path)) throw new InvalidOperationException("Prefab save failed");
                    var prefabImporter = AssetImporter.GetAtPath(asset.prefab_path);
                    prefabImporter.userData = JsonUtility.ToJson(asset); prefabImporter.SaveAndReimport();
                }
                finally { Object.DestroyImmediate(go); }
                if (AssetDatabase.GetDependencies(asset.prefab_path, true).Any(p => p.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Blender runtime dependency: " + asset.name);
            }
            AssetDatabase.SaveAssets();
        }

        internal static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No renderer: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static Material ReviewMaterial(string name, Color color)
        {
            var path = ReviewFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .08f);
            EditorUtility.SetDirty(m); return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }

        static void Label(Transform parent, string text, Vector3 position, float size = .07f)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.layer = 5; // Built-in UI layer; detail cameras exclude gallery labels.
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = new Color(.78f, .84f, .87f); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }

        static void Spot(Transform parent, string name, Vector3 position, Vector3 target, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent, false);
            light.transform.localPosition = position; light.transform.LookAt(parent.TransformPoint(target));
            light.type = LightType.Spot; light.range = 18; light.spotAngle = 110; light.innerSpotAngle = 65;
            light.color = color; light.intensity = intensity; light.shadows = LightShadows.Soft;
        }

        static Camera ReviewCamera(Transform parent, string name, Vector3 position, Vector3 lookAt, float size)
        {
            var camera = new GameObject(name).AddComponent<Camera>(); camera.transform.SetParent(parent, false);
            camera.transform.localPosition = position; camera.transform.LookAt(parent.TransformPoint(lookAt));
            camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .01f; camera.farClipPlane = 40;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .045f);
            camera.enabled = false; camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            CaptureCameras.Add(camera); return camera;
        }

        static void BuildReview(Manifest manifest)
        {
            Directory.CreateDirectory(ReviewFolder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CaptureCameras.Clear();
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.18f, .20f, .23f);
            RenderSettings.fog = false; RenderSettings.skybox = null;
            var floor = ReviewMaterial("Classic64_ReviewFloor", new Color(.10f, .12f, .14f));
            var plinth = ReviewMaterial("Classic64_ReviewPlinth", new Color(.22f, .25f, .28f));
            for (int zone = 0; zone < 2; zone++)
            {
                bool horror = zone == 1;
                var stage = new GameObject(horror ? "HorrorLighting" : "NeutralLighting").transform;
                stage.position = new Vector3(zone * 24, 0, 0);
                Box(stage, "ReviewFloor", new Vector3(0, -.12f, .6f), new Vector3(13, .2f, 10), floor);
                Label(stage, horror ? "CLASSIC64 / HORROR LIGHTING" : "CLASSIC64 / NEUTRAL LIGHTING", new Vector3(0, 4.95f, 4.4f), .055f);
                Label(stage, "PILOT  /  11 PROPS  +  6 SURFACES  /  METRE SCALE", new Vector3(0, 4.57f, 4.4f), .025f);
                for (int i = 0; i < manifest.surfaces.Length; i++)
                {
                    float x = -4.75f + i * 1.9f;
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(manifest.surfaces[i].material_path);
                    Box(stage, manifest.surfaces[i].name, new Vector3(x, 3.5f, 4.4f), new Vector3(1.65f, 1.65f, .08f), mat);
                    Label(stage, manifest.surfaces[i].name.Replace("Classic64_", ""), new Vector3(x, 2.46f, 4.3f), .025f);
                }
                var placements = new Dictionary<string, Vector3> {
                    { "BrownDoor", new Vector3(-5, 0, 1.4f) }, { "CommercialShelf", new Vector3(-2.5f, 0, 1.4f) },
                    { "PumpStation", new Vector3(.4f, 0, 1.4f) }, { "Generator", new Vector3(3.7f, 0, 1.4f) },
                    { "Shovel", new Vector3(-4.6f, 0, -1.35f) }, { "ExtinguisherCase", new Vector3(-3.3f, .55f, -1.35f) },
                    { "CardboardBox01", new Vector3(2.6f, 0, -1.35f) }, { "Bush01", new Vector3(4.2f, 0, -1.35f) },
                    { "BookBlack01", new Vector3(-1.4f, .95f, -1.7f) }, { "SoupCan01", new Vector3(-.95f, .95f, -1.7f) },
                    { "SodaCan01", new Vector3(-.55f, .95f, -1.7f) }, { "JunctionBox", new Vector3(-.1f, .95f, -1.7f) }
                };
                Box(stage, "SmallPropsReviewTable", new Vector3(-.75f, .45f, -1.7f), new Vector3(2.25f, .9f, 1), plinth);
                Box(stage, "CaseReviewPlinth", new Vector3(-3.3f, .25f, -1.35f), new Vector3(.65f, .5f, .65f), plinth);
                foreach (var asset in manifest.assets)
                {
                    var key = asset.name.Replace("Classic64_", "");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset.prefab_path));
                    instance.transform.SetParent(stage, false); instance.transform.localPosition = placements[key];
                    // A shallow turn exposes source depth and thin/double-sided surfaces.
                    instance.transform.localRotation = Quaternion.Euler(0, key == "BrownDoor" ? -12 : key == "Bush01" ? 0 : -18, 0);
                    if (key != "BookBlack01" && key != "SoupCan01" && key != "SodaCan01" && key != "JunctionBox")
                        Label(stage, key, new Vector3(placements[key].x + (key == "BrownDoor" ? .45f : 0), .14f, placements[key].z - .9f), .022f);
                }
                Label(stage, "BOOK / SOUP / SODA / JUNCTION BOX", new Vector3(-.75f, .65f, -2.22f), .02f);
                if (horror)
                {
                    Spot(stage, "WarmKey", new Vector3(-4, 6, -3), new Vector3(-1, 1, 1), new Color(1, .47f, .17f), 60);
                    Spot(stage, "ColdRim", new Vector3(5, 5, 3), new Vector3(0, 1, .5f), new Color(.15f, .55f, 1), 65);
                }
                else
                {
                    Spot(stage, "NeutralKey", new Vector3(-4, 7, -4), new Vector3(0, 1, 1), new Color(1, .94f, .86f), 50);
                    Spot(stage, "NeutralFill", new Vector3(5, 5, -2), new Vector3(0, 1, 1), new Color(.72f, .84f, 1), 30);
                }
                string prefix = horror ? "Horror" : "Neutral";
                var overview = ReviewCamera(stage, prefix + "_Overview", new Vector3(0, 7.1f, -11), new Vector3(0, 2.5f, .7f), 4.25f);
                if (!horror) { overview.enabled = true; overview.tag = "MainCamera"; }
                var detail = ReviewCamera(stage, prefix + "_SmallProps", new Vector3(-.6f, 1.85f, -3.3f), new Vector3(-.75f, 1.05f, -1.7f), .64f);
                detail.cullingMask &= ~(1 << 5);
                ReviewCamera(stage, prefix + "_SurfaceSamples", new Vector3(0, 3.5f, -4), new Vector3(0, 3.5f, 4.4f), 1.9f);
            }
            if (GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(m => !m))
                throw new InvalidOperationException("Missing script in review scene");
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Review scene save failed");
            AssetDatabase.SaveAssets();
        }

        static void FinishCaptures()
        {
            if (++warmup < 12 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= FinishCaptures;
            var originalGraphics = GraphicsSettings.defaultRenderPipeline;
            var originalQuality = QualitySettings.renderPipeline;
            var inspectionRenderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            var inspectionPipeline = UniversalRenderPipelineAsset.Create(inspectionRenderer);
            try
            {
                foreach (var camera in CaptureCameras)
                {
                    bool inspection = camera.name.StartsWith("Neutral", StringComparison.Ordinal);
                    GraphicsSettings.defaultRenderPipeline = inspection ? inspectionPipeline : originalGraphics;
                    QualitySettings.renderPipeline = inspection ? inspectionPipeline : originalQuality;
                    int height = camera.name.Contains("SurfaceSamples") ? 500 : 1100;
                    var target = new RenderTexture(1800, height, 24, RenderTextureFormat.ARGB32);
                    var old = RenderTexture.active;
                    var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render(); camera.Render();
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                        File.WriteAllBytes(Output + "/" + camera.name + ".png", image.EncodeToPNG());
                    }
                    finally { camera.targetTexture = null; RenderTexture.active = old; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
                }
                receipt.captures = CaptureCameras.Select(c => Output + "/" + c.name + ".png").ToArray();
                receipt.verified = true;
                File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
                Debug.Log("CLASSIC64_PILOT_VERIFIED props=" + receipt.prefabs + " materials=" + receipt.materials + " parts=" + receipt.mesh_parts);
                if (batch) EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail(e); }
            finally
            {
                QualitySettings.renderPipeline = originalQuality;
                GraphicsSettings.defaultRenderPipeline = originalGraphics;
                Object.DestroyImmediate(inspectionPipeline); Object.DestroyImmediate(inspectionRenderer);
            }
        }

        static void Fail(Exception e)
        {
            EditorApplication.update -= FinishCaptures;
            Debug.LogException(e);
            if (batch) EditorApplication.Exit(1);
        }
    }
}
