using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Discrete finish/settings edits. Thickness belongs to the shared gesture controls.
    internal static class HouseSettingsProperties
    {
        private static bool showMapping;
        internal static HouseEditResult Draw(HouseBuilding building)
        {
            if (building == null) return new HouseEditResult(HouseEditStatus.Unchanged);
            using (new EditorGUI.DisabledScope(HouseEditorSession.instance.Pending.ActiveKind != HousePendingEdit.Kind.None))
            {
                var trims = HouseTrimProperties.Defaults(building, null);
                if (trims.Status != HouseEditStatus.Unchanged) return trims;
                EditorGUI.BeginChangeCheck();
                var inside = HousePropertyFields.Material("Interior wall finish", building.Data.InsideMaterial, wallFinish: true);
                var outside = HousePropertyFields.Material("Exterior wall finish", building.Data.OutsideMaterial, wallFinish: true);
                var floor = HousePropertyFields.Material("Floor finish", building.Data.FloorMaterial);
                var material = HousePropertyFields.Material("Ceiling finish", building.CeilingMaterial);
                bool changed = EditorGUI.EndChangeCheck();
                float x = building.CeilingTextureSize.x, y = building.CeilingTextureSize.y;
                showMapping = EditorGUILayout.Foldout(showMapping, "Ceiling texture scale", true);
                if (showMapping)
                {
                    EditorGUI.BeginChangeCheck();
                    // Delayed fields publish once, rather than once per label-drag pixel.
                    x = HousePropertyFields.DelayedFloat("Repeat width (m)", x);
                    y = HousePropertyFields.DelayedFloat("Repeat height (m)", y);
                    changed |= EditorGUI.EndChangeCheck();
                }
                if (changed)
                    return HouseFinishCommands.SetDefaultMaterials(building, inside, outside, floor, material, new Vector2(x, y));
            }
            return new HouseEditResult(HouseEditStatus.Unchanged);
        }
    }
}
