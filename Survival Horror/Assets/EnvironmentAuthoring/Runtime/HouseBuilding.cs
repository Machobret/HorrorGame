using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Native storage/Undo adapter. HouseStack owns level/boundary interpretation.
    [SelectionBase, DisallowMultipleComponent]
    public sealed class HouseBuilding : MonoBehaviour
    {
        [SerializeField] private HouseStack data = new HouseStack();
        public HouseStack Data => data;
        public float FloorThickness => data.FloorThickness;
        public float CeilingThickness => data.CeilingThickness;
        public float SlabThickness => data.SlabThickness;
        public float SurfaceOffset => data.SurfaceOffset;
        public Material CeilingMaterial => data.CeilingMaterial;
        public Vector2 CeilingTextureSize => data.CeilingTextureSize;
        public IReadOnlyList<HouseStack.Level> Levels => data.Levels;
        public IReadOnlyList<HouseStack.Boundary> Boundaries => data.Boundaries;
        public float FloorElevation(HouseStack.Level level)
            => data.FloorElevation(level);
        public HouseStack.Level Find(HousePlan plan)
            => data.Find(plan);
        public HouseStack.Level Find(string id)
            => data.Find(id);
        public HouseStack.Boundary Above(HouseStack.Level level)
            => data.Above(level);
        public HouseStack.Boundary Below(HouseStack.Level level)
            => data.Below(level);
        public float WallHeight(HouseStack.Level level)
            => data.WallHeight(level);
        public bool TrySetSlabThickness(float total, float finish, out string reason)
            => data.TrySetSlabThickness(total, finish, out reason);
        public bool TrySetLevelHeight(HouseStack.Level level, float height, out string reason)
            => data.TrySetLevelHeight(level, height, out reason);
        public bool TrySetLevelElevation(HouseStack.Level level, float elevation, bool moveOuterLevels, out string reason)
            => data.TrySetLevelElevation(level, elevation, moveOuterLevels, out reason);
        public bool TryRemoveOpening(HouseStack.Boundary boundary, string id)
            => data.TryRemoveOpening(boundary, id);
        public bool TrySetOpening(HouseStack.Boundary boundary, string id, IReadOnlyList<Vector2> points, out string reason)
            => data.TrySetOpening(boundary, id, points, out reason);
        internal void RegisterLevel(HouseStack.Level level, bool below = false)
            => data.RegisterLevel(level, below);
        internal void RegisterBoundary(HouseStack.Boundary boundary)
            => data.RegisterBoundary(boundary);
        internal void ConfigureCeilingFinish(Material material, Vector2 repeat)
            => data.ConfigureCeilingFinish(material, repeat);
        internal bool TryValidateLayout(out string reason)
        {
            if (!data.TryValidateLayout(out reason)) return false;
            reason = "Each level needs a distinct existing native plan.";
            if (Levels.Any(level => level.Plan == null) || Levels.Select(level => level.Plan).Distinct().Count() != Levels.Count) return false;
            reason = "Each registered level must be a direct child of its House in the same scene.";
            if (Levels.Any(level => level.Plan.transform.parent != transform
                || level.Plan.gameObject.scene != gameObject.scene)) return false;
            reason = "";
            return true;
        }

        public static HouseBuilding For(HousePlan plan)
        {
            var building = plan != null ? plan.GetComponentInParent<HouseBuilding>() : null;
            return building != null && building.Find(plan) != null ? building : null;
        }
    }
}
