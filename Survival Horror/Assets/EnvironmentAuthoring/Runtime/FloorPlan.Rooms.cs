using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public bool CanCreateRoom(Vector2 first, Vector2 opposite, out List<Vector2> boundary, out string reason)
        {
            boundary = new List<Vector2> { first, new Vector2(opposite.x, first.y),
                opposite, new Vector2(first.x, opposite.y) };
            reason = "Create the first room in an empty House. To extend a plan, drag from an exterior wall.";
            if (corners.Count != 0 || walls.Count != 0 || cells.Count != 0)
                return false;
            return OutlineGeometry.Validate(boundary, true, out reason);
        }

        public bool TryCreateRoom(Vector2 first, Vector2 opposite, out string reason)
        {
            if (!CanCreateRoom(first, opposite, out var boundary, out reason))
                return false;
            foreach (var point in boundary)
                corners.Add(new Corner(point));
            for (int i = 0; i < 4; i++)
                walls.Add(new Wall(corners[i].Id, corners[(i + 1) % 4].Id));
            cells.Add(new Cell(Guid.NewGuid().ToString("N"), corners[0].Id, walls.ConvertAll(w => w.Id)));
            return true;
        }

        public bool TryGetRoomAttachment(int wallIndex, out Vector2 start, out Vector2 end,
            out Vector2 outward, out string reason)
        {
            start = end = outward = default;
            reason = "Drag from an exterior wall to attach a room.";
            if (wallIndex < 0 || wallIndex >= walls.Count)
                return false;
            var neighbours = cells.FindAll(c => IndexOfWall(c, walls[wallIndex].Id) >= 0);
            if (neighbours.Count != 1)
                return false;
            var boundary = GetCellBoundary(neighbours[0]);
            int edge = IndexOfWall(neighbours[0], walls[wallIndex].Id);
            start = boundary[edge];
            end = boundary[(edge + 1) % boundary.Count];
            Vector2 along = (end - start).normalized;
            // Follow this cell's boundary, not the wall's stored direction. The empty side
            // is right of a counterclockwise boundary and left of a clockwise one.
            outward = OutlineGeometry.SignedArea(boundary) > 0
                ? new Vector2(along.y, -along.x) : new Vector2(-along.y, along.x);
            reason = "";
            return true;
        }

        public bool CanAttachRoom(int wallIndex, float depth, out List<Vector2> boundary, out string reason)
        {
            boundary = new List<Vector2>();
            if (!TryGetRoomAttachment(wallIndex, out var a, out var b, out var outward, out reason))
                return false;
            reason = "Drag outward by more than 1 mm.";
            if (float.IsNaN(depth) || float.IsInfinity(depth) || depth <= OutlineGeometry.Tolerance)
                return false;
            // The new room walks the shared wall in the opposite direction to its neighbour.
            boundary.AddRange(new[] { b, a, a + outward * depth, b + outward * depth });
            if (!OutlineGeometry.Validate(boundary, true, out reason))
                return false;

            var wall = walls[wallIndex];
            var neighbour = cells.Find(c => IndexOfWall(c, wall.Id) >= 0);
            var ids = GetCellCornerIds(neighbour);
            int edge = IndexOfWall(neighbour, wall.Id);
            string aId = ids[edge];
            string bId = ids[(edge + 1) % ids.Count];
            var proposedCorners = new List<Corner>(corners);
            var c = new Corner(boundary[2]);
            var d = new Corner(boundary[3]);
            proposedCorners.Add(c);
            proposedCorners.Add(d);
            var positions = proposedCorners.ConvertAll(corner => corner.Position);
            if (!OutlineGeometry.ValidatePoints(positions, out reason))
                return false;
            var newEdges = new[] { aId, c.Id, d.Id, bId };
            for (int i = 0; i < 3; i++)
            {
                foreach (var existing in walls)
                {
                    if (ConflictsWithWall(newEdges[i], newEdges[i + 1], existing, positions, proposedCorners))
                    {
                        reason = "The new room cannot cross or touch other walls. Attach along one wall at a time.";
                        return false;
                    }
                }
            }
            // Intersection checks alone cannot find geometry completely enclosed by the new room.
            foreach (var corner in corners)
            {
                if (OutlineGeometry.ContainsPoint(boundary, corner.Position))
                {
                    reason = "The new room cannot overlap or enclose the existing plan.";
                    return false;
                }
            }
            reason = "";
            return true;
        }

        public bool TryAttachRoom(int wallIndex, float depth, out string reason)
        {
            if (!CanAttachRoom(wallIndex, depth, out var boundary, out reason))
                return false;
            var shared = walls[wallIndex];
            var neighbour = cells.Find(c => IndexOfWall(c, shared.Id) >= 0);
            var ids = GetCellCornerIds(neighbour);
            int edge = IndexOfWall(neighbour, shared.Id);
            string aId = ids[edge];
            string bId = ids[(edge + 1) % ids.Count];
            var c = new Corner(boundary[2]);
            var d = new Corner(boundary[3]);
            var first = new Wall(aId, c.Id);
            var second = new Wall(c.Id, d.Id);
            var third = new Wall(d.Id, bId);
            corners.Add(c);
            corners.Add(d);
            walls.AddRange(new[] { first, second, third });
            cells.Add(new Cell(Guid.NewGuid().ToString("N"), bId,
                new List<string> { shared.Id, first.Id, second.Id, third.Id }));
            return true;
        }

    }
}
