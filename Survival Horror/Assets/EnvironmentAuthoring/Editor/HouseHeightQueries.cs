using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Read-only spatial queries. Native world placement receives their height explicitly.
    internal static class HouseHeightQueries
    {
        // Keep generated wall caps beneath the walking surface, including under
        // balconies. This clearance changes coverage, never opening/headroom anchors.
        private const float WallTopClearance = .005f;
        internal static float WallTop(HouseStack building, HouseStack.Level level, Vector2 point,
            Clipper2Lib.PathsD coverage, float ceiling)
        {
            // Open-sky walls have no slab edge to cover.
            if (!SlabGeometry.Contains(coverage, point)) return ceiling;
            var above = building.Above(level);
            float top = HouseVerticalLayout.BoundaryTop(building, level, building.Find(above?.UpperId));
            return top - HouseVerticalLayout.FloorSurface(building, level) - WallTopClearance;
        }

        internal static float Ceiling(HousePlan plan)
        {
            var building = HouseBuilding.For(plan);
            return building != null ? building.WallHeight(building.Find(plan))
                : plan.GetComponent<HouseWallBuilder>()?.Data.DefaultHeight ?? 2.8f;
        }

    }
}
