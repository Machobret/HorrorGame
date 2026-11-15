using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class FinishPicker
    {
        static FinishLibrary[] libraries;
        static FinishPicker() => EditorApplication.projectChanged += () => libraries = null;

        internal static Material Draw(string label, Material selected, IEnumerable<Material> supported = null)
        {
            libraries ??= AssetDatabase.FindAssets("t:FinishLibrary")
                .Select(id => AssetDatabase.LoadAssetAtPath<FinishLibrary>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                var result = (Material)EditorGUILayout.ObjectField(label, selected, typeof(Material), false);
                var choices = libraries.Where(l => l && l.materials != null).SelectMany(l => l.materials)
                    .Concat(supported ?? Enumerable.Empty<Material>()).Where(m => m).Distinct().ToArray();
                if (choices.Length == 0) return result;
                // A regular popup commits in this GUI event, so Undo and mixed selections share one path.
                var names = new[]{"Library", "Inherit"}.Concat(choices.Select(m => m.name)).ToArray();
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
