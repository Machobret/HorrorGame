// Batch-only visual verification in the ignored import project.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class DownloadedEnvironmentPackCaptures
{
    public static void RunBatch()
    {
        try
        {
            var receipt = JsonUtility.FromJson<DownloadedEnvironmentPackImporter.Receipt>(File.ReadAllText("ImportReceipt.json"));
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline = UniversalRenderPipelineAsset.Create(data);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
            var light = new GameObject("ReviewLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2.2f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .13f, .17f);
            camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 10000;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Directory.CreateDirectory("../Captures");
            var target = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
            var tile = new Texture2D(320, 320, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            foreach (var pack in receipt.packs)
            {
                var candidates = receipt.assets.Where(a => a.pack == pack.name && string.IsNullOrEmpty(a.duplicate_of)).ToArray();
                // Include multipart assemblies, source-texture caveats and evenly spread objects.
                var selected = candidates.Where(a => a.mesh_parts > 1).Take(4)
                    .Concat(candidates.Where(a => a.source_texture_warnings.Length > 0).Take(2))
                    .Concat(Enumerable.Range(0, 16).Select(i => candidates[Math.Min(candidates.Length - 1, i * candidates.Length / 16)]))
                    .Distinct().Take(16).ToArray();
                var atlas = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
                atlas.SetPixels(Enumerable.Repeat(new Color(.1f, .13f, .17f), 1280 * 1280).ToArray());
                for (int i = 0; i < selected.Length; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(selected[i].prefab));
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    float radius = Mathf.Max(.05f, bounds.extents.magnitude);
                    camera.orthographicSize = radius * 1.08f;
                    camera.transform.position = bounds.center + new Vector3(1, .7f, 1).normalized * radius * 4;
                    camera.transform.LookAt(bounds.center);
                    camera.farClipPlane = Mathf.Max(100, radius * 10);
                    camera.Render(); camera.Render();
                    RenderTexture.active = target;
                    tile.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); tile.Apply();
                    if (tile.GetPixels32().Count(c => c.r > 230 && c.b > 230 && c.g < 30) > 1000)
                        throw new InvalidOperationException("Magenta/error shader in capture: " + selected[i].prefab);
                    atlas.SetPixels((i % 4) * 320, (3 - i / 4) * 320, 320, 320, tile.GetPixels());
                    Object.DestroyImmediate(instance);
                }
                atlas.Apply(); File.WriteAllBytes("../Captures/" + pack.name + ".png", atlas.EncodeToPNG());
                File.WriteAllText("../Captures/" + pack.name + "_Index.txt", string.Join("\n", selected.Select((a, i) => (i + 1) + ": " + a.prefab)));
                Object.DestroyImmediate(atlas);
                Debug.Log("PACK_CAPTURED " + pack.name + " samples=" + selected.Length);
            }
            RenderTexture.active = null; camera.targetTexture = null; target.Release();
            File.WriteAllText("../Captures/Verified.txt", "URP captures completed with no magenta error shaders.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("../CaptureFailure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
