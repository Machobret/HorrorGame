using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public static partial class HouseWallLayout
    {
        // Only parallel continuations need a sideways translation. Angled faces
        // already meet by extending their square ends to their line intersection.
        private static void AlignContinuations(Dictionary<string, Node> nodes, List<PhysicalRun> runs,
            FloorPlan plan, Result result)
        {
            var links = new Dictionary<PhysicalRun, List<(PhysicalRun Run, Vector2 Delta)>>();
            var rooms = new Dictionary<FloorPlan.Wall, (FloorPlan.Cell Left, FloorPlan.Cell Right)>();
            foreach (var run in runs) links.Add(run, new List<(PhysicalRun, Vector2)>());
            foreach (var node in nodes.Values)
            {
                if (!Continuation(node, out var a, out var b)
                    || Vector2.Dot(a.Direction, b.Direction) > -.999999f
                    || Mathf.Abs(Cross(a.Direction, b.Direction)) > Tolerance
                    || !SharedFace(plan, a, b, rooms, out var faceA, out var faceB)) continue;
                var delta = faceA - faceB;
                links[a.Run].Add((b.Run, delta));
                links[b.Run].Add((a.Run, -delta));
            }

            // Anchor the widest body after choosing continuations by direction.
            // Thin sections move; their authored profile and thickness never change.
            var ordered = new List<PhysicalRun>(runs);
            ordered.Sort((a, b) =>
            {
                int width = (b.Depths.y - b.Depths.x).CompareTo(a.Depths.y - a.Depths.x);
                return width != 0 ? width : string.CompareOrdinal(a.StableId, b.StableId);
            });
            var visited = new HashSet<PhysicalRun>();
            foreach (var anchor in ordered)
            {
                if (!visited.Add(anchor)) continue;
                var component = new List<PhysicalRun>();
                var pending = new Queue<PhysicalRun>();
                pending.Enqueue(anchor);
                bool consistent = true;
                while (pending.Count > 0)
                {
                    var run = pending.Dequeue(); component.Add(run);
                    foreach (var link in links[run])
                    {
                        var shift = run.Shift + link.Delta;
                        if (visited.Add(link.Run))
                        { link.Run.Shift = shift; pending.Enqueue(link.Run); }
                        else if ((link.Run.Shift - shift).sqrMagnitude > Tolerance * Tolerance)
                            consistent = false;
                    }
                }
                // Conflicting constraints must not shear a run or change authored
                // data. Keep the original datum for this ambiguous component.
                if (!consistent) foreach (var run in component) run.Shift = Vector2.zero;
            }
            foreach (var run in runs)
                foreach (var wall in run.Walls) result.Placements.Add(wall.Id, run.Shift);
        }

        private static bool ExteriorCorner(Node node, FloorPlan plan, out Branch a, out Branch b)
        {
            a = b = null;
            if (node.Branches.Count <= 2) return false;
            var facade = node.Branches.FindAll(branch =>
                plan.GetEffectiveWallType(branch.LogicalWall) == FloorPlan.WallType.Exterior);
            if (facade.Count != 2) return false;
            // A partition cannot replace either leg of the outside corner just
            // because it points closer to a straight continuation.
            a = facade[0]; b = facade[1];
            if (string.CompareOrdinal(a.LogicalWall.Id, b.LogicalWall.Id) > 0) (a, b) = (b, a);
            return true;
        }

        private static bool Continuation(Node node, out Branch a, out Branch b)
        {
            a = b = null;
            float best = float.PositiveInfinity;
            string bestKey = null;
            for (int i = 0; i < node.Branches.Count; i++)
            for (int j = i + 1; j < node.Branches.Count; j++)
            {
                var first = node.Branches[i]; var second = node.Branches[j];
                if (string.CompareOrdinal(first.LogicalWall.Id, second.LogicalWall.Id) > 0)
                    (first, second) = (second, first);
                float turn = Vector2.Dot(first.Direction, second.Direction);
                string key = first.LogicalWall.Id + ":" + second.LogicalWall.Id;
                if (turn > best || turn == best && string.CompareOrdinal(key, bestKey) >= 0) continue;
                a = first; b = second; best = turn; bestKey = key;
            }
            return a != null;
        }

        private static bool SharedFace(FloorPlan plan, Branch a, Branch b,
            Dictionary<FloorPlan.Wall, (FloorPlan.Cell Left, FloorPlan.Cell Right)> rooms,
            out Vector2 faceA, out Vector2 faceB)
        {
            faceA = faceB = Vector2.zero;
            var roomsA = Rooms(a.LogicalWall); var roomsB = Rooms(b.LogicalWall);
            FloorPlan.Cell shared = null;
            foreach (var room in new[] { roomsA.Left, roomsA.Right })
            {
                if (room == null || room != roomsB.Left && room != roomsB.Right) continue;
                // Two common rooms give two incompatible face choices when the
                // bodies differ. Leave that case centred rather than guess a room.
                if (shared != null && shared != room) return false;
                shared = room;
            }
            if (shared == null) return false;
            // A freestanding partition can have the same room on both sides.
            // That identifies no particular face, even though the room is unique.
            if ((roomsA.Left == shared) == (roomsA.Right == shared)
                || (roomsB.Left == shared) == (roomsB.Right == shared)) return false;
            faceA = Face(a, roomsA.Left == shared); faceB = Face(b, roomsB.Left == shared);
            return true;

            (FloorPlan.Cell Left, FloorPlan.Cell Right) Rooms(FloorPlan.Wall wall)
            {
                if (rooms != null && rooms.TryGetValue(wall, out var retained)) return retained;
                var value = (plan.RoomOnSide(wall, true), plan.RoomOnSide(wall, false));
                rooms?.Add(wall, value);
                return value;
            }

            Vector2 Face(Branch branch, bool left)
            {
                var wall = branch.LogicalWall;
                var saved = plan.GetPosition(wall.EndId) - plan.GetPosition(wall.StartId);
                if (Vector2.Dot(saved, branch.Direction) < 0) left = !left;
                return Perpendicular(branch.Direction) * (left ? branch.Depths.y : branch.Depths.x);
            }
        }

        private static bool SharedFaceEnds(FloorPlan plan, Branch a, Branch b, out float first, out float second)
        {
            first = second = 0;
            float cross = Cross(a.Direction, b.Direction);
            if (Mathf.Abs(cross) < .001f || !SharedFace(plan, a, b, null, out var faceA, out var faceB)) return false;
            var p = a.Run.Shift + faceA; var q = b.Run.Shift + faceB;
            var meeting = p + a.Direction * (Cross(q - p, b.Direction) / cross);
            float reach = 4 * Mathf.Max(Mathf.Abs(a.Depths.x), Mathf.Abs(a.Depths.y), Mathf.Abs(b.Depths.x), Mathf.Abs(b.Depths.y))
                + Mathf.Max(a.Run.Shift.magnitude, b.Run.Shift.magnitude);
            if (!float.IsFinite(meeting.x) || !float.IsFinite(meeting.y) || meeting.magnitude > reach) return false;
            first = Vector2.Dot(meeting - a.Run.Shift, a.Direction);
            second = Vector2.Dot(meeting - b.Run.Shift, b.Direction);
            return true;
        }

        private static bool DatumIntersection(Branch a, Branch b, out Vector2 intersection)
        {
            intersection = Vector2.zero;
            float cross = Cross(a.Direction, b.Direction);
            if (Mathf.Abs(cross) < .001f) return false;
            intersection = a.Run.Shift + a.Direction * (Cross(b.Run.Shift - a.Run.Shift, b.Direction) / cross);
            float reach = 4 * Mathf.Max(Mathf.Abs(a.Depths.x), Mathf.Abs(a.Depths.y), Mathf.Abs(b.Depths.x), Mathf.Abs(b.Depths.y))
                + Mathf.Max(a.Run.Shift.magnitude, b.Run.Shift.magnitude);
            return float.IsFinite(intersection.x) && float.IsFinite(intersection.y) && intersection.magnitude <= reach;
        }
    }
}
