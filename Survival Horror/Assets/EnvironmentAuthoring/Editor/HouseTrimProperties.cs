using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseTrimProperties
    {
        private static bool showDefaults;
        internal static HouseEditResult Defaults(HouseBuilding building, HousePlan level)
        {
            showDefaults = EditorGUILayout.Foldout(showDefaults, "Exterior trim defaults", true);
            if (!showDefaults) return new HouseEditResult(HouseEditStatus.Unchanged);
            var kit = level != null ? level.GetComponent<HouseWallBuilder>()?.Data : null;
            if (level != null && kit == null) return new HouseEditResult(HouseEditStatus.Unchanged);
            var inherited = building?.Data.DefaultTrim;
            var current = level == null ? inherited : HouseTrimLayout.Default(level);
            var value = current?.Copy() ?? new WallTrim();
            EditorGUI.BeginChangeCheck();
            int mode = level == null ? 1 : HousePropertyFields.Popup("Trim", !kit.OverrideTrim ? 0 : kit.DefaultTrim?.Mesh == null ? 2 : 1,
                new[] { "Inherit House", "Override", "None" });
            using (new EditorGUI.DisabledScope(level != null && mode != 1))
            {
                value.Mesh = MeshField(value.Mesh);
                if (value.Mesh != null)
                {
                    value.Material = HousePropertyFields.Material("Material", value.Material);
                    value.Height = HousePropertyFields.DelayedFloat("Height (m)", value.Height);
                    value.Depth = HousePropertyFields.DelayedFloat("Projection (m)", value.Depth);
                    value.Offset = HousePropertyFields.DelayedFloat("Base offset (m)", value.Offset);
                }
            }
            if (!EditorGUI.EndChangeCheck()) return new HouseEditResult(HouseEditStatus.Unchanged);
            if (mode == 2) value.Mesh = null;
            return level == null ? HouseTrimCommands.SetDefault(building, value.Mesh == null ? null : value)
                : HouseTrimCommands.SetLevelDefault(level, mode != 0, value);
        }
        private static Mesh MeshField(Mesh mesh)
        {
            var family = HouseEditorSession.instance.TrimFamily;
            bool changed = GUI.changed;
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                HouseEditorSession.instance.TrimFamily = (TrimFamily)EditorGUI.ObjectField(HousePropertyFields.InputRect(new GUIContent("Trim family")), family, typeof(TrimFamily), false);
            GUI.changed = changed; // Browsing a catalog is intent, not an attachment edit.
            var meshes = (HouseEditorSession.instance.TrimFamily?.Meshes ?? Array.Empty<Mesh>()).Where(m => m != null).Distinct().ToArray();
            if (meshes.Length > 0)
            {
                int index = Array.IndexOf(meshes, mesh) + 1;
                int next = HousePropertyFields.Popup("Profile", index, new[] { "None / custom" }.Concat(meshes.Select(m => m.name)).ToArray());
                if (next != index) mesh = next == 0 ? null : meshes[next - 1];
            }
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return (Mesh)EditorGUI.ObjectField(HousePropertyFields.InputRect(new GUIContent("Trim mesh")), mesh, typeof(Mesh), false);
        }

        internal static void Draw(HouseEditorSession session, HouseEditorController controller)
        {
            var plan = session.SessionPlan;
            controller.RefreshSelection();
            var ids = new HashSet<string>(session.Selection.Items.Where(i => i.Type == HouseEditSelection.Kind.Trim).Select(i => i.Id));
            var walls = plan.Data.Walls.Where(w => ids.Contains(w.Id)).ToArray();
            EditorGUILayout.LabelField("Exterior trim", EditorStyles.boldLabel);
            if (GUILayout.Button("Select level perimeter"))
            {
                controller.Select(plan.Data.Walls.Where(w => HouseTrimLayout.Eligible(plan.Data, w))
                    .Select(w => new HouseEditSelection.Item(HouseEditSelection.Kind.Trim, w.Id)).ToArray());
            }
            if (walls.Length == 0)
            {
                EditorGUILayout.HelpBox("Hover the bottom band of an exterior wall and click to select its trim area. Shift adds; Ctrl removes. Or select the level perimeter above.", MessageType.Info);
                return;
            }
            var values = walls.Select(w => HouseTrimLayout.Resolve(plan, w)?.Copy() ?? new WallTrim()).ToArray();
            EditorGUILayout.LabelField(walls.Length + " wall attachment" + (walls.Length == 1 ? "" : "s"));

            void Edit(Action<WallTrim> edit)
            {
                var changes = walls.ToDictionary(w => w.Id, w => HouseTrimLayout.Resolve(plan, w)?.Copy() ?? new WallTrim());
                foreach (var value in changes.Values) edit(value);
                controller.RunCommand(() => HouseTrimCommands.Set(plan, changes));
            }
            void Field<T>(Func<WallTrim, T> read, Func<T, T> draw, Action<WallTrim, T> write)
            {
                T first = read(values[0]);
                bool previous = EditorGUI.showMixedValue;
                try
                {
                    EditorGUI.showMixedValue = values.Skip(1).Any(v => !EqualityComparer<T>.Default.Equals(read(v), first));
                    EditorGUI.BeginChangeCheck();
                    T next = draw(first);
                    if (EditorGUI.EndChangeCheck()) Edit(value => write(value, next));
                }
                finally { EditorGUI.showMixedValue = previous; }
            }
            Field(v => v.Mesh, MeshField, (v, value) => v.Mesh = value);
            var effective = values.Select(v => HouseFinishResolution.Trim(v, plan)).ToArray();
            EditorGUI.showMixedValue = effective.Any(value => value != effective[0]);
            EditorGUI.BeginChangeCheck();
            bool anyOverrides = values.Any(v => v.Material != null);
            var material = HousePropertyFields.InheritedMaterial(new GUIContent("Material", "Reset restores the exterior default."),
                values[0].Material, effective[0], anyOverrides, out _);
            bool materialChanged = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (materialChanged) Edit(value => value.Material = material);
            Field(v => v.Height, value => HousePropertyFields.DelayedFloat("Height (m)", value), (v, value) => v.Height = value);
            Field(v => v.Depth, value => HousePropertyFields.DelayedFloat("Projection (m)", value), (v, value) => v.Depth = value);
            Field(v => v.Offset, value => HousePropertyFields.DelayedFloat("Base offset (m)", value), (v, value) => v.Offset = value);
            EditorGUILayout.HelpBox("Pieces repeat along +X at their authored width. +Y is up; +Z projects outward. Remainders are cropped. Corners use square-ended overlap. Acute turns may look blunt; inspect tapered profiles for visible intersections.", MessageType.None);
            if (values.All(v => v.Mesh == null))
                EditorGUILayout.HelpBox("Choose an imported mesh, or create a starter with Assets > Create > Environment > Plain Trim Mesh.", MessageType.Info);
            var data = HouseGenerationData.Create(plan);
            foreach (string problem in walls.Select(w => HouseTrimLayout.Problem(plan, w, data)).Where(p => p.Length > 0).Distinct())
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(walls.All(w => !w.OverridesTrim)))
                if (GUILayout.Button("Reset selected trims")) controller.RunCommand(() => HouseTrimCommands.Reset(plan, ids));
            using (new EditorGUI.DisabledScope(values.All(v => v.Mesh == null)))
                if (GUILayout.Button("None for selected walls"))
                    controller.RunCommand(() => HouseTrimCommands.Set(plan, walls.Select(w => w.Id), null));
        }
    }
}
