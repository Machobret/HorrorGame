using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // This view changes session options; committed geometry goes through edit commands.
    internal static class HouseSlabProperties
    {
        internal static string BoundaryLabel(HouseBuilding building, HouseStack.Boundary boundary)
        {
            var lower = building.Find(boundary.LowerId)?.Plan;
            var upper = building.Find(boundary.UpperId)?.Plan;
            return upper != null ? "Floor: " + HouseSceneUI.LevelName(upper)
                + (lower != null ? " / Ceiling: " + HouseSceneUI.LevelName(lower) : "")
                : "Roof: " + HouseSceneUI.LevelName(lower);
        }

        internal static void Draw(HouseEditorSession session, HouseEditorController controller)
        {
            var plan = session.SessionPlan;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            bool drawing = session.ActiveMode == HouseEditorSession.Mode.Slab;
            EditorGUILayout.LabelField(drawing ? "Slab cut" : session.SelectedSlabCeiling ? "Ceiling opening" : "Floor opening", EditorStyles.boldLabel);
            if (drawing)
            {
                bool ceiling = HousePropertyFields.Popup("Boundary", session.SlabCeiling ? 1 : 0, new[] { "Floor", "Ceiling" }) == 1;
                bool polygon = HousePropertyFields.Popup("Shape", session.SlabPolygon ? 1 : 0, new[] { "Rectangle", "Polygon" }) == 1;
                controller.SetSlabOptions(ceiling, polygon);
            }
            var boundary = controller.SelectedSlabBoundary(building);
            if (boundary?.Openings.Exists(o => o.Id == session.SlabSelection) != true) return;
            EditorGUILayout.LabelField(BoundaryLabel(building, boundary), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Clear selection"))
            {
                controller.ClearSelection();
                SceneView.RepaintAll();
            }
        }
    }
}
