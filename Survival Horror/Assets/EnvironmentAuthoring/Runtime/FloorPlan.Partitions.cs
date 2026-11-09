using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public bool CanRemovePartition(int wallIndex, out List<string> removedWallIds, out string reason)
        {
            return FindMerge(wallIndex, out _, out _, out _, out removedWallIds, out reason);
        }

        public bool TryRemovePartition(int wallIndex, out string reason)
        {
            if (!FindMerge(wallIndex, out int keptIndex, out int removedIndex,
                out var mergedCell, out var removedWallIds, out reason))
                return false;


            cells[keptIndex] = mergedCell;
            cells.RemoveAt(removedIndex);
            walls.RemoveAll(w => removedWallIds.Contains(w.Id));
            // Keep endpoints, junctions, and straight boundary points if any surviving wall uses them.
            corners.RemoveAll(c => !walls.Exists(w => w.StartId == c.Id || w.EndId == c.Id));
            return true;
        }

        private bool FindMerge(int wallIndex, out int keptIndex, out int removedIndex,
            out Cell mergedCell, out List<string> removedWallIds, out string reason)
        {
            keptIndex = removedIndex = -1;
            mergedCell = null;
            removedWallIds = new List<string>();
            reason = "Choose an interior partition shared by two cells.";
            if (wallIndex < 0 || wallIndex >= walls.Count)
                return false;
            string selectedId = walls[wallIndex].Id;
            for (int i = 0; i < cells.Count; i++)
            {
                if (IndexOfWall(cells[i], selectedId) < 0)
                    continue;
                if (keptIndex < 0)
                    keptIndex = i;
                else if (removedIndex < 0)
                    removedIndex = i;
                else
                    return false;
            }
            if (removedIndex < 0)
                return false;

            double firstArea = Math.Abs(OutlineGeometry.SignedArea(GetCellBoundary(cells[keptIndex])));
            double secondArea = Math.Abs(OutlineGeometry.SignedArea(GetCellBoundary(cells[removedIndex])));
            bool keepFirst = firstArea > secondArea;
            if (Math.Abs(firstArea - secondArea) <= OutlineGeometry.Tolerance * OutlineGeometry.Tolerance)
                keepFirst = string.CompareOrdinal(cells[keptIndex].Id, cells[removedIndex].Id) < 0;
            if (!keepFirst)
                (keptIndex, removedIndex) = (removedIndex, keptIndex);
            var kept = cells[keptIndex];
            var removed = cells[removedIndex];
            foreach (string id in kept.WallIds)
                if (IndexOfWall(removed, id) >= 0)
                    removedWallIds.Add(id);

            // Remove the whole shared boundary, including any inserted or bent segments.
            var remaining = new List<Wall>();
            foreach (var cell in new[] { kept, removed })
                foreach (string id in cell.WallIds)
                    if (!removedWallIds.Contains(id))
                        remaining.Add(walls.Find(w => w.Id == id));
            reason = "This merge would create a hole or a touching boundary.";
            if (remaining.Count < 3)
                return false;

            // Start along a retained wall in the surviving cell's walking direction.
            var firstWall = remaining[0];
            string start = GetCellCornerIds(kept)[IndexOfWall(kept, firstWall.Id)];
            var orderedWalls = new List<string> { firstWall.Id };
            var boundary = new List<Vector2> { GetPosition(start) };
            string next = firstWall.StartId == start ? firstWall.EndId : firstWall.StartId;
            remaining.RemoveAt(0);
            while (next != start && remaining.Count > 0)
            {
                int found = -1;
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i].StartId != next && remaining[i].EndId != next)
                        continue;
                    if (found >= 0)
                        return false; // More than one continuation would pinch or branch the boundary.
                    found = i;
                }
                if (found < 0)
                    return false;
                boundary.Add(GetPosition(next));
                var wall = remaining[found];
                orderedWalls.Add(wall.Id);
                next = wall.StartId == next ? wall.EndId : wall.StartId;
                remaining.RemoveAt(found);
            }
            // Leftover walls form another loop, such as a hole around a third cell.
            if (next != start || remaining.Count != 0)
                return false;
            if (!OutlineGeometry.Validate(boundary, true, out reason))
                return false;
            mergedCell = new Cell(kept.Id, start, orderedWalls, kept.WallMaterial, kept.FloorMaterial, kept.CeilingMaterial, kept.IsOutdoor);
            return true;
        }

        private static string SmallestWallId(List<string> route, HashSet<string> originalWallIds)
        {
            string smallest = null;
            foreach (string id in route)
                if ((originalWallIds == null || originalWallIds.Contains(id))
                    && (smallest == null || string.CompareOrdinal(id, smallest) < 0))
                    smallest = id;
            return smallest;
        }

        private static int IndexOfWall(Cell cell, string wallId)
        {
            for (int i = 0; i < cell.WallIds.Count; i++)
                if (cell.WallIds[i] == wallId)
                    return i;
            return -1;
        }

        private static bool ConflictsWithWall(string startId, string endId, Wall other,
            IReadOnlyList<Vector2> positions, List<Corner> corners)
        {
            Vector2 a = positions[corners.FindIndex(corner => corner.Id == startId)];
            Vector2 b = positions[corners.FindIndex(corner => corner.Id == endId)];
            Vector2 c = positions[corners.FindIndex(corner => corner.Id == other.StartId)];
            Vector2 d = positions[corners.FindIndex(corner => corner.Id == other.EndId)];
            return ConflictsWithWall(startId, endId, other, a, b, c, d);
        }

        private static bool ConflictsWithWall(string startId, string endId, Wall other,
            Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            string shared = startId == other.StartId || startId == other.EndId ? startId
                : endId == other.StartId || endId == other.EndId ? endId : null;
            if (shared == null)
                return OutlineGeometry.SegmentsTouch(a, b, c, d);

            // Meeting at a shared corner is allowed; doubling back along either wall is not.
            Vector2 junction = shared == startId ? a : b;
            Vector2 firstEnd = shared == startId ? b : a;
            Vector2 secondEnd = shared == other.StartId ? d : c;
            return OutlineGeometry.PointOnSegment(firstEnd, junction, secondEnd)
                || OutlineGeometry.PointOnSegment(secondEnd, junction, firstEnd);
        }
    }
}
