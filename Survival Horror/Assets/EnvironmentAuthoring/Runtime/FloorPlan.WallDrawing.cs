using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        // Partitions reject overlapping intervals; drawing tools can replace their construction.
        private enum ExistingEdgePolicy { Reject, Replace }

        public bool CanCopyFootprint(out string reason)
            => PrepareFootprint(out _, out _, out _, out reason);

        public bool TryCopyFootprintFrom(FloorPlan source, out string reason)
        {
            reason = "Copy a footprint into an empty plan.";
            if (source == null || corners.Count != 0 || walls.Count != 0 || cells.Count != 0) return false;
            if (!source.PrepareFootprint(out var newCorners, out var newWalls, out var newCells, out reason)) return false;
            corners = newCorners;
            walls = newWalls;
            cells = newCells;
            return true;
        }

        // Editor callers validate an isolated candidate and obtain confirmation before
        // publishing this replacement. Keep target rooms only when their whole shape survives.
        internal bool TryReplaceFootprintFrom(FloorPlan source, out string reason)
        {
            reason = "Choose another plan as the footprint source.";
            if (source == null || source == this) return false;
            if (!source.PrepareFootprint(out var newCorners, out var newWalls, out var newCells, out reason)) return false;
            var retainedWalls = new HashSet<string>();
            for (int i = 0; i < newCells.Count; i++)
            {
                var boundary = new List<Vector2>();
                foreach (string id in GetCellCornerIds(newCells[i], newWalls)) boundary.Add(PositionOf(id, newCorners));
                var oldRoom = cells.Find(c => SameRoomBoundary(GetCellBoundary(c), boundary));
                if (oldRoom == null) continue;

                newWalls.RemoveAll(w => newCells[i].WallIds.Contains(w.Id));
                // Incomplete walls inside an unchanged room are authored data too.
                foreach (var wall in walls)
                    if ((oldRoom.WallIds.Contains(wall.Id) || RoomOnSide(wall, true) == oldRoom || RoomOnSide(wall, false) == oldRoom)
                        && retainedWalls.Add(wall.Id)) newWalls.Add(wall.Copy());
                newCells[i] = oldRoom.Copy();
            }
            // An identical copy must not invent new IDs, touch paint or create an Undo step.
            if (newCells.Count == cells.Count && retainedWalls.Count == walls.Count
                && newCells.TrueForAll(c => cells.Exists(old => old.Id == c.Id))) return true;

            newCorners.RemoveAll(c => !newWalls.Exists(w => w.StartId == c.Id || w.EndId == c.Id));
            foreach (var corner in corners)
                if (newWalls.Exists(w => w.StartId == corner.Id || w.EndId == corner.Id)) newCorners.Add(corner);
            corners = newCorners;
            walls = newWalls;
            cells = newCells;
            return true;
        }

        private static bool SameRoomBoundary(IReadOnlyList<Vector2> first, IReadOnlyList<Vector2> second)
        {
            var a = WithoutStraightCorners(first);
            var b = WithoutStraightCorners(second);
            if (a.Count < 3 || a.Count != b.Count) return false;
            int start = b.FindIndex(p => p.Equals(a[0]));
            if (start < 0) return false;
            bool forward = true, backward = true;
            for (int i = 0; i < a.Count; i++)
            {
                forward &= a[i].Equals(b[(start + i) % b.Count]);
                backward &= a[i].Equals(b[(start - i + b.Count) % b.Count]);
            }
            return forward || backward;
        }

        private static List<Vector2> WithoutStraightCorners(IReadOnlyList<Vector2> points)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                var incoming = points[i] - points[(i + points.Count - 1) % points.Count];
                var outgoing = points[(i + 1) % points.Count] - points[i];
                // Ignore exact straight-edge subdivisions, not small authored shape changes.
                if (incoming.x * outgoing.y == incoming.y * outgoing.x && Vector2.Dot(incoming, outgoing) > 0) continue;
                result.Add(points[i]);
            }
            return result;
        }

        private bool PrepareFootprint(out List<Corner> newCorners, out List<Wall> newWalls,
            out List<Cell> newCells, out string reason)
        {
            newCorners = new List<Corner>();
            newWalls = new List<Wall>();
            newCells = new List<Cell>();
            var copiedCorners = new Dictionary<string, Corner>();
            foreach (var wall in walls)
            {
                // Footprint inheritance copies the complete logical outer envelope.
                // Retain virtual construction, omit internal partitions and loose runs.
                if (cells.FindAll(c => IndexOfWall(c, wall.Id) >= 0).Count != 1) continue;
                newWalls.Add(new Wall(Guid.NewGuid().ToString("N"), CopyCorner(wall.StartId), CopyCorner(wall.EndId), presence: wall.Presence));
            }
            string CopyCorner(string id)
            {
                if (!copiedCorners.TryGetValue(id, out var copy))
                {
                    copy = new Corner(GetPosition(id));
                    copiedCorners.Add(id, copy);
                }
                return copy.Id;
            }
            newCorners.AddRange(copiedCorners.Values);
            if (!FindRoomFaces(newWalls, newCorners, out var faces, out reason)) return false;
            var boundaries = new List<List<Vector2>>();
            foreach (var face in faces)
            {
                var boundary = new List<Vector2>();
                foreach (string id in GetCellCornerIds(face, newWalls)) boundary.Add(PositionOf(id, newCorners));
                boundaries.Add(boundary);
            }
            if (!ValidateRoomFaces(boundaries, out _, out reason)) return false;
            foreach (var face in faces)
                newCells.Add(new Cell(Guid.NewGuid().ToString("N"), face.StartCornerId, new List<string>(face.WallIds)));
            return true;
        }

        public bool CanStartWall(Vector2 point, out string reason)
        {
            if (!OutlineGeometry.ValidatePoints(new[] { point }, out reason))
                return false;
            reason = "";
            return true;
        }

        private bool IsOnWall(Vector2 point) => walls.Exists(w =>
            OutlineGeometry.PointOnSegment(point, GetPosition(w.StartId), GetPosition(w.EndId)));

        public bool CanAddWall(Vector2 start, Vector2 end, out bool endsOnWall, out string reason)
            => PrepareWall(start, end, PhysicalPresence.Wall, ExistingEdgePolicy.Reject,
                out _, out _, out _, out endsOnWall, out _, out reason);

        public bool TryAddWall(Vector2 start, Vector2 end, out bool endsOnWall, out string reason)
            => TryAddGraphEdge(start, end, PhysicalPresence.Wall, ExistingEdgePolicy.Reject, out endsOnWall, out _, out reason);

        public bool TryAddEdge(Vector2 start, Vector2 end, PhysicalPresence presence,
            out bool endsOnWall, out List<string> removedOpeningIds, out string reason)
            => TryAddGraphEdge(start, end, presence, ExistingEdgePolicy.Replace, out endsOnWall, out removedOpeningIds, out reason);

        private bool TryAddGraphEdge(Vector2 start, Vector2 end, PhysicalPresence presence, ExistingEdgePolicy existingEdges,
            out bool endsOnWall, out List<string> removedOpeningIds, out string reason)
        {
            if (!PrepareWall(start, end, presence, existingEdges, out var newCorners, out var newWalls, out var newCells,
                out endsOnWall, out removedOpeningIds, out reason))
                return false;
            corners = newCorners;
            walls = newWalls;
            cells = newCells;
            return true;
        }

        private bool PrepareWall(Vector2 start, Vector2 end, PhysicalPresence presence, ExistingEdgePolicy existingEdges,
            out List<Corner> newCorners,
            out List<Wall> newWalls, out List<Cell> newCells, out bool endsOnWall,
            out List<string> removedOpeningIds, out string reason)
        {
            newCorners = null;
            newWalls = null;
            newCells = null;
            removedOpeningIds = new List<string>();
            // Only the clicked endpoint ends drawing. Crossings along the segment do not.
            endsOnWall = IsOnWall(end);
            reason = "Choose a physical wall or a virtual room boundary.";
            if (!Enum.IsDefined(typeof(PhysicalPresence), presence)) return false;
            if (!CanStartWall(start, out reason) || !CanStartWall(end, out reason)
                || !OutlineGeometry.ValidatePoints(new[] { start, end }, out reason))
                return false;

            start = ResolveEdgePoint(start);
            end = ResolveEdgePoint(end);
            if (!OutlineGeometry.ValidatePoints(new[] { start, end }, out reason)) return false;

            // Replacement concerns the full authored module interval, before splitting
            // can transfer an opening to a surviving piece by its centre.
            if (existingEdges == ExistingEdgePolicy.Replace) foreach (var wall in walls)
            {
                if (!AlignedOverlap(wall, start, end, out float overlapStart, out float overlapEnd)) continue;
                foreach (var opening in wall.Openings)
                    if (Mathf.Min(overlapEnd, opening.Distance + opening.Width / 2)
                        > Mathf.Max(overlapStart, opening.Distance - opening.Width / 2))
                        removedOpeningIds.Add(opening.Id);
            }

            // Work on copies so an invalid join never leaves half a wall or split boundary.
            newCorners = new List<Corner>(corners);
            newWalls = walls.ConvertAll(w => w.Copy());
            newCells = cells.ConvertAll(c => c.Copy());
            var removed = new HashSet<string>(removedOpeningIds);
            foreach (var wall in newWalls) wall.MutableOpenings.RemoveAll(opening => removed.Contains(opening.Id));
            string first = AddWallPoint(start, newCorners, newWalls, newCells);
            string last = AddWallPoint(end, newCorners, newWalls, newCells);
            start = PositionOf(first, newCorners);
            end = PositionOf(last, newCorners);
            if (!OutlineGeometry.ValidatePoints(new[] { start, end }, out reason))
                return false; // Two nearby clicks may both resolve to the same existing corner.

            // Each crossed wall gets one shared junction. Existing corners on the new line
            // are collected below too, so crossing a T or X junction does not duplicate it.
            for (int i = 0; i < newWalls.Count; i++)
            {
                var wall = newWalls[i];
                if (OutlineGeometry.TryInteriorIntersection(start, end,
                    PositionOf(wall.StartId, newCorners), PositionOf(wall.EndId, newCorners), out float fraction))
                {
                    InsertCorner(i, fraction, newCorners, newWalls, newCells);
                    i++; // The two pieces meet the new line only at their shared endpoint.
                }
            }
            if (!OutlineGeometry.ValidatePoints(newCorners.ConvertAll(c => c.Position), out reason))
                return false;
            Vector2 along = end - start;
            var route = newCorners.FindAll(c => OutlineGeometry.PointOnSegment(c.Position, start, end));
            route.Sort((a, b) => Vector2.Dot(a.Position - start, along).CompareTo(Vector2.Dot(b.Position - start, along)));
            var positions = newCorners.ConvertAll(c => c.Position);
            for (int i = 0; i + 1 < route.Count; i++)
            {
                var a = route[i];
                var b = route[i + 1];
                var existing = newWalls.Find(wall => wall.StartId == a.Id && wall.EndId == b.Id
                    || wall.StartId == b.Id && wall.EndId == a.Id);
                if (existing != null)
                {
                    if (existingEdges == ExistingEdgePolicy.Reject)
                    {
                        reason = "A new partition cannot overlap an existing edge.";
                        return false;
                    }
                    // Change construction on this interval, not its room-topology role.
                    existing.SetPresence(presence);
                    continue;
                }
                foreach (var wall in newWalls)
                {
                    if (ConflictsWithWall(a.Id, b.Id, wall, positions, newCorners))
                    {
                        reason = "A wall cannot overlap an existing segment or create a junction smaller than 1 mm.";
                        return false;
                    }
                }
                newWalls.Add(new Wall(Guid.NewGuid().ToString("N"), a.Id, b.Id, presence: presence));
            }
            return UpdateDividedRooms(newCorners, newWalls, ref newCells, out reason);
        }

        private Vector2 ResolveEdgePoint(Vector2 point)
        {
            var corner = corners.Find(item => Vector2.Distance(item.Position, point) <= OutlineGeometry.Tolerance);
            if (corner != null) return corner.Position;
            foreach (var wall in walls)
            {
                var a = GetPosition(wall.StartId); var b = GetPosition(wall.EndId);
                if (!OutlineGeometry.PointOnSegment(point, a, b)) continue;
                return a + (b - a) * Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
            }
            return point;
        }

        private bool AlignedOverlap(Wall wall, Vector2 start, Vector2 end, out float first, out float last)
        {
            var a = GetPosition(wall.StartId); var edge = GetPosition(wall.EndId) - a;
            float length = edge.magnitude;
            first = last = 0;
            if (length <= OutlineGeometry.Tolerance) return false;
            float DistanceFromLine(Vector2 point) => Mathf.Abs(edge.x * (point.y - a.y) - edge.y * (point.x - a.x)) / length;
            if (DistanceFromLine(start) > OutlineGeometry.Tolerance || DistanceFromLine(end) > OutlineGeometry.Tolerance) return false;
            float s = Vector2.Dot(start - a, edge) / length, e = Vector2.Dot(end - a, edge) / length;
            first = Mathf.Max(0, Mathf.Min(s, e)); last = Mathf.Min(length, Mathf.Max(s, e));
            return last > first;
        }

        private static string AddWallPoint(Vector2 point, List<Corner> corners, List<Wall> walls, List<Cell> cells)
        {
            var existing = corners.Find(c => Vector2.Distance(c.Position, point) <= OutlineGeometry.Tolerance);
            if (existing != null)
                return existing.Id;
            for (int i = 0; i < walls.Count; i++)
            {
                Vector2 a = PositionOf(walls[i].StartId, corners);
                Vector2 b = PositionOf(walls[i].EndId, corners);
                if (OutlineGeometry.PointOnSegment(point, a, b))
                    return InsertCorner(i, Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude),
                        corners, walls, cells);
            }
            var corner = new Corner(point);
            corners.Add(corner);
            return corner.Id;
        }

        private bool UpdateDividedRooms(List<Corner> newCorners, List<Wall> newWalls,
            ref List<Cell> newCells, out string reason)
        {
            if (!FindRoomFaces(newWalls, newCorners, out var faces, out reason)) return false;
            var boundaries = faces.ConvertAll(f => GetCellCornerIds(f, newWalls).ConvertAll(id => PositionOf(id, newCorners)));
            if (!ValidateRoomFaces(boundaries, out var samples, out reason)) return false;
            var result = new List<Cell>();
            var assigned = new HashSet<int>();
            foreach (var room in newCells)
            {
                // Read the original boundary: deletion may already have removed one of its walls.
                var boundary = GetCellBoundary(cells.Find(c => c.Id == room.Id));
                var children = new List<int>();
                for (int i = 0; i < faces.Count; i++)
                    if (OutlineGeometry.ContainsPoint(boundary, samples[i])) children.Add(i);
                if (children.Count == 1 && new HashSet<string>(faces[children[0]].WallIds).SetEquals(room.WallIds))
                {
                    result.Add(room); // Preserve unchanged winding, boundary start and identity.
                    assigned.Add(children[0]);
                    continue;
                }
                int kept = -1;
                double largest = -1;
                string smallest = null;
                var oldIds = new HashSet<string>(cells.Find(c => c.Id == room.Id).WallIds);
                foreach (int i in children)
                {
                    double area = Math.Abs(OutlineGeometry.SignedArea(boundaries[i]));
                    string tie = SmallestWallId(new List<string>(faces[i].WallIds), oldIds);
                    if (area > largest + 0.000001 || (Math.Abs(area - largest) <= 0.000001
                        && tie != null && (smallest == null || string.CompareOrdinal(tie, smallest) < 0)))
                    { kept = i; largest = area; smallest = tie; }
                }
                foreach (int i in children)
                {
                    var ids = new List<string>(faces[i].WallIds);
                    if (OutlineGeometry.SignedArea(boundary) < 0) ids.Reverse();
                    result.Add(new Cell(i == kept ? room.Id : Guid.NewGuid().ToString("N"), faces[i].StartCornerId, ids, room.WallMaterial, room.FloorMaterial, room.CeilingMaterial, room.IsOutdoor));
                    assigned.Add(i);
                }
            }
            // Faces outside the old rooms are newly enclosed rooms, including the first one.
            for (int i = 0; i < faces.Count; i++)
                if (!assigned.Contains(i))
                    result.Add(new Cell(Guid.NewGuid().ToString("N"), faces[i].StartCornerId, new List<string>(faces[i].WallIds)));
            newCells = result;
            reason = "";
            return true;
        }

        private static bool ValidateRoomFaces(List<List<Vector2>> boundaries, out List<Vector2> samples, out string reason)
        {
            samples = new List<Vector2>();
            foreach (var boundary in boundaries)
            {
                OutlineGeometry.TryTriangulate(boundary, out var triangles);
                samples.Add((boundary[triangles[0]] + boundary[triangles[1]] + boundary[triangles[2]]) / 3);
            }
            // Disconnected nested loops describe a hole, which one-boundary cells cannot store.
            for (int i = 0; i < boundaries.Count; i++)
                for (int j = 0; j < boundaries.Count; j++)
                    if (i != j && OutlineGeometry.ContainsPoint(boundaries[j], samples[i]))
                    {
                        reason = "Rooms with holes are not supported yet. Connect the run to room boundaries.";
                        return false;
                    }
            reason = "";
            return true;
        }

        private static bool FindRoomFaces(List<Wall> walls, List<Corner> corners,
            out List<Cell> faces, out string reason)
        {
            faces = new List<Cell>();
            var indices = corners.Select((corner, index) => (corner.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
            var vertices = corners.Select(corner => corner.Position).ToArray();
            var edges = walls.Select(wall => new Vector2Int(indices[wall.StartId], indices[wall.EndId])).ToArray();
            string From(int edge) => edge % 2 == 0 ? walls[edge / 2].StartId : walls[edge / 2].EndId;
            foreach (var walk in PlanarFaceWalk.Trace(vertices, edges))
            {
                var points = walk.ConvertAll(e => PositionOf(From(e), corners));
                if (points.Count < 3 || OutlineGeometry.SignedArea(points) <= 0)
                    continue; // The outer face runs clockwise; a freestanding tree has no face.
                if (!OutlineGeometry.Validate(points, true, out reason))
                {
                    reason = "This connection would create a hole or a pinched room boundary. " + reason;
                    return false;
                }
                faces.Add(new Cell("", From(walk[0]), walk.ConvertAll(e => walls[e / 2].Id)));
            }
            reason = "";
            return true;
        }
    }
}
