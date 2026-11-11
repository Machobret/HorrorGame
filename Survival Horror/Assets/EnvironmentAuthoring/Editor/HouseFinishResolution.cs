using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // Material inheritance is shared by property display and output binding.
    // This owner reads authored settings only; it never generates or edits geometry.
    internal static class HouseFinishResolution
    {
        internal static WallFamily Family(HousePlan plan, FloorPlan.Wall wall)
            => plan.GetComponent<HouseWallBuilder>()?.Data.FamilyFor(plan.Data, wall);

        internal static Material Role(HousePlan plan, FloorPlan.Wall wall, int slot)
            => Family(plan, wall)?.RoleMaterial(wall, slot);

        internal static Material RenderMaterial(Material material)
            => material != null ? material : GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

        internal static Material RoomSurface(HouseBuilding building, HousePlan plan, FloorPlan.Cell room, bool ceiling)
        {
            var material = ceiling ? room?.CeilingMaterial : room?.FloorMaterial;
            // Unity cleared references can be fake-null; use native equality, not ??.
            if (material != null) return material;
            var levelDefault = ceiling ? plan?.Data.CeilingMaterial : plan?.Data.FloorMaterial;
            return levelDefault != null ? levelDefault : ceiling ? building?.CeilingMaterial : building?.Data.FloorMaterial;
        }

        internal static Material Surface(HouseBuilding building, HouseSlabLayout.Patch patch, bool ceiling)
        {
            var region = patch.Region(ceiling);
            if (region.OverrideMaterial) return region.Material;
            var level = ceiling ? patch.Lower : patch.Upper;
            string id = ceiling ? patch.LowerRoom : patch.UpperRoom;
            var room = level?.Plan?.Data.Cells.FirstOrDefault(c => c.Id == id);
            return RoomSurface(building, level?.Plan, room, ceiling);
        }

        internal static Material Trim(WallTrim trim, HousePlan plan)
            => trim.Material != null ? trim.Material : RoomWall(plan, null);

        internal static Material Wall(HousePlan plan, FloorPlan.Wall wall, bool left, bool roomOnly = false)
        {
            if (wall.IsFence && !roomOnly)
            {
                var material = left ? wall.LeftMaterial : wall.RightMaterial;
                var defaults = wall.Fence.Panel?.Materials;
                int slot = left ? 0 : 1;
                return material != null ? material : defaults != null && defaults.Length > 0
                    ? defaults[Mathf.Min(slot, defaults.Length - 1)] : null;
            }
            var (inside, outside) = Defaults(plan);
            return roomOnly ? FloorPlan.RoomWallMaterial(plan.Data.RoomOnSide(wall, left), inside, outside)
                : plan.Data.WallMaterial(wall, left, inside, outside);
        }

        internal static Material RoomWall(HousePlan plan, FloorPlan.Cell room)
        {
            var (inside, outside) = Defaults(plan);
            return FloorPlan.RoomWallMaterial(room, inside, outside);
        }

        private static (Material Inside, Material Outside) Defaults(HousePlan plan)
        {
            var level = plan.Data;
            var house = HouseBuilding.For(plan)?.Data;
            return (level?.InsideMaterial != null ? level.InsideMaterial : house?.InsideMaterial,
                level?.OutsideMaterial != null ? level.OutsideMaterial : house?.OutsideMaterial);
        }

    }
}
