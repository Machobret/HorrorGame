using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseTrimCommands
    {
        internal static HouseEditResult Reset(HousePlan plan, IEnumerable<string> ids)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            foreach (string id in ids.Distinct())
            {
                var wall = preview.Plan.Walls.FirstOrDefault(w => w.Id == id);
                if (wall == null) return HouseEditResult.Reject("Choose existing walls.");
                wall.ResetTrim();
            }
            return preview.Validate(out reason) ? HouseEditCommands.CommitPreview(preview, "Reset trim inheritance") : HouseEditResult.Reject(reason);
        }

        internal static HouseEditResult SetDefault(HouseBuilding building, WallTrim value)
        {
            if (building == null || building.Levels.Count == 0) return HouseEditResult.Reject("Choose an existing House.");
            if (!HouseTrimLayout.ValidateProfile(value, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(building.Levels[0].Plan);
            preview.Building.SetDefaultTrim(value);
            return HouseEditCommit.PublishBuildingGeometry(preview, "Change House trim default");
        }

        internal static HouseEditResult SetLevelDefault(HousePlan plan, bool useOverride, WallTrim value)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            if (!HouseTrimLayout.ValidateProfile(value, out reason)) return HouseEditResult.Reject(reason);
            var builder = plan.GetComponent<HouseWallBuilder>();
            if (builder == null) return HouseEditResult.Reject("Choose a level with a wall kit.");
            var defaults = useOverride ? value : HouseBuilding.For(plan)?.Data.DefaultTrim;
            foreach (var wall in plan.Data.Walls.Where(w => HouseTrimLayout.Eligible(plan.Data, w)))
                if (!HouseTrimLayout.TryPieces(plan.Data, wall, builder.Data, out _, out reason, defaults: defaults))
                    return HouseEditResult.Reject(reason);
            using var settings = new SerializedObject(builder);
            var data = settings.FindProperty("data");
            data.FindPropertyRelative("OverrideTrim").boolValue = useOverride;
            var trim = data.FindPropertyRelative("DefaultTrim"); value ??= new WallTrim();
            trim.FindPropertyRelative("mesh").objectReferenceValue = value.Mesh;
            trim.FindPropertyRelative("material").objectReferenceValue = value.Material;
            trim.FindPropertyRelative("height").floatValue = value.Height;
            trim.FindPropertyRelative("depth").floatValue = value.Depth;
            trim.FindPropertyRelative("offset").floatValue = value.Offset;
            return HouseEditCommands.ApplyBuilderSettings(settings);
        }

        internal static HouseEditResult Set(HousePlan plan, IEnumerable<string> wallIds, WallTrim settings)
            => Set(plan, wallIds.Distinct().ToDictionary(id => id, _ => settings));

        internal static HouseEditResult Set(HousePlan plan, IReadOnlyDictionary<string, WallTrim> settings)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            if (settings.Count == 0) return new HouseEditResult(HouseEditStatus.Unchanged);
            using var preview = new HouseEditPreview(plan);
            foreach (var pair in settings)
            {
                if (!HouseTrimLayout.ValidateProfile(pair.Value, out reason)) return HouseEditResult.Reject(reason);
                var wall = preview.Plan.Walls.FirstOrDefault(w => w.Id == pair.Key);
                if (wall == null || pair.Value != null && !HouseTrimLayout.Eligible(preview.Plan, wall))
                    return HouseEditResult.Reject("Select physical exterior walls to attach a trim.");
                // Removing an absent attachment has no authored effect and must preserve Redo.
                if (pair.Value == null && wall.Trim == null && HouseTrimLayout.Resolve(plan, plan.Data.Walls.First(w => w.Id == pair.Key))?.Mesh == null) continue;
                var value = pair.Value?.Copy();
                // Changing an attachment must not restart an existing wall's repeat phase.
                value?.AdvancePattern((wall.Trim?.PatternOffset ?? 0) - value.PatternOffset);
                wall.SetTrim(value);
            }
            return HouseEditCommands.CommitPreview(preview,
                settings.Values.All(v => v == null) ? "Remove exterior trim" : "Change exterior trim");
        }
    }
}
