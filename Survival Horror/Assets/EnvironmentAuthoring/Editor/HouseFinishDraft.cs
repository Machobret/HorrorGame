using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Finish-only candidates copy one owner's data, without neighbour geometry or native objects.
    internal sealed class HouseFinishDraft : IDisposable
    {
        private readonly Object source;
        private readonly string original;
        private readonly FloorPlan plan;
        private readonly HouseStack building;
        private bool disposed;
        internal bool HasConflict => disposed || source == null || JsonUtility.ToJson(source) != original;
        internal HouseFinishDraft(HousePlan source)
        {
            this.source = source;
            original = JsonUtility.ToJson(source);
            plan = source.Data.Copy();
        }
        internal HouseFinishDraft(HouseBuilding source)
        {
            this.source = source;
            original = JsonUtility.ToJson(source);
            building = source.Data.Copy();
        }
        internal HouseEditResult Commit(string action) => HouseEditCommit.PublishFinish(this, action);
        internal Object Source => source;
        internal string CandidateJson => "{\"data\":" + JsonUtility.ToJson(plan != null ? (object)plan : building) + "}";
        internal bool SetRoomMaterials(System.Collections.Generic.IEnumerable<string> ids, Material material)
            => plan.TrySetRoomMaterials(ids, material);
        internal bool SetRoomSurfaceMaterials(System.Collections.Generic.IEnumerable<string> ids, bool ceiling, Material material)
            => plan.TrySetRoomSurfaceMaterials(ids, ceiling, material);
        internal bool SetWallMaterials(System.Collections.Generic.IEnumerable<string> ids, bool left, Material material)
            => plan.TrySetWallMaterials(ids, left, material);
        internal bool SetWallRoleMaterials(System.Collections.Generic.IEnumerable<string> ids, string role, Material material)
            => plan.TrySetWallRoleMaterials(ids, role, material);
        internal bool ApplyRoomFinishToWalls(System.Collections.Generic.IEnumerable<string> roomIds)
        {
            var ids = roomIds?.Distinct().ToArray();
            if (ids == null || ids.Length == 0 || ids.Any(id => !plan.Cells.Any(room => room.Id == id))) return false;
            foreach (var wall in plan.Walls)
                foreach (bool left in new[] { true, false })
                {
                    var room = plan.RoomOnSide(wall, left);
                    if (room != null && ids.Contains(room.Id)) plan.TrySetWallMaterials(new[] { wall.Id }, left, room.WallMaterial);
                }
            return true;
        }
        internal bool SetSurfaceFinish(string boundaryId, string regionId, bool ceiling, bool useOverride, Material material)
            => SetSurfaceFinishes(boundaryId, new[] { regionId }, ceiling, useOverride, material);

        internal bool SetSurfaceFinishes(string boundaryId, System.Collections.Generic.IEnumerable<string> regionIds,
            bool ceiling, bool useOverride, Material material)
        {
            var boundary = building?.Boundaries.FirstOrDefault(item => item.Id == boundaryId);
            var ids = regionIds?.Distinct().ToArray();
            if (boundary == null || ids == null || ids.Length == 0
                || ids.Any(id => !boundary.Regions.Any(segment => segment.Id == id && segment.Ceiling == ceiling))) return false;
            foreach (var segment in boundary.Regions.Where(segment => ids.Contains(segment.Id)))
                segment.SetFinish(useOverride, material);
            return true;
        }
        internal void SetBooleanCutMaterial(Material material) => building.SetBooleanCutMaterial(material);
        internal void SetCeilingSettings(Material material, Vector2 repeat) => building.ConfigureCeilingFinish(material, repeat);
        internal void SetDefaultMaterials(Material inside, Material outside, Material floor)
            => building.ConfigureDefaultMaterials(inside, outside, floor);
        internal void SetLevelWallFinishes(Material inside, Material outside)
        { plan.InsideMaterial = inside; plan.OutsideMaterial = outside; }
        internal void SetLevelCeiling(Material material) => plan.CeilingMaterial = material;
        internal void SetFloorSettings(bool show, Material material, Vector2 repeat, bool collision)
        {
            plan.ShowFloors = show; plan.FloorMaterial = material;
            plan.FloorTextureSize = repeat; plan.GenerateCollision = collision;
        }
        public void Dispose() { disposed = true; }
    }
}
