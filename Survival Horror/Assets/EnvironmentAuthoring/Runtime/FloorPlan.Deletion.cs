using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public bool TryDeleteCorner(string cornerId, out string reason)
        {
            var incident = walls.Where(w => w.StartId == cornerId || w.EndId == cornerId).ToArray();
            reason = "Choose an existing wall junction.";
            if (incident.Length == 0) return false;
            var nextWalls = walls.Where(w => !incident.Contains(w)).ToList();
            var nextCells = cells.ConvertAll(c => c.Copy());
            if (!UpdateDividedRooms(corners, nextWalls, ref nextCells, out reason)) return false;
            walls = nextWalls; cells = nextCells;
            corners.RemoveAll(c => !walls.Exists(w => w.StartId == c.Id || w.EndId == c.Id));
            return true;
        }

        public bool CanDissolveCorner(string cornerId, out string reason)
            => Copy().TryDissolveCorner(cornerId, out reason);

        public bool TryDissolveCorner(string cornerId, out string reason)
        {
            var incident = walls.Where(w => w.StartId == cornerId || w.EndId == cornerId).ToArray();
            reason = "Dissolve requires exactly two walls at the junction.";
            if (incident.Length != 2) return false;
            var a = incident[0]; var b = incident[1];
            string start = a.StartId == cornerId ? a.EndId : a.StartId;
            string end = b.StartId == cornerId ? b.EndId : b.StartId;
            bool reverseA = a.StartId == cornerId, reverseB = b.EndId == cornerId;
            Material Left(Wall w, bool reverse) => reverse ? w.RightMaterial : w.LeftMaterial;
            Material Right(Wall w, bool reverse) => reverse ? w.LeftMaterial : w.RightMaterial;
            reason = "Match the two walls' geometry, finishes and presence before dissolving this junction.";
            if (start == end || a.Type != b.Type || a.Presence != b.Presence || a.Family != b.Family
                || Left(a, reverseA) != Left(b, reverseB) || Right(a, reverseA) != Right(b, reverseB)
                || !a.SameMaterialRoles(b)) return false;
            reason = "Match the adjoining trims before dissolving this junction; a partial trim cannot cover the merged wall.";
            if (a.OverridesTrim != b.OverridesTrim || a.TrimDisabled != b.TrimDisabled
                || (a.Trim == null ? b.Trim != null : !a.Trim.SameProfile(b.Trim))) return false;
            float firstLength = Vector2.Distance(GetPosition(start), GetPosition(cornerId));
            float secondLength = Vector2.Distance(GetPosition(cornerId), GetPosition(end));
            bool straight = OutlineGeometry.PointOnSegment(GetPosition(cornerId), GetPosition(start), GetPosition(end));
            WallTrim trim = a.Trim?.Copy();
            if (trim != null)
            {
                // Pattern phase follows logical wall direction. Reversing a wall
                // reverses its phase as well as its start point.
                float phase = reverseA ? -trim.PatternOffset - firstLength : trim.PatternOffset;
                float nextPhase = reverseB ? -b.Trim.PatternOffset - secondLength : b.Trim.PatternOffset;
                float repeat = trim.Mesh != null ? trim.Mesh.bounds.size.x : 0;
                float difference = nextPhase - phase - firstLength;
                reason = "Align the adjoining trim repeats before dissolving this straight junction.";
                if (straight && repeat > .0001f && Mathf.Abs(difference - Mathf.Round(difference / repeat) * repeat) > .0001f) return false;
                trim.AdvancePattern(phase - trim.PatternOffset);
            }
            reason = "Keep walls containing openings straight when dissolving their junction.";
            if ((a.Openings.Count > 0 || b.Openings.Count > 0)
                && !straight) return false;
            var merged = new Wall(a.Id, start, end, a.Type, a.Presence);
            merged.SetMaterials(Left(a, reverseA), Right(a, reverseA));
            merged.CopyMaterialRolesFrom(a);
            merged.SetFamily(a.Family);
            if (a.OverridesTrim) merged.SetTrim(trim); else merged.ResetTrim();
            foreach (var wall in incident)
                foreach (var opening in wall.Openings)
                {
                    var copy = opening.Copy();
                    float length = Vector2.Distance(GetPosition(wall.StartId), GetPosition(wall.EndId));
                    bool reverse = wall == a ? reverseA : reverseB;
                    copy.Move((wall == a ? 0 : firstLength) + (reverse ? length - copy.Distance : copy.Distance));
                    merged.MutableOpenings.Add(copy);
                }
            var next = Copy();
            next.walls.RemoveAll(w => w.Id == a.Id || w.Id == b.Id);
            next.walls.Insert(walls.IndexOf(a), merged);
            next.corners.RemoveAll(c => c.Id == cornerId);
            for (int i = 0; i < next.cells.Count; i++)
            {
                var room = cells[i];
                bool usesA = room.WallIds.Contains(a.Id), usesB = room.WallIds.Contains(b.Id);
                reason = "Both walls must belong to the same room boundaries.";
                if (usesA != usesB) return false;
                if (!usesA) continue;
                next.cells[i] = new Cell(room.Id, room.StartCornerId == cornerId ? (room.WallIds[0] == a.Id ? end : start) : room.StartCornerId,
                    room.WallIds.Where(id => id != b.Id).ToList(), room.WallMaterial, room.FloorMaterial,
                    room.CeilingMaterial, room.IsOutdoor);
                reason = "Dissolving this corner would invert its room.";
                if (OutlineGeometry.SignedArea(next.GetCellBoundary(next.cells[i])) * OutlineGeometry.SignedArea(GetCellBoundary(room)) <= 0) return false;
            }
            if (!next.CanMovePositions(next.GetPositions(), out reason)) return false;
            foreach (var opening in merged.Openings)
                if (!next.CanPlaceOpening(merged.Id, opening.Distance, opening.Width, opening.Id, out reason)) return false;
            corners = next.corners; walls = next.walls; cells = next.cells;
            reason = "";
            return true;
        }

        public bool CanDeleteWall(int wallIndex, out List<string> removedWallIds, out string reason)
        {
            removedWallIds = new List<string>();
            reason = "Choose a wall to delete.";
            if (wallIndex < 0 || wallIndex >= walls.Count) return false;
            string id = walls[wallIndex].Id;
            int owners = cells.FindAll(c => IndexOfWall(c, id) >= 0).Count;
            // Preserve the established whole-boundary merge, including bends and split segments.
            if (owners == 2) return CanRemovePartition(wallIndex, out removedWallIds, out reason);
            removedWallIds.Add(id);
            if (owners == 0) { reason = ""; return true; }
            return PrepareBoundaryDeletion(wallIndex, out _, out _, out reason);
        }

        public bool TryDeleteWall(int wallIndex, out string reason)
        {
            if (wallIndex < 0 || wallIndex >= walls.Count)
            { reason = "Choose a wall to delete."; return false; }
            string id = walls[wallIndex].Id;
            int owners = cells.FindAll(c => IndexOfWall(c, id) >= 0).Count;
            if (owners == 0) return TryRemoveInteriorWall(wallIndex, out reason);
            if (owners == 2) return TryRemovePartition(wallIndex, out reason);
            if (!PrepareBoundaryDeletion(wallIndex, out var remainingWalls, out var remainingCells, out reason)) return false;
            walls = remainingWalls;
            cells = remainingCells;
            corners.RemoveAll(c => !walls.Exists(w => w.StartId == c.Id || w.EndId == c.Id));
            return true;
        }

        private bool PrepareBoundaryDeletion(int wallIndex, out List<Wall> remainingWalls,
            out List<Cell> remainingCells, out string reason)
        {
            remainingWalls = new List<Wall>(walls);
            remainingWalls.RemoveAt(wallIndex);
            remainingCells = cells.ConvertAll(c => c.Copy());
            // Reuse loop detection. Open rooms disappear; unaffected closed rooms keep their IDs.
            // Work on copies so a rejected deletion cannot partly edit the plan.
            return UpdateDividedRooms(corners, remainingWalls, ref remainingCells, out reason);
        }
    }
}
