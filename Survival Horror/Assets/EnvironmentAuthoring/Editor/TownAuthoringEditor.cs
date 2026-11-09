using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    [CustomEditor(typeof(TownAuthoring), true)]
    public sealed class TownAuthoringEditor : UnityEditor.Editor
    {
        [MenuItem("GameObject/Environment/Wall", false, 15)]
        private static void Wall() => EnvironmentCreateTool.Arm(EnvironmentCreateCommands.Kind.Wall, SceneView.lastActiveSceneView);
        [MenuItem("GameObject/Environment/Road", false, 16)]
        private static void Road() => EnvironmentCreateTool.Arm(EnvironmentCreateCommands.Kind.Road, SceneView.lastActiveSceneView);
        [MenuItem("GameObject/Environment/Sidewalk", false, 17)]
        private static void Sidewalk() => EnvironmentCreateTool.Arm(EnvironmentCreateCommands.Kind.Sidewalk, SceneView.lastActiveSceneView);

        public override void OnInspectorGUI()
        {
            var layout = (TownAuthoring)target;
            EditorGUILayout.LabelField(layout is TownRoad ? "Road layout" : layout is TownSidewalk ? "Sidewalk layout" : "Wall / fence layout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{layout.Path.Points.Count} points / {layout.Path.Spans.Count} spans");
            if (GUILayout.Button("Edit layout")) TownEditingContext.Enter(layout);
            if (!TownCommands.CanEdit(layout, out string reason)) EditorGUILayout.HelpBox(reason, MessageType.Warning);
            EditorGUILayout.HelpBox("Models, materials and selection controls appear in the existing properties window while editing.", MessageType.None);
        }
    }
}
