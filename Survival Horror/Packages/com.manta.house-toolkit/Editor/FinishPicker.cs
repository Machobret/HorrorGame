using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class FinishPicker
    {
        static Dictionary<Material, string> libraryNames;
        static FinishPicker() => EditorApplication.projectChanged += () => libraryNames = null;

        static Dictionary<Material, string> LoadLibraryNames()
        {
            var names = new Dictionary<Material, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:FinishLibrary"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var library = AssetDatabase.LoadAssetAtPath<FinishLibrary>(path);
                if (!library || library.materials == null) continue;
                string folder = Path.GetDirectoryName(path).Replace('\\', '/') + "/";
                foreach (var material in library.materials)
                {
                    if (!material || names.ContainsKey(material)) continue;
                    string assetPath = AssetDatabase.GetAssetPath(material);
                    var category = assetPath.StartsWith(folder, System.StringComparison.Ordinal)
                        ? Path.GetDirectoryName(assetPath.Substring(folder.Length)) : null;
                    names[material] = string.IsNullOrEmpty(category) ? material.name
                        : category.Replace('\\', '/') + "/" + material.name;
                }
            }
            return names;
        }

        internal static Material Draw(string label, Material selected, IEnumerable<Material> supported = null)
        {
            libraryNames ??= LoadLibraryNames();
            using (new EditorGUILayout.HorizontalScope())
            {
                var result = (Material)EditorGUILayout.ObjectField(label, selected, typeof(Material), false);
                var choices = libraryNames.Keys
                    .Concat(supported ?? Enumerable.Empty<Material>()).Where(m => m).Distinct().ToArray();
                if (choices.Length == 0) return result;
                // A regular popup commits in this GUI event, so Undo and mixed selections share one path.
                var names = new[]{"Library", "Inherit"}.Concat(choices.Select(m => libraryNames.TryGetValue(m, out var name) ? name : m.name)).ToArray();
                int index = EditorGUILayout.Popup(0, names, GUILayout.Width(70));
                return index == 0 ? result : index == 1 ? null : choices[index - 2];
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
