using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseBooleanCommands
    {
        internal static HouseEditResult SetCutMaterial(HousePlan plan, Material material)
        {
            if (!CanEdit(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var draft = new HouseFinishDraft(HouseBuilding.For(plan));
            draft.SetBooleanCutMaterial(material);
            return draft.Commit("Change Boolean cut material");
        }

        internal static HouseEditResult Set(HousePlan plan, string id, HouseStack.BooleanShape shape,
            Vector3 center, Quaternion rotation, Vector3 size)
        {
            if (!CanEdit(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (preview.Building == null) return HouseEditResult.Reject("Choose a registered House level.");
            if (!preview.Building.TrySetBooleanVolume(id, shape, center, rotation, size, out reason)) return HouseEditResult.Reject(reason);
            // Only volume data changes. Logical wall fit, headroom and slab topology
            // stay untouched; the existing writer owns freshness and native Undo.
            return HouseEditCommit.PublishBooleans(preview, id == null ? "Add Boolean shape" : "Change Boolean shape");
        }

        internal static HouseEditResult Delete(HousePlan plan, string id)
        {
            if (!CanEdit(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (preview.Building == null || !preview.Building.RemoveBooleanVolume(id))
                return HouseEditResult.Reject("Choose an existing Boolean shape.");
            return HouseEditCommit.PublishBooleans(preview, "Delete Boolean shape");
        }

        internal static HouseEditResult PublishTransform(HouseEditPreview preview)
        {
            if (!CanEdit(preview.Source, out string reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishBooleans(preview, "Transform Boolean shape");
        }

        private static bool CanEdit(HousePlan plan, out string reason)
        {
            if (!HouseEditValidation.Source(plan, out reason)) return false;
            reason = "Choose an editable scene House outside Play mode.";
            if (EditorUtility.IsPersistent(plan) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return false;
            reason = "Keep the House upright at unit scale.";
            if (!plan.HasSupportedTransform) return false;
            reason = "Unpack the House before cutting its inherited output.";
            // A House-wide shape also affects the other levels, so their output
            // follows the same read-only/unpack rule as shared slab edits.
            if (HouseBuilding.For(plan).Levels.Any(level => level.Plan != null && HouseRebuildCoordinator.NeedsSceneInstance(level.Plan))) return false;
            reason = "";
            return true;
        }
    }
}
