using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public static partial class HouseWallLayout
    {
        private sealed class PhysicalRun
        {
            internal string StartId, EndId;
            internal string StableId;
            internal Vector2 Depths;
            internal Vector2 Shift;
            internal WallModuleDefinition Definition;
            internal readonly List<FloorPlan.Wall> Walls = new List<FloorPlan.Wall>();
        }

        // A virtual room edge can split a straight wall without making a physical
        // junction. Fit that continuous body once, then restore logical ownership
        // for openings, finishes and selection. The saved wall graph is unchanged.
        private static List<PhysicalRun> PhysicalRuns(Dictionary<string, Node> nodes, FloorPlan plan)
        {
            var result = new List<PhysicalRun>();
            var visited = new HashSet<string>();
            foreach (var wall in plan.Walls)
            {
                if (visited.Contains(wall.Id) || !nodes[wall.StartId].Branches.Any(b => b.Wall == wall)) continue;
                var run = new PhysicalRun { StartId = wall.StartId, EndId = wall.EndId,
                    Definition = nodes[wall.StartId].Branches.First(b => b.Wall == wall).Definition };
                run.Walls.Add(wall); visited.Add(wall.Id);
                Extend(true); Extend(false);
                result.Add(run);

                void Extend(bool start)
                {
                    string nodeId = start ? run.StartId : run.EndId;
                    var current = wall;
                    while (Continues(nodes[nodeId], current, out var next))
                    {
                        if (!visited.Add(next.Id)) break;
                        if (start) run.Walls.Insert(0, next); else run.Walls.Add(next);
                        nodeId = next.StartId == nodeId ? next.EndId : next.StartId;
                        current = next;
                    }
                    if (start) run.StartId = nodeId; else run.EndId = nodeId;
                }
            }
            // Junction fitting needs just terminal branches, with the run's stable
            // representative ID. Internal logical nodes have no physical junction.
            foreach (var run in result)
            {
                run.Depths = nodes[run.StartId].Branches.First(b => b.Wall == run.Walls[0]).Depths;
                run.StableId = run.Walls.Min(w => w.Id);
            }
            foreach (var node in nodes.Values) node.Branches.Clear();
            foreach (var run in result)
            {
                Vector2 along = (nodes[run.EndId].Position - nodes[run.StartId].Position).normalized;
                nodes[run.StartId].Branches.Add(new Branch { Run = run, Wall = run.Walls[0], LogicalWall = run.Walls[0], Direction = along, Definition = run.Definition, Depths = run.Depths });
                nodes[run.EndId].Branches.Add(new Branch { Run = run, Wall = run.Walls[0], LogicalWall = run.Walls[run.Walls.Count-1], Direction = -along, Definition = run.Definition, Depths = ReverseDepths(run.Depths) });
            }
            return result;
        }

        private static bool Continues(Node node, FloorPlan.Wall current, out FloorPlan.Wall next)
        {
            next = null;
            if (node.Branches.Count != 2) return false;
            var a = node.Branches[0]; var b = node.Branches[1];
            if ((a.Depths - ReverseDepths(b.Depths)).sqrMagnitude > Tolerance * Tolerance || a.Definition != b.Definition || Vector2.Dot(a.Direction, b.Direction) > -0.999999f
                || Mathf.Abs(Vector2.Dot(Perpendicular(a.Direction), b.Direction)) > Tolerance) return false;
            next = a.Wall == current ? b.Wall : a.Wall;
            return true;
        }

        private static void RestoreLogicalSpans(FloorPlan plan, WallKit kit, List<PhysicalRun> runs, Result result)
        {
            var fitted = result.Spans.ToDictionary(s => s.WallId);
            result.Spans.Clear();
            foreach (var run in runs)
            {
                var span = fitted[run.Walls[0].Id];
                Vector2 along = (span.End - span.Start).normalized;
                float length = Vector2.Distance(span.Start, span.End);
                foreach (var wall in run.Walls)
                {
                    float a = Vector2.Dot(plan.GetPosition(wall.StartId) - span.Start, along);
                    float b = Vector2.Dot(plan.GetPosition(wall.EndId) - span.Start, along);
                    float low = wall == run.Walls[0] ? 0 : Mathf.Max(0, Mathf.Min(a, b));
                    float high = wall == run.Walls[run.Walls.Count - 1] ? length : Mathf.Min(length, Mathf.Max(a, b));
                    // A fitted endpoint can consume a very short logical fragment.
                    if (high - low <= Tolerance) continue;
                    // Preserve exact authored endpoints on interior fragments. Reprojecting
                    // through a changed remote fitted origin introduces float drift and
                    // needlessly replaces otherwise unchanged generated/painted groups.
                    var logicalLow = plan.GetPosition(a <= b ? wall.StartId : wall.EndId) + run.Shift;
                    var logicalHigh = plan.GetPosition(a <= b ? wall.EndId : wall.StartId) + run.Shift;
                    var piece = new Span { WallId = wall.Id, Definition = span.Definition,
                        Depths = span.Depths, Start = low <= Tolerance ? span.Start : logicalLow,
                        End = high >= length - Tolerance ? span.End : logicalHigh,
                        StartClearance = Mathf.Clamp(span.StartClearance - low, 0, high - low),
                        EndClearance = Mathf.Clamp(high - (length - span.EndClearance), 0, high - low) };
                    // Exterior +Z points outward; interior pieces retain their own
                    // authored direction even when the run joins reversed edges.
                    bool reverse = plan.TryGetExteriorOutward(wall, out var outward)
                        ? Vector2.Dot(Perpendicular(along), outward) < 0
                        : Vector2.Dot(plan.GetPosition(wall.EndId) - plan.GetPosition(wall.StartId), along) < 0;
                    if (reverse)
                    {
                        piece.Depths = ReverseDepths(piece.Depths);
                        (piece.Start, piece.End) = (piece.End, piece.Start);
                        (piece.StartClearance, piece.EndClearance) = (piece.EndClearance, piece.StartClearance);
                    }
                    AssignInsert(wall, piece);
                    result.Spans.Add(piece);
                }
            }
        }
    }
}
