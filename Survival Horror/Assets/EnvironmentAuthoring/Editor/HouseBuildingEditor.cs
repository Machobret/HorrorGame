using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    [CustomEditor(typeof(HouseBuilding))]
    public sealed class HouseBuildingEditor : UnityEditor.Editor
    {
        private bool HasFrameBounds() => HouseEditingContext.SelectedWallFrameBounds((HouseBuilding)target, out _);
        private Bounds OnGetFrameBounds() { HouseEditingContext.SelectedWallFrameBounds((HouseBuilding)target, out var bounds); return bounds; }

        private string slabSettingsProblem;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HouseSlabThicknessControls(() => target as HouseBuilding).Root);
            root.Add(new IMGUIContainer(OnInspectorGUI));
            return root;
        }

        public override void OnInspectorGUI()
        {
            var building = (HouseBuilding)target;
            var result = HouseSettingsProperties.Draw(building);
            if (result.Status != HouseEditStatus.Unchanged)
                slabSettingsProblem = result.Status == HouseEditStatus.Rejected ? result.Message : "";
            if (!string.IsNullOrEmpty(slabSettingsProblem))
                EditorGUILayout.HelpBox(slabSettingsProblem, MessageType.Warning);
            foreach (var level in building.Levels)
                if (level.Plan != null && GUILayout.Button($"Edit {HouseSceneUI.LevelName(level.Plan)} ({building.Data.Elevation(level):0.##} m)"))
                    HouseEditingContext.SelectLevel(level.Plan);
            if (GUILayout.Button("Add Above")) HouseEditingContext.ChooseLevelInheritance(building, false, GUILayoutUtility.GetLastRect());
            if (GUILayout.Button("Add Below")) HouseEditingContext.ChooseLevelInheritance(building, true, GUILayoutUtility.GetLastRect());
            EditorGUILayout.HelpBox("Place level props under its Content child. Floor areas retain their material settings through supported layout edits. Slab openings never remove walls or props.", MessageType.Info);
            HouseSlabBuilder.Output(building, out string outputProblem);
            if (!string.IsNullOrEmpty(outputProblem))
            {
                EditorGUILayout.HelpBox(outputProblem, MessageType.Warning);
            }
        }

        internal void AddFromInspector(HouseBuilding building, bool below)
        {
            try { HouseEditingContext.AddLevelAndSelect(building, below); }
            catch (System.InvalidOperationException error) { slabSettingsProblem = error.Message; }
        }

    }
}
