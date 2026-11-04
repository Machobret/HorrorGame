#if UNITY_EDITOR
using HorrorGame.Chapter1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Chapter1.Editor
{
    [CustomEditor(typeof(PowerPanelController))]
    public sealed class PowerPanelControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PowerPanelController controller = (PowerPanelController)target;
            if (GUILayout.Button("Open Editor", GUILayout.Height(30)))
            {
                // Power dots are authored against the real level map, never against a synthetic grid window.
                HorrorEngine.MapController map = controller.ResolveSourceMap();
                if (map) { controller.SerializeToData(); EditorUtility.SetDirty(controller); HorrorEngine.MappingEditorWindow.Init(map); }
                else EditorUtility.DisplayDialog("Power Circuit Editor", "No MapController with Map Data was found in this scene.", "OK");
            }
        }
    }

    [CustomEditor(typeof(PowerCircuitPanelElement))]
    public sealed class PowerCircuitPanelElementEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            PowerCircuitPanelElement element = (PowerCircuitPanelElement)target;
            if (element.Room) return; // Room-bound dots are positioned in the Map Editor, with its map-space G tool.
            Vector3 position = element.transform.position;
            Handles.color = Color.cyan;
            Handles.DrawWireCube(position, new Vector3(element.Size.x, .1f, element.Size.y));
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(element.transform, "Move Power Circuit Element"); element.transform.position = new Vector3(moved.x, element.transform.position.y, moved.z); EditorUtility.SetDirty(element); }
        }
    }

    [InitializeOnLoad]
    public static class PowerPanelSerializer
    {
        static PowerPanelSerializer() { EditorSceneManager.sceneSaved -= Serialize; EditorSceneManager.sceneSaved += Serialize; }
        private static void Serialize(Scene scene)
        {
            foreach (PowerPanelController controller in Object.FindObjectsByType<PowerPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (controller.AutoSerializeChildren) controller.SerializeToData();
        }
    }
}
#endif
