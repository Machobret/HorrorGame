using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Shape data belongs to HouseStack. This panel only selects targets/options
    // and sends committed field edits through the same commands as Scene edits.
    internal static class HouseBooleanProperties
    {
        internal static void Draw(HouseEditorSession session, HouseEditorController controller)
        {
            var plan = session.SessionPlan;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            EditorGUILayout.LabelField("Boolean cuts", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(controller.HasSceneGesture))
            {
                EditorGUI.BeginChangeCheck();
                var cutMaterial = HousePropertyFields.Material(new GUIContent("Wall cut material", "Finish for newly exposed wall faces. None uses the source's first material. Slab cuts retain their top and bottom layer finishes."), building.Data.BooleanCutMaterial, wallFinish: true);
                if (EditorGUI.EndChangeCheck()) controller.RunCommand(() => HouseBooleanCommands.SetCutMaterial(plan, cutMaterial));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add box")) controller.BooleanTool.Arm(HouseStack.BooleanShape.Box);
                    if (GUILayout.Button(new GUIContent("Add icosphere", "Place a faceted icosphere cut."))) controller.BooleanTool.Arm(HouseStack.BooleanShape.Icosphere);
                }
                session.ShowBooleanShapes = EditorGUILayout.ToggleLeft("Show shape helpers", session.ShowBooleanShapes);
                if (session.PlaceBooleanShape)
                {
                    EditorGUILayout.LabelField("Click in the Scene view to place a " + session.BooleanShape.ToString().ToLowerInvariant() + ". Escape cancels.", EditorStyles.wordWrappedLabel);
                    return;
                }
                var volumes = building.Data.BooleanVolumes;
                var selected = controller.BooleanTool.Selected;
                var labels = new[] { "Select a shape" }.Concat(volumes.Select((volume, i) => volume.Shape + " " + (i + 1))).ToArray();
                int index = selected == null ? 0 : volumes.ToList().FindIndex(volume => volume.Id == selected.Id) + 1;
                int next = HousePropertyFields.Popup("Shape", index, labels);
                if (next != index)
                {
                    if (next == 0) controller.ClearSelection();
                    else controller.Select(HouseEditSelection.Kind.BooleanShape, volumes[next - 1].Id);
                    return;
                }
                if (selected == null) return;
                int tool = session.BooleanGizmo == Tool.Rotate ? 1 : session.BooleanGizmo == Tool.Scale ? 2 : 0;
                int chosen = GUILayout.Toolbar(tool, new[] { "Move (W)", "Rotate (E)", "Resize (R)" });
                if (chosen != tool)
                {
                    session.BooleanGizmo = chosen == 1 ? Tool.Rotate : chosen == 2 ? Tool.Scale : Tool.Move;
                    HouseSceneUI.Activate(HouseEditorSession.Mode.Boolean);
                }
                EditorGUILayout.Space(4);
                EditorGUI.BeginChangeCheck();
                var center = Vector("Position (House)", selected.Center);
                var rotation = Vector("Rotation", selected.Rotation.eulerAngles);
                var size = Vector("Size (m)", selected.Size);
                if (EditorGUI.EndChangeCheck()) controller.RunCommand(() => HouseBooleanCommands.Set(plan, selected.Id,
                    selected.Shape, center, Quaternion.Euler(rotation), size));
                if (GUILayout.Button("Remove shape")) controller.RunCommand(() => HouseBooleanCommands.Delete(plan, selected.Id));
            }
        }

        private static Vector3 Vector(string label, Vector3 value)
        {
            EditorGUILayout.LabelField(label);
            float previous = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 12;
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    value.x = EditorGUILayout.DelayedFloatField(new GUIContent("X"), value.x);
                    value.y = EditorGUILayout.DelayedFloatField(new GUIContent("Y"), value.y);
                    value.z = EditorGUILayout.DelayedFloatField(new GUIContent("Z"), value.z);
                }
            }
            finally { EditorGUIUtility.labelWidth = previous; }
            return value;
        }
    }
}
