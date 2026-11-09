using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    public static class HouseSlabBuilder
    {
        private const string DuplicateOwnerProblem = "Multiple generated slab roots claim this owner. Resolve the duplicate output before rebuilding.";

        internal static void RemoveOrphanedOutput(HouseBuilding building)
        {
            foreach (var output in building.GetComponentsInChildren<HouseSlabOutput>(true))
                if (output != null && output.Owner == null && !EditorUtility.IsPersistent(output)
                    && output.gameObject.scene.IsValid() && output.gameObject.scene.isLoaded
                    && !EditorSceneManager.IsPreviewScene(output.gameObject.scene)
                    && !PrefabUtility.IsPartOfNonAssetPrefabInstance(output))
                    RemoveOwnedObject(output.gameObject);
        }

        public static HouseSlabOutput Output(HouseBuilding building) => Output(building, out _);

        internal static HouseSlabOutput Output(HouseBuilding building, out string problem)
        {
            problem = "";
            if (building == null) return null;
            var owned = OwnedOutputs(building);
            if (owned.Length > 1) { problem = DuplicateOwnerProblem; return null; }
            var output = owned.FirstOrDefault();
            if (output != null) problem = output.Problem;
            return output;
        }

        private static HouseSlabOutput[] OwnedOutputs(HouseBuilding building)
            => Resources.FindObjectsOfTypeAll<HouseSlabOutput>()
                .Where(output => output.Owner == building && output.gameObject.scene == building.gameObject.scene).ToArray();

        public static void Refresh(HouseBuilding building)
        {
            HouseRebuildCoordinator.RefreshSlabsNow(building);
        }

        internal static bool IsCurrent(HouseBuilding building, HouseGenerationData data)
        {
            var owned = OwnedOutputs(building);
            return owned.Length == 1 && Current(owned[0], Hash128.Compute("slabs-v5" + data.SlabInputs).ToString(), building.TryValidateLayout(out _));
        }

        private static bool Current(HouseSlabOutput output, string signature, bool validBindings) => output.Signature == signature
            && output.Panels.All(panel => PanelIsHealthy(panel, output) && (!validBindings || PanelHasCurrentBinding(panel, output)));

        internal static void Build(HouseBuilding building, HouseGenerationData data = null)
        {
            if (building == null || Application.isPlaying || EditorUtility.IsPersistent(building))
                return;
            if (building.Levels.Any(l => l?.Plan != null && HouseRebuildCoordinator.NeedsSceneInstance(l.Plan)))
                return;
            HouseRebuildCoordinator.PrepareBuilding(building);
            data ??= HouseGenerationData.Create(building);
            var owned = OwnedOutputs(building);
            if (owned.Length > 1)
            {
                foreach (var duplicate in owned)
                {
                    if (PrefabUtility.IsPartOfNonAssetPrefabInstance(duplicate)) continue;
                    if (duplicate.Problem == DuplicateOwnerProblem && !duplicate.gameObject.activeSelf && duplicate.Signature == "") continue;
                    duplicate.Problem = DuplicateOwnerProblem;
                    duplicate.Signature = "";
                    duplicate.gameObject.SetActive(false);
                    EditorUtility.SetDirty(duplicate);
                    EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                    data.HasOutputChanges = true;
                }
                return;
            }
            var output = owned.FirstOrDefault();
            bool validBindings = building.TryValidateLayout(out _);
            if (output != null && validBindings) PlaceOutput(output.transform, building);
            // Panel signatures retain healthy geometry and paint when only finish or layer changes.
            string signature = Hash128.Compute("slabs-v5" + data.SlabInputs).ToString();
            if (output != null && Current(output, signature, validBindings))
                return;
            if (output == null)
            {
                var root = new GameObject("Generated floor and ceiling panels");
                if (validBindings) PlaceOutput(root.transform, building);
                else root.transform.SetParent(building.transform, false);
                output = root.AddComponent<HouseSlabOutput>();
                output.Owner = building;
            }

            output.gameObject.SetActive(true);
            var priorChildren = output.transform.Cast<Transform>().Select(t => t.gameObject).ToArray();
            try
            {
                output.Problem = "";
                string problem = data.SlabProblem;
                if (problem.Length == 0 && building.Levels.Any(level => level?.Plan != null && !level.Plan.HasSupportedTransform))
                    problem = "Floor and ceiling panels require an upright House at world scale (1, 1, 1).";
                if (problem.Length == 0 && (!float.IsFinite(building.FloorThickness) || !float.IsFinite(building.CeilingThickness)
                    || building.FloorThickness < .01f || building.CeilingThickness < .01f))
                    problem = "Floor and ceiling thickness must be at least 0.01 m.";
                if (problem.Length > 0)
                {
                    // Invalid metadata is a diagnostic, never permission to reconstruct
                    // saved records or destroy retained painted geometry.
                    foreach (var panel in output.Panels)
                        if (panel.Root != null) panel.Root.SetActive(false);
                    output.Problem = problem;
                    output.Signature = signature; // A complete diagnostic result can be cached.
                    data.HasOutputChanges = true;
                    EditorUtility.SetDirty(output);
                    return;
                }

                var next = new List<HouseSlabOutput.Panel>();
                var patches = data.Patches;
                bool completed = true;
                foreach (var boundary in building.Boundaries)
                    foreach (var opening in boundary.Openings)
                    {
                        var hole = SlabGeometry.Shape(opening.Points);
                        if (!patches.Any(p => p.Boundary == boundary && SlabGeometry.Intersect(p.Shape, hole).Count > 0))
                            Warn("Some openings miss every room panel. Their contours are retained.");
                        foreach (var level in new[]{building.Find(boundary.LowerId), building.Find(boundary.UpperId)})
                            if (level?.Plan != null && level.Plan.Data.Walls.Any(w => w.IsPhysical && CrossesOpening(level.Plan.Data.GetPosition(w.StartId), level.Plan.Data.GetPosition(w.EndId), opening.Points)))
                                Warn("A wall crosses a slab opening. The cut removes panels only; wall and prop collision remain.");
                    }

                // A floor belongs to the room above, a ceiling to the room below.
                // Opposite-side finish seams must not split either native mesh.
                // Keep the extra opposite binding only for a supporting underside,
                // whose independent finish is rendered by the same floor panel.
                foreach (bool ceiling in new[] { false, true })
                    foreach (var surface in patches.Where(patch => ceiling ? patch.LowerRoom != null : patch.UpperRoom != null)
                        .GroupBy(patch => PanelKey(patch, ceiling)))
                    {
                        string key = surface.Key;
                        var shape = SlabGeometry.Union(new PathsD(surface.Where(patch => !patch.Floor.CutAway)
                            .SelectMany(patch => data.VisibleShape(patch))));
                        if (shape.Count == 0)
                        {
                            // Also retain pre-coalescing output when the first rebuild
                            // sees an omitted face; no painted binding is discarded.
                            if (surface.All(patch => patch.Floor.CutAway))
                                foreach (var retained in output.Panels.Where(panel => panel.Key == key
                                    || surface.Any(patch => panel.Key == PairedKey(patch, ceiling))))
                                    RetainHidden(retained.Key);
                            continue;
                        }
                        Panel(surface.First(), shape, ceiling, key, surface);
                    }

                foreach (var old in output.Panels)
                    if (!next.Contains(old) && old.Root != null)
                        RemoveOwnedObject(old.Root);
                foreach (var panel in next)
                {
                    panel.MeshPartCount = panel.Root.GetComponentsInChildren<MeshFilter>(true).Length;
                    panel.ModuleCount = panel.Root.GetComponentsInChildren<WallModule>(true).Length;
                }
                output.Panels = next;
                output.gameObject.layer = building.gameObject.layer;
                output.gameObject.hideFlags |= HideFlags.NotEditable;
                GeneratedMeshResources.RecordOwnedHierarchy(output.gameObject, HouseOutputUndo.AddComponent<GeneratedMeshOwnership>);
                if (completed) output.Signature = signature;
                data.HasOutputChanges = true;
                EditorUtility.SetDirty(output);
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                void Warn(string warning)
                {
                    if (!output.Problem.Contains(warning))
                        output.Problem += (output.Problem.Length > 0 ? "\n" : "") + warning;
                }

                // Failed interpretation is not authored removal. Retain one current
                // binding so identical recovery restores paint, just as trim output does.
                void RetainHidden(string key)
                {
                    var retained = output.Panels.Find(p => p.Key == key && p.Root != null);
                    if (retained == null) return;
                    retained.Root.SetActive(false);
                    next.Add(retained);
                }

                void Panel(HouseSlabLayout.Patch patch, PathsD shape, bool ceiling, string key, IEnumerable<HouseSlabLayout.Patch> pieces)
                {
                    var plan = ceiling ? patch.Lower.Plan : patch.Upper.Plan;
                    var size = ceiling ? building.CeilingTextureSize : plan.Data.FloorTextureSize;
                    if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= 0 || size.y <= 0)
                    {
                        size = Vector2.one;
                        output.Problem = "Invalid texture size: using 1 m repeats; collision remains available.";
                    }

                    bool roof = ceiling && patch.UpperRoom == null;
                    bool support = !ceiling && patch.LowerRoom == null;
                    var slab = HouseVerticalLayout.SlabAt(building, patch.WalkingHeight);
                    float top = ceiling && !roof ? slab.FinishBottom : slab.Top;
                    float bottom = ceiling ? slab.Bottom : slab.FinishBottom;
                    // Straight boundary nodes do not change the surface or invalidate its paint.
                    string meshSignature = Hash128.Compute(SurfacePartition.ShapeKey(shape) + "|" + top.ToString("R") + "|" + bottom.ToString("R") + "|" + size.x.ToString("R") + "|" + size.y.ToString("R")
                        + "|" + support + "|" + (support ? building.CeilingThickness.ToString("R") : "")).ToString();
                    var panel = output.Panels.Find(p => p.Key == key)
                        ?? output.Panels.Find(p => p.Plan == plan && p.Ceiling == ceiling && p.Signature == meshSignature
                            && pieces.Any(piece => p.Key == PairedKey(piece, ceiling)));
                    bool healthy = PanelIsHealthy(panel, output);
                    if (!healthy || panel.Signature != meshSignature)
                    {
                        var mesh = GeneratedMeshResources.Register(SlabGeometry.Extrude(shape, bottom, top, size));
                        var supportMesh = support ? GeneratedMeshResources.Register(SlabGeometry.Extrude(shape, bottom - building.CeilingThickness, bottom, size)) : null;
                        if (mesh == null || support && supportMesh == null)
                        {
                            completed = false;
                            Warn("A panel could not be triangulated. Its authored contour is retained.");
                            RetainHidden(key);
                            return;
                        }
                        // Only planar shape edits keep this native binding. A different
                        // owner, vertical band, Boolean state or component layout uses
                        // the existing replacement path, including level creation/deletion.
                        var supportPart = panel?.Root != null ? panel.Root.transform.Find("Supporting slab") : null;
                        bool reuse = healthy && panel.Plan == plan && panel.Ceiling == ceiling
                            && panel.ModuleCount == 0
                            && SameHeightBand(panel.Root.GetComponent<MeshFilter>(), bottom, top)
                            && panel.Root.transform.localPosition == Vector3.zero
                            && panel.Root.transform.localRotation == Quaternion.identity
                            && panel.Root.transform.localScale == Vector3.one
                            && panel.MeshPartCount == (support ? 2 : 1)
                            && panel.Root.GetComponentsInChildren<HouseBooleanMeshState>(true).Length == 0
                            && (!support || supportPart != null
                                && SameHeightBand(supportPart.GetComponent<MeshFilter>(), bottom - building.CeilingThickness, bottom));
                        var root = reuse ? panel.Root : new GameObject(ceiling ? "Ceiling panel" : "Floor panel");
                        if (!reuse) root.transform.SetParent(output.transform, false);
                        (reuse ? root.GetComponent<MeshFilter>() : root.AddComponent<MeshFilter>()).sharedMesh = mesh;
                        if (support)
                        {
                            // A ground floor still needs its supporting volume.
                            var supportRoot = reuse ? supportPart.gameObject : new GameObject("Supporting slab");
                            if (!reuse) supportRoot.transform.SetParent(root.transform, false);
                            (reuse ? supportRoot.GetComponent<MeshFilter>() : supportRoot.AddComponent<MeshFilter>()).sharedMesh = supportMesh;
                            if (!reuse) supportRoot.AddComponent<MeshRenderer>();
                            (reuse ? supportRoot.GetComponent<MeshCollider>() : supportRoot.AddComponent<MeshCollider>()).sharedMesh = supportMesh;
                        }
                        if (!reuse) root.AddComponent<MeshRenderer>();
                        (reuse ? root.GetComponent<MeshCollider>() : root.AddComponent<MeshCollider>()).sharedMesh = mesh;
                        // Keep the retained binding in next as well as its root;
                        // retirement below compares these source-face records.
                        if (!reuse) panel = new HouseSlabOutput.Panel { Key = key, Root = root };
                        panel.Signature = meshSignature;
                        panel.Plan = plan;
                        panel.Ceiling = ceiling;
                    }

                    // One boundary has two independently owned finishes: the lower
                    // room owns its ceiling, and the upper room owns its floor.
                    var material = HouseFinishResolution.Surface(building, patch, ceiling);
                    // An explicit cleared material remains an override; Unity's default
                    // is used rather than silently inheriting the room finish again.
                    panel.Key = key;
                    panel.Plan = plan;
                    panel.Root.SetActive(true);
                    panel.Root.GetComponent<MeshRenderer>().sharedMaterial = HouseFinishResolution.RenderMaterial(material);
                    panel.Root.GetComponent<MeshRenderer>().enabled = ceiling || plan.Data.ShowFloors;
                    panel.Root.GetComponent<MeshCollider>().enabled = plan.Data.GenerateCollision;
                    foreach (Transform child in panel.Root.transform)
                    {
                        var supportRenderer = child.GetComponent<MeshRenderer>();
                        var supportCollider = child.GetComponent<MeshCollider>();
                        if (supportRenderer != null)
                        {
                            var supportMaterial = HouseFinishResolution.Surface(building, patch, true);
                            supportRenderer.sharedMaterial = HouseFinishResolution.RenderMaterial(supportMaterial);
                            supportRenderer.enabled = plan.Data.ShowFloors;
                        }
                        if (supportCollider != null) supportCollider.enabled = plan.Data.GenerateCollision;
                    }
                    ApplyLayer(panel.Root, plan.gameObject.layer);
                    next.Add(panel);
                }
            }
            catch
            {
                // Failed derived output is retried from source; discard only newly staged children.
                foreach (var child in output.transform.Cast<Transform>().Select(t => t.gameObject).ToArray())
                    if (!priorChildren.Contains(child)) HouseOutputUndo.Remove(child);
                throw;
            }
        }

        internal static void PlaceOutput(Transform root, HouseBuilding building)
        {
            // These vertices belong to House space, not to the lifetime of one level.
            if (root.parent == building.transform && root.localPosition == Vector3.zero
                && root.localRotation == Quaternion.identity && root.localScale == Vector3.one) return;
            root.SetParent(building.transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private static string PairedKey(HouseSlabLayout.Patch patch, bool ceiling)
            => patch.Boundary.Id + ":" + patch.Key + (ceiling ? ":ceiling" : ":floor");

        private static string PanelKey(HouseSlabLayout.Patch patch, bool ceiling)
        {
            // Roofs occupy the full slab band; ground/overhang support has an
            // independently finished underside. Keep those physical bands distinct.
            bool roof = ceiling && patch.UpperRoom == null;
            bool support = !ceiling && patch.LowerRoom == null;
            string band = roof ? ":roof" : support ? ":support" : "";
            // An unowned underside uses one level/House default. Its derived
            // spatial ID changes as contours move, but is not a render identity.
            if (support && patch.Ceiling.OverrideMaterial) band += ":" + patch.Ceiling.Id;
            return patch.Boundary.Id + ":" + patch.Region(ceiling).Id + band + (ceiling ? ":ceiling" : ":floor");
        }

        private static bool SameHeightBand(MeshFilter part, float bottom, float top)
        {
            if (part == null || part.sharedMesh == null) return false;
            var bounds = part.sharedMesh.bounds;
            return Mathf.Abs(bounds.min.y - bottom) < .000001f && Mathf.Abs(bounds.max.y - top) < .000001f;
        }

        private static bool CrossesOpening(Vector2 a, Vector2 b, IReadOnlyList<Vector2> points)
        {
            if (OutlineGeometry.Contains(points, a) || OutlineGeometry.Contains(points, b))
                return true;
            var direction = b - a;
            for (int i = 0; i < points.Count; i++)
            {
                var start = points[i];
                var edge = points[(i + 1) % points.Count] - start;
                float denominator = direction.x * edge.y - direction.y * edge.x;
                if (Mathf.Abs(denominator) < 1e-7f)
                    continue;
                var delta = start - a;
                float along = (delta.x * edge.y - delta.y * edge.x) / denominator;
                float across = (delta.x * direction.y - delta.y * direction.x) / denominator;
                if (along >= 0 && along <= 1 && across >= 0 && across <= 1)
                    return true;
            }

            return false;
        }

        // A binding change requires reconciliation, not replacement of healthy painted meshes.
        private static bool PanelHasCurrentBinding(HouseSlabOutput.Panel panel, HouseSlabOutput output)
        {
            if (output.Owner == null) return false;
            var boundary = output.Owner.Boundaries.FirstOrDefault(item => item != null
                && panel.Key.StartsWith(item.Id + ":", StringComparison.Ordinal));
            if (boundary == null) return false;
            var lower = output.Owner.Find(boundary.LowerId);
            var upper = output.Owner.Find(boundary.UpperId);
            var expected = panel.Ceiling ? lower : upper;
            return expected?.Plan != null && expected.Plan == panel.Plan;
        }

        private static bool PanelIsHealthy(HouseSlabOutput.Panel panel, HouseSlabOutput output)
        {
            if (panel?.Root == null || panel.Root.transform.parent != output.transform) return false;
            var filters = panel.Root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != panel.MeshPartCount) return false;
            if (panel.Root.GetComponentsInChildren<WallModule>(true).Length != panel.ModuleCount) return false;
            if (filters.Length == 0 || filters.Any(f => f.sharedMesh == null || f.GetComponent<MeshRenderer>() == null)) return false;

            // Painting may clone only the render mesh while collision retains the original.
            // Both must exist; differing identities alone are not damaged output.
            return filters.All(filter => filter.GetComponent<MeshCollider>() != null
                && filter.GetComponent<MeshCollider>().sharedMesh != null);
        }

        private static void ApplyLayer(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
                child.gameObject.hideFlags |= HideFlags.NotEditable;
            }
        }

        internal static void RemoveOwnedObject(GameObject root) => HouseOutputUndo.Remove(root);
    }
}
