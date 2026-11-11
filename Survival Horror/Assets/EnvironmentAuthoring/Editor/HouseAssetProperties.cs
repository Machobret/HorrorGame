using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Selection/default presentation. Existing commands own publication and Undo.
    internal static class HouseAssetProperties
    {
        internal static HouseEditResult Defaults(SerializedObject settings)
        {
            var plan = ((HouseWallBuilder)settings.targetObject).GetComponent<HousePlan>();
            var trims = HouseTrimProperties.Defaults(HouseBuilding.For(plan), plan);
            if (trims.Status != HouseEditStatus.Unchanged) return trims;
            settings.Update();
            var data = settings.FindProperty("data");
            EditorGUI.BeginChangeCheck();
            Field<WallFamily>("DefaultFamily", "Interior family");
            Field<WallFamily>("ExteriorFamily", "Exterior family");
            var result = EditorGUI.EndChangeCheck() ? HouseEditCommands.ApplyBuilderSettings(settings)
                : new HouseEditResult(HouseEditStatus.Unchanged);
            return result;

            void Field<T>(string name, string label) where T : UnityEngine.Object
            {
                var property = data.FindPropertyRelative(name);
                property.objectReferenceValue = EditorGUI.ObjectField(HousePropertyFields.InputRect(new GUIContent(label)),
                    property.objectReferenceValue, typeof(T), false);
            }
        }

        // A displayed choice carries the module's interface, so model selection never
        // asks the user to match internal shape codes or silently changes segment width.
        internal sealed class OpeningChoice
        {
            internal FloorPlan.OpeningKind Kind;
            internal WallModuleDefinition Module;
            internal WallOpeningInsert Model;
            internal string Label => Model != null ? Model.name
                : (Kind == FloorPlan.OpeningKind.Window ? "Window" : "Door")
                    + $" opening only ({Module.OpeningOutline.width:0.##} x {Module.OpeningOutline.height:0.##} m)";
        }

        internal static OpeningChoice[] OpeningChoices(WallFamily family, WallKit kit, float width)
        {
            var choices = new List<OpeningChoice>();
            if (family == null) return choices.ToArray();
            var grouped = new HashSet<WallOpeningInsert>((family.OpeningFamilies ?? Array.Empty<OpeningFamily>())
                .Where(f => f != null).SelectMany(f => f.Sets ?? Array.Empty<OpeningFamily.ModelSet>())
                .Where(set => set != null).SelectMany(set => set.Models ?? Array.Empty<WallOpeningInsert>()));
            foreach (var entry in family.Openings ?? Array.Empty<WallFamily.Opening>())
            {
                if (entry?.Definition == null || Mathf.Abs(entry.Definition.Width - width) > .0001f) continue;
                choices.Add(new OpeningChoice { Kind = entry.Kind, Module = entry.Definition });
                foreach (var model in (kit?.Inserts ?? Array.Empty<WallOpeningInsert>()).Where(i => i != null).Distinct())
                    if (!grouped.Contains(model) && model.Fits(family, entry.Kind, entry.Definition))
                        choices.Add(new OpeningChoice { Kind = entry.Kind, Module = entry.Definition, Model = model });
            }
            return choices.ToArray();
        }

        internal static void Walls(HouseEditorController controller, HousePlan plan, FloorPlan.Wall[] walls)
        {
            if (!walls.All(w => w.IsPhysical)) return;
            var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            WallGeometryFamily Effective(FloorPlan.Wall wall) => wall.FamilyOverride != null ? wall.FamilyOverride : kit?.FamilyFor(plan.Data, wall);
            var effective = Effective(walls[0]);
            EditorGUI.showMixedValue = walls.Any(w => Effective(w) != effective);
            EditorGUI.BeginChangeCheck();
            var family = HousePropertyFields.ObjectOverride(new GUIContent("Wall / fence family", "Choose a Wall Family or Fence Family. Fences use their authored height and remove door/window openings. Reset restores the Interior/Exterior default."),
                walls[0].FamilyOverride, effective, walls.Any(w => w.FamilyOverride != null));
            if (EditorGUI.EndChangeCheck())
                controller.RunCommand(() => HouseEditCommands.SetWallFamily(plan, walls.Select(w => w.Id), family));
            EditorGUI.showMixedValue = false;

        }

    }
}
