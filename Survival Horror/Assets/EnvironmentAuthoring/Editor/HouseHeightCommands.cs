using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Validates changes affecting the whole vertical stack and neighbouring openings.
    internal static class HouseHeightCommands
    {
        internal static HouseEditResult SetLevelElevation(HousePlan plan, float elevation, bool moveOuterLevels)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            var building = HouseBuilding.For(plan);
            if (building == null) return HouseEditResult.Reject("Choose a registered House level.");
            if (HouseRebuildCoordinator.NeedsSceneInstance(plan))
                return HouseEditResult.Reject("Unpack the House before changing elevations; generated mesh prefab baking is not supported.");
            if (building.Data.Elevation(building.Find(plan)) == elevation) return new HouseEditResult(HouseEditStatus.Unchanged);
            using var preview = new HouseEditPreview(plan);
            if (!preview.Building.TrySetLevelElevation(preview.Level, elevation, moveOuterLevels, out string reason))
                return HouseEditResult.Reject(reason);
            // Candidate poses are computed from captured matrices, never written to live levels.
            var result = HouseEditCommit.PublishBuildingGeometry(preview, "Change level elevation");
            return result;
        }

        internal static HouseEditResult SetLevelHeight(HousePlan plan, float height)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            if (HouseRebuildCoordinator.NeedsSceneInstance(plan))
                return HouseEditResult.Reject("Unpack the House before changing level height.");
            var building = HouseBuilding.For(plan);
            if (building == null) return HouseEditResult.Reject("Choose a registered House level.");
            float current = building.WallHeight(building.Find(plan));
            if (float.IsFinite(height) && Mathf.Abs(height - current) < .0001f)
                return new HouseEditResult(HouseEditStatus.Unchanged);
            using var preview = new HouseEditPreview(plan);
            if (!preview.Building.TrySetLevelHeight(preview.Level, height, out string reason))
                return HouseEditResult.Reject(reason);
            var result = HouseEditCommit.PublishBuildingGeometry(preview, "Change level wall height");
            return result;
        }

    }
}
