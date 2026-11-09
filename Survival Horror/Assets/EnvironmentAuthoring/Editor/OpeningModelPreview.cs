using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Thumbnail-only resources. Never instantiate a model in the user's scene or regenerate on mouse movement.
    [InitializeOnLoad]
    internal static class OpeningModelPreview
    {
        private static readonly Dictionary<(Mesh, Material, Material), Texture2D> images = new();
        private static GUIStyle cardStyle;
        static OpeningModelPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.projectChanged += Clear;
            Undo.undoRedoPerformed += Clear;
            EditorApplication.quitting += Clear;
        }
        internal static void Clear()
        {
            foreach (var image in images.Values) if (image != null) Object.DestroyImmediate(image);
            images.Clear();
            cardStyle = null;
        }
        internal static Texture2D Image(WallOpeningInsert model)
        {
            if (model == null) return null;
            // Unity owns prefab thumbnails; do not clone gameplay objects or dispose its cached images.
            if (model.UsesPrefab) return AssetPreview.GetAssetPreview(model.Prefab) ?? AssetPreview.GetMiniThumbnail(model.Prefab);
            if (!model.HasValidMaterials) return null;
            var key = (model.Mesh, model.Material, model.GlassMaterial);
            if (images.TryGetValue(key, out var image)) return image;
            if (Event.current != null && Event.current.type != EventType.Repaint) return null;
            // Bound browsing memory; this stores thumbnails, never historical mesh versions.
            if (images.Count >= 128) Clear();
            var preview = new PreviewRenderUtility();
            try
            {
                var bounds = model.Mesh.bounds;
                var camera = preview.camera;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x) * .58f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                camera.transform.SetPositionAndRotation(bounds.center + Vector3.forward * 5, Quaternion.LookRotation(Vector3.back));
                preview.lights[0].intensity = 1; preview.lights[0].transform.rotation = camera.transform.rotation;
                preview.ambientColor = Color.gray;
                preview.BeginStaticPreview(new Rect(0, 0, 128, 128));
                for (int slot = 0; slot < model.Mesh.subMeshCount; slot++)
                    preview.DrawMesh(model.Mesh, Matrix4x4.identity, model.MaterialFor(slot), slot);
                preview.Render(true);
                image = preview.EndStaticPreview(); image.hideFlags = HideFlags.HideAndDontSave;
                images.Add(key, image); return image;
            }
            finally { preview.Cleanup(); }
        }
        internal static WallOpeningInsert Grid(WallOpeningInsert[] models, WallOpeningInsert selected, out bool picked, bool mixed = false)
        {
            var next = selected; picked = false;
            cardStyle ??= new GUIStyle(EditorStyles.miniButton) { fixedHeight = 0, stretchHeight = true };
            for (int row = 0; row * 4 < models.Length; row++)
            {
                using var horizontal = new EditorGUILayout.HorizontalScope();
                for (int i = row * 4; i < Mathf.Min(models.Length, row * 4 + 4); i++)
                {
                    var model = models[i]; var rect = GUILayoutUtility.GetRect(48, 70, GUILayout.ExpandWidth(true));
                    var background = GUI.backgroundColor;
                    if (!mixed && selected == model) GUI.backgroundColor = new Color(.45f, .7f, 1);
                    // Mini-button backgrounds stay single-line high even in a tall preview rect.
                    bool clicked = GUI.Button(rect, new GUIContent("", model.name), cardStyle);
                    GUI.backgroundColor = background;
                    if (clicked) { next = model; picked = true; GUI.changed = true; }
                    var picture = rect; picture.height -= 16; picture.xMin += 3; picture.xMax -= 3;
                    var image = Image(model);
                    if (image != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(picture, image, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(rect.x, rect.yMax - 17, rect.width, 16), (i + 1).ToString("00"), EditorStyles.centeredGreyMiniLabel);
                }
            }
            if (selected != null)
            {
                EditorGUILayout.LabelField(mixed ? "Mixed models" : selected.name, EditorStyles.wordWrappedMiniLabel);
                var rect = GUILayoutUtility.GetRect(0, 100, GUILayout.ExpandWidth(true));
                var image = Image(selected);
                if (image != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit);
            }
            return next;
        }
    }
}
