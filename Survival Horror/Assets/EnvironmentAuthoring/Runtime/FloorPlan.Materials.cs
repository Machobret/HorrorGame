using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public bool TrySetRoomMaterial(string roomId, Material material)
            => TrySetRoomMaterials(new[] { roomId }, material);

        public bool TrySetRoomMaterials(IEnumerable<string> roomIds, Material material)
        {
            // Validate the complete selection before changing any room.
            var selected = roomIds.Distinct().Select(id => cells.Find(c => c.Id == id)).ToArray();
            if (selected.Length == 0 || selected.Any(room => room == null))
                return false;
            foreach (var room in selected)
                room.SetWallMaterial(material);
            return true;
        }

        public bool TrySetRoomSurfaceMaterials(IEnumerable<string> roomIds, bool ceiling, Material material)
        {
            var selected = roomIds.Distinct().Select(id => cells.Find(c => c.Id == id)).ToArray();
            if (selected.Length == 0 || selected.Any(room => room == null)) return false;
            foreach (var room in selected) room.SetSurfaceMaterial(ceiling, material);
            return true;
        }

        public bool TrySetWallMaterials(IEnumerable<string> wallIds, bool left, Material material)
        {
            var selected = wallIds.Distinct().Select(id => walls.Find(w => w.Id == id)).ToArray();
            if (selected.Length == 0 || selected.Any(w => w == null))
                return false;
            foreach (var wall in selected)
                wall.SetMaterials(left ? material : wall.LeftMaterial, left ? wall.RightMaterial : material);
            return true;
        }

        public bool TrySetWallRoleMaterials(IEnumerable<string> wallIds, string role, Material material)
        {
            var selected = wallIds?.Distinct().Select(id => walls.Find(wall => wall.Id == id)).ToArray();
            if (string.IsNullOrWhiteSpace(role) || selected == null || selected.Length == 0 || selected.Any(wall => wall == null))
                return false;
            foreach (var wall in selected) wall.SetRoleMaterial(role, material);
            return true;
        }

        // Left/right are relative to the saved start-to-end direction, never the camera.
        // Boundaries give exact ownership; only an incomplete wall needs a point-in-room check.
        public Cell RoomOnSide(Wall wall, bool left)
        {
            var along = (GetPosition(wall.EndId) - GetPosition(wall.StartId)).normalized;
            var side = new Vector2(-along.y, along.x) * (left ? 1 : -1);
            bool referenced = false;
            foreach (var room in cells)
            {
                int index = IndexOfWall(room, wall.Id);
                if (index < 0)
                    continue;
                referenced = true;
                var boundary = GetCellBoundary(room);
                var edge = boundary[(index + 1) % boundary.Count] - boundary[index];
                var inward = new Vector2(-edge.y, edge.x) * (OutlineGeometry.SignedArea(boundary) > 0 ? 1 : -1);
                if (Vector2.Dot(inward, side) > 0)
                    return room;
            }

            // A referenced boundary's other side is outside, not another copy of its owner.
            if (referenced)
                return null;
            var point = (GetPosition(wall.StartId) + GetPosition(wall.EndId)) / 2 + side * .001f;
            return cells.Find(c => OutlineGeometry.ContainsPoint(GetCellBoundary(c), point));
        }

        public Material WallMaterial(Wall wall, bool left, Material fallback, Material outside = null)
        {
            var explicitMaterial = left ? wall.LeftMaterial : wall.RightMaterial;
            if (explicitMaterial != null)
                return explicitMaterial;
            return RoomWallMaterial(RoomOnSide(wall, left), fallback, outside);
        }

        // Ownership differs for wall faces and step bands; inheritance does not.
        internal static Material RoomWallMaterial(Cell room, Material fallback, Material outside)
        {
            if (room?.WallMaterial != null) return room.WallMaterial;
            return room == null || room.IsOutdoor ? (outside != null ? outside : fallback) : fallback;
        }
    }
}
