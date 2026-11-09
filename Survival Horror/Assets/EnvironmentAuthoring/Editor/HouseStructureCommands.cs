using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    public enum HouseLevelInheritance { None, OutsideContours, All, Complete }

    // Structural commands publish authored hierarchy and output in one Undo action.
    internal static class HouseStructureCommands
    {
        internal static HousePlan CreateHouse(UnityEngine.SceneManagement.Scene scene, Vector3? floorPoint = null)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create House");
            var root = new GameObject("House");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var building = root.AddComponent<HouseBuilding>();
            EnvironmentProjectSettings.instance.Apply(building);
            var child = new GameObject("Level 1");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = Vector3.up * building.SurfaceOffset;
            var plan = child.AddComponent<HousePlan>();
            EnvironmentProjectSettings.instance.Apply(child.AddComponent<HouseWallBuilder>());
            // Nominal storey height is authored data, independent of mutable kit dimensions.
            var level = new HouseStack.Level(plan, content: CreateContent(plan));
            building.RegisterLevel(level);
            building.RegisterBoundary(new HouseStack.Boundary(upperId: level.Id));
            building.RegisterBoundary(new HouseStack.Boundary(lowerId: level.Id));
            foreach (var boundary in building.Boundaries)
                HouseSurfaceLayout.Seed(boundary, string.IsNullOrEmpty(boundary.LowerId) ? null : plan.Data,
                    string.IsNullOrEmpty(boundary.UpperId) ? null : plan.Data);
            // Source objects must exist in history before output refers to them.
            if (floorPoint.HasValue) root.transform.position = floorPoint.Value - Vector3.up * building.SurfaceOffset;
            Undo.RegisterCreatedObjectUndo(root, "Create House");
            HouseOutputUndo.Refresh(building, "Create House");
            Undo.CollapseUndoOperations(group);
            HouseEditCommit.NotifyAuthoredChanged();
            Undo.IncrementCurrentGroup();
            return plan;
        }

        internal static HouseWallBuilder EnableWalls(HousePlan plan)
        {
            if (!HouseEditValidation.Source(plan, out _)) return null;
            var existing = plan.GetComponent<HouseWallBuilder>();
            if (existing != null) return existing;
            HouseOutputUndo.Prepare(plan);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Enable 3D Walls");
            var builder = Undo.AddComponent<HouseWallBuilder>(plan.gameObject);
            // Initial families and the insert catalog are one configuration, including
            // array size changes on this newly created native component.
            Undo.RegisterCompleteObjectUndo(builder, "Choose Wall Kit");
            EnvironmentProjectSettings.instance.Apply(builder);
            EditorUtility.SetDirty(builder);
            PrefabUtility.RecordPrefabInstancePropertyModifications(builder);
            Undo.FlushUndoRecordObjects();
            HouseOutputUndo.Refresh(plan, "Enable 3D Walls");
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            HouseEditCommit.NotifyAuthoredChanged();
            return builder;
        }

        internal static HouseStack.Level AddLevel(HouseBuilding building, bool below = false, HouseLevelInheritance inheritance = HouseLevelInheritance.OutsideContours)
        {
            if (building == null) throw new System.InvalidOperationException("Choose an existing House.");
            if (!building.TryValidateLayout(out string sourceProblem)) throw new System.InvalidOperationException(sourceProblem);
            var previous = below ? building.Levels.First() : building.Levels.Last();
            if (!System.Enum.IsDefined(typeof(HouseLevelInheritance), inheritance))
                throw new System.InvalidOperationException("Choose a level inheritance mode.");
            if (inheritance == HouseLevelInheritance.OutsideContours && !previous.Plan.Data.CanCopyFootprint(out string reason))
                throw new System.InvalidOperationException("Cannot copy the exterior footprint: " + reason);
            NextElevation(building, previous, below);
            var candidate = PrepareAddedLevel(building, below, inheritance, out var footprint, out var level);
            float elevation = candidate.Elevation(level);
            HouseOutputUndo.Prepare(building);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(below ? "Add level below" : "Add level above");
            int anchor = building.Levels.IndexOf(building.Data.Anchor);
            int number = below ? anchor + 1 : building.Levels.Count - anchor + 1;
            var root = new GameObject("Level " + (below ? "B" : "") + number);
            root.transform.SetParent(building.transform, false);
            var plan = root.AddComponent<HousePlan>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(footprint), plan.Data);
            var oldBuilder = previous.Plan.GetComponent<HouseWallBuilder>();
            if (oldBuilder != null)
                EditorUtility.CopySerialized(oldBuilder, root.AddComponent<HouseWallBuilder>());
            level.Bind(plan, CreateContent(plan));
            root.transform.localPosition = Vector3.up * (elevation + candidate.SurfaceOffset);
            Undo.RegisterCreatedObjectUndo(root, "Create level");
            Undo.RecordObject(building, "Register level");
            // Only the former extreme slab and the new level change. Retain the
            // existing native bindings/records instead of replacing the whole stack.
            var original = below ? building.Below(previous) : building.Above(previous);
            var prepared = candidate.Boundaries.First(item => item.Id == original.Id);
            original.Reconnect(prepared.LowerId, prepared.UpperId);
            // Ordinary creation keeps the shared slab's opening records themselves.
            // Only Complete above replaces a pristine roof with the copied floor.
            if (inheritance == HouseLevelInheritance.Complete && !below) original.ReplaceLayout(prepared);
            else
            {
                original.ReplaceDividerGraph(prepared);
                original.ReplaceRegions(prepared.Regions);
            }
            building.RegisterLevel(level, below);
            var extreme = below ? candidate.Below(level) : candidate.Above(level);
            building.RegisterBoundary(extreme.Copy());
            EditorUtility.SetDirty(building);
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            Undo.FlushUndoRecordObjects();
            HouseOutputUndo.Refresh(building, "Create level");
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            HouseEditCommit.NotifyAuthoredChanged();
            return level;
        }

        internal static bool CanDeleteLevel(HousePlan plan, out string reason)
        {
            reason = "Choose an existing House level.";
            var building = HouseBuilding.For(plan);
            if (building == null) return false;
            reason = "Keep the House upright at unit scale.";
            if (!plan.HasSupportedTransform) return false;
            reason = "Unpack the House instance before changing its levels.";
            if (HouseRebuildCoordinator.NeedsSceneInstance(plan)) return false;
            return building.Data.CanRemoveLevel(building.Find(plan), out reason);
        }

        internal static HouseEditResult DeleteLevel(HousePlan plan, System.Func<string, bool> confirm)
        {
            if (!CanDeleteLevel(plan, out string reason) || !HouseEditValidation.Source(plan, out reason))
                return HouseEditResult.Reject(reason);
            var building = HouseBuilding.For(plan);
            string source = HouseEditInputs.SourceInputs(building);
            var level = building.Find(plan);
            bool bottom = building.Levels.IndexOf(level) == 0;
            var original = bottom ? building.Above(level) : building.Below(level);
            var candidate = building.Data.Copy();
            if (!candidate.TryRemoveLevel(candidate.Find(level.Id), out reason)) return HouseEditResult.Reject(reason);
            var shared = candidate.Boundaries.First(boundary => boundary.Id == original.Id);
            FloorPlan Read(HouseStack stack, string id) => stack.Find(id)?.Plan?.Data;
            if (!HouseSurfaceEdit.TryReconcile(original, shared,
                Read(building.Data, original.LowerId), Read(building.Data, original.UpperId),
                Read(candidate, shared.LowerId), Read(candidate, shared.UpperId), out reason)
                || !candidate.TryValidateLayout(out reason)
                || !HouseSlabLayout.TryPatches(candidate, item => item.Plan.Data, out _, out reason))
                return HouseEditResult.Reject(reason);
            string message = "Delete " + HouseSceneUI.LevelName(plan) + " and all its walls, openings and placed content?\n\n"
                + "Remaining level elevations and their shared slab settings are preserved. This can be undone.";
            if (confirm == null) return HouseEditResult.Reject("Confirm deletion of the level and its content.");
            if (!confirm(message)) return new HouseEditResult(HouseEditStatus.Unchanged);
            if (HouseEditInputs.SourceInputs(building) != source)
                return HouseEditResult.Reject("The House changed while confirming deletion. Try again.");

            HouseOutputUndo.Prepare(building);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete level");
            Undo.RecordObject(building, "Delete level");
            building.Data.TryRemoveLevel(level, out _);
            original.ReplaceDividerGraph(shared);
            original.ReplaceRegions(shared.Regions);
            EditorUtility.SetDirty(building);
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            Undo.FlushUndoRecordObjects();
            HouseOutputUndo.Run(building.gameObject, () =>
            {
                HouseOutputUndo.Destroy(plan.gameObject);
                HouseRebuildCoordinator.RefreshNow(building);
            }, "Delete level");
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            HouseEditCommit.NotifyAuthoredChanged();
            return new HouseEditResult(HouseEditStatus.Applied);
        }

        private static HouseStack PrepareAddedLevel(HouseBuilding building, bool below, HouseLevelInheritance inheritance,
            out FloorPlan footprint, out HouseStack.Level added)
        {
            if (!HouseSlabLayout.TryPatches(building.Data, item => item.Plan.Data, out _, out string reason))
                throw new System.InvalidOperationException(reason);
            var previous = below ? building.Levels.First() : building.Levels.Last();
            var copiedCorners = new System.Collections.Generic.Dictionary<string, string>();
            footprint = inheritance == HouseLevelInheritance.All || inheritance == HouseLevelInheritance.Complete
                ? previous.Plan.Data.CopyForNewLevel(out copiedCorners) : new FloorPlan();
            if (inheritance == HouseLevelInheritance.OutsideContours)
            {
                if (!footprint.TryCopyFootprintFrom(previous.Plan.Data, out reason))
                    throw new System.InvalidOperationException(reason);
                // Interior junctions leave straight subdivisions on the old perimeter.
                // On a new contour-only level those subdivisions have no purpose.
                foreach (var corner in footprint.Corners.ToArray())
                {
                    var edges = footprint.Walls.Where(w => w.StartId == corner.Id || w.EndId == corner.Id).ToArray();
                    if (edges.Length != 2) continue;
                    var a = footprint.GetPosition(edges[0].StartId == corner.Id ? edges[0].EndId : edges[0].StartId);
                    var b = footprint.GetPosition(edges[1].StartId == corner.Id ? edges[1].EndId : edges[1].StartId);
                    if (OutlineGeometry.PointOnSegment(corner.Position, a, b)) footprint.TryDissolveCorner(corner.Id, out _);
                }
            }
            footprint.InsideMaterial = previous.Plan.Data.InsideMaterial;
            footprint.OutsideMaterial = previous.Plan.Data.OutsideMaterial;
            footprint.FloorMaterial = previous.Plan.Data.FloorMaterial;
            footprint.CeilingMaterial = previous.Plan.Data.CeilingMaterial;
            footprint.FloorTextureSize = previous.Plan.Data.FloorTextureSize;
            var candidate = building.Data.Copy();
            added = new HouseStack.Level(span: previous.Span);
            var original = below ? building.Below(previous) : building.Above(previous);
            var shared = candidate.Boundaries.First(item => item.Id == original.Id);
            candidate.RegisterLevel(added, below);
            shared.Reconnect(below ? added.Id : previous.Id, below ? previous.Id : added.Id);
            var extreme = new HouseStack.Boundary(lowerId: below ? null : added.Id, upperId: below ? added.Id : null);
            candidate.RegisterBoundary(extreme);
            string addedId = added.Id;
            var newPlan = footprint;
            FloorPlan Read(HouseStack.Level item) => item.Id == addedId ? newPlan : item.Plan.Data;
            FloorPlan Old(string id) => building.Find(id)?.Plan?.Data;
            FloorPlan Next(string id) => candidate.Find(id) is HouseStack.Level item ? Read(item) : null;
            if (!HouseSurfaceEdit.TryReconcile(original, shared, Old(original.LowerId), Old(original.UpperId),
                Next(shared.LowerId), Next(shared.UpperId), out reason))
                throw new System.InvalidOperationException(reason);
            HouseSurfaceLayout.Seed(extreme, below ? null : footprint, below ? footprint : null);
            if (inheritance == HouseLevelInheritance.Complete)
            {
                // The level's floor is shared with the ceiling below. Do not erase
                // an existing customized roof while repeating the floor above it.
                if (!below && (original.Openings.Count > 0 || original.Dividers.Count > 0
                    || original.Regions.Any(segment => segment.IsCustomized || segment.CutAway)))
                    throw new System.InvalidOperationException("The existing roof has slab edits. Choose Rooms and walls to preserve them, or clear those edits before copying a complete floor layout.");
                var sourceFloor = building.Below(previous);
                var targetFloor = candidate.Below(added);
                var copiedFloor = sourceFloor.Copy();
                copiedFloor.Reconnect(targetFloor.LowerId, targetFloor.UpperId);
                // Repeat the authored slab layout against the new adjoining rooms.
                // Opposite-room seams remain derived rather than becoming dividers.
                if (!HouseSurfaceEdit.TryReconcile(sourceFloor, copiedFloor, Old(sourceFloor.LowerId), Old(sourceFloor.UpperId),
                    Next(targetFloor.LowerId), Next(targetFloor.UpperId), out reason))
                    throw new System.InvalidOperationException(reason);
                targetFloor.ReplaceLayout(copiedFloor.CopyForNewLevel(previous.Id, added.Id, copiedCorners));
            }
            if (!candidate.TryValidateLayout(out reason)
                || !HouseSlabLayout.TryPatches(candidate, Read, out _, out reason))
                throw new System.InvalidOperationException(reason);
            return candidate;
        }

        private static Transform CreateContent(HousePlan plan)
        {
            var root = new GameObject("Content");
            root.transform.SetParent(plan.transform, false);
            return root.transform;
        }

        private static float NextElevation(HouseBuilding building, HouseStack.Level previous, bool below)
        {
            float elevation = building.Data.Elevation(previous) + (below ? -1 : 1) * (previous.Span);
            float roof = elevation + previous.Span;
            if (!float.IsFinite(elevation) || Mathf.Abs(elevation - building.Data.Elevation(previous)) - building.SlabThickness < .2999f
                || !below && (!float.IsFinite(roof) || roof - elevation - building.SlabThickness < .2999f))
                throw new System.InvalidOperationException("Keep finite level elevations and at least 0.3 m clearance.");
            return elevation;
        }
    }
}
