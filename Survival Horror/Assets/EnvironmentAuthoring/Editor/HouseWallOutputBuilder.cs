using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Output application is independent of the Inspector and active editing context.
    internal static partial class HouseWallOutputBuilder
    {
        internal static void RemoveOrphanedOutput(HousePlan plan)
        {
            // Explicit publication captures these children before retiring them.
            var outputs = plan.GetComponentsInChildren<HouseWallOutput>(true);
            foreach (var output in outputs)
                if (output != null && output.Owner == null && output.gameObject.scene.IsValid()
                    && output.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(output)
                    && !IsInheritedOutput(output))
                    HouseOutputUndo.Remove(output.gameObject);
        }

        internal static bool IsCurrent(HouseWallBuilder builder, HouseGenerationData data)
        {
            var plan = builder.GetComponent<HousePlan>();
            var owned = Resources.FindObjectsOfTypeAll<HouseWallOutput>()
                .Where(output => output.Owner == builder && output.gameObject.scene == plan.gameObject.scene).ToArray();
            if (owned.Length != 1) return false;
            var output = owned[0];
            string signature = HouseInputSignatures.WallOutputInputs(plan, builder, data) + data.WallHeightInputs(plan);
            if (!Current(output, signature)) return false;
            // This interpretation cache is lost on reload; restoring it changes no native output.
            if (output.PlacementLayout == null || output.PlacementInputs != signature)
            { output.PlacementLayout = data.WallLayout(plan, builder); output.PlacementInputs = signature; }
            return true;
        }

        private static bool Current(HouseWallOutput output, string signature) => output.InputSignature == signature
            && output.Groups.All(group => GroupIsHealthy(group) && group.Root.transform.parent == output.transform
                // A hidden edited wall/corner can recover after its original mesh is restored.
                // Invalid trim fits instead wait for their own asset-input signature to change.
                && (group.Root.activeSelf || group.Key.StartsWith("trim:", StringComparison.Ordinal) || HasUnownedMeshes(group.Root)));

        internal static void Build(HouseWallBuilder builder, bool force = false, HouseGenerationData data = null)
        {
            if (builder == null || Application.isPlaying || EditorUtility.IsPersistent(builder)) return;
            // Prefab Stage cannot persist scene-owned generated meshes. Baked scene
            // instances retain their separate read-only handling below.
            if (PrefabStageUtility.GetCurrentPrefabStage()?.scene == builder.gameObject.scene) return;
            var plan = builder.GetComponent<HousePlan>();
            data ??= HouseGenerationData.Create(plan);
            string signature = HouseInputSignatures.WallOutputInputs(plan, builder, data) + data.WallHeightInputs(plan);
            var owned = Resources.FindObjectsOfTypeAll<HouseWallOutput>()
                .Where(o => o.Owner == builder && o.gameObject.scene == plan.gameObject.scene).ToArray();
            if (owned.Length > 1)
            {
                if (HouseRebuildCoordinator.NeedsSceneInstance(plan)) return;
                foreach (var duplicate in owned)
                    if (!IsInheritedOutput(duplicate))
                    {
                        const string duplicateProblem = "Multiple generated wall roots claim this owner. Resolve the duplicate output before rebuilding.";
                        if (duplicate.Problem == duplicateProblem && !duplicate.gameObject.activeSelf && duplicate.InputSignature == "") continue;
                        duplicate.Problem = duplicateProblem;
                        duplicate.InputSignature = "";
                        duplicate.gameObject.SetActive(false);
                        EditorUtility.SetDirty(duplicate);
                        EditorSceneManager.MarkSceneDirty(plan.gameObject.scene);
                        data.HasOutputChanges = true;
                    }
                return;
            }
            var output = owned.FirstOrDefault();
            if (output != null && !IsInheritedOutput(output) && !HouseRebuildCoordinator.NeedsSceneInstance(plan))
                data.HasOutputChanges |= HouseRebuildCoordinator.RestoreOutputPose(output, builder.transform);
            // Inherited output is never rewritten. An old/changed interpretation
            // remains preserved with the existing unpack diagnostic.
            if (!force && output != null && Current(output, signature))
            {
                // Reload loses this nonserialized snapshot. Reconstruct it once only
                // after proving current source inputs match the retained output.
                if (output.PlacementLayout == null || output.PlacementInputs != signature)
                {
                    output.PlacementLayout = data.WallLayout(plan, builder);
                    output.PlacementInputs = signature;
                }
                return;
            }
            if (output == null)
            {
                var root = new GameObject("Generated Walls — edit the House plan");
                root.transform.SetParent(builder.transform, false);
                output = root.AddComponent<HouseWallOutput>();
                output.Owner = builder;
            }
            var layout = data.WallLayout(plan, builder);
            string problem = layout.Problem;
            if (problem.Length == 0)
                problem = plan.Data.Walls.SelectMany(w => w.Openings.Select(o => HouseWallLayout.InsertProblem(plan.Data, w, builder.Data, o)))
                    .FirstOrDefault(p => p.Length > 0) ?? "";
            bool canApply = layout.IsValid && problem.Length == 0 && plan.HasSupportedTransform && !IsInheritedOutput(output);
            if (!plan.HasSupportedTransform)
                problem = "Keep the House upright at world scale (1, 1, 1).";
            if (IsInheritedOutput(output))
                problem = "This output belongs to the prefab. Unpack the scene instance and click Rebuild Walls. Prefab mesh baking is not supported.";
            output.Problem = problem;
            output.ProblemPoints = layout.ProblemPoints.ToArray();
            // Never leave stale walls visible over a plan that the current kit cannot fit.
            output.gameObject.SetActive(canApply && builder.Data.ShowWalls);
            if (canApply)
            {
                var priorChildren = output.transform.Cast<Transform>().Select(t => t.gameObject).ToArray();
                try { UpdateGroups(output, layout, builder, data); }
                catch
                {
                    // Newly staged groups have no authored data and must not survive a failed apply.
                    foreach (var child in output.transform.Cast<Transform>().Select(t => t.gameObject).ToArray())
                        if (!priorChildren.Contains(child)) HouseOutputUndo.Remove(child);
                    throw;
                }
            }
            // Native selection bases cover standalone plans too. The editing context
            // maps Scene picks on registered levels/output to the whole House.
            // Do not call DisablePicking here: it records Undo and would invalidate Redo.
            foreach (var child in output.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = plan.gameObject.layer;
                child.gameObject.hideFlags |= HideFlags.NotEditable;
            }
            // Only completed output may claim these inputs, including a validated
            // diagnostic result. An exception leaves the old signature available for retry.
            GeneratedMeshResources.RecordOwnedHierarchy(output.gameObject, HouseOutputUndo.AddComponent<GeneratedMeshOwnership>);
            output.InputSignature = signature;
            if (canApply)
            {
                output.PlacementLayout = layout;
                output.PlacementInputs = signature;
            }
            data.HasOutputChanges = true;
            EditorUtility.SetDirty(output);
            if (PrefabUtility.IsPartOfPrefabInstance(output))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(output);
                PrefabUtility.RecordPrefabInstancePropertyModifications(output.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        internal static void PlaceSpan(Transform parent, HouseWallLayout.Span span, float? height = null,
            float topExtension = 0, IEnumerable<WallModuleDefinition> blankFamily = null)
        {
            var root = new GameObject("Wall " + span.WallId);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(span.Start.x, 0, span.Start.y);
            Vector2 delta = span.End - span.Start;
            root.transform.localRotation = RotationAlong(delta.normalized);
            var pieces = span.DoorwayId != null
                ? new System.Collections.Generic.List<WallBlankLengths.Piece> { new WallBlankLengths.Piece(span.Definition,span.Definition.Width) }
                : WallBlankLengths.Fit(span.Definition, delta.magnitude, blankFamily);
            float cursor=0;
            foreach(var piece in pieces)
            {
                var definition=piece.Definition;
                bool crop=piece.Fit==WallModule.LengthFit.Cropped;
                var child = new GameObject(span.DoorwayId != null ? span.OpeningKind + " " + span.DoorwayId : "Blank");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(cursor, 0, 0);
                cursor+=piece.Length;
                var module=child.AddComponent<WallModule>();
                // Authored bodies are closed; ordinary placement length is only a transform.
                float nativeLength=crop ? piece.Length : definition.Width;
                var body=crop ? WallLengthFit.Crop(definition,nativeLength) : definition.Body;
                float fittedHeight=height ?? definition.Height;
                float openingHead=span.DoorwayId==null ? 0 : definition.OpeningOutline.yMax;
                body=WallHeightClip.Fit(body,fittedHeight,fittedHeight+topExtension,openingHead);
                if(!module.TryConfigureGenerated(definition,piece.Length,piece.Fit,body,
                    WallModuleExamples.CreatePart(child,"Body"),
                    span.Insert != null && !span.Insert.UsesPrefab ? WallModuleExamples.CreatePart(child,"Opening insert") : null,out string reason))
                    throw new InvalidOperationException(reason);
                module.SetOpeningId(span.DoorwayId);
                module.SetInsert(span.Insert);

            }
        }

        private static Quaternion RotationAlong(Vector2 along)
            => Quaternion.LookRotation(new Vector3(-along.y, 0, along.x), Vector3.up);

        private static bool IsInheritedOutput(HouseWallOutput output)
            => PrefabUtility.IsPartOfNonAssetPrefabInstance(output)
                && !PrefabUtility.IsAddedGameObjectOverride(output.gameObject);

    }
}
