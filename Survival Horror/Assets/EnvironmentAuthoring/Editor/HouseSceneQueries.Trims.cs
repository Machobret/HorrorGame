using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseSceneQueries
    {
        // A small prospective band exists even before a trim asset is assigned.
        // Both picking and feedback read these same boxes in plan coordinates.
        internal static IEnumerable<Matrix4x4> TrimBands(HousePlan plan, FloorPlan.Wall wall)
        {
            if (!HouseTrimLayout.Eligible(plan.Data, wall)) yield break;
            var builder = plan.GetComponent<HouseWallBuilder>();
            if (builder == null || HouseWallLayout.DefinitionFor(plan.Data, wall, builder.Data) == null) yield break;
            plan.Data.TryGetExteriorOutward(wall, out var outward);
            var start = plan.Data.GetPosition(wall.StartId);
            var end = plan.Data.GetPosition(wall.EndId);
            var along = new Vector2(outward.y, -outward.x);
            bool reverse = Vector2.Dot(end - start, along) < 0;
            var origin = (reverse ? end : start) + RenderedWallLayout(plan).PlacementOffset(wall.Id);
            float length = Vector2.Distance(start, end);
            var settings = HouseTrimLayout.Resolve(plan, wall) ?? new WallTrim();
            float height = Mathf.Max(.15f, settings.Height);
            var depths = HouseTrimLayout.DepthRange(plan.Data, wall, builder.Data, Mathf.Max(.05f, settings.Depth));
            if (!float.IsFinite(depths.y) || depths.y <= depths.x) yield break;
            var point = origin + outward * depths.x;
            // The selectable attachment band stays on this level's uniform floor plane.
            float top = Mathf.Max(.15f, settings.Offset + height);
            float bottom = Mathf.Min(0, settings.Offset);
            yield return Matrix4x4.TRS(new Vector3(point.x, bottom, point.y),
                Quaternion.LookRotation(new Vector3(outward.x, 0, outward.y)),
                new Vector3(length, (top - bottom) / .2f, (depths.y - depths.x) / .15f));
        }

        internal static HouseSceneHit? PickTrim(HousePlan plan, Vector2 mouse)
            => PickTrim(plan, HandleUtility.GUIPointToWorldRay(mouse));

        // Delete targets only an actual visible attachment, never the prospective
        // band used by the authoring tool. Empty bands must still target the wall.
        internal static HouseSceneHit? PickAttachedTrim(HousePlan plan, Vector2 mouse)
        {
            if (!plan.Data.Walls.Any(w => HouseTrimLayout.Resolve(plan, w)?.Mesh != null)) return null;
            var output = WallOutput(plan);
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (output == null || !Visibility(plan).TryHit(ray, out var surface, out float distance)) return null;
            var group = output.Groups.FirstOrDefault(g => g.Root != null && surface.IsChildOf(g.Root.transform));
            var wall = plan.Data.Walls.FirstOrDefault(w => HouseTrimLayout.Resolve(plan, w)?.Mesh != null && group?.Key == "trim:" + w.Id);
            return wall == null ? (HouseSceneHit?)null : new HouseSceneHit
                { Plan = plan, Kind = HouseEditSelection.Kind.Trim, Id = wall.Id, Distance = distance };
        }

        internal static HouseSceneHit? PickTrim(HousePlan plan, Ray ray)
        {
            HouseSceneHit? best = null;
            var output = plan.GetComponent<HouseWallBuilder>()?.Output;
            foreach (var wall in plan.Data.Walls.Where(w => HouseTrimLayout.Eligible(plan.Data, w)))
                foreach (var band in TrimBands(plan, wall))
                {
                    var world = plan.transform.localToWorldMatrix * band;
                    var local = world.inverse;
                    var direction = local.MultiplyVector(ray.direction);
                    // Exterior attachment areas are never picked from the interior side.
                    if (direction.z >= -.00001f) continue;
                    var origin = local.MultiplyPoint3x4(ray.origin);
                    float distance = (.15f - origin.z) / direction.z;
                    var point = origin + direction * distance;
                    if (distance < 0 || best.HasValue && distance >= best.Value.Distance
                        || point.x < 0 || point.x > 1 || point.y < 0 || point.y > .2f) continue;
                    var ownWall = output?.Groups.FirstOrDefault(g => g.Key == "wall:" + wall.Id)?.Root;
                    if (!Visibility(plan).Visible(ray, distance, ownSurface: ownWall?.transform)) continue;
                    best = new HouseSceneHit { Plan = plan, Kind = HouseEditSelection.Kind.Trim, Id = wall.Id, Distance = distance };
                }
            return best;
        }
    }
}
