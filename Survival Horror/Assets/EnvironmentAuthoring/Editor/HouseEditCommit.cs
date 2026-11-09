using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Single publication boundary for authored Undo, dirty/prefab recording and refresh.
    internal static class HouseEditCommit
    {
        // Read-only views may cache within an event, but an accepted native write
        // ends that snapshot. Commands do not know which views are currently open.
        internal static event System.Action AuthoredChanged;
        internal static void NotifyAuthoredChanged() => AuthoredChanged?.Invoke();
        internal static HouseEditResult RenameLevel(HousePlan plan, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return HouseEditResult.Reject("Give the level a name.");
            if (plan.name == name) return new HouseEditResult(HouseEditStatus.Unchanged);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename level");
            Undo.RecordObject(plan.gameObject, "Rename level");
            plan.name = name;
            EditorUtility.SetDirty(plan.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(plan.gameObject);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            NotifyAuthoredChanged();
            SceneView.RepaintAll();
            return new HouseEditResult(HouseEditStatus.Applied);
        }

        internal static HouseEditResult PublishPlanGeometry(HouseEditPreview preview, string action, bool allowExistingFitProblems = false)
        {
            if (preview.HasConflict) return Conflict();
            if (!preview.ValidateCapturedSource(out string sourceProblem)) return HouseEditResult.Reject(sourceProblem);
            string reason;
            Dictionary<HousePlan, HouseWallLayout.Result> layouts;
            bool valid = allowExistingFitProblems ? HouseEditValidation.Geometry(preview, false, out reason, out layouts)
                : preview.Validate(out reason, out layouts);
            return valid ? WritePlan(preview, action, layouts) : HouseEditResult.Reject(reason);
        }

        private static HouseEditResult Conflict() => HouseEditResult.Reject("The House or its kit changed during this gesture. Try the edit again.");

        private static HouseEditResult WritePlan(HouseEditPreview preview, string action,
            IReadOnlyDictionary<HousePlan, HouseWallLayout.Result> layouts)
        {
            // The synchronous publication entry already checked source freshness.
            // Validation invokes no callbacks or deferred work that could change it.
            string planJson = preview.PlanJson();
            // Topology and its adjoining slab records form one accepted edit.
            // The writer filters unchanged objects and records one native Undo group.
            return CommitObjects(new Object[] { preview.Source, preview.SourceBuilding },
                new[] { planJson, preview.BuildingJson() }, action, preview.Source, preview, layouts);
        }

        internal static HouseEditResult PublishBuildingGeometry(HouseEditPreview preview, string action, bool requireEditedFit = false)
        {
            if (!BuildingSource(preview, out string reason)) return HouseEditResult.Reject(reason);
            // This scope publishes only Building data. Exact equality means no
            // authored edit, so fitting unchanged walls adds no safety.
            if (preview.BuildingJson() == JsonUtility.ToJson(preview.SourceBuilding))
                return new HouseEditResult(HouseEditStatus.Unchanged);
            if (!HouseEditValidation.Geometry(preview, requireEditedFit, out reason)) return HouseEditResult.Reject(reason);
            return WriteBuilding(preview, action);
        }

        internal static HouseEditResult PublishSlabCuts(HouseEditPreview preview, string action)
        {
            if (!BuildingSource(preview, out string reason)) return HouseEditResult.Reject(reason);
            if (!HouseSlabLayout.TryPatches(preview.Building, preview.OriginalPlanFor, out _, out reason)) return HouseEditResult.Reject(reason);
            // Cuts and their attached material dividers do not change uniform
            // level heights or wall topology. The slab read validates their graph.
            return WriteBuilding(preview, action);
        }

        internal static HouseEditResult RestoreDrawingStep(HousePlan plan, string expectedInputs, string planJson, string buildingJson)
        {
            if (HouseEditInputs.SourceInputs(plan) != expectedInputs) return Conflict();
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            // Only the drawing chain supplies its immediately preceding accepted snapshot.
            var building = HouseBuilding.For(plan);
            return CommitObjects(new Object[] { plan, building }, new[] { planJson, buildingJson }, "Remove last drawn point", plan);
        }

        internal static HouseEditResult PublishFinish(HouseFinishDraft draft, string action)
        {
            if (draft.HasConflict) return HouseEditResult.Reject("The edited settings changed. Try the edit again.");
            // The draft exposes finish setters only, validated by their domain operations.
            return CommitObjects(new[] { draft.Source }, new[] { draft.CandidateJson }, action, draft.Source as HousePlan);
        }

        // Boolean transforms borrow the geometry candidate, but may publish only
        // cutter settings. Do not let an unrelated mutable stack edit ride along.
        internal static HouseEditResult PublishBooleans(HouseEditPreview preview, string action)
        {
            if (!BuildingSource(preview, out string reason)) return HouseEditResult.Reject(reason);
            var expected = preview.OriginalBuilding.Copy();
            expected.CopyBooleanSettings(preview.Building);
            if (JsonUtility.ToJson(expected) != JsonUtility.ToJson(preview.Building))
                return HouseEditResult.Reject("Boolean edits can only change cut shapes and their material.");
            return WriteBuilding(preview, action);
        }

        private static bool BuildingSource(HouseEditPreview preview, out string reason)
        {
            reason = "The House or its kit changed during this gesture. Try the edit again.";
            if (preview.HasConflict) return false;
            reason = "Keep the House upright at unit scale.";
            if (!preview.HasSupportedTransform) return false;
            reason = "A building-only edit cannot also change a level plan.";
            if (preview.PlanJson() != preview.Original) return false;
            reason = "Choose an existing House.";
            return preview.Building != null && preview.Building.TryValidateLayout(out reason);
        }

        private static HouseEditResult WriteBuilding(HouseEditPreview preview, string action)
        {
            var building = preview.SourceBuilding;
            return CommitObjects(new Object[] { building }, new[] { preview.BuildingJson() }, action, null);
        }

        private static HouseEditResult CommitObjects(Object[] targets, string[] candidateJson, string action, HousePlan affectedPlan,
            HouseEditPreview geometry = null, IReadOnlyDictionary<HousePlan, HouseWallLayout.Result> layouts = null)
        {
            var changed = Enumerable.Range(0, targets.Length).Where(i => JsonUtility.ToJson(targets[i]) != candidateJson[i]).ToArray();
            if (changed.Length == 0) return new HouseEditResult(HouseEditStatus.Unchanged);
            var owner = (Component)affectedPlan ?? targets.OfType<HouseBuilding>().FirstOrDefault();
            HouseOutputUndo.Prepare(owner);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(action);
            Undo.RecordObjects(changed.Select(i => targets[i]).ToArray(), action);
            foreach (int i in changed)
            {
                JsonUtility.FromJsonOverwrite(candidateJson[i], targets[i]);
                EditorUtility.SetDirty(targets[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets[i]);
            }
            Undo.FlushUndoRecordObjects();
            if (owner != null) HouseOutputUndo.Refresh(owner, action,
                geometry != null ? HouseGenerationData.ForPublication(geometry, layouts) : null);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            NotifyAuthoredChanged();
            SceneView.RepaintAll();
            return new HouseEditResult(HouseEditStatus.Applied);
        }
    }
}
