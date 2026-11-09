using UnityEngine;

namespace EnvironmentAuthoring
{
    // Building-space heights only. Polygon ownership stays in the room/slab geometry code.
    public static class HouseVerticalLayout
    {
        public readonly struct Slab
        {
            public readonly float Top, Bottom, FinishBottom;
            public Slab(float top, float total, float finish)
            {
                Top = top;
                Bottom = top - total;
                FinishBottom = top - finish;
            }
        }

        public static float FloorSurface(HouseStack building, HouseStack.Level level)
            => building.Elevation(level) + building.SurfaceOffset;

        public static float NominalCeiling(HouseStack building, HouseStack.Level level)
            => FloorSurface(building, level) + building.WallHeight(level);

        public static float BoundaryTop(HouseStack building, HouseStack.Level lower,
            HouseStack.Level upper)
            => upper != null ? FloorSurface(building, upper)
                : NominalCeiling(building, lower) + building.SlabThickness;

        public static Slab SlabAt(HouseStack building, float finishedFloor)
            => new Slab(finishedFloor, building.SlabThickness, building.FloorThickness);

        public static float Clearance(HouseStack building, HouseStack.Level lower, float boundaryTop)
            => SlabAt(building, boundaryTop).Bottom - FloorSurface(building, lower);

        public static Vector3 PlanPosition(HouseStack building, HouseStack.Level level)
            => Vector3.up * FloorSurface(building, level);

        // Native entry points resolve the saved data once. Candidates use the plain overloads.
        public static float FloorSurface(HouseBuilding building, HouseStack.Level level)
            => FloorSurface(building.Data, level);
        public static float NominalCeiling(HouseBuilding building, HouseStack.Level level)
            => NominalCeiling(building.Data, level);
        public static float BoundaryTop(HouseBuilding building, HouseStack.Level lower, HouseStack.Level upper)
            => BoundaryTop(building.Data, lower, upper);
        public static Slab SlabAt(HouseBuilding building, float finishedFloor) => SlabAt(building.Data, finishedFloor);
        public static float Clearance(HouseBuilding building, HouseStack.Level lower, float top)
            => Clearance(building.Data, lower, top);
        public static Vector3 PlanPosition(HouseBuilding building, HouseStack.Level level) => PlanPosition(building.Data, level);
    }
}
