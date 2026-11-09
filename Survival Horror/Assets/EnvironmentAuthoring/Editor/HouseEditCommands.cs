using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    public enum HouseEditStatus { Applied, Unchanged, Rejected }

    public readonly struct HouseEditResult
    {
        public readonly HouseEditStatus Status;
        public readonly string Message;
        public bool Applied => Status == HouseEditStatus.Applied;
        public HouseEditResult(HouseEditStatus status, string message = "") { Status = status; Message = message; }
        internal static HouseEditResult Reject(string message) => new HouseEditResult(HouseEditStatus.Rejected, message);
    }

    // Both property fields and Scene gestures enter here. Candidates validate feedback;
    // HouseEditCommit revalidates the publication scope and records authored Undo.
    public static partial class HouseEditCommands
    {
        public static HouseEditResult CommitPreview(HouseEditPreview preview, string action)
        {
            return HouseEditCommit.PublishPlanGeometry(preview, action);
        }

        public static HouseEditResult MoveNodes(HousePlan plan, IReadOnlyDictionary<string, Vector2> positions)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            return preview.PrepareMoveNodes(positions, out string reason) ? CommitPreview(preview, "Move House nodes") : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult MoveOpenings(HousePlan plan, IReadOnlyDictionary<string, float> distances)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            return preview.MoveOpenings(distances, out string reason) ? CommitPreview(preview, "Move House openings") : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult SetWallType(HousePlan plan, IEnumerable<string> ids, FloorPlan.WallType type)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            return preview.SetWallType(ids, type, out string reason) ? CommitPreview(preview, "Change wall type") : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult SetWallPresence(HousePlan plan, IEnumerable<string> ids, FloorPlan.PhysicalPresence presence)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.SetWallPresence(ids, presence, out reason)) return HouseEditResult.Reject(reason);
            return CommitPreview(preview, presence == FloorPlan.PhysicalPresence.Wall ? "Make walls" : "Make room boundaries");
        }

        public static HouseEditResult SetRoomOutdoor(HousePlan plan, IEnumerable<string> ids, bool outdoor)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.SetRoomOutdoor(ids, outdoor, out reason)) return HouseEditResult.Reject(reason);
            return CommitPreview(preview, outdoor ? "Classify rooms as Outdoor" : "Classify rooms as Indoor");
        }

        public static HouseEditResult SetOpeningKind(HousePlan plan, IEnumerable<string> ids, FloorPlan.OpeningKind kind)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            return preview.SetOpeningKind(ids, kind, out string reason) ? CommitPreview(preview, "Change opening type") : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult InsertNode(HousePlan plan, string wallId, float fraction, out string nodeId)
        {
            nodeId = null;
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            var previousIds = new HashSet<string>(plan.Data.Corners.Select(c => c.Id));
            using var preview = new HouseEditPreview(plan);
            if (!preview.InsertNode(wallId, fraction, out string reason)) return HouseEditResult.Reject(reason);
            var result = CommitPreview(preview, "Insert House corner");
            if (result.Applied) nodeId = plan.Data.Corners.First(c => !previousIds.Contains(c.Id)).Id;
            return result;
        }

        public static HouseEditResult AddWall(HousePlan plan, Vector2 start, Vector2 end, out bool endsOnWall)
        {
            endsOnWall = false;
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            if (!preview.AddWall(start, end, out endsOnWall, out string reason)) return HouseEditResult.Reject(reason);
            return CommitPreview(preview, "Draw House wall");
        }

        internal static HouseEditResult RestoreDrawingStep(HousePlan plan, string expectedInputs, string planJson, string buildingJson)
        {
            return HouseEditCommit.RestoreDrawingStep(plan, expectedInputs, planJson, buildingJson);
        }

        public static HouseEditResult DeleteWall(HousePlan plan, string wallId)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            int index = preview.Plan.Walls.ToList().FindIndex(w => w.Id == wallId);
            if (!preview.Plan.TryDeleteWall(index, out string reason) || !preview.PrepareSurfaces(out reason))
                return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishPlanGeometry(preview, "Delete House wall", allowExistingFitProblems: true);
        }

        public static HouseEditResult AddOpening(HousePlan plan, string wallId, FloorPlan.OpeningKind kind, float distance, float width)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            if (!preview.Plan.TryAddOpening(wallId, distance, width, kind, out string reason)) return HouseEditResult.Reject(reason);
            return preview.Validate(out reason) ? CommitPreview(preview, "Place " + kind) : HouseEditResult.Reject(reason);
        }

        public static HouseEditResult PlaceOpening(HousePlan plan, int wallIndex, float distance, FloorPlan.OpeningKind kind, float width = 0,
            OpeningFamily family = null, OpeningFamily.Condition condition = OpeningFamily.Condition.Clean, WallOpeningInsert model = null)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            if (family != null)
            {
                if (wallIndex < 0 || wallIndex >= plan.Data.Walls.Count) return HouseEditResult.Reject("Choose an existing wall.");
                var host = plan.Data.Walls[wallIndex];
                var choices = family.Compatible(plan.GetComponent<HouseWallBuilder>()?.Data.FamilyFor(plan.Data, host), width == 0 ? 2 : width, condition).ToArray();
                if (kind != family.Kind || choices.Length == 0 || model != null && !choices.Contains(model))
                    return HouseEditResult.Reject("Choose a compatible window/door family and model.");
                model = model != null ? model : choices[new System.Random().Next(choices.Length)];
            }
            if (!OpeningPlacement.CanPlace(plan, wallIndex, distance, kind, out var definition, out string reason, width, model?.Shape))
                return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.Plan.TryAddOpening(plan.Data.Walls[wallIndex].Id, distance, definition.Width, kind, out reason))
                return HouseEditResult.Reject(reason);
            var opening = preview.Plan.Walls[wallIndex].Openings.Last();
            opening.SetAssets(model?.Shape, model, family, condition);
            return HouseEditCommit.PublishPlanGeometry(preview, "Place " + kind);
        }

        public static HouseEditResult RemoveOpening(HousePlan plan, string openingId)
        {
            if (!HouseEditValidation.Source(plan, out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            using var preview = new HouseEditPreview(plan);
            var wall = preview.Plan.Walls.FirstOrDefault(w => w.Openings.Any(o => o.Id == openingId));
            if (wall == null) return HouseEditResult.Reject("The opening no longer exists.");
            preview.Plan.TryRemoveOpening(wall.Id, openingId);
            return HouseEditCommit.PublishPlanGeometry(preview, "Delete opening", allowExistingFitProblems: true);
        }

        public static HouseEditResult ApplyRoomFinishToWalls(HousePlan plan, IEnumerable<string> roomIds)
            => HouseFinishCommands.ApplyRoomFinishToWalls(plan, roomIds);

        public static HouseEditResult SetRoomFinishes(HousePlan plan, IEnumerable<string> ids, Material material)
            => HouseFinishCommands.SetRoomFinishes(plan, ids, material);

        public static HouseEditResult SetRoomSurfaceFinishes(HousePlan plan, IEnumerable<string> ids, bool ceiling, Material material)
            => HouseFinishCommands.SetRoomSurfaceFinishes(plan, ids, ceiling, material);

        public static HouseEditResult SetWallFinishes(HousePlan plan, IEnumerable<string> ids, bool left, Material material)
            => HouseFinishCommands.SetWallFinishes(plan, ids, left, material);

        public static HouseEditResult SetLevelElevation(HousePlan plan, float elevation, bool moveOuterLevels)
            => HouseHeightCommands.SetLevelElevation(plan, elevation, moveOuterLevels);

        public static HouseEditResult SetLevelHeight(HousePlan plan, float height)
            => HouseHeightCommands.SetLevelHeight(plan, height);

        public static HouseEditResult SetSlabOpening(HouseBuilding building, string boundaryId, string openingId,
            IReadOnlyList<Vector2> points, string action = "Draw slab opening")
            => HouseSlabCommands.SetSlabOpening(building, boundaryId, openingId, points, action);

        public static HouseEditResult DeleteSlabOpening(HouseBuilding building, string boundaryId, string openingId)
            => HouseSlabCommands.DeleteSlabOpening(building, boundaryId, openingId);

        public static HouseWallBuilder EnableWalls(HousePlan plan)
            => HouseStructureCommands.EnableWalls(plan);

        public static HouseStack.Level AddLevel(HouseBuilding building, bool below = false,
            HouseLevelInheritance inheritance = HouseLevelInheritance.OutsideContours)
            => HouseStructureCommands.AddLevel(building, below, inheritance);

        public static IReadOnlyList<HouseStack.Level> FootprintSources(HousePlan target)
            => HouseFootprintCommands.Sources(target);

        // The UI supplies confirmation; without it a populated target cannot be replaced.
        public static HouseEditResult CopyExteriorFootprint(HousePlan target, HousePlan source,
            System.Func<string, bool> confirmReplacement = null)
            => HouseFootprintCommands.Copy(target, source, confirmReplacement);


        public static HouseEditResult SetFloorSettings(HousePlan plan, bool show, Material material, Vector2 repeat, bool collision)
            => HouseFinishCommands.SetFloorSettings(plan, show, material, repeat, collision);

        public static HouseEditResult SetSlabSettings(HouseBuilding building, float floorThickness, float ceilingThickness,
            Material material, Vector2 repeat)
            => HouseSlabCommands.SetSlabSettings(building, floorThickness, ceilingThickness, material, repeat);

        public static HouseEditResult SetSlabEnvelope(HouseBuilding building, float total, float finish)
            => HouseSlabCommands.SetSlabEnvelope(building, total, finish);

        internal static HouseEditResult CommitSlabPreview(HouseEditPreview preview)
            => HouseSlabCommands.CommitSlabPreview(preview);

        public static HouseEditResult RenameLevel(HousePlan plan, string name)
            => HouseEditCommit.RenameLevel(plan, name);

    }
}
