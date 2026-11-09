using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseRulerSlabProperties
    {
        internal static void Draw(HouseEditorSession session, HouseEditorController controller, bool ceiling)
        {
            var plan = session.SessionPlan;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            EditorGUILayout.LabelField(HouseSceneUI.LevelName(plan) + (ceiling ? " · ceiling slab" : " · floor slab"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Thickness affects every level and room.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Clear selection")) controller.ClearSelection();
        }
    }
}
