using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Commands publish immediately. OnValidate/import/Undo only queue read-only freshness checks.
    [InitializeOnLoad]
    internal static class TownRebuild
    {
        private static readonly ConcurrentQueue<TownAuthoring> pending = new ConcurrentQueue<TownAuthoring>();
        internal static int BuildCount { get; private set; }
        private static readonly HashSet<TownAuthoring> stale = new HashSet<TownAuthoring>();
        internal static bool NeedsRebuild(TownAuthoring target) => target != null && stale.Contains(target);
        internal sealed class Part
        {
            internal string Key;
            internal Mesh Mesh;
            internal Material[] Materials;
            internal Vector3 Position, Scale = Vector3.one;
            internal Quaternion Rotation = Quaternion.identity;
            internal bool Collision;
        }
        static TownRebuild()
        {
            TownAuthoring.Changed += Request;
            Undo.undoRedoPerformed += RequestAll;
            EditorSceneManager.sceneOpened += (_, __) => RequestAll();
            EditorApplication.projectChanged += RequestAll;
            EditorApplication.update += Drain;
            EditorApplication.delayCall += RequestAll;
        }
        internal static void Request(TownAuthoring target) => pending.Enqueue(target);
        private static void RequestAll()
        {
            foreach (var target in Resources.FindObjectsOfTypeAll<TownAuthoring>())
                if (!EditorUtility.IsPersistent(target) && target.gameObject.scene.IsValid()
                    && !EditorSceneManager.IsPreviewScene(target.gameObject.scene)) Request(target);
        }
        internal static void Drain()
        {
            var targets = new HashSet<TownAuthoring>();
            while (pending.TryDequeue(out var target)) if (target != null) targets.Add(target);
            stale.RemoveWhere(target => target == null);
            foreach (var target in targets)
            {
                if (!TownCommands.CanEdit(target, out _)) continue;
                var output = FindOutput(target);
                if (output != null) HouseRebuildCoordinator.RestoreOutputPose(output, target.transform);
                if (Current(target, output, Signature(target))) stale.Remove(target);
                else stale.Add(target);
            }
            if (targets.Count > 0) GeneratedMeshResources.Prune();
        }
        internal static string Signature(TownAuthoring target)
        {
            // Asset GUIDs survive a fresh editor session. Instance IDs in Unity's
            // JSON would unnecessarily regenerate every saved surface on reopen.
            var stamp = new StringBuilder("town-shared-surface-v2|");
            var seenModules = new HashSet<TownModule>();
            var meshStamps = new Dictionary<Mesh, string>();
            void Value(object value) => stamp.Append(value is float number ? number.ToString("R", CultureInfo.InvariantCulture) : value).Append('|');
            void Asset(Object asset) => Value(HouseInputSignatures.AssetIdentity(asset));
            void Mesh(Mesh mesh)
            {
                if (mesh == null) { Value("none"); return; }
                if (!meshStamps.TryGetValue(mesh, out string signature)) meshStamps[mesh] = signature = GeneratedMeshResources.SourceSignature(mesh);
                Value(signature);
            }
            void Module(TownModule module)
            {
                Asset(module); if (module == null || !seenModules.Add(module)) return;
                Mesh(module.Mesh); Value(module.Kind); Value(module.AllowStretch); Value(module.Length); Value(module.Height); Value(module.Width);
                foreach (var material in module.Materials ?? Array.Empty<Material>()) Asset(material);
            }
            foreach (var point in target.Path.Points) { Value(point.Id); Value(point.Position.ToString("R")); }
            foreach (var span in target.Path.Spans)
            { Value(span.Id); Value(span.Start); Value(span.End); if (target is TownWallRun) { Module(span.Model); Asset(span.Material); Asset(span.BackMaterial); } }
            if (target is TownWallRun wall)
            { Module(wall.Model); Value(wall.Height); Asset(wall.FrontMaterial); Asset(wall.BackMaterial); Value(wall.Posts); Mesh(wall.PostMesh); Asset(wall.PostMaterial); Value(wall.PostWidth); Value(wall.PostHeight); Value(wall.GenerateCollision);
                foreach (var finish in wall.PostMaterials ?? Array.Empty<Material>()) Asset(finish); }
            else if (target is TownSidewalk walk)
            { Value(walk.Width); Value(walk.Thickness); Asset(walk.Material); Value(walk.MetresPerRepeat); Value(walk.Curbs); Value(walk.CurbHeight); Asset(walk.CurbMaterial); Value(walk.GenerateCollision); }
            else if (target is TownRoad road)
            { Value(road.Width); Value(road.Thickness); Value(road.MetresPerRepeat); Asset(road.Material); Value(road.LeftSidewalk); Value(road.RightSidewalk); Value(road.SidewalkWidth); Value(road.SidewalkHeight); Asset(road.SidewalkMaterial); Value(road.Curbs); Value(road.CurbHeight); Asset(road.CurbMaterial); Value(road.GenerateCollision); }
            return Hash128.Compute(stamp.ToString()).ToString();
        }
        private static TownOutput FindOutput(TownAuthoring target)
        {
            var output = target.Output;
            if (output == null) output = Resources.FindObjectsOfTypeAll<TownOutput>()
                .FirstOrDefault(item => item.Owner == target && item.gameObject.scene == target.gameObject.scene);
            if (output != null) target.Output = output;
            return output;
        }

        private static bool Current(TownAuthoring target, TownOutput output, string signature)
        {
            if (output == null) return target.Path.Spans.Count == 0;
            var retained = output.GetComponentsInChildren<TownOutputPart>(true);
            bool collision = target is TownWallRun wall ? wall.GenerateCollision
                : target is TownSidewalk walk ? walk.GenerateCollision : ((TownRoad)target).GenerateCollision;
            return output.Signature == signature && output.gameObject.activeSelf
                && (retained.Length > 0 || target.Path.Spans.Count == 0) && retained.All(part => part.Source != null
                    && part.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null && (part.gameObject.activeSelf || filter.sharedMesh != part.Source)
                    && part.GetComponent<MeshRenderer>() != null
                    && (!collision || part.TryGetComponent<MeshCollider>(out var collider) && collider.sharedMesh != null));
        }

        internal static bool Refresh(TownAuthoring target, bool recordUndo = false)
        {
            if (target == null || EditorApplication.isPlayingOrWillChangePlaymode || !TownCommands.CanEdit(target, out _)) return false;
            var output = FindOutput(target);
            string signature = Signature(target);
            GeneratedMeshResources.Prune();
            if (Current(target, output, signature))
            {
                if (output != null) HouseRebuildCoordinator.RestoreOutputPose(output, target.transform);
                stale.Remove(target); return true;
            }
            int group = -1;
            if (!recordUndo)
            {
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Rebuild layout");
            }
            bool result = false;
            GeneratedOutputUndo.Run(target.gameObject, () => Resources.FindObjectsOfTypeAll<TownOutput>()
                .Where(item => item.Owner == target && item.gameObject.scene == target.gameObject.scene)
                .Select(item => item.gameObject).ToArray(), () => result = Build(target, signature), "Generate layout");
            if (group >= 0) { Undo.CollapseUndoOperations(group); Undo.IncrementCurrentGroup(); }
            if (result) stale.Remove(target);
            return result;
        }

        private static bool Build(TownAuthoring target, string signature)
        {
            var output = FindOutput(target);
            if (!target.TryValidate(out string problem))
            { if (output != null) { output.Problem = problem; output.gameObject.SetActive(false); } return false; }
            var retained = output != null ? output.GetComponentsInChildren<TownOutputPart>(true) : Array.Empty<TownOutputPart>();
            var parts = new List<Part>();
            try
            {
                if (target is TownWallRun wall) TownWallBuilder.Build(wall, parts);
                else if (target is TownSidewalk walk) TownSidewalkBuilder.Build(walk, parts);
                else if (target is TownRoad road) TownRoadBuilder.Build(road, parts);
            }
            catch (ArgumentException error)
            {
                if (output == null) output = CreateOutput(target);
                output.Problem = error.Message; output.gameObject.SetActive(false); return false;
            }
            output = output != null ? output : CreateOutput(target);
            output.transform.SetParent(target.transform, false); output.transform.localPosition = Vector3.zero;
            output.transform.localRotation = Quaternion.identity; output.transform.localScale = Vector3.one;
            if (retained.Any(part => string.IsNullOrEmpty(part.Key)) || retained.Select(part => part.Key).Distinct().Count() != retained.Length)
            {
                output.Problem = "Generated parts have duplicate or missing identities. Retained output is hidden; remove the copied generated part, then Rebuild.";
                output.gameObject.SetActive(false); return false;
            }
            output.gameObject.SetActive(true);
            var existing = retained.ToDictionary(part => part.Key);
            var desired = new HashSet<string>(parts.Select(part => part.Key));
            bool preserved = false;
            foreach (var part in parts)
            {
                if (!existing.TryGetValue(part.Key, out var native))
                {
                    var go = new GameObject(part.Key); go.transform.SetParent(output.transform, false);
                    native = go.AddComponent<TownOutputPart>(); native.Key = part.Key;
                    go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>();
                }
                var filter = native.GetComponent<MeshFilter>();
                if (filter == null) filter = GeneratedOutputUndo.AddComponent<MeshFilter>(native.gameObject);
                if (filter.sharedMesh != null && (native.Source != null ? filter.sharedMesh != native.Source : filter.sharedMesh != part.Mesh))
                { native.gameObject.SetActive(false); preserved = true; continue; }
                native.gameObject.SetActive(true); native.Source = part.Mesh;
                filter.sharedMesh = part.Mesh;
                var renderer = native.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = GeneratedOutputUndo.AddComponent<MeshRenderer>(native.gameObject);
                renderer.sharedMaterials = part.Materials.Select(material => material != null ? material
                    : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                        ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial
                        : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat")).ToArray();
                native.transform.SetLocalPositionAndRotation(part.Position, part.Rotation); native.transform.localScale = part.Scale;
                var collider = native.GetComponent<MeshCollider>();
                if (part.Collision && part.Mesh != null)
                { if (collider == null) collider = GeneratedOutputUndo.AddComponent<MeshCollider>(native.gameObject); collider.sharedMesh = part.Mesh; }
                else if (collider != null) GeneratedOutputUndo.Destroy(collider);
            }
            foreach (var old in existing.Values.Where(part => !desired.Contains(part.Key)))
            {
                string id = old.Key.Split('/')[0];
                bool stillAuthored = target.Path.Find(id) != null || target.Path.FindSpan(id) != null;
                if (stillAuthored && old.Source != null && old.TryGetComponent<MeshFilter>(out var oldFilter) && oldFilter.sharedMesh != old.Source)
                { old.gameObject.SetActive(false); preserved = true; }
                else GeneratedOutputUndo.Remove(old.gameObject);
            }
            GeneratedMeshResources.RecordOwnedHierarchy(output.gameObject, GeneratedOutputUndo.AddComponent<GeneratedMeshOwnership>);
            output.Signature = signature; output.Problem = preserved ? "Modified output is retained hidden. Save its mesh, restore its original source, then Rebuild to replace it." : "";
            EditorUtility.SetDirty(output); EditorUtility.SetDirty(target); BuildCount++;
            return true;
        }
        private static TownOutput CreateOutput(TownAuthoring target)
        {
            var root = new GameObject("Generated town geometry"); root.transform.SetParent(target.transform, false);
            var output = root.AddComponent<TownOutput>(); output.Owner = target; target.Output = output; return output;
        }
        internal static void Force(TownAuthoring target)
        { Refresh(target); }
    }
}
