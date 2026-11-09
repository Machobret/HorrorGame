using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // A final derived-mesh modifier. Layout, fitting and materials retain their existing owners.
    internal static class HouseBooleanOutput
    {
        internal static void Walls(HousePlan plan, HouseGenerationData data)
        {
            var building = HouseBuilding.For(plan);
            var output = plan.GetComponent<HouseWallBuilder>()?.Output;
            if (output == null || building == null) return;
            bool changed = false;
            foreach (var group in output.Groups)
                if (group.Root != null) changed |= Apply(group.Root, building, plan.Data.GenerateCollision,
                    data.BooleanInputs, data.HasOutputChanges, ref group.BooleanSignature);
            if (!changed) return;
            data.HasOutputChanges = true;
            GeneratedMeshResources.RecordOwnedHierarchy(output.gameObject, HouseOutputUndo.AddComponent<GeneratedMeshOwnership>);
            EditorUtility.SetDirty(output);
        }

        internal static void Slabs(HouseBuilding building, HouseGenerationData data)
        {
            var output = HouseSlabBuilder.Output(building);
            if (output == null) return;
            bool changed = false;
            foreach (var panel in output.Panels)
                if (panel.Root != null && panel.Plan != null) changed |= Apply(panel.Root, building, panel.Plan.Data.GenerateCollision,
                    data.BooleanInputs, data.HasOutputChanges, ref panel.BooleanSignature,
                    preserveLayerFinish: panel.ModuleCount == 0);
            if (!changed) return;
            data.HasOutputChanges = true;
            GeneratedMeshResources.RecordOwnedHierarchy(output.gameObject, HouseOutputUndo.AddComponent<GeneratedMeshOwnership>);
            EditorUtility.SetDirty(output);
        }

        private static string Signature(GameObject root, HouseBuilding building, string inputs, bool preserveLayerFinish) =>
            Hash128.Compute(inputs + HouseInputSignatures.AssetIdentity(building.Data.BooleanCutMaterial)
                + JsonUtility.ToJson(building.transform.worldToLocalMatrix * root.transform.localToWorldMatrix)
                + (preserveLayerFinish ? "|slab-layer-finish-v1" : "")).ToString();

        private static bool Current(GameObject root, HouseBuilding building, string inputs, string previous, bool preserveLayerFinish = false) =>
            inputs.Length == 0 && string.IsNullOrEmpty(previous)
            || previous == Signature(root, building, inputs, preserveLayerFinish) && root.GetComponentsInChildren<MeshFilter>(true).All(filter =>
                filter.GetComponent<HouseBooleanMeshState>() is not HouseBooleanMeshState state || filter.sharedMesh == state.Result);

        internal static bool WallsCurrent(HousePlan plan, HouseGenerationData data)
        {
            var building = HouseBuilding.For(plan);
            var output = plan.GetComponent<HouseWallBuilder>()?.Output;
            return building == null || output == null || output.Groups.All(group => group.Root != null
                && Current(group.Root, building, data.BooleanInputs, group.BooleanSignature));
        }

        internal static bool SlabsCurrent(HouseBuilding building, HouseGenerationData data)
        {
            var output = HouseSlabBuilder.Output(building);
            return output == null || output.Panels.All(panel => panel.Root != null && Current(panel.Root, building,
                data.BooleanInputs, panel.BooleanSignature, panel.ModuleCount == 0));
        }

        private static bool Apply(GameObject root, HouseBuilding building, bool collision, string inputs, bool rebuilt,
            ref string previous, bool preserveLayerFinish = false)
        {
            if (inputs.Length == 0 && string.IsNullOrEmpty(previous)) return false;
            string signature = Signature(root, building, inputs, preserveLayerFinish);
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (!rebuilt && Current(root, building, inputs, previous, preserveLayerFinish)) return false;
            bool changed = previous != signature;
            bool complete = true;
            foreach (var filter in filters)
            {
                var state = filter.GetComponent<HouseBooleanMeshState>();
                var source = state != null ? state.Source : filter.sharedMesh;
                if (source == null) continue;
                if (state != null && filter.sharedMesh != state.Result && filter.sharedMesh != state.Source)
                {
                    // A replacement of already cut output can contain new paint. Keep
                    // both it and the original; do not interpret it as an uncut source.
                    const string problem = "Boolean output was replaced after cutting. Its mesh is preserved; restore the previous cut mesh before changing shapes.";
                    if (state.Problem != problem) { state.Problem = problem; Debug.LogWarning(problem, filter); }
                    complete = false;
                    continue;
                }
                var toMesh = filter.transform.worldToLocalMatrix * building.transform.localToWorldMatrix;
                var collider = filter.GetComponent<MeshCollider>();
                var collisionSource = state != null ? state.CollisionSource : collider != null ? collider.sharedMesh : null;
                var renderer = filter.GetComponent<MeshRenderer>();
                var sourceMaterials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>();
                // A material-only wall rebuild can replace the source slots while
                // retaining the cut mesh. Keep those edits, excluding our extra slot.
                if (state?.SourceMaterials != null && sourceMaterials.Length > state.SourceMaterials.Length)
                    sourceMaterials = sourceMaterials.Take(state.SourceMaterials.Length).ToArray();
                try
                {
                    var result = HouseBooleanMesh.SubtractAll(source, building.Data.BooleanVolumes, toMesh);
                    var collisionResult = collisionSource == null ? null : collisionSource == source ? result
                        : HouseBooleanMesh.SubtractAll(collisionSource, building.Data.BooleanVolumes, toMesh);
                    if (result == source && collisionResult == collisionSource)
                    {
                        if (state == null) continue;
                        filter.sharedMesh = source;
                        changed |= SetMaterials(renderer, sourceMaterials);
                        if (collider != null) { collider.sharedMesh = collisionSource; collider.enabled = collision; }
                        HouseOutputUndo.Destroy(state);
                        changed = true;
                        continue;
                    }
                    if (state == null)
                    {
                        state = HouseOutputUndo.AddComponent<HouseBooleanMeshState>(filter.gameObject);
                        state.hideFlags |= HideFlags.HideInInspector;
                        state.Source = source; state.CollisionSource = collisionSource;
                        changed = true;
                    }
                    changed |= filter.sharedMesh != result || state.CollisionResult != collisionResult;
                    state.Result = result; state.CollisionResult = collisionResult; state.Problem = "";
                    state.SourceMaterials = sourceMaterials;
                    filter.sharedMesh = result;
                    var materials = new Material[result.subMeshCount];
                    for (int slot = 0; slot < source.subMeshCount; slot++)
                        materials[slot] = sourceMaterials.Length == 0 ? null : sourceMaterials[Mathf.Min(slot, sourceMaterials.Length - 1)];
                    for (int slot = source.subMeshCount; slot < materials.Length; slot++)
                        // Slab finish and supporting volume are already separate solids.
                        // Keep their own finish on cuts; walls use the House cut material.
                        materials[slot] = !preserveLayerFinish && building.Data.BooleanCutMaterial != null ? building.Data.BooleanCutMaterial
                            : sourceMaterials.FirstOrDefault();
                    changed |= SetMaterials(renderer, materials);
                    if (collider != null)
                    {
                        // Unity cannot cook an empty mesh. Retain its source assignment
                        // disabled until restoring nonempty physical geometry.
                        bool empty = collisionResult == null || collisionResult.vertexCount == 0;
                        collider.sharedMesh = empty ? collisionSource : collisionResult;
                        collider.enabled = collision && !empty;
                    }
                    EditorUtility.SetDirty(state);
                }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
                {
                    complete = false;
                    if (state == null)
                    {
                        state = HouseOutputUndo.AddComponent<HouseBooleanMeshState>(filter.gameObject);
                        state.hideFlags |= HideFlags.HideInInspector;
                        state.Source = source; state.Result = filter.sharedMesh; state.CollisionSource = collisionSource;
                    }
                    if (state.Problem != error.Message) { state.Problem = error.Message; Debug.LogWarning(error.Message, filter); }
                    EditorUtility.SetDirty(state);
                }
            }
            if (complete) previous = inputs.Length == 0 ? "" : signature;
            return changed;
        }

        private static bool SetMaterials(MeshRenderer renderer, Material[] materials)
        {
            if (renderer == null || renderer.sharedMaterials.SequenceEqual(materials)) return false;
            // Change assignments only, never the shared material asset itself.
            renderer.sharedMaterials = materials;
            return true;
        }
    }
}
