// Offline quality evidence and native Unity labels; runs only in the isolated review project.
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

public static class DownloadedPrefabReview
{
    [Serializable] public class Entry
    {
        public string id, pack, source_object, prefab, guid, status, category;
        public string[] missing_images, reasons, labels;
        public bool fallback;
        public float[] bounds_m;
        public int triangles, mesh_parts;
    }
    [Serializable] public class Input { public string scope; public Entry[] assets; }
    [Serializable] public class Metric
    {
        public string id, prefab;
        public int triangles, vertices, degenerate_triangles, invalid_vertices, front_pixels, back_pixels, textureless_opaque_slots;
        public float surface_area_m2, thin_ratio;
    }
    [Serializable] public class Metrics { public Metric[] assets; }
    static readonly Color Background = new Color(.10f, .13f, .17f);
    static string Output = "../QualityReview/";
    static string InputPath = "ReviewInput.json";
    const int Tile = 224;
    static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]> {
        ['A']=new byte[]{126,17,17,17,126},['B']=new byte[]{127,73,73,73,54},['C']=new byte[]{62,65,65,65,34},
        ['D']=new byte[]{127,65,65,34,28},['E']=new byte[]{127,73,73,73,65},['F']=new byte[]{127,9,9,9,1},
        ['G']=new byte[]{62,65,73,73,122},['H']=new byte[]{127,8,8,8,127},['I']=new byte[]{0,65,127,65,0},
        ['J']=new byte[]{32,64,65,63,1},['K']=new byte[]{127,8,20,34,65},['L']=new byte[]{127,64,64,64,64},
        ['M']=new byte[]{127,2,12,2,127},['N']=new byte[]{127,4,8,16,127},['O']=new byte[]{62,65,65,65,62},
        ['P']=new byte[]{127,9,9,9,6},['Q']=new byte[]{62,65,81,33,94},['R']=new byte[]{127,9,25,41,70},
        ['S']=new byte[]{70,73,73,73,49},['T']=new byte[]{1,1,127,1,1},['U']=new byte[]{63,64,64,64,63},
        ['V']=new byte[]{31,32,64,32,31},['W']=new byte[]{63,64,56,64,63},['X']=new byte[]{99,20,8,20,99},
        ['Y']=new byte[]{7,8,112,8,7},['Z']=new byte[]{97,81,73,69,67},
        ['0']=new byte[]{62,81,73,69,62},['1']=new byte[]{0,66,127,64,0},['2']=new byte[]{66,97,81,73,70},
        ['3']=new byte[]{33,65,69,75,49},['4']=new byte[]{24,20,18,127,16},['5']=new byte[]{39,69,69,69,57},
        ['6']=new byte[]{60,74,73,73,48},['7']=new byte[]{1,113,9,5,3},['8']=new byte[]{54,73,73,73,54},
        ['9']=new byte[]{6,73,73,41,30},['_']=new byte[]{64,64,64,64,64},['.']=new byte[]{0,96,96,0,0},
        ['-']=new byte[]{8,8,8,8,8},[' ']=new byte[]{0,0,0,0,0}
    };

    static void Text(Texture2D image, string text, int x, int y)
    {
        foreach (char character in text.ToUpperInvariant())
        {
            var glyph = Glyphs.TryGetValue(character, out var value) ? value : Glyphs['-'];
            for (int col = 0; col < 5; col++) for (int row = 0; row < 7; row++)
                if ((glyph[col] & (1 << row)) != 0)
                    for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++)
                        image.SetPixel(x + col * 2 + dx, y - row * 2 - dy, new Color(.86f, .9f, .95f));
            x += 12;
        }
    }

    static Metric Measure(Entry entry, GameObject instance)
    {
        var metric = new Metric { id = entry.id, prefab = entry.prefab,
            thin_ratio = entry.bounds_m.Min() / Mathf.Max(.001f, entry.bounds_m.Max()) };
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh; var vertices = mesh.vertices; var indices = mesh.triangles;
            metric.vertices += vertices.Length; metric.triangles += indices.Length / 3;
            foreach (var p in vertices) if (float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z)) metric.invalid_vertices++;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = filter.transform.TransformPoint(vertices[indices[i]]);
                var b = filter.transform.TransformPoint(vertices[indices[i + 1]]);
                var c = filter.transform.TransformPoint(vertices[indices[i + 2]]);
                float area = Vector3.Cross(b - a, c - a).magnitude * .5f;
                metric.surface_area_m2 += area;
                if (area < 1e-9f) metric.degenerate_triangles++;
            }
            metric.textureless_opaque_slots += filter.GetComponent<Renderer>().sharedMaterials
                .Count(m => !m.mainTexture && m.GetFloat("_Surface") < .5f);
        }
        return metric;
    }

    public static void RenderBatch()
    {
        try
        {
            var input = JsonUtility.FromJson<Input>(File.ReadAllText(InputPath));
            Directory.CreateDirectory(Output + "thumbnails"); Directory.CreateDirectory(Output + "sheets");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
            var key = new GameObject("Key").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.5f;
            key.transform.rotation = Quaternion.Euler(45, -35, 0);
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .6f;
            fill.transform.rotation = Quaternion.Euler(35, 145, 0);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
            camera.orthographic = true; camera.nearClipPlane = .001f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var target = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32);
            var tile = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            var metrics = new List<Metric>();
            foreach (var pack in input.assets.GroupBy(a => a.pack))
            {
                var entries = pack.ToArray();
                Texture2D sheet = null; int sheetNumber = 0;
                for (int index = 0; index < entries.Length; index++)
                {
                    if (index % 36 == 0)
                    {
                        sheetNumber++;
                        sheet = new Texture2D(Tile * 6, (Tile + 48) * 6, TextureFormat.RGB24, false);
                        sheet.SetPixels(Enumerable.Repeat(Background, sheet.width * sheet.height).ToArray());
                    }
                    var entry = entries[index];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab));
                    try
                    {
                        var metric = Measure(entry, instance);
                        var renderers = instance.GetComponentsInChildren<Renderer>();
                        var bounds = renderers[0].bounds;
                        foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                        float radius = Mathf.Max(.03f, bounds.extents.magnitude);
                        camera.orthographicSize = radius * 1.05f; camera.farClipPlane = Mathf.Max(100, radius * 10);
                        for (int angle = 0; angle < 2; angle++)
                        {
                            camera.transform.position = bounds.center + new Vector3(angle == 0 ? 1 : -1, .65f, angle == 0 ? 1 : -1).normalized * radius * 4;
                            camera.transform.LookAt(bounds.center); camera.Render(); camera.Render();
                            RenderTexture.active = target; tile.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0); tile.Apply();
                            var pixels = tile.GetPixels32(); var background = pixels[0];
                            if (pixels.Count(p => p.r > 230 && p.b > 230 && p.g < 30) > 100)
                                throw new InvalidOperationException("Shader error pixels: " + entry.prefab);
                            int visible = pixels.Count(p => Math.Abs(p.r - background.r) + Math.Abs(p.g - background.g) + Math.Abs(p.b - background.b) > 24);
                            if (angle == 0) metric.front_pixels = visible; else metric.back_pixels = visible;
                            File.WriteAllBytes(Output + "thumbnails/" + entry.id + (angle == 0 ? "" : "_back") + ".png", tile.EncodeToPNG());
                            if (angle == 0)
                            {
                                int x = index % 6 * Tile, y = (5 - index % 36 / 6) * (Tile + 48);
                                sheet.SetPixels(x, y + 48, Tile, Tile, tile.GetPixels());
                                string label = entry.id + " " + entry.source_object.ToUpperInvariant();
                                Text(sheet, label.Substring(0, Math.Min(18, label.Length)), x + 4, y + 38);
                                if (label.Length > 18) Text(sheet, label.Substring(18, Math.Min(18, label.Length - 18)), x + 4, y + 18);
                            }
                        }
                        metrics.Add(metric);
                    }
                    finally { Object.DestroyImmediate(instance); }
                    if (index % 36 == 35 || index == entries.Length - 1)
                    {
                        sheet.Apply(); File.WriteAllBytes(Output + "sheets/" + pack.Key + "_" + sheetNumber.ToString("00") + ".png", sheet.EncodeToPNG());
                        Object.DestroyImmediate(sheet); Debug.Log("REVIEW_SHEET " + pack.Key + " " + sheetNumber + " completed=" + (index + 1));
                    }
                }
                File.WriteAllText(Output + "ReviewMetrics.json", JsonUtility.ToJson(new Metrics { assets = metrics.ToArray() }, true));
            }
            Debug.Log("REVIEW_RENDERED " + metrics.Count); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(Output + "ReviewFailure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void RenderRepairsBatch()
    {
        DownloadedPrefabMaintenance.VerifyRetained();
        Output = "../MaintenanceReview/";
        InputPath = "MaintenanceReviewInput.json";
        RenderBatch();
    }

    public static void LabelBatch()
    {
        try
        {
            var input = JsonUtility.FromJson<Input>(File.ReadAllText("QualityCatalog.json"));
            var owned = new HashSet<string>(new[] { "DownloadedPack", "DownloadedKeep", "DownloadedReview", "DownloadedRepair", "DownloadedBackgroundOnly", "DownloadedExclude",
                "DownloadedMissingTextures", "DownloadedFallbackMaterial", "DownloadedScaleReview", "DownloadedArchitectureReview", "DownloadedComponents", "DownloadedVegetationReview",
                "DownloadedSettingReview", "DownloadedGeometryCleanup", "DownloadedProxyGeometry", "DownloadedSampleLayout", "DownloadedInvalidGeometry", "DownloadedVisualQualityReview", "DownloadedScaleCalibrated" });
            foreach (var entry in input.assets)
            {
                if (AssetDatabase.AssetPathToGUID(entry.prefab) != entry.guid) throw new InvalidOperationException("GUID mismatch: " + entry.prefab);
                var prefab = AssetDatabase.LoadMainAssetAtPath(entry.prefab);
                var labels = AssetDatabase.GetLabels(prefab).Where(l => !owned.Contains(l))
                    .Concat(entry.labels).Distinct().ToArray();
                if (!new HashSet<string>(AssetDatabase.GetLabels(prefab)).SetEquals(labels)) AssetDatabase.SetLabels(prefab, labels);
            }
            AssetDatabase.SaveAssets();
            if (!File.Exists("MaintenancePlan.json")) ComposeEvidence(input);
            File.WriteAllText(Output + "LabelsVerified.txt", "Labelled " + input.assets.Length + " prefab assets through AssetDatabase; GUIDs verified before assignment.\n");
            Debug.Log("REVIEW_LABELLED " + input.assets.Length); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(Output + "LabelFailure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void ComposeEvidence(Input input)
    {
        var highlights = new[] { "P0127", "P0181", "P0183", "P0396", "P0461", "P0227", "P0926", "P0757" };
        var captions = new[] { "MISSING TEXTURE", "MISSING TEXTURE", "FALLBACK MATERIAL", "FALLBACK MATERIAL", "PHOTO PROXY", "DETACHED PARTS", "SOURCE ARRAY", "19 DEGENERATE TRIS" };
        var sheet = new Texture2D(Tile * 4, (Tile + 48) * 2, TextureFormat.RGB24, false);
        sheet.SetPixels(Enumerable.Repeat(Background, sheet.width * sheet.height).ToArray());
        for (int i = 0; i < highlights.Length; i++)
        {
            var tile = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
            tile.LoadImage(File.ReadAllBytes(Output + "thumbnails/" + highlights[i] + (highlights[i] == "P0461" ? "_back" : "") + ".png"));
            int x = i % 4 * Tile, y = (1 - i / 4) * (Tile + 48);
            sheet.SetPixels(x, y + 48, Tile, Tile, tile.GetPixels());
            Text(sheet, highlights[i], x + 4, y + 38); Text(sheet, captions[i], x + 4, y + 18);
            Object.DestroyImmediate(tile);
        }
        sheet.Apply(); File.WriteAllBytes(Output + "FindingsOverview.png", sheet.EncodeToPNG()); Object.DestroyImmediate(sheet);
        foreach (string group in new[] { "MissingTextures", "FallbackMaterial" })
        {
            var entries = input.assets.Where(a => group == "MissingTextures" ? a.missing_images.Length > 0 : a.fallback).ToArray();
            for (int page = 0; page * 18 < entries.Length; page++)
            {
                var atlas = new Texture2D(Tile * 6, (Tile + 48) * 3, TextureFormat.RGB24, false);
                atlas.SetPixels(Enumerable.Repeat(Background, atlas.width * atlas.height).ToArray());
                for (int index = 0; index < 18 && page * 18 + index < entries.Length; index++)
                {
                    var entry = entries[page * 18 + index];
                    var image = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
                    image.LoadImage(File.ReadAllBytes(Output + "thumbnails/" + entry.id + ".png"));
                    int x = index % 6 * Tile, y = (2 - index / 6) * (Tile + 48);
                    atlas.SetPixels(x, y + 48, Tile, Tile, image.GetPixels());
                    string label = entry.id + " " + entry.source_object;
                    Text(atlas, label.Substring(0, Math.Min(18, label.Length)), x + 4, y + 38);
                    if (label.Length > 18) Text(atlas, label.Substring(18, Math.Min(18, label.Length - 18)), x + 4, y + 18);
                    Object.DestroyImmediate(image);
                }
                atlas.Apply(); File.WriteAllBytes(Output + "sheets/" + group + "_" + (page + 1).ToString("00") + ".png", atlas.EncodeToPNG()); Object.DestroyImmediate(atlas);
            }
        }
    }
}
