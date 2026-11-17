using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class FinishPicker
    {
        const string PickCommand = "HouseToolkit.ChooseFinish";
        static Material[] library;
        static SwatchPopup delivering;

        static FinishPicker() => EditorApplication.projectChanged += () => library = null;

        static Material[] Choices(IEnumerable<Material> supported)
        {
            library ??= AssetDatabase.FindAssets("t:FinishLibrary")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FinishLibrary>)
                .Where(asset => asset && asset.materials != null)
                .SelectMany(asset => asset.materials).Where(material => material).Distinct().ToArray();
            return supported == null ? library : library.Concat(supported).Where(material => material).Distinct().ToArray();
        }

        internal static Material Draw(string label, Material selected, IEnumerable<Material> supported = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var result = (Material)EditorGUILayout.ObjectField(label, selected, typeof(Material), false);
                Rect rect = GUILayoutUtility.GetRect(74, EditorGUIUtility.singleLineHeight, GUILayout.Width(74));
                int id = GUIUtility.GetControlID(label.GetHashCode(), FocusType.Passive, rect);
                if (Event.current.type == EventType.ExecuteCommand && Event.current.commandName == PickCommand &&
                    delivering != null && delivering.control == id)
                {
                    result = delivering.choice;
                    GUI.changed = true;
                    Event.current.Use();
                }

                if (EditorGUI.DropdownButton(rect, new GUIContent("Swatches"), FocusType.Keyboard))
                    PopupWindow.Show(rect, new SwatchPopup(id, EditorWindow.focusedWindow,
                        Choices(supported), EditorGUI.showMixedValue ? null : selected));
                return result;
            }
        }

        sealed class SwatchPopup : PopupWindowContent
        {
            const int Columns = 3, TileWidth = 112, TileHeight = 82;
            readonly EditorWindow owner;
            readonly Material[][] groups;
            readonly string[] surfaces;
            readonly float height;
            int surfaceIndex;
            readonly Material selected;
            readonly GUIStyle caption;
            Vector2 scroll;
            internal readonly int control;
            internal Material choice;

            internal SwatchPopup(int control, EditorWindow owner, Material[] materials, Material selected)
            {
                this.control = control;
                this.owner = owner;
                var bySurface = materials.GroupBy(SurfaceName).OrderBy(group => group.Key).ToArray();
                surfaces = new[] { "All surfaces" }.Concat(bySurface.Select(group => group.Key)).ToArray();
                groups = bySurface.Select(group => group.OrderBy(m => m.name).ToArray()).ToArray();
                height = Mathf.Min(420, 30 + groups.Sum(group => 24 + ((group.Length + Columns - 1) / Columns) * TileHeight));
                this.selected = selected;
                caption = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            }

            static string SurfaceName(Material material)
            {
                const string prefix = "Surface_";
                var label = AssetDatabase.GetLabels(material)
                    .FirstOrDefault(value => value.StartsWith(prefix, System.StringComparison.Ordinal) && value.Length > prefix.Length);
                if (label != null) return label.Substring(prefix.Length);
                var folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(material));
                return string.IsNullOrEmpty(folder) ? "Other" : System.IO.Path.GetFileName(folder);
            }

            public override Vector2 GetWindowSize() => new Vector2(Columns * TileWidth + 24, height);

            public override void OnGUI(Rect rect)
            {
                bool inherit;
                using (new EditorGUILayout.HorizontalScope())
                {
                    int next = EditorGUILayout.Popup(surfaceIndex, surfaces);
                    if (next != surfaceIndex) { surfaceIndex = next; scroll = Vector2.zero; }
                    inherit = GUILayout.Button("Inherit", EditorStyles.miniButton, GUILayout.Width(70));
                }
                if (inherit) { Choose(null); return; }
                Material picked = null;
                using (var view = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = view.scrollPosition;
                    for (int i = 0; i < groups.Length; i++)
                    {
                        if (surfaceIndex != 0 && surfaceIndex != i + 1) continue;
                        if (surfaceIndex == 0) EditorGUILayout.LabelField(surfaces[i + 1], EditorStyles.boldLabel);
                        picked = DrawGroup(groups[i]);
                        if (picked) break;
                    }
                }
                if (picked) Choose(picked);
            }

            Material DrawGroup(Material[] materials)
            {
                for (int i = 0; i < materials.Length; i += Columns)
                {
                    Rect row = GUILayoutUtility.GetRect(Columns * TileWidth, TileHeight);
                    for (int j = 0; j < Columns && i + j < materials.Length; j++)
                    {
                        var material = materials[i + j];
                        if (!material) continue;
                        Rect tile = new Rect(row.x + j * TileWidth, row.y, TileWidth - 2, TileHeight - 2);
                        if (GUI.Toggle(tile, material == selected, GUIContent.none, GUI.skin.button) != (material == selected))
                            return material;
                        Rect image = new Rect(tile.x + 4, tile.y + 4, tile.width - 8, 43);
                        var tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                        var texture = material.mainTexture ? material.mainTexture : Texture2D.whiteTexture;
                        var previousColor = GUI.color;
                        GUI.color *= tint;
                        if (texture.wrapModeU == TextureWrapMode.Repeat)
                        {
                            float repeats = image.width * texture.height / (image.height * texture.width);
                            GUI.DrawTextureWithTexCoords(image, texture, new Rect(0, 0, repeats, 1));
                        }
                        else GUI.DrawTexture(image, texture, ScaleMode.ScaleToFit);
                        GUI.color = previousColor;
                        GUI.Label(new Rect(tile.x + 2, image.yMax + 2, tile.width - 4, 30), material.name, caption);
                    }
                }
                return null;
            }

            void Choose(Material material)
            {
                choice = material;
                // Send the result back through the owning IMGUI field's normal Undo/multi-edit path.
                var popup = editorWindow;
                popup.Close();
                if (!owner) return;
                delivering = this;
                try { owner.SendEvent(EditorGUIUtility.CommandEvent(PickCommand)); }
                finally { delivering = null; }
            }
        }
    }

    [CustomEditor(typeof(TrimFamily))]
    internal sealed class TrimInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "material");
            var property = serializedObject.FindProperty("material");
            EditorGUI.BeginChangeCheck();
            var finish = FinishPicker.Draw("Material", property.objectReferenceValue as Material);
            if (EditorGUI.EndChangeCheck()) property.objectReferenceValue = finish;
            if (!serializedObject.ApplyModifiedProperties()) return;
            foreach (var wall in Object.FindObjectsByType<HouseWall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (wall.Trim == target) WallBuilder.Rebuild(wall, out _);
        }
    }
}
