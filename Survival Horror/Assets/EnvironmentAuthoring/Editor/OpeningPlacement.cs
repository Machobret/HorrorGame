using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Read-only placement calculations are shared by the hover tool and its checks.
    public static class OpeningPlacement
    {
        public static WallModuleDefinition ResolveModule(HousePlan plan, FloorPlan.Wall wall, HouseWallBuilder builder,
            FloorPlan.OpeningKind kind, float width = 0, string shape = null)
            => builder == null || !wall.IsPhysical ? null : HouseWallLayout.OpeningFor(plan.Data, wall, builder.Data, kind, width, shape);

        public static bool CanPlace(HousePlan plan, int wallIndex, float distance, FloorPlan.OpeningKind kind,
            out WallModuleDefinition definition, out string reason, float width = 0, string shape = null)
        {
            definition = null;
            reason = "Choose a doorway or window.";
            if (kind != FloorPlan.OpeningKind.Doorway && kind != FloorPlan.OpeningKind.Window) return false;
            reason = "Hover over a wall.";
            if (wallIndex < 0 || wallIndex >= plan.Data.Walls.Count) return false;
            var builder = plan.GetComponent<HouseWallBuilder>();
            definition = ResolveModule(plan, plan.Data.Walls[wallIndex], builder, kind, width, shape);
            reason = "Keep the House upright at unit scale.";
            if (!plan.HasSupportedTransform) return false;
            reason = "Enable 3D walls and assign a " + (width == 0 ? "" : $"{width:0.####} m ")
                + kind.ToString().ToLowerInvariant() + " segment for this wall family.";
            if (definition == null) return false;
            if (HouseBuilding.For(plan) != null)
            {
                reason = "Keep enough level ceiling clearance for the opening.";
                if (HouseHeightQueries.Ceiling(plan) < definition.OpeningOutline.yMax + .01f) return false;
            }
            return HouseWallLayout.CanPlaceOpening(plan.Data, builder.Data, plan.Data.Walls[wallIndex].Id, distance,
                definition.Width, kind, null, out reason,
                layout: HouseSceneQueries.WallLayout(plan), shape: shape);
        }

        public static bool TryPlace(HousePlan plan, int wallIndex, float distance, FloorPlan.OpeningKind kind, out string reason, float width = 0)
        {
            var result = HouseEditCommands.PlaceOpening(plan, wallIndex, distance, kind, width);
            reason = result.Message;
            return result.Applied;
        }

        public static Matrix4x4 PreviewMatrix(HousePlan plan, int wallIndex, float distance, float width)
        {
            var wall = plan.Data.Walls[wallIndex];
            Vector2 start = plan.Data.GetPosition(wall.StartId);
            Vector2 along = (plan.Data.GetPosition(wall.EndId) - start).normalized;
            Vector2 logicalCenter = start + along * distance;
            Vector2 center = logicalCenter + HouseSceneQueries.WallLayout(plan).PlacementOffset(wall.Id);
            // Match the generator's facing, including the recessed outside of a window.
            if (plan.Data.TryGetExteriorOutward(wall, out var outward)
                && Vector2.Dot(new Vector2(-along.y, along.x), outward) < 0) along = -along;
            Vector2 origin = center - along * width / 2;
            return plan.transform.localToWorldMatrix * Matrix4x4.TRS(new Vector3(origin.x, 0, origin.y),
                Quaternion.LookRotation(new Vector3(-along.y, 0, along.x), Vector3.up), Vector3.one);
        }

        // Pick the visible wall faces without requiring physics colliders. Top-down views
        // use the existing floor-plan line picking instead because these planes are edge-on.
        public static bool RaycastWall(HousePlan plan, Ray ray, out int wallIndex, out float distance)
            => RaycastWall(plan, ray, out wallIndex, out distance, out _);

        internal static bool RaycastWall(HousePlan plan, Ray ray, out int wallIndex, out float distance, out float rayDistance)
        {
            wallIndex = -1; distance = 0;
            rayDistance = float.PositiveInfinity;
            var builder = plan.GetComponent<HouseWallBuilder>();
            var layout = HouseSceneQueries.RenderedWallLayout(plan);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < plan.Data.Walls.Count; i++)
            {
                var wall = plan.Data.Walls[i];
                if (!wall.IsPhysical) continue;
                var definition = builder == null ? null : HouseWallLayout.DefinitionFor(plan.Data, wall, builder.Data);
                float height = definition == null ? 2.8f : definition.Height;
                var depths = definition == null ? new Vector2(-.15f, .15f) : HouseSceneQueries.WallDepths(plan, wall, builder.Data);
                Vector2 logicalStart = plan.Data.GetPosition(wall.StartId);
                Vector2 start = logicalStart, delta = plan.Data.GetPosition(wall.EndId) - start;
                start += layout.PlacementOffset(wall.Id);
                if (plan.Data.TryGetExteriorOutward(wall, out var outward)
                    && Vector2.Dot(new Vector2(-delta.y, delta.x), outward) < 0)
                    depths = HouseWallLayout.ReverseDepths(depths);
                Vector3 normal = plan.transform.TransformDirection(new Vector3(-delta.y, 0, delta.x).normalized);
                var origin = plan.ToWorld(start, 0);
                foreach (float side in new[] { depths.x, depths.y })
                {
                    var face = new Plane(normal, origin + normal * side);
                    if (!face.Raycast(ray, out float hit) || hit >= nearest) continue;
                    Vector3 local = plan.transform.InverseTransformPoint(ray.GetPoint(hit));
                    float along = Vector2.Dot(new Vector2(local.x, local.z) - start, delta.normalized);
                    // Infinite face planes also hit beyond the wall. Reject those points
                    // before searching room footprints for floor and ceiling heights.
                    if (along < 0 || along > delta.magnitude) continue;
                    var point = logicalStart + delta.normalized * along;
                    if (local.y < 0 || local.y > HouseHeightQueries.Ceiling(plan)) continue;
                    nearest = rayDistance = hit; wallIndex = i; distance = along;
                }
            }
            return wallIndex >= 0;
        }
    }
}
