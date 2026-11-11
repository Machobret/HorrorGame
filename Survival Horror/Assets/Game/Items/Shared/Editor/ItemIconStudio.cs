using System;
using System.Collections.Generic;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HorrorGame.Items.Editor
{
    /// <summary>Render-only prefab previews. Never instantiates gameplay scripts or edits a level.</summary>
    public sealed class ItemIconStudio : EditorWindow
    {
        [SerializeField] GameObject source;
        [SerializeField] Vector3 cameraRotation = new Vector3(15, 150, 0);
        [SerializeField] Vector3 modelRotation, targetOffset;
        [SerializeField] float distance = 3, zoom = 1, keyLight = 1.3f, fillLight = .65f, ambient = .25f;
        [SerializeField] bool orthographic, transparent = true, pointFilter, assignItem;
        [SerializeField] Color background = new Color(.12f, .13f, .12f, 1);
        [SerializeField] int resolution = 512;
        [SerializeField] ItemData item;
        [SerializeField] bool lightingOpen, modelOpen;
        PreviewRenderUtility preview;
        GameObject visual;
        readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
        Bounds bounds;
        Texture rendered;
        bool needsRender = true;
        Vector2 scroll;
        string problem;
        static readonly int[] Sizes = { 128, 256, 512, 1024, 2048 };
        static readonly string[] SizeLabels = { "128", "256", "512", "1024", "2048" };

        [MenuItem("Horror Game/Items/Icon Studio")]
        public static void Open()
        {
            var window = GetWindow<ItemIconStudio>("Item Icon Studio");
            window.minSize = new Vector2(650, 440);
            if (Selection.activeObject is GameObject selected && AssetDatabase.Contains(selected)) window.SetSource(selected);
        }

        void OnEnable() { needsRender = true; }
        void OnDisable() { Release(); }
        void Release()
        {
            if (preview != null) preview.Cleanup();
            preview = null; visual = null; rendered = null; transforms.Clear();
        }

        void SetSource(GameObject prefab)
        {
            if (prefab && !AssetDatabase.Contains(prefab))
            {
                ShowNotification(new GUIContent("Choose a prefab or model from the Project window.")); return;
            }
            source = prefab;
            var pickup = source ? source.GetComponent<PickupItem>() : null;
            item = pickup ? pickup.Entry.Item : null;
            assignItem = false; targetOffset = Vector3.zero; modelRotation = Vector3.zero;
            Rebuild(); Frame(); Repaint();
        }

        void Rebuild()
        {
            Release(); problem = null;
            if (!source) return;
            try
            {
                preview = new PreviewRenderUtility();
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                var data = preview.camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
                preview.camera.allowHDR = false;
                visual = new GameObject("Icon render model") { hideFlags = HideFlags.HideAndDontSave };
                preview.AddSingleGO(visual);
                CopyTransform(source.transform, visual.transform, true);
                foreach (var entry in transforms)
                {
                    if (!IsActive(entry.Key)) continue;
                    var meshRenderer = entry.Key.GetComponent<MeshRenderer>();
                    var filter = entry.Key.GetComponent<MeshFilter>();
                    if (meshRenderer && meshRenderer.enabled && filter && filter.sharedMesh)
                    {
                        entry.Value.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        CopyRenderer(meshRenderer, entry.Value.gameObject.AddComponent<MeshRenderer>());
                    }
                    var skin = entry.Key.GetComponent<SkinnedMeshRenderer>();
                    if (skin && skin.enabled && skin.sharedMesh)
                    {
                        var clone = entry.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
                        clone.sharedMesh = skin.sharedMesh; clone.localBounds = skin.localBounds; clone.updateWhenOffscreen = true;
                        clone.bones = Array.ConvertAll(skin.bones, b => b && transforms.TryGetValue(b, out var target) ? target : null);
                        clone.rootBone = skin.rootBone && transforms.TryGetValue(skin.rootBone, out var rootBone) ? rootBone : null;
                        CopyRenderer(skin, clone);
                    }
                }
                UpdateBounds();
                if (visual.GetComponentsInChildren<Renderer>().Length == 0) throw new InvalidOperationException("This prefab has no enabled mesh renderers to capture.");
                needsRender = true;
            }
            catch (Exception ex) { problem = ex.Message; Release(); Debug.LogException(ex); }
        }

        void CopyTransform(Transform original, Transform parent, bool isRoot)
        {
            var clone = new GameObject(original.name) { hideFlags = HideFlags.HideAndDontSave };
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = isRoot ? Vector3.zero : original.localPosition;
            clone.transform.localRotation = original.localRotation; clone.transform.localScale = original.localScale;
            transforms.Add(original, clone.transform);
            foreach (Transform child in original) CopyTransform(child, clone.transform, false);
        }
        bool IsActive(Transform original)
        {
            for (var t = original; t; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == source.transform) break;
            }
            return true;
        }
        static void CopyRenderer(Renderer original, Renderer clone)
        {
            clone.sharedMaterials = original.sharedMaterials;
            clone.shadowCastingMode = original.shadowCastingMode; clone.receiveShadows = original.receiveShadows;
            var block = new MaterialPropertyBlock(); original.GetPropertyBlock(block); clone.SetPropertyBlock(block);
        }
        void UpdateBounds()
        {
            visual.transform.rotation = Quaternion.Euler(modelRotation);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }
        float Radius => Mathf.Max(.001f, bounds.extents.magnitude);
        void Frame()
        {
            if (!visual) return;
            UpdateBounds(); zoom = 1; targetOffset = Vector3.zero;
            distance = Radius / Mathf.Sin(22.5f * Mathf.Deg2Rad) * 1.08f;
            needsRender = true;
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Item Icon Studio", EditorStyles.boldLabel);
            var chosen = (GameObject)EditorGUILayout.ObjectField("Prefab", source, typeof(GameObject), false);
            if (chosen != source) SetSource(chosen);
            if (GUILayout.Button("Use selected prefab") && Selection.activeObject is GameObject selected) SetSource(selected);
            if (source && preview == null && string.IsNullOrEmpty(problem)) { Rebuild(); }
            using (new EditorGUI.DisabledScope(!visual))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
                distance = EditorGUILayout.Slider("Distance (m)", distance, Radius * .05f, Radius * 20);
                zoom = EditorGUILayout.Slider("Zoom", zoom, .25f, 4);
                cameraRotation.x = EditorGUILayout.Slider("Rotation X", cameraRotation.x, -180, 180);
                cameraRotation.y = EditorGUILayout.Slider("Rotation Y", cameraRotation.y, -180, 180);
                cameraRotation.z = EditorGUILayout.Slider("Rotation Z", cameraRotation.z, -180, 180);
                orthographic = EditorGUILayout.Toggle("Orthographic", orthographic);
                targetOffset = EditorGUILayout.Vector3Field("Framing offset (m)", targetOffset);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Frame item")) Frame();
                if (GUILayout.Button("Reset view")) { cameraRotation = new Vector3(15, 150, 0); modelRotation = Vector3.zero; Frame(); }
                EditorGUILayout.EndHorizontal();
                modelOpen = EditorGUILayout.Foldout(modelOpen, "Rotate model", true);
                if (modelOpen) modelRotation = EditorGUILayout.Vector3Field("Model XYZ", modelRotation);
                lightingOpen = EditorGUILayout.Foldout(lightingOpen, "Lighting", true);
                if (lightingOpen)
                {
                    keyLight = EditorGUILayout.Slider("Key light", keyLight, 0, 4);
                    fillLight = EditorGUILayout.Slider("Fill light", fillLight, 0, 4);
                    ambient = EditorGUILayout.Slider("Ambient", ambient, 0, 1);
                }
                transparent = EditorGUILayout.Toggle("Transparent background", transparent);
                if (!transparent) background = EditorGUILayout.ColorField("Background", background);
                resolution = EditorGUILayout.IntPopup("Image size", resolution, SizeLabels, Sizes);
                pointFilter = EditorGUILayout.Toggle("Pixel filtering", pointFilter);
                if (EditorGUI.EndChangeCheck()) { needsRender = true; Repaint(); }
                EditorGUILayout.Space();
                item = (ItemData)EditorGUILayout.ObjectField("Item data (optional)", item, typeof(ItemData), false);
                using (new EditorGUI.DisabledScope(!item)) assignItem = EditorGUILayout.Toggle("Assign exported icon", assignItem);
                if (GUILayout.Button("Export PNG", GUILayout.Height(32))) Export();
            }
            if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            EditorGUILayout.HelpBox("Drag the preview to orbit. Scroll to change distance. Exports import as inventory-ready sprites.", MessageType.None);
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
            Rect area = GUILayoutUtility.GetRect(128, 4096, 128, 4096, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float side = Mathf.Min(area.width, area.height);
            Rect imageRect = new Rect(area.center.x - side / 2, area.center.y - side / 2, side, side);
            if (Event.current.type == EventType.Repaint && visual)
            {
                try { if (needsRender || !rendered) RenderImage(); }
                catch (Exception ex) { problem = ex.Message; needsRender = false; }
                if (rendered) EditorGUI.DrawTextureTransparent(imageRect, rendered, ScaleMode.ScaleToFit);
            }
            if (visual && imageRect.Contains(Event.current.mousePosition))
            {
                var e = Event.current;
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    cameraRotation.y = Mathf.Repeat(cameraRotation.y + e.delta.x * .5f + 180, 360) - 180;
                    cameraRotation.x = Mathf.Clamp(cameraRotation.x + e.delta.y * .5f, -180, 180);
                    needsRender = true; e.Use(); Repaint();
                }
                if (e.type == EventType.ScrollWheel)
                {
                    distance = Mathf.Clamp(distance * Mathf.Exp(e.delta.y * .04f), Radius * .05f, Radius * 20);
                    needsRender = true; e.Use(); Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void RenderImage()
        {
            UpdateBounds();
            var camera = preview.camera;
            camera.transform.rotation = Quaternion.Euler(cameraRotation);
            camera.transform.position = bounds.center + targetOffset - camera.transform.forward * distance;
            camera.nearClipPlane = Mathf.Max(.0001f, Radius * .005f); camera.farClipPlane = distance + Radius * 30;
            camera.orthographic = orthographic; camera.orthographicSize = Radius * 1.08f / zoom;
            camera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(22.5f * Mathf.Deg2Rad) / zoom) * Mathf.Rad2Deg;
            camera.backgroundColor = transparent ? Color.clear : new Color(background.r, background.g, background.b, 1);
            preview.ambientColor = new Color(ambient, ambient, ambient, 1);
            preview.lights[0].intensity = keyLight; preview.lights[0].color = new Color(1, .91f, .8f);
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0) * camera.transform.rotation;
            preview.lights[1].intensity = fillLight; preview.lights[1].color = new Color(.75f, .85f, 1);
            preview.lights[1].transform.rotation = Quaternion.Euler(10, 135, 0) * camera.transform.rotation;
            preview.BeginPreview(new Rect(0, 0, resolution, resolution), GUIStyle.none);
            try { preview.Render(true); }
            finally { rendered = preview.EndPreview(); }
            needsRender = false;
        }

        Texture2D ReadImage()
        {
            RenderImage();
            var previous = RenderTexture.active;
            var image = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = (RenderTexture)rendered;
                image.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); image.Apply(); return image;
            }
            catch { Object.DestroyImmediate(image); throw; }
            finally { RenderTexture.active = previous; }
        }
        void Export()
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject("Export item icon", source.name + "_Icon", "png", "Choose where to save the icon.", folder);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var image = ReadImage();
                try { File.WriteAllBytes(path, image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = resolution; importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear; importer.SaveAndReimport();
                if (assignItem && item)
                {
                    Undo.RecordObject(item, "Assign item icon"); item.Image = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item);
                }
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Sprite>(path));
                ShowNotification(new GUIContent("Icon exported"));
            }
            catch (Exception ex) { problem = ex.Message; Debug.LogException(ex); }
        }

        [MenuItem("Horror Game/Items/Validate Icon Studio")]
        public static void Validate()
        {
            var studio = CreateInstance<ItemIconStudio>();
            try
            {
                studio.SetSource(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/NeighborhoodStore/Prefabs/CashRegister.prefab"));
                if (!studio.visual) throw new Exception(studio.problem ?? "No preview model.");
                var before = studio.source.GetComponentsInChildren<Transform>(true).Length;
                var image = studio.ReadImage();
                try
                {
                    int visible = 0, clear = 0;
                    foreach (var pixel in image.GetPixels32()) { if (pixel.a > 200) visible++; if (pixel.a == 0) clear++; }
                    if (visible < 100 || clear < 100) throw new Exception("Preview must contain a visible item and transparent background. visible=" + visible + " clear=" + clear);
                    Directory.CreateDirectory("Exports/Reviews/ItemIconStudio");
                    File.WriteAllBytes("Exports/Reviews/ItemIconStudio/Register.png", image.EncodeToPNG());
                    if (before != studio.source.GetComponentsInChildren<Transform>(true).Length) throw new Exception("Source prefab changed.");
                    Debug.Log("ITEM_ICON_STUDIO_OK: visible mesh, transparent pixels, source preserved. " + visible + " opaque pixels, " + clear + " transparent pixels.");
                }
                finally { Object.DestroyImmediate(image); }
                studio.cameraRotation.y += 60; studio.distance *= 1.1f; studio.zoom = 1.2f;
                var changed = studio.ReadImage(); Object.DestroyImmediate(changed);
            }
            finally { Object.DestroyImmediate(studio); }
        }
    }
}
