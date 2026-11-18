using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using HouseToolkit;

namespace HorrorGame.Environment.Editor
{
    public static class ShopWindowAdBuilder
    {
        const string Root = "Assets/Game/Environment/NeighborhoodStore/WindowAds/";
        const string Fascia = "CornerMarket_Fascia";
        static readonly (string Name, string Label)[] Ads =
        {
            ("WindowAd_HeroHQ_Protection", "Ad - Hero HQ protection"),
            ("WindowAd_HeroHQ_StayCalm", "Ad - Stay calm notice"),
            ("WindowAd_Shop_Essentials", "Ad - Everyday essentials"),
            ("WindowAd_Shop_Break", "Ad - Drinks and snacks"),
            ("WindowAd_Shop_ColdDrinks", "Ad - Ice cold drinks"),
            ("WindowAd_Shop_Coffee", "Ad - Hot coffee"),
            ("WindowAd_Shop_Hours", "Ad - Store hours"),
        };
        static readonly string[] Names = Ads.Select(ad => ad.Name).Append(Fascia).ToArray();

        [MenuItem("Horror Game/Environment/Create Missing Shop Window Ads")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating assets.");
            Directory.CreateDirectory(Root + "Materials");
            Directory.CreateDirectory(Root + "Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is required.");
            foreach (string name in Names)
            {
                string texturePath = Root + "Textures/" + name + ".png";
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Missing banner: " + texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = name == Fascia ? 1024 : 512;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 0;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
                string materialPath = Root + "Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    material = new Material(shader);
                    material.SetTexture("_BaseMap", texture);
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Smoothness", 0);
                    material.SetFloat("_Metallic", 0);
                    material.SetFloat("_Cull", (float)CullMode.Off);
                    if (name == Fascia)
                    {
                        material.SetTexture("_EmissionMap", texture);
                        material.SetColor("_EmissionColor", Color.white * .18f);
                        material.EnableKeyword("_EMISSION");
                    }
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                string prefabPath = Root + "Prefabs/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) continue;
                var root = new GameObject(name);
                try
                {
                    var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    sheet.name = "Printed banner";
                    sheet.transform.SetParent(root.transform, false);
                    sheet.transform.localScale = name == Fascia ? new Vector3(3, 1, 1) : new Vector3(1.2f, .6f, 1);
                    Object.DestroyImmediate(sheet.GetComponent<Collider>());
                    var renderer = sheet.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            CreatePalette();
            foreach (string name in Names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + name + ".prefab");
                var renderer = prefab.GetComponentInChildren<MeshRenderer>();
                if (!renderer || !renderer.sharedMaterial.GetTexture("_BaseMap") || prefab.GetComponentInChildren<Collider>())
                    throw new InvalidOperationException("Invalid banner prefab: " + name);
            }
            Debug.Log("SHOP_WINDOW_ADS_PASS: seven window ads and one fascia sign.");
        }

        [MenuItem("Horror Game/Environment/Create Shop Advertisement Selection")]
        public static void CreatePalette()
        {
            const string path = Root + "ShopWindowAdvertisements.asset";
            var palette = AssetDatabase.LoadAssetAtPath<FixturePalette>(path);
            if (!palette)
            {
                palette = ScriptableObject.CreateInstance<FixturePalette>();
                AssetDatabase.CreateAsset(palette, path);
            }
            var choices = (palette.fixtures ?? Array.Empty<FixtureChoice>()).ToList();
            foreach (var ad in Ads)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + ad.Name + ".prefab");
                if (!prefab) throw new InvalidOperationException("Build the window ad prefabs first.");
                if (choices.Any(choice => choice != null && choice.prefab == prefab)) continue;
                choices.Add(new FixtureChoice { label = ad.Label, prefab = prefab,
                    mount = FixtureMount.Wall, modelRotation = new Vector3(0, 180, 0),
                    allowOpeningSurface = true, surfaceClearance = .012f });
            }
            palette.fixtures = choices.ToArray();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
        }

        public static void BuildAndReviewBatch()
        {
            Build();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var objects = new System.Collections.Generic.List<GameObject>();
            RenderTexture target = null;
            Texture2D capture = null;
            var previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < Ads.Length; i++)
                {
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + Ads[i].Name + ".prefab"), scene);
                    objects.Add(item);
                    item.transform.position = new Vector3((i % 3 - 1) * 1.3f, (1 - i / 3) * .7f, 0);
                    var renderer = item.GetComponentInChildren<MeshRenderer>();
                    var previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    previewMaterial.SetTexture("_BaseMap", renderer.sharedMaterial.GetTexture("_BaseMap"));
                    renderer.sharedMaterial = previewMaterial;
                }
                var cameraObject = new GameObject("Review camera");
                objects.Add(cameraObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -3);
                camera.orthographic = true;
                camera.orthographicSize = 1.13f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .06f, .07f);
                camera.scene = scene;
                target = new RenderTexture(1800, 1000, 24);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(1800, 1000, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, 1800, 1000), 0, 0);
                capture.Apply();
                Directory.CreateDirectory("Exports/Reviews/ShopWindowAds");
                File.WriteAllBytes("Exports/Reviews/ShopWindowAds/WindowAds.png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var item in objects)
                {
                    if (!item) continue;
                    var camera = item.GetComponent<Camera>();
                    if (camera) camera.targetTexture = null;
                }
                if (target) Object.DestroyImmediate(target);
                if (capture) Object.DestroyImmediate(capture);
                foreach (var item in objects) if (item) Object.DestroyImmediate(item);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
