using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseSceneQueries
    {
        internal static bool SelectedWallBounds(HousePlan plan, HouseEditSelection selection, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var output = plan.GetComponent<HouseWallBuilder>()?.Output;
            foreach (var wall in plan.Data.Walls.Where(w => selection.Contains(HouseEditSelection.Kind.Wall, w.Id)))
            {
                bool rendered = false;
                var group = output?.Groups.Find(g => g.Key == "wall:" + wall.Id);
                if (group?.Root != null && group.Root.activeInHierarchy)
                    foreach (var renderer in group.Root.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (!renderer.enabled || renderer.forceRenderingOff) continue;
                        if (!found) bounds = renderer.bounds;
                        else bounds.Encapsulate(renderer.bounds);
                        found = rendered = true;
                    }
                if (rendered) continue;
                // Wall-less boundaries and hidden output still have editable logical walls.
                var start = NodeWorld(plan, plan.Data.GetPosition(wall.StartId));
                var end = NodeWorld(plan, plan.Data.GetPosition(wall.EndId));
                if (!found) bounds = new Bounds(start, Vector3.zero);
                bounds.Encapsulate(start); bounds.Encapsulate(end);
                var up = plan.transform.up * WallHeight(plan,wall).Max;
                bounds.Encapsulate(start+up); bounds.Encapsulate(end+up);
                found = true;
            }
            return found;
        }

        internal static HouseSceneHit? PickControl(HousePlan plan, Vector2 mouse, HouseEditSelection selection)
            => PickNode(plan, mouse, selection) ?? PickDividerControl(plan, mouse, selection);

        internal static HouseSceneHit? PickDividerControl(HousePlan plan, Vector2 mouse, HouseEditSelection selection)
        {
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            if (level == null) return null;
            HouseSceneHit? result = null;
            float nearest = 12;
            foreach (var boundary in new[] { building.Below(level), building.Above(level) }.Where(b => b != null))
            {
                if (!boundary.TryValidateSurfaceData(out _)) continue;
                bool ceiling = boundary.LowerId == level.Id;
                var handles = selection.HandleDividerNodes(plan, boundary);
                foreach (var node in boundary.DividerNodes.Where(n => handles.Contains(n.Id)))
                {
                    if (!ceiling && !DividerHandleOnFloor(plan, boundary, node.Position)) continue;
                    var world = SlabWorld(plan, ceiling, node.Position);
                    if (UnityEditor.SceneView.currentDrawingSceneView.camera.WorldToViewportPoint(world).z <= 0) continue;
                    float pixels = Vector2.Distance(mouse, UnityEditor.HandleUtility.WorldToGUIPoint(world));
                    if (pixels >= nearest) continue;
                    nearest = pixels;
                    result = new HouseSceneHit { Plan = plan, Kind = HouseEditSelection.Kind.DividerNode,
                        Id = node.Id, BoundaryId = boundary.Id, Height = SlabHeight(plan, ceiling) };
                }
            }
            if (result.HasValue) return result;
            return PickFloorBand(plan, mouse);
        }

        internal static float WallTop(HousePlan plan, FloorPlan.Wall wall, float along)
        {
            if (wall.IsFence) return wall.Fence.Panel != null ? wall.Fence.Panel.Height : 0;
            var native = HouseWallLayout.DefinitionFor(plan.Data, wall, plan.GetComponent<HouseWallBuilder>()?.Data);
            if (native != null && native.KeepNativeHeight) return native.Height;
            var start = plan.Data.GetPosition(wall.StartId);
            var end = plan.Data.GetPosition(wall.EndId);
            if (HouseBuilding.For(plan) == null)
            {
                var builder = plan.GetComponent<HouseWallBuilder>();
                var definition = builder == null ? null : HouseWallLayout.DefinitionFor(plan.Data, wall, builder.Data);
                if (definition != null) return definition.Height;
            }
            float fraction = Mathf.Clamp01(along / Vector2.Distance(start, end));
            return HeightSections(plan, start, end).First(s => fraction >= s.From && fraction <= s.To).CoverageTop;
        }

        internal static HouseSectionData.Range WallHeight(HousePlan plan, FloorPlan.Wall wall)
        {
            var native = HouseWallLayout.DefinitionFor(plan.Data, wall, plan.GetComponent<HouseWallBuilder>()?.Data);
            if (native != null && native.KeepNativeHeight) return new HouseSectionData.Range(native.Height, native.Height);
            if (wall.IsFence)
            {
                float height = wall.Fence.Panel != null ? wall.Fence.Panel.Height : 0;
                return new HouseSectionData.Range(height, height);
            }
            if (HouseBuilding.For(plan) == null)
            {
                var builder = plan.GetComponent<HouseWallBuilder>();
                var definition = builder == null ? null : HouseWallLayout.DefinitionFor(plan.Data, wall, builder.Data);
                if (definition != null) return new HouseSectionData.Range(definition.Height, definition.Height);
            }
            // Facade coverage may continue over a slab edge, beyond the clear storey height.
            var sections = HeightSections(plan, plan.Data.GetPosition(wall.StartId), plan.Data.GetPosition(wall.EndId));
            return new HouseSectionData.Range(sections.Min(s => s.CoverageTop), sections.Max(s => s.CoverageTop));
        }

        private static HouseSceneHit WallTopTarget(HousePlan plan, HouseSceneHit hit, Ray ray)
        {
            if (hit.Kind != HouseEditSelection.Kind.Opening) return hit;
            var wall = plan.Data.Walls.First(w => w.Openings.Any(o => o.Id == hit.Id));
            float top = WallTop(plan, wall, hit.Along);
            float y = plan.transform.InverseTransformPoint(ray.GetPoint(hit.Distance)).y;
            // The top 10 cm targets the whole wall. Short walls get a smaller band.
            // Classify only a visible surface/aperture hit: there is no invisible X-ray control.
            float band = Mathf.Min(.1f, Mathf.Max(0, top - hit.Height) * .1f);
            if (y < top - band || y > top + .002f) return hit;
            hit.Kind = HouseEditSelection.Kind.Wall;
            hit.Id = wall.Id;
            return hit;
        }

        private static HouseSceneHit? PickRenderedWall(HousePlan plan, Ray ray)
        {
            var builder = plan.GetComponent<HouseWallBuilder>();
            var output = WallOutput(plan);
            if (builder == null || !builder.Data.ShowWalls || output == null || !output.gameObject.activeInHierarchy
                || !Visibility(plan).TryHit(ray, out var surface, out float distance)) return null;
            var group = output.Groups.FirstOrDefault(g => g.Root != null && surface.IsChildOf(g.Root.transform));
            var wall = group == null ? null : plan.Data.Walls.FirstOrDefault(w => group.Key == "wall:" + w.Id);
            if (wall == null) return null;
            var local = plan.transform.InverseTransformPoint(ray.GetPoint(distance));
            float along = Vector2.Dot(new Vector2(local.x, local.z) - plan.Data.GetPosition(wall.StartId), Direction(plan, wall));
            var module = surface.GetComponentInParent<WallModule>();
            var opening = OpeningForModule(plan, wall, module);
            return new HouseSceneHit { Plan = plan, Kind = opening == null ? HouseEditSelection.Kind.Wall : HouseEditSelection.Kind.Opening,
                Id = opening == null ? wall.Id : opening.Id, Along = along,
                Height = 0, Distance = distance };
        }

        private static FloorPlan.Opening OpeningForModule(HousePlan plan, FloorPlan.Wall wall, WallModule module)
        {
            if (module == null) return null;
            // Picking and highlighting resolve the same saved module. Its fitted mesh may
            // differ from the kit asset after trimming or painting; never reconstruct it.
            var center = plan.transform.InverseTransformPoint(module.transform.TransformPoint(new Vector3(module.Length / 2, 0, 0)));
            float along = Vector2.Dot(new Vector2(center.x, center.z) - plan.Data.GetPosition(wall.StartId), Direction(plan, wall));
            var builder = plan.GetComponent<HouseWallBuilder>();
            return wall.Openings.FirstOrDefault(o => Mathf.Abs(o.Distance - along) < .001f
                && module.Definition == OpeningPlacement.ResolveModule(plan, wall, builder, o.Kind, o.Width));
        }

        internal static IEnumerable<MeshFilter> OpeningMeshes(HousePlan plan, FloorPlan.Wall wall, FloorPlan.Opening opening)
        {
            var group = WallOutput(plan)?.Groups.FirstOrDefault(g => g.Key == "wall:" + wall.Id);
            if (group?.Root == null || !group.Root.activeInHierarchy) yield break;
            foreach (var module in group.Root.GetComponentsInChildren<WallModule>())
                if (OpeningForModule(plan, wall, module)?.Id == opening.Id)
                    foreach (var filter in module.GetComponentsInChildren<MeshFilter>())
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        if (filter.sharedMesh != null && renderer != null && renderer.enabled) yield return filter;
                    }
        }
    }
}
