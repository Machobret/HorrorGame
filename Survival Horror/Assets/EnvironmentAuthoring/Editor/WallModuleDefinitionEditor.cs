using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    [CustomEditor(typeof(WallModuleDefinition))]
    public sealed class WallModuleDefinitionEditor : UnityEditor.Editor
    {
        private List<WallKitValidation.Issue> report;
        private bool showNotes;

        private void OnEnable() => report = WallKitValidation.Check((WallModuleDefinition)target);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string field in new[] { "DisplayName", "PreserveImportOverrides", "FamilyId", "OpeningShape",
                "Width", "Height", "Thickness", "CoverSlabEdge", "KeepNativeHeight", "AllowLengthStretch", "OpeningOutline", "Body" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            bool changed = serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Validate module") || changed)
                report = WallKitValidation.Check((WallModuleDefinition)target);
            DrawReport(report, ref showNotes);
            EditorGUILayout.HelpBox("Authoring instructions: docs/WALL_KIT_AUTHORING.md. Validation reads assets without changing them. Rerun after mesh reimports or Undo.", MessageType.None);
        }

        internal static void DrawReport(List<WallKitValidation.Issue> report, ref bool showNotes)
        {
            if (report == null) return;
            int errors = report.Count(i => i.Severity == MessageType.Error);
            int warnings = report.Count(i => i.Severity == MessageType.Warning);
            EditorGUILayout.LabelField($"Last check: {errors} errors, {warnings} warnings", EditorStyles.boldLabel);
            foreach (var issue in report.Where(i => i.Severity != MessageType.Info).OrderByDescending(i => i.Severity))
                EditorGUILayout.HelpBox(issue.Message, issue.Severity);
            int notes = report.Count(i => i.Severity == MessageType.Info);
            if (notes > 0)
            {
                showNotes = EditorGUILayout.Foldout(showNotes, $"{notes} informational notes", true);
                if (showNotes)
                    foreach (var issue in report.Where(i => i.Severity == MessageType.Info))
                        EditorGUILayout.HelpBox(issue.Message, MessageType.Info);
            }
            EditorGUILayout.HelpBox("These checks do not prove that openings are cut through, caps seal every edge, or decorative corners fit. Inspect those in the sample scene.", MessageType.None);
        }
    }
}
