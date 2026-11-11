using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorGame.Lighting.Editor
{
    [CustomEditor(typeof(AtmosphericFog))]
    public sealed class AtmosphericFogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var fog = (AtmosphericFog)target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) fog.ApplySettings();
            EditorGUILayout.HelpBox("This object injects real scene fog immediately, in the editor and in-game. Toggle the GameObject checkbox to add/remove it. Deleting it restores the base scene fog.", MessageType.Info);
            if (GUILayout.Button("Show Fog")) Change(fog, true);
            if (GUILayout.Button("Remove Added Fog")) Change(fog, false);
        }

        private static void Change(AtmosphericFog fog, bool enabled)
        {
            Undo.RecordObject(fog, "Toggle atmospheric fog");
            if (enabled) fog.ShowImmediately(); else fog.HideImmediately();
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(fog.gameObject.scene);
            SceneView.RepaintAll();
        }

        [MenuItem("GameObject/Horror Game/Atmospheric Fog", false, 10)]
        public static void Create()
        {
            var obj = new GameObject("Atmospheric Fog");
            Undo.RegisterCreatedObjectUndo(obj, "Add atmospheric fog");
            obj.AddComponent<AtmosphericFog>().ApplySettings();
            Selection.activeGameObject = obj;
        }
    }
}
