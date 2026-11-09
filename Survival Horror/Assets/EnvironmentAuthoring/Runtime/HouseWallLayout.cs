using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Work out placements before touching GameObjects. An invalid fit leaves the plan intact.
    public static partial class HouseWallLayout
    {
        private const float Tolerance = 0.0001f;
        internal const float JoinClearance = .001f;

        public sealed class Span
        {
            public string WallId;
            public string DoorwayId; // Stable ID for either opening kind.
            public FloorPlan.OpeningKind OpeningKind;
            public Vector2 Start, End;
            public Vector2 Depths; // Signed local Z interval about the fixed wall datum.
            public WallModuleDefinition Definition;
            public WallOpeningInsert Insert;
            // Overlapping wall bodies may enter a junction, but openings must stay outside it.
            public float StartClearance, EndClearance;
        }

        public sealed class Result
        {
            public readonly List<Span> Spans = new List<Span>();
            internal readonly List<CornerCover> Corners = new List<CornerCover>();
            internal readonly HashSet<string> OmittedCoverNodes = new HashSet<string>();
            internal readonly Dictionary<string, float> TopInsets = new Dictionary<string, float>();
            public readonly List<Vector2> ProblemPoints = new List<Vector2>();
            internal readonly Dictionary<string, Vector2> Placements = new Dictionary<string, Vector2>();
            internal readonly Dictionary<string, Vector2> Junctions = new Dictionary<string, Vector2>();
            // Translation only: saved wall nodes, direction and opening distances stay intact.
            public Vector2 PlacementOffset(string wallId) => Placements.TryGetValue(wallId, out var offset) ? offset : Vector2.zero;
            public Vector2 JunctionOffset(string nodeId) => Junctions.TryGetValue(nodeId, out var offset) ? offset : Vector2.zero;
            internal Vector2 DepthsFor(WallModuleDefinition definition) => definition != null ? definition.ConnectionDepths : Vector2.zero;
            public string Problem = "";
            public bool IsValid => Problem.Length == 0;
            internal void Reject(string message, Vector2 point)
            {
                if (Problem.Length == 0) Problem = message;
                ProblemPoints.Add(point);
            }
        }

        private sealed class Branch
        {
            public FloorPlan.Wall Wall, LogicalWall;
            public Vector2 Depths; // Relative to the branch direction, which points away from the node.
            public Vector2 Direction;
            public WallModuleDefinition Definition;
            public PhysicalRun Run;
        }

        private sealed class Node
        {
            public string Id;
            public Vector2 Position;
            public Vector2 FittedPosition;
            public readonly List<Branch> Branches = new List<Branch>();
            public readonly Dictionary<string, float> Ends = new Dictionary<string, float>();
            public float Offset(string wallId) => Ends.TryGetValue(wallId, out float distance) ? distance : 0;
        }

        public static Result Calculate(FloorPlan plan, WallKit builder, bool includeDoorways = true)
        {
            if (!HasPhysicalWalls(plan)) return new Result();
            if (builder == null) return new Result { Problem = "Choose a complete wall family." };
            return CalculateLayout(plan, builder, includeDoorways);
        }

        // A broken wall family must not conceal a new fit failure in a usable family.
        // This partial result is for edit validation only; output still requires the full kit.
        public static Result CalculateRequiredWalls(FloorPlan plan, WallKit builder, bool includeDoorways = true)
        {
            return CalculateLayout(plan, builder, includeDoorways, true);
        }

        internal static Vector2 ReverseDepths(Vector2 depths) => new Vector2(-depths.y, -depths.x);
        private static bool UsableBlank(WallModuleDefinition definition)
            => definition != null && definition.AllowLengthStretch && definition.IsValid(out _);

        private static Result CalculateLayout(FloorPlan plan, WallKit builder, bool includeDoorways,
            bool skipInvalidFamilies = false)
        {
            var result = new Result();
            var nodes = new Dictionary<string, Node>();
            var familyProblems = new Dictionary<WallFamily, string>();
            foreach (var corner in plan.Corners) nodes.Add(corner.Id, new Node { Id = corner.Id, Position = corner.Position });
            foreach (var wall in plan.Walls)
            {
                if (!wall.IsPhysical) continue;
                var family = builder.FamilyFor(plan, wall);
                if (family == null)
                {
                    if (skipInvalidFamilies) continue;
                    result.Problem = "Choose a complete wall family.";
                    return result;
                }
                if (!familyProblems.TryGetValue(family, out string familyProblem))
                {
                    family.IsValid(out familyProblem);
                    familyProblems.Add(family, familyProblem);
                }
                if (familyProblem.Length > 0)
                {
                    if (skipInvalidFamilies) continue;
                    result.Problem = familyProblem;
                    return result;
                }
                var definition = DefinitionFor(plan, wall, builder);
                if (skipInvalidFamilies && !UsableBlank(definition)) continue;
                Vector2 direction = (nodes[wall.EndId].Position - nodes[wall.StartId].Position).normalized;
                var depths = definition.ConnectionDepths;
                if (plan.TryGetExteriorOutward(wall, out var outward) && Vector2.Dot(Perpendicular(direction), outward) < 0)
                    depths = ReverseDepths(depths);
                nodes[wall.StartId].Branches.Add(new Branch { Wall = wall, LogicalWall = wall, Direction = direction, Definition = definition, Depths = depths });
                nodes[wall.EndId].Branches.Add(new Branch { Wall = wall, LogicalWall = wall, Direction = -direction, Definition = definition, Depths = ReverseDepths(depths) });
            }

            if (!result.IsValid) return result;
            var runs = PhysicalRuns(nodes, plan);
            AlignContinuations(nodes, runs, plan, result);
            foreach (var node in nodes.Values)
            {
                // Interior bends need no separate cover: match the square-end
                // face corners. Mixed facade/partition junctions keep facade fitting.
                bool interiorBend = node.Branches.Count == 2
                    && plan.GetEffectiveWallType(node.Branches[0].LogicalWall) == FloorPlan.WallType.Interior
                    && plan.GetEffectiveWallType(node.Branches[1].LogicalWall) == FloorPlan.WallType.Interior;
                if (interiorBend) result.OmittedCoverNodes.Add(node.Id);
                FitCorner(node, !interiorBend, plan);
                BuryContactingEnds(node);
                result.Junctions.Add(node.Id, node.FittedPosition - node.Position);
                bool exteriorCorner = ExteriorCorner(node, plan, out var a, out var b);
                if (!exteriorCorner && node.Branches.Count == 2) Continuation(node, out a, out b);
                if(!interiorBend && (node.Branches.Count == 2 || exteriorCorner) && a != null && UsesCornerCover(a.Direction,b.Direction))
                {
                    result.Corners.Add(FitCornerCover(node.Id,node.FittedPosition,a.LogicalWall.Id,b.LogicalWall.Id,
                        a.Direction,b.Direction,a.Depths,b.Depths));
                }
            }

            // Connected physical runs get different millimetre top insets. Logical
            // material/opening splits of one straight run keep the same height.
            var topSlots = new Dictionary<PhysicalRun, int>();
            foreach (var run in runs.OrderBy(r => r.StableId, System.StringComparer.Ordinal))
            {
                var used = new HashSet<int>();
                bool joined = false;
                var along = (nodes[run.EndId].Position - nodes[run.StartId].Position).normalized;
                foreach (var node in new[] { nodes[run.StartId], nodes[run.EndId] })
                    foreach (var branch in node.Branches)
                        if (Mathf.Abs(Vector2.Dot(along, branch.Direction)) < .999999f)
                        {
                            joined = true;
                            if (topSlots.TryGetValue(branch.Run, out int slot)) used.Add(slot);
                        }
                // Seed from the undirected axis, so adding a distant room does not
                // change ordinary straight runs. Resolve only local axis-bin ties.
                float angle = Mathf.Atan2(along.y, along.x);
                if (angle < 0) angle += Mathf.PI;
                int available = joined ? Mathf.RoundToInt(angle * 40 / Mathf.PI) % 40 : 0;
                while (used.Contains(available)) available++;
                topSlots.Add(run, available);
                foreach (var wall in run.Walls) result.TopInsets.Add(wall.Id, available * JoinClearance / 10);
            }
            foreach (var cover in result.Corners)
                cover.TopInset = nodes[cover.Id].Branches.Max(b => topSlots[b.Run]) * JoinClearance / 10 + JoinClearance;

            foreach (var run in runs)
            {
                var wall = run.Walls[0];
                var definition = run.Definition;
                var a = nodes[run.StartId];
                var b = nodes[run.EndId];
                Vector2 along = (b.Position - a.Position).normalized;
                float length = Vector2.Distance(a.Position, b.Position) - a.Offset(wall.Id) - b.Offset(wall.Id);
                if (length <= Tolerance)
                {
                    result.Reject("A wall is too short for its junctions. Increase its length or use a thinner wall type.", (a.Position + b.Position) / 2);
                    continue;
                }
                var span = new Span { WallId = wall.Id, Definition = definition,
                    Start = a.Position + run.Shift + along * a.Offset(wall.Id), End = b.Position + run.Shift - along * b.Offset(wall.Id),
                    Depths = run.Depths };
                result.Spans.Add(span);
            }
            // A third branch can already occupy the nominal missing corner.
            // The height owner omits it only if that wall covers its full height.
            for (int i = result.Corners.Count - 1; result.IsValid && i >= 0; i--)
            {
                var cover = result.Corners[i];
                var footprint = cover.Footprint();
                bool buried = false;
                for (int j = 0; j < runs.Count && !buried; j++)
                {
                    if (runs[j].StartId != cover.Id && runs[j].EndId != cover.Id) continue;
                    var span = result.Spans[j];
                    var along = (span.End - span.Start).normalized;
                    float length = Vector2.Distance(span.Start, span.End);
                    buried = footprint.All(point =>
                    {
                        var relative = point - span.Start;
                        float x = Vector2.Dot(relative, along), z = Vector2.Dot(relative, Perpendicular(along));
                        return x >= -JoinClearance * 2 && x <= length + JoinClearance * 2
                            && z >= span.Depths.x - Tolerance && z <= span.Depths.y + Tolerance;
                    });
                    if (buried) cover.CoveringWall = (runs[j].StartId == cover.Id ? runs[j].Walls[0]
                        : runs[j].Walls[runs[j].Walls.Count - 1]).Id;
                }
                if (!buried) continue;
                result.OmittedCoverNodes.Add(cover.Id);
            }
            if (result.IsValid) CheckClearances(runs, result);
            if (result.IsValid) CheckCornerClearances(runs,result);
            if (result.IsValid) RestoreLogicalSpans(plan, builder, runs, result);
            if (result.IsValid && includeDoorways) FitDoorways(plan, builder, result);
            return result;
        }

        public static WallModuleDefinition DefinitionFor(FloorPlan plan, FloorPlan.Wall wall, WallKit builder)
            => !wall.IsPhysical || builder == null ? null
                : builder.FamilyFor(plan, wall) is WallFamily family ? family.ReferenceBlank
                : null;

        public static Vector2 WallDepths(FloorPlan plan, FloorPlan.Wall wall, WallKit kit)
        {
            var module = DefinitionFor(plan, wall, kit);
            return module != null ? module.ConnectionDepths : Vector2.zero;
        }

        private static bool HasPhysicalWalls(FloorPlan plan)
        {
            foreach (var wall in plan.Walls) if (wall.IsPhysical) return true;
            return false;
        }

        public static bool ValidateKit(WallKit builder, out string problem)
        {
            problem = "Choose a complete wall family.";
            if (builder?.DefaultFamily == null || !builder.DefaultFamily.IsValid(out problem)) return false;
            return builder.ExteriorFamily == null || builder.ExteriorFamily.IsValid(out problem);
        }

        private static void FitCorner(Node node, bool allowCover, FloorPlan plan)
        {
            node.FittedPosition = node.Position;
            bool exteriorCorner = ExteriorCorner(node, plan, out var a, out var b);
            if (!exteriorCorner && !Continuation(node, out a, out b)) return;
            // Acute interior matching has two blunt silhouettes. Stable wall IDs
            // choose one so collection reordering cannot flip the visible join.
            if (!allowCover && Vector2.Dot(a.Direction, b.Direction) > Tolerance
                && string.CompareOrdinal(a.LogicalWall.Id, b.LogicalWall.Id) > 0)
                (a, b) = (b, a);
            float first = 0, second = 0;
            if (node.Branches.Count == 2 || exteriorCorner)
                CornerEnds(a.Direction, b.Direction, a.Depths, b.Depths, out first, out second, allowCover);
            // A lateral alignment can reach another bend. Fit its old square-end
            // rule at the intersection of the translated datums, including its cover.
            if (DatumIntersection(a, b, out var intersection))
            {
                first += Vector2.Dot(intersection - a.Run.Shift, a.Direction);
                second += Vector2.Dot(intersection - b.Run.Shift, b.Direction);
                node.FittedPosition += intersection;
            }
            // At a T/X, the closest directional pair owns the through surface.
            // Fit its common room corners; remaining legs still end against it.
            if (!exteriorCorner && node.Branches.Count > 2 && SharedFaceEnds(plan, a, b, out float faceA, out float faceB))
            { first = faceA; second = faceB; }
            node.Ends[a.Wall.Id] = first;
            node.Ends[b.Wall.Id] = second;
            foreach (var branch in node.Branches)
            {
                // Interior legs retain their midpoint endpoint at a facade corner.
                if (branch == a || branch == b || exteriorCorner) continue;
                // The terminating leg reaches the chosen through-wall datum;
                // it does not inherit that wall's thickness or sideways translation.
                var through = a.Depths.y - a.Depths.x <= b.Depths.y - b.Depths.x ? a : b;
                if (DatumIntersection(branch, through, out var meeting))
                    node.Ends[branch.Wall.Id] = Vector2.Dot(meeting - branch.Run.Shift, branch.Direction);
            }
        }

        private static void BuryContactingEnds(Node node)
        {
            var shorten = new List<Branch>();
            foreach (var branch in node.Branches)
                foreach (var other in node.Branches)
                {
                    if (branch == other || Mathf.Abs(Vector2.Dot(branch.Direction, other.Direction)) > .00001f) continue;
                    var tip = branch.Run.Shift + branch.Direction * node.Offset(branch.Wall.Id);
                    var normal = Perpendicular(other.Direction);
                    float face = Vector2.Dot(-branch.Direction, normal) > 0 ? other.Depths.y : other.Depths.x;
                    if (Mathf.Abs(Vector2.Dot(tip - other.Run.Shift, normal) - face) > .00001f) continue;
                    var side = Perpendicular(branch.Direction);
                    float from = Vector2.Dot(tip + side * branch.Depths.x - other.Run.Shift, other.Direction);
                    float to = Vector2.Dot(tip + side * branch.Depths.y - other.Run.Shift, other.Direction);
                    if (Mathf.Max(from, to) <= node.Offset(other.Wall.Id) + Tolerance) continue;
                    shorten.Add(branch);
                    break;
                }
            // Read all contacts before changing any end: branch enumeration order
            // must not decide which exposed cap remains coplanar.
            foreach (var branch in shorten) node.Ends[branch.Wall.Id] = node.Offset(branch.Wall.Id) + JoinClearance;
        }

        // Returns signed offsets along the two directions pointing away from a node.
        // Local +Z is the left normal of each direction, including at reversed ends.
        internal static void CornerEnds(Vector2 a, Vector2 b, Vector2 depthsA, Vector2 depthsB,
            out float first, out float second, bool allowCover = true)
        {
            first = second = 0;
            float cross = Cross(a, b);
            bool acute = Vector2.Dot(a, b) > Tolerance;
            if ((allowCover && (UsesCornerCover(a,b) || acute)) || Mathf.Abs(cross) < .001f) return;
            float faceA = cross > 0 ? depthsA.x : depthsA.y;
            // Same-side corners give acute interior bends a blunt overlap. For
            // equal symmetric sections this shortens one end and extends the
            // other by less than a half-thickness, without adding any geometry.
            float faceB = acute ? (cross > 0 ? depthsB.x : depthsB.y) : (cross > 0 ? depthsB.y : depthsB.x);
            var p = Perpendicular(a) * faceA;
            var q = Perpendicular(b) * faceB;
            var anchor = p + a * (Cross(q - p, b) / cross);
            float reach = 4 * Mathf.Max(Mathf.Abs(depthsA.x), Mathf.Abs(depthsA.y), Mathf.Abs(depthsB.x), Mathf.Abs(depthsB.y));
            // Nearly parallel unequal sections can place the intersection far away.
            // Use the same centre rule rather than create spikes or another join type.
            if (!float.IsFinite(anchor.x) || !float.IsFinite(anchor.y) || anchor.magnitude > reach) return;
            first = Vector2.Dot(anchor, a); second = Vector2.Dot(anchor, b);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static Vector2 Perpendicular(Vector2 v) => new Vector2(-v.y, v.x);

        private static Vector2[] Rectangle(Span span)
        {
            var side = Perpendicular((span.End - span.Start).normalized);
            return new[] { span.Start + side * span.Depths.x, span.End + side * span.Depths.x,
                span.End + side * span.Depths.y, span.Start + side * span.Depths.y };
        }

        private static void CheckClearances(List<PhysicalRun> runs, Result result)
        {
            var rectangles = result.Spans.ConvertAll(Rectangle);
            for (int i = 0; i < runs.Count; i++)
            for (int j = i + 1; j < runs.Count; j++)
            {
                var a = runs[i]; var b = runs[j];
                string shared = a.StartId == b.StartId || a.StartId == b.EndId ? a.StartId
                    : a.EndId == b.StartId || a.EndId == b.EndId ? a.EndId : null;
                if (shared == null)
                {
                    if (PolygonsOverlap(rectangles[i], rectangles[j]))
                        result.Reject("Wall thickness leaves overlapping pieces. Move the nearby walls apart or reduce their thickness.", result.Spans[i].Start);
                    continue;
                }
                // Intentional incident overlap is allowed. Openings must still clear
                // its actual footprint, including acute turns and unequal sections.
                var overlap = Intersection(rectangles[i], rectangles[j]);
                if (overlap.Count == 0) continue;
                ReserveOpeningEnd(result.Spans[i], a.StartId == shared, overlap);
                ReserveOpeningEnd(result.Spans[j], b.StartId == shared, overlap);
            }
        }

        private static void ReserveOpeningEnd(Span span, bool start, IReadOnlyList<Vector2> overlap)
        {
            var direction = (span.End - span.Start).normalized * (start ? 1 : -1);
            var origin = start ? span.Start : span.End;
            float distance = 0;
            foreach (var point in overlap) distance = Mathf.Max(distance, Vector2.Dot(point - origin, direction));
            if (start) span.StartClearance = Mathf.Max(span.StartClearance, distance);
            else span.EndClearance = Mathf.Max(span.EndClearance, distance);
        }

        // Convex rectangle intersection is only for opening clearance, never output
        // geometry. Both rendered modules keep all their square-ended faces.
        private static List<Vector2> Intersection(Vector2[] subject, Vector2[] clip)
        {
            var polygon = new List<Vector2>(subject);
            for (int edge = 0; edge < clip.Length && polygon.Count > 0; edge++)
            {
                var origin = clip[edge]; var along = clip[(edge + 1) % clip.Length] - origin;
                var next = new List<Vector2>();
                for (int i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                    float da = Cross(along, a - origin), db = Cross(along, b - origin);
                    if (da >= 0) next.Add(a);
                    if ((da >= 0) != (db >= 0)) next.Add(Vector2.LerpUnclamped(a, b, da / (da - db)));
                }
                polygon = next;
            }
            return polygon;
        }

        private static bool PolygonsOverlap(Vector2[] a, Vector2[] b)
        {
            foreach (var polygon in new[] { a, b })
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 axis = Perpendicular(polygon[(i + 1) % polygon.Length] - polygon[i]).normalized;
                    float minA = float.PositiveInfinity, maxA = float.NegativeInfinity;
                    float minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
                    foreach (var p in a) { float d = Vector2.Dot(p, axis); minA = Mathf.Min(minA, d); maxA = Mathf.Max(maxA, d); }
                    foreach (var p in b) { float d = Vector2.Dot(p, axis); minB = Mathf.Min(minB, d); maxB = Mathf.Max(maxB, d); }
                    if (maxA <= minB + Tolerance || maxB <= minA + Tolerance) return false;
                }
            return true;
        }
    }
}
