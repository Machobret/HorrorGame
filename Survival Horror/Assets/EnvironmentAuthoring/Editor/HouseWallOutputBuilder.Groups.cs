using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseWallOutputBuilder
    {
        [Serializable]
        private sealed class SpanInputs
        {
            public string OpeningId, Definition;
            public FloorPlan.OpeningKind Kind;
            public Vector2 Start, End;
        }

        private static void UpdateGroups(HouseWallOutput output, HouseWallLayout.Result layout, HouseWallBuilder builder, HouseGenerationData data)
        {
            var previous = output.Groups.ToDictionary(g => g.Key);
            var next = new List<HouseWallOutput.Group>();
            foreach (var wall in layout.Spans.GroupBy(s => s.WallId))
            {
                var plan = builder.GetComponent<HousePlan>();
                var authored = plan.Data;
                var owner = authored.Walls.First(w => w.Id == wall.Key);
                var blankFamily = builder.Data.BlanksFor(authored, owner).ToArray();
                var origin = wall.First().Start;
                var position = new Vector3(origin.x, 0, origin.y);
                string inputs = WallGroupInputs(plan, wall, blankFamily, data, origin);
                float topInset = layout.TopInsets[wall.Key];
                if (topInset > 0) inputs += ":join-top:" + topInset.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                string key = "wall:" + wall.Key;
                if (RetainEditedGroup(output, previous, next, key, inputs)) continue;
                var group = RetainOrCreate(output, previous, "wall:" + wall.Key, inputs, out bool changed);
                group.Root.SetActive(true);
                if (changed)
                {
                    foreach (var span in wall)
                        if (HouseBuilding.For(builder.GetComponent<HousePlan>()) != null)
                            HouseHeightWalls.Place(group.Root.transform, builder.GetComponent<HousePlan>(), span, data, topInset);
                        else
                            PlaceSpan(group.Root.transform, span, height: span.Definition.Height - topInset, blankFamily: blankFamily);
                    // Placement methods use plan coordinates. Store the built shape
                    // relative to this wall so translating it only moves its root.
                    foreach (Transform child in group.Root.transform) child.localPosition -= position;
                }
                group.Root.transform.localPosition = position;
                ApplyWallMaterials(group.Root, builder.GetComponent<HousePlan>(), wall.Key);
                next.Add(group);
            }

            var failedTrims = UpdateTrimGroups(output, previous, next, builder, data);
            UpdateCornerGroups(output, previous, next, builder, data, layout, failedTrims);


            // A classification change can remove a cover while its logical join
            // survives. Keep an edited result recoverable until its source is restored.
            foreach (string nodeId in layout.OmittedCoverNodes)
                RetainEditedGroup(output, previous, next, "corner:" + nodeId);

            // Remove deleted/replaced groups from this owned output root.
            // Children outside this owned output root are never touched.
            var retained = new HashSet<GameObject>(next.Select(g => g.Root));
            for (int i = output.transform.childCount - 1; i >= 0; i--)
            {
                var child = output.transform.GetChild(i).gameObject;
                if (!retained.Contains(child))
                    HouseSlabBuilder.RemoveOwnedObject(child);
            }

            foreach (var group in next)
            {
                group.MeshPartCount = group.Root.GetComponentsInChildren<MeshFilter>(true).Length;
                group.ModuleCount = group.Root.GetComponentsInChildren<WallModule>(true).Length;
            }
            output.Groups = next;
        }

        private static string WallGroupInputs(HousePlan plan, IEnumerable<HouseWallLayout.Span> wall,
            IEnumerable<WallModuleDefinition> blankFamily, HouseGenerationData data, Vector2 origin)
        {
            string inputs = "modular-wall-local-v2" + HouseHeightWalls.Signature(plan, wall, data);
            foreach (var span in wall)
            {
                inputs += JsonUtility.ToJson(new SpanInputs { OpeningId = span.DoorwayId,
                    Definition = data.KitSignature(span.Definition), Kind = span.OpeningKind,
                    Start = HouseInputSignatures.LocalShapePoint(span.Start - origin), End = HouseInputSignatures.LocalShapePoint(span.End - origin) });
                if (span.Insert != null) inputs += ":insert:" + HouseInputSignatures.AssetIdentity(span.Insert)
                    + ":" + (span.Insert.UsesPrefab ? "prefab-door" : data.MeshSignature(span.Insert.Mesh));

            }
            foreach (var variant in blankFamily) inputs += data.KitSignature(variant);
            return inputs;
        }

        // Serialized creation records identify derived base meshes, including after reload.
        // Unknown replacements (persistent or not) may contain authored paint.
        private static bool HasUnownedMeshes(GameObject root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var marker = filter.GetComponent<GeneratedMeshOwnership>();
                var cut = filter.GetComponent<HouseBooleanMeshState>();
                foreach (var mesh in new[] { filter.sharedMesh, cut != null ? cut.Source : null })
                {
                    if (mesh == null || GeneratedMeshResources.IsModuleSource(filter.gameObject, mesh)) continue;
                    if (marker == null || !marker.Meshes.Contains(mesh)) return true;
                }
            }
            return false;
        }

        private static bool RetainEditedGroup(HouseWallOutput output,
            Dictionary<string, HouseWallOutput.Group> previous, List<HouseWallOutput.Group> next,
            string key, string inputs = null)
        {
            if (!previous.TryGetValue(key, out var prior) || prior.Root == null
                || prior.Root.transform.parent != output.transform
                || inputs != null && GroupIsHealthy(prior) && prior.Signature == GroupSignature(key, inputs)
                || !HasUnownedMeshes(prior.Root)) return false;
            // A matching input hash permits reuse, never ownership of an unknown mesh.
            prior.Root.SetActive(false); next.Add(prior);
            output.Problem += (output.Problem.Length == 0 ? "" : "\n")
                + key + " retains an edited output mesh. Export it or restore its source mesh before rebuilding.";
            return true;
        }

        private static string GroupSignature(string key, string inputs) => key.StartsWith("wall:", StringComparison.Ordinal)
            ? GeometryKey(inputs) : Hash128.Compute(inputs).ToString();

        private static string GeometryKey(string inputs) => "wall-geometry-v1:" + Hash128.Compute(inputs);

        private static HouseWallOutput.Group RetainOrCreate(HouseWallOutput output, Dictionary<string, HouseWallOutput.Group> previous, string key, string inputs, out bool changed)
        {
            string signature = GroupSignature(key, inputs);
            if (previous.TryGetValue(key, out var group) && GroupIsHealthy(group) && group.Root.transform.parent == output.transform && group.Signature == signature)
            {
                changed = false;
                return group;
            }

            changed = true;
            var root = new GameObject(key);
            root.transform.SetParent(output.transform, false);
            return new HouseWallOutput.Group{Key = key, Signature = signature, Root = root};
        }

        private static bool GroupIsHealthy(HouseWallOutput.Group group)
        {
            if (group.Root == null) return false;
            var filters = group.Root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != group.MeshPartCount) return false;
            if (group.Root.GetComponentsInChildren<WallModule>(true).Length != group.ModuleCount) return false;
            if (filters.Length == 0 || filters.Any(f => f.sharedMesh == null || f.GetComponent<MeshRenderer>() == null)) return false;
            if (!group.Key.StartsWith("wall:", StringComparison.Ordinal)) return true;
            return WallModulesAreHealthy(group.Root);
        }

        internal static bool WallModulesAreHealthy(GameObject root)
        {
            var modules = root.GetComponentsInChildren<WallModule>(true);
            return modules.Length > 0 && modules.All(m => m.Definition != null && m.Body != null);
        }

        private static void ApplyWallMaterials(GameObject root, HousePlan plan, string wallId)
        {
            var wall = plan.Data.Walls.First(w => w.Id == wallId);
            var stored = plan.Data.GetPosition(wall.EndId) - plan.Data.GetPosition(wall.StartId);
            // Finish ownership is per logical wall side, not per repeated module.
            // Resolve it once; a reversed module only swaps the two bindings.
            var left = HouseFinishResolution.RenderMaterial(HouseFinishResolution.Wall(plan, wall, true));
            var right = HouseFinishResolution.RenderMaterial(HouseFinishResolution.Wall(plan, wall, false));
            var family = HouseFinishResolution.Family(plan, wall);
            foreach (var module in root.GetComponentsInChildren<WallModule>(true))
            {
                var along = plan.transform.InverseTransformDirection(module.transform.right);
                bool sideAIsLeft = Vector2.Dot(stored, new Vector2(along.x, along.z)) > 0;
                if (family != null && family.HasMaterialRoles)
                    module.SetRoleMaterials(HouseFinishResolution.RenderMaterial(family.RoleMaterial(wall, 0)),
                        HouseFinishResolution.RenderMaterial(family.RoleMaterial(wall, 1)));
                else module.SetSideMaterials(sideAIsLeft ? left : right, sideAIsLeft ? right : left);
                module.ApplyInsertMaterial(family != null && family.HasMaterialRoles ? family.RoleMaterial(wall, 0) : null,
                    family != null && family.HasMaterialRoles ? family.RoleMaterial(wall, 1) : null);
            }
            ApplyWallProjectionRange(root);
        }

        private static void ApplyWallProjectionRange(GameObject root)
        {
            var modules = root.GetComponentsInChildren<WallModule>(true).Where(module => module.Body != null).ToArray();
            if (modules.Length == 0) return;
            var renderers = modules.Select(module => module.Body.GetComponent<MeshRenderer>()).ToArray();
            float minimum = renderers.Min(renderer => renderer.bounds.min.y);
            float maximum = renderers.Max(renderer => renderer.bounds.max.y);
            foreach (var module in modules)
                if (module.ConfigureVerticalProjection(minimum, maximum)) EditorUtility.SetDirty(module);
        }


    }
}
