using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace EnvironmentAuthoring.Editor
{
    public static partial class HouseEditCommands
    {
        public static HouseEditResult ApplyBuilderSettings(SerializedObject data)
        {
            if (!data.hasModifiedProperties) return new HouseEditResult(HouseEditStatus.Unchanged);
            foreach (var plan in data.targetObjects.OfType<HouseWallBuilder>().Select(builder => builder.GetComponent<HousePlan>()).Distinct())
                HouseOutputUndo.Prepare(plan);
            // Unity's serialized Inspector already records Undo. Do not record the same edit twice.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change wall kit settings");
            bool applied = data.ApplyModifiedProperties();
            if (applied)
                foreach (var builder in data.targetObjects.OfType<HouseWallBuilder>())
                {
                    EditorUtility.SetDirty(builder);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(builder);
                }
            Undo.FlushUndoRecordObjects();
            if (applied)
                foreach (var plan in data.targetObjects.OfType<HouseWallBuilder>().Select(builder => builder.GetComponent<HousePlan>()).Distinct())
                    HouseOutputUndo.Refresh(plan, "Change wall kit settings");
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            if (applied) HouseEditCommit.NotifyAuthoredChanged();
            return new HouseEditResult(applied ? HouseEditStatus.Applied : HouseEditStatus.Unchanged);
        }

        public static HouseEditResult SetWallFamily(HousePlan plan, IEnumerable<string> ids, UnityEngine.Object family)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            if (family != null && !(family is WallFamily) && !(family is FenceFamily))
                return HouseEditResult.Reject("Choose a wall or fence family.");
            if (family is WallFamily masonry && !masonry.IsValid(out reason)) return HouseEditResult.Reject(reason);
            if (family is FenceFamily fence && !fence.IsValid(out reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            var selected = new HashSet<string>(ids);
            var walls = preview.Plan.Walls.Where(w => selected.Contains(w.Id)).ToArray();
            if (walls.Length == 0 || walls.Length != selected.Count || walls.Any(w => !w.IsPhysical))
                return HouseEditResult.Reject("Choose existing physical walls.");
            foreach (var wall in walls)
            {
                wall.SetFamily(family as WallGeometryFamily);
                if (wall.IsFence) wall.MutableOpenings.Clear();
            }
            return preview.Validate(out reason) ? CommitPreview(preview, "Change wall family") : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult AddOpeningModels(HouseWallBuilder builder, IEnumerable<WallOpeningInsert> models)
        {
            if (builder == null) return HouseEditResult.Reject("Choose an editable House level.");
            if (!HouseEditValidation.Source(builder.GetComponent<HousePlan>(), out string reason)) return HouseEditResult.Reject(reason);
            var current = builder.Data.Inserts ?? System.Array.Empty<WallOpeningInsert>();
            var additions = models.Where(model => model != null && !current.Contains(model)).Distinct().ToArray();
            if (additions.Length == 0) return new HouseEditResult(HouseEditStatus.Unchanged);
            using var settings = new SerializedObject(builder);
            var catalog = settings.FindProperty("data").FindPropertyRelative("Inserts");
            catalog.arraySize = current.Length + additions.Length;
            for (int i = 0; i < additions.Length; i++)
                catalog.GetArrayElementAtIndex(current.Length + i).objectReferenceValue = additions[i];
            return ApplyBuilderSettings(settings);
        }

        public static HouseEditResult SetOpeningAssets(HousePlan plan, string id, FloorPlan.OpeningKind kind,
            float width, string shape, WallOpeningInsert insert)
            => SetOpeningAssets(plan, new[] { id }, kind, width, shape, insert);

        public static HouseEditResult SetOpeningAssets(HousePlan plan, IEnumerable<string> ids, FloorPlan.OpeningKind kind,
            float width, string shape, WallOpeningInsert insert)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            foreach (string id in ids.Distinct())
            {
                var wall = preview.Plan.Walls.FirstOrDefault(w => w.Openings.Any(o => o.Id == id));
                if (wall == null || !preview.Plan.TryChangeOpening(wall.Id, id, kind, width == 0 ? wall.Openings.First(o => o.Id == id).Width : width, out reason))
                    return HouseEditResult.Reject(wall == null ? "Choose an existing opening." : reason);
                wall.Openings.First(o => o.Id == id).SetAssets(shape, insert);
            }
            return preview.Validate(out reason) ? CommitPreview(preview, "Change opening assets") : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult ClearOpeningModels(HousePlan plan, IEnumerable<string> ids)
            => ChangeExistingOpenings(plan, ids, 0, true);

        public static HouseEditResult SetOpeningWidths(HousePlan plan, IEnumerable<string> ids, float width)
            => ChangeExistingOpenings(plan, ids, width, false);

        private static HouseEditResult ChangeExistingOpenings(HousePlan plan, IEnumerable<string> ids, float width, bool clear)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            foreach (string id in ids.Distinct())
            {
                var wall = preview.Plan.Walls.FirstOrDefault(w => w.Openings.Any(o => o.Id == id));
                var opening = wall?.Openings.First(o => o.Id == id);
                if (opening == null) return HouseEditResult.Reject("Choose existing openings.");
                if (clear) opening.SetAssets(opening.Shape, null);
                else if (!preview.Plan.TryChangeOpening(wall.Id, id, opening.Kind, width, out reason)) return HouseEditResult.Reject(reason);
                if (!clear && opening.InsertFamily != null && !opening.InsertFamily.Compatible(
                    plan.GetComponent<HouseWallBuilder>()?.Data.FamilyFor(preview.Plan, wall), width, opening.Condition).Contains(opening.Insert))
                    return HouseEditResult.Reject("This family/model has no compatible segment at the requested width.");
            }
            return preview.Validate(out reason) ? CommitPreview(preview, clear ? "Clear opening models" : "Change opening widths") : HouseEditResult.Reject(reason);
        }

        // Null model means randomize now. Only the resolved model is ever rendered or rebuilt.
        public static HouseEditResult SetOpeningFamily(HousePlan plan, IEnumerable<string> ids, OpeningFamily family,
            OpeningFamily.Condition condition, WallOpeningInsert model = null, float width = 0)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            if (family == null) return HouseEditResult.Reject("Choose a window or door family.");
            var selected = ids.Distinct().ToArray();
            if (selected.Length == 0) return new HouseEditResult(HouseEditStatus.Unchanged);
            using var preview = new HouseEditPreview(plan);
            var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            var random = new System.Random();
            foreach (string id in selected)
            {
                var wall = preview.Plan.Walls.FirstOrDefault(w => w.Openings.Any(o => o.Id == id));
                var opening = wall?.Openings.First(o => o.Id == id);
                if (opening == null) return HouseEditResult.Reject("Choose existing openings.");
                float size = width == 0 ? opening.Width : width;
                var choices = family.Compatible(kit?.FamilyFor(preview.Plan, wall), size, condition).ToArray();
                if (choices.Length == 0 || model != null && !choices.Contains(model))
                    return HouseEditResult.Reject("The chosen family, condition or model does not fit every selected opening.");
                var chosen = model != null ? model : choices[random.Next(choices.Length)];
                if (!preview.Plan.TryChangeOpening(wall.Id, id, family.Kind, size, out reason)) return HouseEditResult.Reject(reason);
                opening.SetAssets(chosen.Shape, chosen, family, condition);
            }
            return preview.Validate(out reason) ? CommitPreview(preview, "Change window/door family") : HouseEditResult.Reject(reason);
        }

    }
}
