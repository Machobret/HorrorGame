using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    [CustomEditor(typeof(HouseWallBuilder))]
    public sealed class HouseWallBuilderEditor : UnityEditor.Editor
    {
        private bool HasFrameBounds() => HouseEditingContext.SelectedWallFrameBounds((HouseWallBuilder)target, out _);
        private Bounds OnGetFrameBounds() { HouseEditingContext.SelectedWallFrameBounds((HouseWallBuilder)target, out var bounds); return bounds; }

        private System.Collections.Generic.List<WallKitValidation.Issue> kitReport;
        private bool showKitNotes;
        private bool showAdvanced;
        private string kitMessage;

        public static HouseWallBuilder Enable(HousePlan plan) => HouseEditCommands.EnableWalls(plan);

        public static void AssignExampleKit(HouseWallBuilder builder)
        {
            // New Houses use the same closed family contract as imported user assets.
            var kit = builder.Data;
            kit.DefaultFamily = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.CompleteFamilyPath);
            kit.ExteriorFamily = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.ExteriorFamilyPath);
            kit.Inserts = WallFamilyExamples.CompleteModels();
        }

        public override void OnInspectorGUI()
        {
            var builder = (HouseWallBuilder)target;
            var plan = builder.GetComponent<HousePlan>();
            using (new EditorGUI.DisabledScope(!HouseEditorSession.instance.CanEditSource))
            {
                var defaults = HouseAssetProperties.Defaults(serializedObject);
                var finishes = HouseMaterialEditor.LevelWallDefaults(plan);
                if (finishes.Status != HouseEditStatus.Unchanged) kitMessage = finishes.Message;
                if (defaults.Applied) { kitReport = null; kitMessage = ""; }
                else if (defaults.Status == HouseEditStatus.Rejected) kitMessage = defaults.Message;
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced / asset setup", true);
                if (showAdvanced)
                {
                    serializedObject.Update();
                    var settings = serializedObject.FindProperty("data");
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("Inserts"), new GUIContent("Custom insert models", "Optional off-family custom catalog. Normal window/door choices come from the wall family."));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("ShowWalls"));
                    if (HouseEditCommands.ApplyBuilderSettings(serializedObject).Applied) kitReport = null;
                    if (GUILayout.Button("Rebuild House")) HouseRebuildCoordinator.RebuildHouse(plan);
                    var family = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.CompleteFamilyPath);
                    if (family != null && GUILayout.Button("Use complete sample families"))
                    {
                        serializedObject.Update();
                        settings.FindPropertyRelative("DefaultFamily").objectReferenceValue = family;
                        settings.FindPropertyRelative("ExteriorFamily").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.ExteriorFamilyPath);
                        var models = WallFamilyExamples.CompleteModels();
                        var modelProperty = settings.FindPropertyRelative("Inserts"); modelProperty.arraySize = models.Length;
                        for (int i = 0; i < models.Length; i++) modelProperty.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
                        kitMessage = HouseEditCommands.ApplyBuilderSettings(serializedObject).Message;
                        serializedObject.Update(); kitReport = null;
                    }
                    if (GUILayout.Button("Validate kit")) kitReport = WallKitValidation.CheckKit(builder);
                    WallModuleDefinitionEditor.DrawReport(kitReport, ref showKitNotes);
                }
            }
            if (builder.Output != null && !string.IsNullOrEmpty(builder.Output.Problem))
                EditorGUILayout.HelpBox(builder.Output.Problem, MessageType.Warning);
            if (!string.IsNullOrEmpty(kitMessage)) EditorGUILayout.HelpBox(kitMessage, MessageType.Warning);
            if (GUILayout.Button("Edit House")) HouseEditingContext.Enter(plan);
        }

        private void OnSceneGUI()
        {
            var builder = (HouseWallBuilder)target;
            var plan = builder.GetComponent<HousePlan>();
            if (builder.Output == null) return;
            foreach (var point in builder.Output.ProblemPoints)
            {
                using (new Handles.DrawingScope(Color.red))
                {
                    var world = HouseSceneQueries.NodeWorld(plan, point);
                    Handles.DrawWireDisc(world, plan.transform.up, 0.2f);
                    Handles.Label(world + Vector3.up * 0.2f, "Unsupported wall fit");
                }
            }
        }

        public static void Refresh(HouseWallBuilder builder, bool force = false)
        {
            HouseRebuildCoordinator.RefreshWallsNow(builder, force);
        }

    }
}
