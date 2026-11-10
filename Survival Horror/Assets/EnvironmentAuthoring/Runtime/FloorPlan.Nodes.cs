using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public bool CanAddCorner(Vector2 position, out string reason)
        {
            if (IsClosed)
            {
                reason = "This outline is already closed.";
                return false;
            }
            var positions = GetPositions();
            positions.Add(position);
            return OutlineGeometry.Validate(positions, false, out reason);
        }

        public bool TryAddCorner(Vector2 position, out string reason)
        {
            if (!CanAddCorner(position, out reason))
                return false;

            var corner = new Corner(position);
            if (corners.Count > 0)
                walls.Add(new Wall(corners[corners.Count - 1].Id, corner.Id));
            corners.Add(corner);
            return true;
        }

        public bool CanClose(out string reason)
        {
            if (IsClosed)
            {
                reason = "This outline is already closed.";
                return false;
            }
            return OutlineGeometry.Validate(GetPositions(), true, out reason);
        }

        public bool TryClose(out string reason)
        {
            if (!CanClose(out reason))
                return false;

            walls.Add(new Wall(corners[corners.Count - 1].Id, corners[0].Id));
            cells.Add(new Cell(Guid.NewGuid().ToString("N"), corners[0].Id, walls.ConvertAll(w => w.Id)));
            return true;
        }

        public bool CanInsertCorner(int wallIndex, float fraction, out string reason)
        {
            if (wallIndex < 0 || wallIndex >= walls.Count)
            {
                reason = "That wall does not exist.";
                return false;
            }
            // A fraction places the new corner on the wall: 0 is its start, 1 is its end.
            if (float.IsNaN(fraction) || fraction <= 0f || fraction >= 1f)
            {
                reason = "Choose a position between the wall's existing corners.";
                return false;
            }
            var wall = walls[wallIndex];
            Vector2 position = Vector2.Lerp(GetPosition(wall.StartId), GetPosition(wall.EndId), fraction);
            var positions = GetPositions();
            positions.Insert(CornerIndex(wall.StartId) + 1, position);
            if (!OutlineGeometry.ValidatePoints(positions, out reason))
                return false;
            // Splitting a valid segment leaves its shape unchanged, including open exterior
            // runs. Thickness overrides must not change whether its corner can be inserted.
            foreach (var cell in cells)
            {
                int edge = IndexOfWall(cell, wall.Id);
                if (edge < 0)
                    continue;
                var boundary = GetCellBoundary(cell);
                boundary.Insert(edge + 1, position);
                if (!OutlineGeometry.Validate(boundary, true, out reason))
                    return false;
            }
            return true;
        }

        public bool TryInsertCorner(int wallIndex, float fraction, out string reason)
        {
            if (!CanInsertCorner(wallIndex, fraction, out reason))
                return false;

            InsertCorner(wallIndex, fraction, corners, walls, cells);
            return true;
        }

        private static string InsertCorner(int wallIndex, float fraction,
            List<Corner> corners, List<Wall> walls, List<Cell> cells)
        {
            var wall = walls[wallIndex];
            var position = Vector2.Lerp(PositionOf(wall.StartId, corners), PositionOf(wall.EndId, corners), fraction);
            var corner = new Corner(position);
            var newWall = new Wall(Guid.NewGuid().ToString("N"), corner.Id, wall.EndId, wall.Type, wall.Presence);
            newWall.SetMaterials(wall.LeftMaterial, wall.RightMaterial);
            newWall.CopyMaterialRolesFrom(wall);
            newWall.SetFamily(wall.Family);

            float cut = Vector2.Distance(PositionOf(wall.StartId, corners), position);
            newWall.CopyTrimFrom(wall);
            newWall.Trim?.AdvancePattern(cut);
            // Keep the opening on the segment containing its centre. If the cut crosses
            // its surround, fitting reports it; the user's opening is never discarded.
            for (int i = wall.MutableOpenings.Count - 1; i >= 0; i--)
            {
                var door = wall.MutableOpenings[i];
                if (door.Distance <= cut) continue;
                door.Move(door.Distance - cut);
                newWall.MutableOpenings.Insert(0, door);
                wall.MutableOpenings.RemoveAt(i);
            }

            // The segment containing the old start keeps the wall's ID. Only the second is new.
            foreach (var cell in cells)
            {
                int edge = IndexOfWall(cell, wall.Id);
                if (edge < 0)
                    continue;
                bool forward = GetCellCornerIds(cell, walls)[edge] == wall.StartId;
                // Reverse walks encounter the new second segment before the retained first one.
                cell.InsertWall(edge + (forward ? 1 : 0), newWall.Id);
            }
            corners.Insert(corners.FindIndex(c => c.Id == wall.StartId) + 1, corner);
            walls.Insert(wallIndex + 1, newWall);
            wall.SetEnd(corner.Id);
            return corner.Id;
        }

        public bool CanMoveCorner(int index, Vector2 position, out string reason)
        {
            if (index < 0 || index >= corners.Count)
            {
                reason = "That corner does not exist.";
                return false;
            }
            var positions = GetPositions();
            positions[index] = position;
            return CanMovePositions(positions, out reason);
        }

        public bool TryMoveCorners(IReadOnlyDictionary<string, Vector2> targets, out string reason)
        {
            var positions = GetPositions();
            foreach (var target in targets)
            {
                int index = CornerIndex(target.Key);
                if (index < 0) { reason = "A selected node no longer exists."; return false; }
                positions[index] = target.Value;
            }
            // Validate the final group shape once, not a sequence of invalid intermediate moves.
            if (!CanMovePositions(positions, out reason)) return false;
            for (int i = 0; i < corners.Count; i++) corners[i].Move(positions[i]);
            return true;
        }

        private bool CanMovePositions(List<Vector2> positions, out string reason)
        {
            if (!OutlineGeometry.ValidatePoints(positions, out reason))
                return false;
            // Resolve endpoints once. Searching the corner list inside every wall
            // pair turns this exact intersection check into cubic work on dense plans.
            var starts = new Vector2[walls.Count];
            var ends = new Vector2[walls.Count];
            for (int i = 0; i < walls.Count; i++)
            {
                starts[i] = positions[CornerIndex(walls[i].StartId)];
                ends[i] = positions[CornerIndex(walls[i].EndId)];
            }
            for (int i = 0; i < walls.Count; i++)
            {
                for (int j = i + 1; j < walls.Count; j++)
                {
                    if (ConflictsWithWall(walls[i].StartId, walls[i].EndId, walls[j], starts[i], ends[i], starts[j], ends[j]))
                    {
                        reason = "Walls cannot cross, overlap, or touch away from shared corners.";
                        return false;
                    }
                }
            }
            foreach (var cell in cells)
            {
                var boundary = GetCellBoundary(cell, positions);
                if (!OutlineGeometry.Validate(boundary, true, out reason))
                    return false;
                if (OutlineGeometry.SignedArea(boundary) * OutlineGeometry.SignedArea(GetCellBoundary(cell)) <= 0)
                {
                    reason = "A move cannot turn a cell inside out.";
                    return false;
                }
            }
            for (int i = 0; i < walls.Count; i++)
            {
                if (!IsIncompleteWall(i))
                    continue;
                var wall = walls[i];
                // At least one end is strictly inside: a boundary attachment is never both ends.
                var owner = cells.Find(c => OutlineGeometry.ContainsPoint(GetCellBoundary(c), GetPosition(wall.StartId))
                    || OutlineGeometry.ContainsPoint(GetCellBoundary(c), GetPosition(wall.EndId)));
                if (owner == null) continue; // Exterior open runs have no containing room.
                var boundary = GetCellBoundary(owner, positions);
                var ids = GetCellCornerIds(owner);
                Vector2 a = positions[CornerIndex(wall.StartId)];
                Vector2 b = positions[CornerIndex(wall.EndId)];
                // Final segment intersections alone miss a large drag that jumps a whole room
                // past a freestanding wall, or moves its free end through its own boundary anchor.
                if ((!ids.Contains(wall.StartId) && !OutlineGeometry.ContainsPoint(boundary, a))
                    || (!ids.Contains(wall.EndId) && !OutlineGeometry.ContainsPoint(boundary, b)))
                {
                    reason = "Keep incomplete walls inside their room; a free endpoint cannot close a partition yet.";
                    return false;
                }
            }
            return true;
        }

        public bool TryMoveCorner(int index, Vector2 position, out string reason)
        {
            if (!CanMoveCorner(index, position, out reason))
                return false;

            // Moving a corner changes its position, not its identity or attached walls.
            corners[index].Move(position);
            return true;
        }

        public bool TryGetCell(out List<Vector2> boundary)
        {
            // Kept for single-outline callers. Multiple cells must be read through Cells.
            boundary = new List<Vector2>();
            if (cells.Count != 1)
                return false;
            boundary = GetCellBoundary(cells[0]);
            return OutlineGeometry.Validate(boundary, true, out _);
        }

    }
}
