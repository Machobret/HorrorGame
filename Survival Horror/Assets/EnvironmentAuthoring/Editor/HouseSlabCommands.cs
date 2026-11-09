using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Shared slab geometry and thickness operations; finish-only changes use a smaller draft.
    internal static class HouseSlabCommands
    {
        internal static HouseEditResult SetAreaCutAway(HousePlan plan, string boundaryId, IEnumerable<string> regionIds, bool cutAway)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!PrepareCutEdit(preview, boundaryId, out var boundary, out reason)) return HouseEditResult.Reject(reason);
            var level = preview.Building.Find(preview.SourceBuilding.Find(plan).Id);
            if (boundary.UpperId != level.Id && boundary.LowerId != level.Id)
                return HouseEditResult.Reject("Choose a slab adjoining the active level.");
            var ids = regionIds?.Distinct().ToArray();
            if (ids == null || ids.Length == 0 || ids.Any(id => !boundary.Regions.Any(segment => segment.Id == id && !segment.Ceiling)))
                return HouseEditResult.Reject("Choose existing floor areas from one slab.");
            foreach (var segment in boundary.Regions.Where(segment => ids.Contains(segment.Id))) segment.SetCutAway(cutAway);
            return HouseEditCommit.PublishSlabCuts(preview, cutAway ? "Cut away floor area" : "Restore floor area");
        }

        internal static HouseEditResult SetRoomCutAway(HousePlan plan, IEnumerable<string> roomIds, bool cutAway)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = level == null ? null : building.Below(level);
            var ids = roomIds?.Distinct().ToArray();
            if (boundary == null || ids == null || ids.Length == 0 || ids.Any(id => !plan.Data.Cells.Any(room => room.Id == id)))
                return HouseEditResult.Reject("Choose rooms on the active level.");
            return SetAreaCutAway(plan, boundary.Id, HouseSlabLayout.Patches(building, boundary.Id)
                .Where(patch => patch.Upper == level && ids.Contains(patch.UpperRoom)).Select(patch => patch.Floor.Id), cutAway);
        }

        internal static HouseEditResult InsertOpeningVertex(HousePlan plan, string boundaryId, string openingId, int edgeIndex, Vector2 point)
        {
            var building = HouseBuilding.For(plan);
            var opening = building?.Boundaries.FirstOrDefault(b => b.Id == boundaryId)?.Openings.FirstOrDefault(o => o.Id == openingId);
            if (opening == null || edgeIndex < 0 || edgeIndex >= opening.Points.Count)
                return HouseEditResult.Reject("Choose an existing slab-opening edge.");
            var start = opening.Points[edgeIndex]; var end = opening.Points[(edgeIndex + 1) % opening.Points.Count];
            if (!OutlineGeometry.PointOnSegment(point, start, end)
                || Vector2.Distance(point, start) <= OutlineGeometry.Tolerance || Vector2.Distance(point, end) <= OutlineGeometry.Tolerance)
                return HouseEditResult.Reject("Insert the point inside the selected edge.");
            using var preview = new HouseEditPreview(plan);
            if (!PrepareCutEdit(preview, boundaryId, out var boundary, out string reason)) return HouseEditResult.Reject(reason);
            boundary.Openings.First(o => o.Id == openingId).InsertPoint(edgeIndex, point);
            if (!ReconcileCutEdit(preview, boundary, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishSlabCuts(preview, "Insert slab-opening point");
        }

        internal static HouseEditResult SetSlabOpening(HouseBuilding building, string boundaryId, string openingId,
            IReadOnlyList<Vector2> points, string action = "Draw slab opening")
        {
            if (building == null) return HouseEditResult.Reject("Choose an existing House.");
            if (!building.TryValidateLayout(out string reason)) return HouseEditResult.Reject(reason);
            var plan = building.Levels.FirstOrDefault(l => l.Plan != null)?.Plan;
            if (plan == null) return HouseEditResult.Reject("Choose a building with a level.");
            using var preview = new HouseEditPreview(plan);
            return SetSlabOpening(preview, boundaryId, openingId, points, action);
        }

        internal static HouseEditResult SetSlabOpening(HouseEditPreview preview, string boundaryId, string openingId,
            IReadOnlyList<Vector2> points, string action = "Draw slab opening")
        {
            if (!PrepareCutEdit(preview, boundaryId, out var boundary, out string reason)) return HouseEditResult.Reject(reason);
            if (openingId != null && !boundary.Openings.Any(opening => opening.Id == openingId))
                return HouseEditResult.Reject("The slab opening no longer exists on this boundary.");
            if (!preview.Building.TrySetOpening(boundary, openingId, points, out reason)) return HouseEditResult.Reject(reason);
            if (!ReconcileCutEdit(preview, boundary, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishSlabCuts(preview, action);
        }

        internal static HouseEditResult DeleteSlabOpening(HouseBuilding building, string boundaryId, string openingId)
        {
            if (building == null) return HouseEditResult.Reject("Choose an existing House.");
            if (!building.TryValidateLayout(out string reason)) return HouseEditResult.Reject(reason);
            var plan = building.Levels.FirstOrDefault(l => l.Plan != null)?.Plan;
            if (plan == null) return HouseEditResult.Reject("Choose a building with a level.");
            using var preview = new HouseEditPreview(plan);
            return DeleteSlabOpening(preview, boundaryId, openingId);
        }

        internal static HouseEditResult DeleteSlabOpening(HouseEditPreview preview, string boundaryId, string openingId)
        {
            if (!PrepareCutEdit(preview, boundaryId, out var boundary, out string reason)) return HouseEditResult.Reject(reason);
            if (!boundary.Openings.Any(opening => opening.Id == openingId))
                return HouseEditResult.Reject("The slab opening no longer exists on this boundary.");
            if (!preview.Building.TryRemoveOpening(boundary, openingId))
                return HouseEditResult.Reject("The slab opening could not be removed.");
            if (!ReconcileCutEdit(preview, boundary, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishSlabCuts(preview, "Delete slab opening");
        }

        private static bool ReconcileCutEdit(HouseEditPreview preview, HouseStack.Boundary boundary, out string reason)
        {
            var before = preview.OriginalBuilding.Boundaries.First(item => item.Id == boundary.Id);
            if (!preview.ResolveCutAttachments(boundary, out reason)) return false;
            return HouseSurfaceEdit.TryReconcileCuts(before, boundary,
                preview.OriginalPlanFor(preview.OriginalBuilding.Find(boundary.LowerId)),
                preview.OriginalPlanFor(preview.OriginalBuilding.Find(boundary.UpperId)), out reason);
        }

        private static bool PrepareCutEdit(HouseEditPreview preview, string boundaryId,
            out HouseStack.Boundary boundary, out string reason)
        {
            boundary = null;
            reason = "Choose an existing House.";
            if (preview?.SourceBuilding == null) return false;
            reason = "The House or its kit changed during this gesture. Try the edit again.";
            if (preview.HasConflict) return false;
            if (!HouseEditValidation.Source(preview.Source, out reason)) return false;
            preview.ResetGeometry();
            if (!preview.Building.TryValidateLayout(out reason)) return false;
            // Cuts publish only stack data; interpret its records against the captured
            // accepted graphs, never an unrelated planar proposal in a borrowed preview.
            if (!HouseSlabLayout.TryPatches(preview.Building, preview.OriginalPlanFor, out _, out reason)) return false;
            boundary = preview.Building.Boundaries.FirstOrDefault(item => item.Id == boundaryId);
            reason = boundary == null ? "The slab boundary no longer exists in this House." : "";
            return boundary != null;
        }

        internal static HouseEditResult SetSlabSettings(HouseBuilding building, float floorThickness, float ceilingThickness,
            Material material, Vector2 repeat)
        {
            if (building == null) return HouseEditResult.Reject("Choose an existing House.");
            if (!building.TryValidateLayout(out string structureProblem)) return HouseEditResult.Reject(structureProblem);
            var plan = building.Levels.FirstOrDefault(l => l.Plan != null)?.Plan;
            if (plan == null) return HouseEditResult.Reject("Choose a building with a level.");
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            if (ceilingThickness < .01f && ceilingThickness >= .01f - .00000001f) ceilingThickness = .01f;
            if (floorThickness == building.FloorThickness && ceilingThickness == building.CeilingThickness)
                return HouseFinishCommands.SetCeilingSettings(building, material, repeat);
            using var preview = new HouseEditPreview(plan);
            return PrepareSlabSettings(preview, floorThickness, ceilingThickness, material, repeat, out string reason, validateGeometry: false)
                ? HouseEditCommit.PublishBuildingGeometry(preview, "Change slab settings") : HouseEditResult.Reject(reason);
        }

        internal static HouseEditResult SetSlabEnvelope(HouseBuilding building, float total, float finish)
            => SetSlabSettings(building, finish, total - finish, building.CeilingMaterial, building.CeilingTextureSize);

        internal static bool PrepareSlabSettings(HouseEditPreview preview, float floorThickness, float ceilingThickness,
            Material material, Vector2 repeat, out string reason, bool validateGeometry = true)
        {
            // Prepare only the isolated candidate. The pending owner checks freshness
            // before each update; immediate commands construct a fresh candidate.
            // Final publication independently rechecks source and candidate geometry.
            preview.ResetBuildingSettings();
            var building = preview.SourceBuilding;
            // Normalize the subtraction-sized error before deciding whether this is an
            // edit. An effective no-op must not erase Redo.
            if (ceilingThickness < .01f && ceilingThickness >= .01f - .00000001f) ceilingThickness = .01f;
            bool changed = floorThickness != building.FloorThickness || ceilingThickness != building.CeilingThickness;
            if (changed)
            {
                reason = "Unpack the House before changing slab thickness; generated mesh prefab baking is not supported.";
                if (building.Levels.Any(l => l.Plan != null && HouseRebuildCoordinator.NeedsSceneInstance(l.Plan))) return false;
                reason = "Use finite slab/finish thicknesses, with at least 0.01 m for each finish and supporting structure.";
                if (!float.IsFinite(floorThickness) || !float.IsFinite(ceilingThickness)
                    || floorThickness < .01f || ceilingThickness < .01f - .00000001f) return false;
                ceilingThickness = Mathf.Max(.01f, ceilingThickness);
                if (!preview.Building.TrySetSlabThickness(floorThickness + ceilingThickness, floorThickness, out reason)) return false;
            }
            preview.Building.ConfigureCeilingFinish(material, repeat);
            if (changed && validateGeometry)
            {
                if (!HouseEditValidation.UniformHeights(preview, out reason)) return false;
            }
            reason = "";
            return true;
        }

        internal static HouseEditResult CommitSlabPreview(HouseEditPreview preview)
        {
            return HouseEditCommit.PublishBuildingGeometry(preview, "Change slab thickness");
        }
    }
}
