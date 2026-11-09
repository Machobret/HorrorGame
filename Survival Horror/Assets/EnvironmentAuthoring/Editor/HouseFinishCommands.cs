using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Settings edits that do not change topology or vertical geometry.
    internal static class HouseFinishCommands
    {
        internal static HouseEditResult SetLevelWallFinishes(HousePlan plan, Material inside, Material outside)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var draft = new HouseFinishDraft(plan);
            draft.SetLevelWallFinishes(inside, outside);
            return draft.Commit("Change level wall finishes");
        }

        internal static HouseEditResult ApplyRoomFinishToWalls(HousePlan plan, IEnumerable<string> roomIds)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var draft = new HouseFinishDraft(plan);
            if (!draft.ApplyRoomFinishToWalls(roomIds)) return HouseEditResult.Reject("Choose existing rooms.");
            return draft.Commit("Apply room finish to facing walls");
        }

        internal static HouseEditResult SetRoomFinishes(HousePlan plan, IEnumerable<string> ids, Material material)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseFinishDraft(plan);
            if (!preview.SetRoomMaterials(ids, material)) return HouseEditResult.Reject("Choose existing rooms.");
            return preview.Commit( "Change room wall finishes");
        }

        internal static HouseEditResult SetRoomSurfaceFinishes(HousePlan plan, IEnumerable<string> ids, bool ceiling, Material material)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseFinishDraft(plan);
            if (!preview.SetRoomSurfaceMaterials(ids, ceiling, material)) return HouseEditResult.Reject("Choose existing rooms.");
            return preview.Commit( ceiling ? "Change room ceiling finishes" : "Change room floor finishes");
        }

        internal static HouseEditResult SetWallFinishes(HousePlan plan, IEnumerable<string> ids, bool left, Material material)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseFinishDraft(plan);
            if (!preview.SetWallMaterials(ids, left, material)) return HouseEditResult.Reject("Choose existing walls.");
            return preview.Commit( "Change wall-side finishes");
        }

        internal static HouseEditResult SetSurfaceFinish(HousePlan plan, string boundaryId, string regionId,
            bool ceiling, bool useOverride, Material material)
            => SetSurfaceFinishes(plan, boundaryId, new[] { regionId }, ceiling, useOverride, material);

        internal static HouseEditResult SetSurfaceFinishes(HousePlan plan, string boundaryId, IEnumerable<string> regionIds,
            bool ceiling, bool useOverride, Material material)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            var house = HouseBuilding.For(plan); var level = house?.Find(plan);
            var boundary = house?.Boundaries.FirstOrDefault(item => item.Id == boundaryId);
            if (boundary == null || (boundary.LowerId != level.Id && boundary.UpperId != level.Id))
                return HouseEditResult.Reject("Choose a slab adjoining the active level.");
            using var draft = new HouseFinishDraft(house);
            if (!draft.SetSurfaceFinishes(boundaryId, regionIds, ceiling, useOverride, material))
                return HouseEditResult.Reject("Choose an existing slab face region.");
            return draft.Commit("Change slab face finish");
        }

        internal static HouseEditResult SetDefaultMaterials(HouseBuilding building, Material inside, Material outside,
            Material floor, Material ceiling, Vector2 ceilingRepeat)
        {
            using var draft = new HouseFinishDraft(building);
            draft.SetDefaultMaterials(inside, outside, floor);
            draft.SetCeilingSettings(ceiling, ceilingRepeat);
            return draft.Commit("Change House default finishes");
        }

        internal static HouseEditResult SetLevelCeiling(HousePlan plan, Material material)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var draft = new HouseFinishDraft(plan);
            draft.SetLevelCeiling(material);
            return draft.Commit("Change level ceiling finish");
        }

        internal static HouseEditResult SetCeilingSettings(HouseBuilding building, Material material, Vector2 repeat)
        {
            using var draft = new HouseFinishDraft(building);
            draft.SetCeilingSettings(material, repeat);
            return draft.Commit("Change slab settings");
        }

        internal static HouseEditResult SetFloorSettings(HousePlan plan, bool show, Material material, Vector2 repeat, bool collision)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseFinishDraft(plan);
            preview.SetFloorSettings(show, material, repeat, collision);
            // Keep existing invalid-setting diagnostics in the builders during this refactor.
            return preview.Commit( "Change floor settings");
        }
    }
}
