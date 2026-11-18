using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    public static class ProjectAssetNavigation
    {
        public static bool IsDoubleClick(Rect rect)
        {
            var e = Event.current;
            return e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && rect.Contains(e.mousePosition);
        }

        public static void Reveal(Object asset)
        {
            if (!asset || !EditorUtility.IsPersistent(asset)) return;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        internal static int Popup(string label, Object asset, int index, string[] names)
        {
            var rect = EditorGUILayout.GetControlRect();
            if (asset && !EditorGUI.showMixedValue)
            {
                var icon = new Rect(rect.xMax - rect.height, rect.y, rect.height, rect.height);
                rect.xMax = icon.x - 2;
                HandleDoubleClick(icon, asset);
                GUI.Label(icon, new GUIContent(AssetPreview.GetMiniThumbnail(asset), "Double-click to reveal " + asset.name));
            }
            return EditorGUI.Popup(rect, label, index, names);
        }

        public static bool HandleDoubleClick(Rect rect, Object asset)
        {
            if (!asset || !IsDoubleClick(rect)) return false;
            Event.current.Use();
            Reveal(asset);
            return true;
        }
    }
}
