using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class TownCommands
    {
        internal static bool CanEdit(TownAuthoring target, out string reason)
        {
            reason = "Use a loaded scene object with upright rotation and unit scale. Unpack prefabs before generating town geometry.";
            return target != null && target.gameObject.scene.IsValid() && target.gameObject.scene.isLoaded
                && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorUtility.IsPersistent(target)
                && !PrefabUtility.IsPartOfPrefabInstance(target) && !PrefabUtility.IsPartOfPrefabAsset(target) && target.HasSupportedTransform;
        }
        internal static bool Edit(TownAuthoring target, string label, Func<TownPath, bool> edit, out string reason)
        {
            if (!CanEdit(target, out reason)) return false;
            var candidate = target.Path.Copy();
            if (!edit(candidate)) { reason = "That edit is not supported for the current selection."; return false; }
            if (!target.TryValidatePath(candidate, out reason)) return false;
            if (!TownPathGeometry.JoinCrossings(candidate, out reason)) return false;
            if (JsonUtility.ToJson(candidate) == JsonUtility.ToJson(target.Path)) return false;
            Publish(target, label, () => target.SetPath(candidate));
            return true;
        }
        internal static bool Settle(TownAuthoring target, IEnumerable<string> ids, out string reason)
        {
            var selected = new HashSet<string>(ids);
            return Edit(target, "Settle town points", path =>
            {
                foreach (var point in path.Points.Where(point => selected.Count == 0 || selected.Contains(point.Id)))
                {
                    var world = target.transform.TransformPoint(point.Position);
                    var hit = Physics.RaycastAll(world + Vector3.up * .002f, Vector3.down, 10000, ~0, QueryTriggerInteraction.Ignore)
                        .Where(result => !result.collider.transform.IsChildOf(target.transform)
                            && result.collider.gameObject.scene == target.gameObject.scene).OrderBy(result => result.distance).FirstOrDefault();
                    if (hit.collider != null) point.Position = target.transform.InverseTransformPoint(hit.point);
                }
                return true;
            }, out reason);
        }
        internal static bool Settings(TownAuthoring target, string label, Action apply, out string reason)
        {
            if (!CanEdit(target, out reason)) return false;
            string before = JsonUtility.ToJson(target);
            string after; bool valid;
            // Stage authored fields only. EditorJsonUtility also serializes native
            // component state and loses transient references during this round trip.
            try { apply(); valid = target.TryValidate(out reason); after = JsonUtility.ToJson(target); }
            finally { JsonUtility.FromJsonOverwrite(before, target); }
            if (!valid) return false;
            if (before == after) return false;
            Publish(target, label, () => JsonUtility.FromJsonOverwrite(after, target));
            return true;
        }

        private static void Publish(TownAuthoring target, string label, Action apply)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RecordObject(target, label);
            apply();
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            // Flush source recording before registering generated hierarchy changes.
            // Their creation/deletion must travel with this one authored action.
            TownRebuild.Refresh(target, recordUndo: true);
            Undo.SetCurrentGroupName(label);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
        }
    }
}
