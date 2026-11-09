using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Endpoint constraints belong to the authored candidate, not the mouse tool.
    // A span uses stable endpoint IDs so inserting a point does not break its host.
    internal static class HouseDividerAttachments
    {
        internal static void ReleaseRemovedDividerHosts(HouseStack.Boundary boundary)
        {
            var nodes = boundary.DividerNodes.ToDictionary(n => n.Id);
            foreach (var node in boundary.DividerNodes)
            {
                var host = node.Attachment;
                if (host == null || !string.IsNullOrEmpty(host.LevelId) || !string.IsNullOrEmpty(host.OpeningId)) continue;
                if (!nodes.ContainsKey(host.StartId) || !nodes.ContainsKey(host.EndId)
                    || !Connected(host.StartId, host.EndId, boundary.Dividers.Select(d => (d.StartId, d.EndId)),
                        id => OutlineGeometry.PointOnSegment(nodes[id].Position, nodes[host.StartId].Position, nodes[host.EndId].Position)))
                    node.Attach(null);
            }
        }

        internal static void CaptureEndpoints(HouseStack.Boundary before, HouseStack.Boundary after,
            FloorPlan plan, string levelId, params Vector2[] endpoints)
        {
            foreach (var point in endpoints)
            {
                var node = after.DividerNodes.FirstOrDefault(n => n.Position == PlanarNoding.AtPrecision(point));
                if (node == null || node.Attachment != null) continue;
                var original = before.DividerNodes.FirstOrDefault(n => n.Id == node.Id);
                if (original != null)
                {
                    // Drawing into an existing straight subdivision creates a T
                    // just as drawing into the middle of an unsplit edge does.
                    // Existing bends and crossings retain their authored freedom.
                    var neighbors = before.Dividers.Where(d => d.StartId == node.Id || d.EndId == node.Id)
                        .Select(d => d.StartId == node.Id ? d.EndId : d.StartId).ToArray();
                    if (neighbors.Length == 2 && after.Dividers.Count(d => d.StartId == node.Id || d.EndId == node.Id) == 3
                        && OutlineGeometry.PointOnSegment(original.Position, before.DividerPosition(neighbors[0]), before.DividerPosition(neighbors[1])))
                    {
                        string a = Terminal(before, node.Id, neighbors[0]), b = Terminal(before, node.Id, neighbors[1]);
                        node.Attach(new HouseStack.DividerAttachment(null, a, b,
                            Fraction(node.Position, before.DividerPosition(a), before.DividerPosition(b))));
                    }
                    continue;
                }
                if (AttachToWall(node, plan, levelId)) continue;
                if (after.Dividers.Count(edge => edge.StartId == node.Id || edge.EndId == node.Id) == 1
                    && AttachToOpening(node, after)) continue;
                var host = before.Dividers.Where(d => OutlineGeometry.PointOnSegment(node.Position,
                    before.DividerPosition(d.StartId), before.DividerPosition(d.EndId)))
                    .OrderBy(d => d.Id, System.StringComparer.Ordinal).FirstOrDefault();
                if (host != null)
                    node.Attach(new HouseStack.DividerAttachment(null, host.StartId, host.EndId,
                        Fraction(node.Position, before.DividerPosition(host.StartId), before.DividerPosition(host.EndId))));
            }
        }

        // Adjacent T nodes share the straight chain's terminals, rather than make
        // their nearest neighbours depend on one another in a cycle.
        private static string Terminal(HouseStack.Boundary boundary, string previous, string current)
        {
            var seen = new HashSet<string> { previous };
            while (seen.Add(current))
            {
                var next = boundary.Dividers.Where(d => d.StartId == current || d.EndId == current)
                    .Select(d => d.StartId == current ? d.EndId : d.StartId)
                    .Where(id => id != previous && OutlineGeometry.PointOnSegment(boundary.DividerPosition(current),
                        boundary.DividerPosition(previous), boundary.DividerPosition(id))).ToArray();
                if (next.Length != 1) break;
                previous = current; current = next[0];
            }
            return current;
        }

        internal static bool AttachToWall(HouseStack.DividerNode node, FloorPlan plan, string levelId)
        {
            var wall = plan?.Walls.Where(w => OutlineGeometry.PointOnSegment(node.Position,
                plan.GetPosition(w.StartId), plan.GetPosition(w.EndId))).OrderBy(w => w.Id, System.StringComparer.Ordinal).FirstOrDefault();
            if (wall == null) return false;
            node.Attach(new HouseStack.DividerAttachment(levelId, wall.StartId, wall.EndId,
                Fraction(node.Position, plan.GetPosition(wall.StartId), plan.GetPosition(wall.EndId))));
            return true;
        }

        private static bool AttachToOpening(HouseStack.DividerNode node, HouseStack.Boundary boundary)
        {
            foreach (var opening in boundary.Openings.OrderBy(o => o.Id, System.StringComparer.Ordinal))
                for (int i = 0; i < opening.Points.Count; i++)
                {
                    int next = (i + 1) % opening.Points.Count;
                    var start = opening.Points[i]; var end = opening.Points[next];
                    if (!OutlineGeometry.PointOnSegment(node.Position, start, end)) continue;
                    opening.EnsurePointIds();
                    node.Attach(new HouseStack.DividerAttachment(null, opening.PointId(i), opening.PointId(next),
                        Fraction(node.Position, start, end), opening.Id));
                    return true;
                }
            return false;
        }

        internal static bool TryOpeningSpan(HouseStack.Boundary boundary, HouseStack.DividerAttachment host,
            out Vector2 start, out Vector2 end)
        {
            start = end = default;
            var opening = boundary.Openings.FirstOrDefault(o => o.Id == host.OpeningId);
            if (opening == null) return false;
            int a = opening.PointIndex(host.StartId), b = opening.PointIndex(host.EndId);
            if (a < 0 || b < 0 || a == b) return false;
            start = opening.Points[a]; end = opening.Points[b];
            // Subdivision retains the original directed span. A newly bent chain
            // has no unique straight sliding host, so reject rather than retarget.
            for (int i = (a + 1) % opening.Points.Count; i != b; i = (i + 1) % opening.Points.Count)
                if (!OutlineGeometry.PointOnSegment(opening.Points[i], start, end)) return false;
            return true;
        }

        internal static bool Resolve(HouseStack.Boundary boundary, HouseStack.Boundary before,
            System.Func<string, FloorPlan> planFor, System.Func<string, FloorPlan> originalPlanFor,
            IReadOnlyDictionary<string, Vector2> requested, out string reason)
        {
            reason = "A divider cannot depend on itself or on a cycle of attached endpoints.";
            var nodes = boundary.DividerNodes.ToDictionary(n => n.Id);
            var originalNodes = before.DividerNodes.ToDictionary(n => n.Id);
            if (requested != null && requested.Any(pair => !nodes.ContainsKey(pair.Key)
                || !float.IsFinite(pair.Value.x) || !float.IsFinite(pair.Value.y)))
            { reason = "Choose existing divider nodes and finite positions."; return false; }
            var completed = new HashSet<string>();
            var visiting = new HashSet<string>();
            string failure = reason;
            foreach (var node in boundary.DividerNodes)
                if (!Visit(node)) { reason = failure; return false; }

            // A hole can extend beyond a slab, but cannot drag its attached floor
            // endpoint into empty space. Passing dividers keep their existing path.
            var floor = planFor(boundary.UpperId) ?? planFor(boundary.LowerId);
            if (floor != null && boundary.DividerNodes.Where(node => !string.IsNullOrEmpty(node.Attachment?.OpeningId))
                .Any(node => !floor.Cells.Any(room => SlabGeometry.Contains(SlabGeometry.Shape(floor.GetCellBoundary(room)), node.Position))))
            { reason = "Keep hole-attached divider endpoints within the floor footprint."; return false; }

            // Resolve all dependent coordinates first: an attached T junction can lie
            // inside another host span, but it is not an endpoint dependency of it.
            foreach (var node in boundary.DividerNodes)
            {
                var host = node.Attachment;
                if (host == null) continue;
                if (!string.IsNullOrEmpty(host.OpeningId)) continue; // Checked by Visit through the same span reader as snapping.
                bool connected, straight;
                if (string.IsNullOrEmpty(host.LevelId))
                {
                    var edges = boundary.Dividers.Select(d => (d.StartId, d.EndId));
                    Vector2 Original(string id) => originalNodes.TryGetValue(id, out var old) ? old.Position : nodes[id].Position;
                    connected = Connected(host.StartId, host.EndId, edges,
                        id => OutlineGeometry.PointOnSegment(Original(id), Original(host.StartId), Original(host.EndId)));
                    straight = Connected(host.StartId, host.EndId, edges,
                        id => OutlineGeometry.PointOnSegment(nodes[id].Position, nodes[host.StartId].Position, nodes[host.EndId].Position));
                }
                else
                {
                    var plan = planFor(host.LevelId);
                    var originalPlan = originalPlanFor(host.LevelId);
                    Vector2 Original(string id) => originalPlan != null && originalPlan.Corners.Any(c => c.Id == id)
                        ? originalPlan.GetPosition(id) : plan.GetPosition(id);
                    var edges = plan.Walls.Select(w => (w.StartId, w.EndId));
                    connected = Connected(host.StartId, host.EndId, edges,
                        id => OutlineGeometry.PointOnSegment(Original(id), Original(host.StartId), Original(host.EndId)));
                    straight = Connected(host.StartId, host.EndId, edges,
                        id => OutlineGeometry.PointOnSegment(plan.GetPosition(id), plan.GetPosition(host.StartId), plan.GetPosition(host.EndId)));
                }
                // Only the old straight chain identifies the host. An alternate path
                // around a closed room must not turn a deletion into a bent-host error.
                // Deleting a host releases its dependent at the current position.
                // A bent host is ambiguous; reject instead of jumping to another edge.
                if (!connected) node.Attach(null);
                else if (!straight)
                {
                    reason = "An attached divider needs a straight host span. Move its host endpoints together or remove the attachment's host first.";
                    return false;
                }
            }
            reason = "";
            return true;

            bool Visit(HouseStack.DividerNode node)
            {
                if (completed.Contains(node.Id)) return true;
                if (!visiting.Add(node.Id)) return false;
                var host = node.Attachment;
                if (host != null)
                {
                    Vector2 a, b;
                    if (!string.IsNullOrEmpty(host.OpeningId))
                    {
                        if (!boundary.Openings.Any(o => o.Id == host.OpeningId))
                        { node.Attach(null); completed.Add(node.Id); visiting.Remove(node.Id); return true; }
                        if (!TryOpeningSpan(boundary, host, out a, out b))
                        { failure = "An attached divider needs its original straight hole edge. Keep inserted points on that edge."; return false; }
                    }
                    else if (string.IsNullOrEmpty(host.LevelId))
                    {
                        if (!nodes.TryGetValue(host.StartId, out var start) || !nodes.TryGetValue(host.EndId, out var end))
                        { node.Attach(null); completed.Add(node.Id); visiting.Remove(node.Id); return true; }
                        if (!Visit(start) || !Visit(end)) return false;
                        a = start.Position; b = end.Position;
                    }
                    else
                    {
                        var plan = planFor(host.LevelId);
                        if (plan == null || !plan.Corners.Any(c => c.Id == host.StartId) || !plan.Corners.Any(c => c.Id == host.EndId))
                        { node.Attach(null); completed.Add(node.Id); visiting.Remove(node.Id); return true; }
                        a = plan.GetPosition(host.StartId); b = plan.GetPosition(host.EndId);
                    }
                    if (!float.IsFinite(a.x) || !float.IsFinite(a.y) || !float.IsFinite(b.x) || !float.IsFinite(b.y)
                        || !float.IsFinite((b - a).sqrMagnitude) || (b - a).sqrMagnitude < .00000001f)
                    { failure = "A divider attachment needs a finite host span of at least 0.1 mm."; return false; }
                    if (requested != null && requested.TryGetValue(node.Id, out var position)) host.Slide(Fraction(position, a, b));
                    node.Move(PlanarNoding.AtPrecision(Vector2.Lerp(a, b, host.Fraction)));
                }
                else if (requested != null && requested.TryGetValue(node.Id, out var point)) node.Move(point);
                visiting.Remove(node.Id); completed.Add(node.Id);
                return true;
            }
        }

        private static float Fraction(Vector2 point, Vector2 start, Vector2 end)
            => Mathf.Clamp01(Vector2.Dot(point - start, end - start) / (end - start).sqrMagnitude);

        private static bool Connected(string start, string end, IEnumerable<(string a, string b)> edges,
            System.Func<string, bool> allowed)
        {
            var adjacency = new Dictionary<string, List<string>>();
            foreach (var edge in edges)
            {
                if (!allowed(edge.a) || !allowed(edge.b)) continue;
                if (!adjacency.TryGetValue(edge.a, out var a)) adjacency[edge.a] = a = new List<string>();
                if (!adjacency.TryGetValue(edge.b, out var b)) adjacency[edge.b] = b = new List<string>();
                a.Add(edge.b); b.Add(edge.a);
            }
            var pending = new Stack<string>(); var seen = new HashSet<string>(); pending.Push(start);
            while (pending.Count > 0)
            {
                string id = pending.Pop();
                if (id == end) return true;
                if (!seen.Add(id) || !adjacency.TryGetValue(id, out var next)) continue;
                foreach (string neighbor in next) pending.Push(neighbor);
            }
            return false;
        }
    }
}
