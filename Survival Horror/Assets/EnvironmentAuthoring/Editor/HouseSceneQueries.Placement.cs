using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseSceneQueries
    {
        internal sealed class PlacementPoint
        {
            internal Vector2 Position;
            internal HouseSceneFeedback.PointKind Kind;
            internal FloorPlan.Corner Corner;
            internal string OwnerId;
        }

        internal sealed class PlacementEdge
        {
            internal HouseSceneFeedback.PointKind Kind;
            internal string Id, BoundaryId, StartId, EndId;
            internal int Index;
            internal Vector2 Start, End;
        }

        internal sealed class PlacementGeometry
        {
            internal readonly List<PlacementPoint> Points = new List<PlacementPoint>();
            internal readonly List<PlacementEdge> Edges = new List<PlacementEdge>();
        }

        internal readonly struct PlacementSnap
        {
            internal readonly Vector2 Position;
            internal readonly bool OnEdge;
            internal readonly bool IsPoint;
            internal readonly int PendingIndex;
            internal PlacementSnap(Vector2 position, bool onEdge = false, int pendingIndex = -1, bool isPoint = false)
            { Position = position; OnEdge = onEdge; PendingIndex = pendingIndex; IsPoint = isPoint; }
        }

        internal static bool GridSnapping(HouseEditorSession session) => session.SnapToGrid && Event.current?.shift != true;

        // Only the chosen slab participates. In ordinary placement this is the active
        // floor below, not the ceiling or a hidden level's saved contour.
        internal static PlacementGeometry PlacementCandidates(HousePlan plan, bool ceiling = false)
        {
            var key = (plan, ceiling);
            if (geometry != null && geometry.Placement.TryGetValue(key, out var cached)) return cached;
            var result = new PlacementGeometry();
            foreach (var node in plan.Data.Corners)
                result.Points.Add(new PlacementPoint { Position = node.Position, Corner = node,
                    Kind = plan.Data.Walls.Any(w => w.IsPhysical && (w.StartId == node.Id || w.EndId == node.Id))
                        ? HouseSceneFeedback.PointKind.Wall : HouseSceneFeedback.PointKind.Divider });
            foreach (var wall in plan.Data.Walls)
                result.Edges.Add(new PlacementEdge { Kind = HouseSceneFeedback.PointKind.Wall, Id = wall.Id,
                    StartId = wall.StartId, EndId = wall.EndId,
                    Start = plan.Data.GetPosition(wall.StartId), End = plan.Data.GetPosition(wall.EndId) });
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = level == null ? null : ceiling ? building.Above(level) : building.Below(level);
            if (boundary != null)
            {
                foreach (var node in boundary.DividerNodes)
                    result.Points.Add(new PlacementPoint { Position = node.Position, OwnerId = node.Id, Kind = HouseSceneFeedback.PointKind.Divider });
                foreach (var divider in boundary.Dividers)
                    result.Edges.Add(new PlacementEdge { Kind = HouseSceneFeedback.PointKind.Divider,
                        Id = divider.Id, BoundaryId = boundary.Id,
                        StartId = divider.StartId, EndId = divider.EndId,
                        Start = boundary.DividerNodes.First(n => n.Id == divider.StartId).Position,
                        End = boundary.DividerNodes.First(n => n.Id == divider.EndId).Position });
                foreach (var cut in boundary.Openings)
                    for (int i = 0; i < cut.Points.Count; i++)
                    {
                        result.Points.Add(new PlacementPoint { Position = cut.Points[i], OwnerId = cut.Id, Kind = HouseSceneFeedback.PointKind.Opening });
                        result.Edges.Add(new PlacementEdge { Kind = HouseSceneFeedback.PointKind.Opening,
                            Id = cut.Id, BoundaryId = boundary.Id, Index = i,
                            Start = cut.Points[i], End = cut.Points[(i + 1) % cut.Points.Count] });
                    }
            }
            geometry?.Placement.Add(key, result);
            return result;
        }

        internal static bool PlacementPointVisible(HousePlan plan, bool ceiling, PlacementPoint point)
        {
            if (!ceiling && (point.Kind == HouseSceneFeedback.PointKind.Divider || point.Kind == HouseSceneFeedback.PointKind.Intersection)
                && !OnEditableFloor(plan, point.Position)) return false;
            if (!ceiling && point.Corner != null) return VisibleNode(plan, point.Corner);
            // A divider endpoint can belong to the same physical wall as an
            // intersection. Its own wall must not occlude that attachment point.
            var wall = plan.Data.Walls.FirstOrDefault(w => w.IsPhysical && OutlineGeometry.PointOnSegment(point.Position,
                plan.Data.GetPosition(w.StartId), plan.Data.GetPosition(w.EndId)));
            return VisibleWorld(plan, SlabWorld(plan, ceiling, point.Position), ownSurface: wall == null ? null : WallSurface(plan, wall));
        }

        internal static IEnumerable<PlacementPoint> NearbyIntersections(HousePlan plan, Vector2 raw, bool ceiling = false)
        {
            var edges = PlacementCandidates(plan, ceiling).Edges.Where(edge =>
            {
                var direction = edge.End - edge.Start;
                var closest = edge.Start + direction * Mathf.Clamp01(Vector2.Dot(raw - edge.Start, direction) / direction.sqrMagnitude);
                return PlacementGuideOpacity(plan, raw, closest) > 0;
            }).ToArray();
            var shown = new List<Vector2>();
            for (int i = 0; i < edges.Length; i++)
                for (int j = i + 1; j < edges.Length; j++)
                {
                    var a = edges[i]; var b = edges[j];
                    // Each authored graph already owns its internal junctions. Only
                    // crossings between independent contours need derived guide points.
                    if (a.Kind == b.Kind && (a.Kind != HouseSceneFeedback.PointKind.Opening || a.Id == b.Id)) continue;
                    if (!OutlineGeometry.TryInteriorIntersection(a.Start, a.End, b.Start, b.End, out float t)) continue;
                    var position = Vector2.Lerp(b.Start, b.End, t);
                    if (PlacementGuideOpacity(plan, raw, position) <= 0
                        || shown.Any(point => Vector2.Distance(position, point) <= OutlineGeometry.Tolerance)) continue;
                    shown.Add(position);
                    yield return new PlacementPoint { Position = position, Kind = HouseSceneFeedback.PointKind.Intersection,
                        OwnerId = a.Kind == HouseSceneFeedback.PointKind.Wall ? a.Id : b.Kind == HouseSceneFeedback.PointKind.Wall ? b.Id : null };
                }
        }

        internal static float PlacementGuideOpacity(HousePlan plan, Vector2 raw, Vector2 point)
        {
            float distance = Vector3.Distance(plan.ToWorld(raw, 0), plan.ToWorld(point, 0));
            float t = Mathf.Clamp01(1 - distance / 10);
            return t * t * (3 - 2 * t);
        }

        internal static bool PlacementEdgeProjection(HousePlan plan, bool ceiling, PlacementEdge edge, Vector2 mouse,
            out Vector2 guiPoint, out float pixels)
        {
            guiPoint = default;
            pixels = float.PositiveInfinity;
            var wall = edge.Kind == HouseSceneFeedback.PointKind.Wall ? plan.Data.Walls.First(w => w.Id == edge.Id) : null;
            foreach (var section in PlacementEdgeSections(plan, ceiling, edge))
            {
                var world = ScreenSegmentPoint(section.Start, section.End, mouse);
                var projected = HandleUtility.WorldToGUIPoint(world);
                float distance = Vector2.Distance(mouse, projected);
                if (distance >= pixels || !VisibleWorld(plan, world, ownSurface: wall == null ? null : WallSurface(plan, wall))) continue;
                guiPoint = projected; pixels = distance;
            }
            return float.IsFinite(pixels);
        }

        private static IEnumerable<SlabEdge> PlacementEdgeSections(HousePlan plan, bool ceiling, PlacementEdge edge)
            => !ceiling && edge.Kind == HouseSceneFeedback.PointKind.Divider
                ? ConstructionPieces(plan, edge.Start, edge.End)
                : SlabEdges(plan, ceiling, new[] { edge.Start, edge.End }, false);

        internal static PlacementSnap ResolvePlacement(HousePlan plan, HouseEditorSession session,
            Vector2 raw, Vector2 mouse, bool ceiling = false, IReadOnlyList<Vector2> pending = null,
            string excludedOpening = null, string excludedEdge = null, IReadOnlyCollection<string> excludedNodes = null)
        {
            // Proposed polygon closure is part of the same resolver as existing nodes.
            // Shift bypasses grid quantization only; closure and edge attraction remain.
            int pendingIndex = pending == null ? -1 : PendingSlabPoint(plan, ceiling, pending, mouse);
            if (pendingIndex >= 0) return new PlacementSnap(pending[pendingIndex], true, pendingIndex, isPoint: true);
            var candidates = PlacementCandidates(plan, ceiling);
            float nearest = 12;
            Vector2? node = null;
            foreach (var point in candidates.Points)
            {
                if (PlacementGuideOpacity(plan, raw, point.Position) <= 0) continue;
                if (point.OwnerId == excludedOpening && excludedOpening != null) continue;
                if (excludedNodes != null && excludedNodes.Contains(point.Corner?.Id ?? point.OwnerId)) continue;
                float pixels = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(SlabWorld(plan, ceiling, point.Position)));
                if (pixels >= nearest || !PlacementPointVisible(plan, ceiling, point)) continue;
                nearest = pixels; node = point.Position;
            }
            if (node.HasValue) return new PlacementSnap(node.Value, true, isPoint: true);
            nearest = 7;
            Vector2? edgePoint = null;
            var nearbyEdges = new List<PlacementEdge>();
            foreach (var edge in candidates.Edges)
            {
                if (edge.Id == excludedOpening || edge.Id == excludedEdge) continue;
                // A single moving endpoint may slide along its original wall. Skip
                // an edge only when both its endpoints are moving with the selection.
                if (excludedNodes != null && excludedNodes.Contains(edge.StartId) && excludedNodes.Contains(edge.EndId)) continue;
                foreach (var section in PlacementEdgeSections(plan, ceiling, edge))
                {
                    var world = ScreenSegmentPoint(section.Start, section.End, mouse);
                    float pixels = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(world));
                    if (pixels < 12 && !nearbyEdges.Contains(edge)) nearbyEdges.Add(edge);
                    if (pixels >= nearest) continue;
                    var wall = edge.Kind == HouseSceneFeedback.PointKind.Wall ? plan.Data.Walls.First(w => w.Id == edge.Id) : null;
                    if (!VisibleWorld(plan, world, ownSurface: wall == null ? null : WallSurface(plan, wall))) continue;
                    var local = plan.transform.InverseTransformPoint(world);
                    var localStart = plan.transform.InverseTransformPoint(section.Start);
                    var localEnd = plan.transform.InverseTransformPoint(section.End);
                    nearest = pixels;
                    edgePoint = SnapOnEdge(session, new Vector2(local.x, local.z),
                        new Vector2(localStart.x, localStart.z), new Vector2(localEnd.x, localEnd.z));
                }
            }
            // Only edges near the cursor can supply a nearby intersection. Avoid a
            // quadratic sweep of the entire floor on each mouse event.
            nearest = 12;
            Vector2? intersection = null;
            for (int i = 0; i < nearbyEdges.Count; i++)
                for (int j = i + 1; j < nearbyEdges.Count; j++)
                {
                    var a = nearbyEdges[i]; var b = nearbyEdges[j];
                    if (!OutlineGeometry.TryInteriorIntersection(a.Start, a.End, b.Start, b.End, out float t)) continue;
                    var position = Vector2.Lerp(b.Start, b.End, t);
                    if (!ceiling && (a.Kind == HouseSceneFeedback.PointKind.Divider || b.Kind == HouseSceneFeedback.PointKind.Divider)
                        && !OnEditableFloor(plan, position)) continue;
                    var world = SlabWorld(plan, ceiling, position);
                    float pixels = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(world));
                    var wallEdge = a.Kind == HouseSceneFeedback.PointKind.Wall ? a : b.Kind == HouseSceneFeedback.PointKind.Wall ? b : null;
                    var wall = wallEdge == null ? null : plan.Data.Walls.First(w => w.Id == wallEdge.Id);
                    if (pixels >= nearest || !VisibleWorld(plan, world, ownSurface: wall == null ? null : WallSurface(plan, wall))) continue;
                    nearest = pixels; intersection = position;
                }
            if (intersection.HasValue) return new PlacementSnap(intersection.Value, true, isPoint: true);
            return edgePoint.HasValue ? new PlacementSnap(edgePoint.Value, true) : new PlacementSnap(Snap(session, raw));
        }

        internal static Vector2 SnapOnEdge(HouseEditorSession session, Vector2 point, Vector2 start, Vector2 end)
        {
            var direction = end - start;
            if (direction.sqrMagnitude < .00000001f) return start;
            float fraction = Mathf.Clamp01(Vector2.Dot(point - start, direction) / direction.sqrMagnitude);
            if (GridSnapping(session))
            {
                // A diagonal/off-grid edge cannot generally meet both grid axes.
                // Step its dominant coordinate on the existing grid, then retain
                // exact incidence on the edge instead of rounding both coordinates.
                bool x = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
                float coordinate = x ? start.x + direction.x * fraction : start.y + direction.y * fraction;
                float snapped = Mathf.Round(coordinate / session.GridSize) * session.GridSize;
                fraction = (snapped - (x ? start.x : start.y)) / (x ? direction.x : direction.y);
            }
            return Vector2.Lerp(start, end, Mathf.Clamp01(fraction));
        }

        internal static float SnapOpeningDistance(HousePlan plan, HouseEditorSession session, FloorPlan.Wall wall, float along)
        {
            var start = plan.Data.GetPosition(wall.StartId);
            var end = plan.Data.GetPosition(wall.EndId);
            var raw = start + (end - start).normalized * along;
            var mouse = HandleUtility.WorldToGUIPoint(NodeWorld(plan, raw));
            var snap = ResolvePlacement(plan, session, raw, mouse, excludedEdge: wall.Id);
            if (snap.OnEdge) along = Vector2.Dot(snap.Position - start, (end - start).normalized);
            if ((snap.IsPoint && OutlineGeometry.PointOnSegment(snap.Position, start, end)) || !GridSnapping(session)) return along;
            return Vector2.Dot(SnapOnEdge(session, start + (end - start).normalized * along, start, end) - start,
                (end - start).normalized);
        }

        internal static Vector2 SnapDividerAnchor(HouseEditPreview preview, HouseEditorSession session,
            HouseStack.Boundary boundary, HouseStack.DividerNode anchor, IReadOnlyCollection<string> moving,
            Vector2 raw, PlacementSnap snap)
        {
            if (!GridSnapping(session)) return snap.Position;
            var host = anchor.Attachment;
            if (host == null) return snap.Position;
            Vector2 start, end;
            if (!string.IsNullOrEmpty(host.OpeningId))
            {
                if (!HouseDividerAttachments.TryOpeningSpan(boundary, host, out start, out end)) return snap.Position;
            }
            else if (string.IsNullOrEmpty(host.LevelId))
            {
                // A moved host belongs to the group transform, not this stationary
                // constraint. Snap one anchor only, preserving existing group spacing.
                if (moving.Contains(host.StartId) || moving.Contains(host.EndId)) return snap.Position;
                var a = boundary.DividerNodes.FirstOrDefault(n => n.Id == host.StartId);
                var b = boundary.DividerNodes.FirstOrDefault(n => n.Id == host.EndId);
                if (a == null || b == null) return snap.Position;
                start = a.Position; end = b.Position;
            }
            else
            {
                var plan = preview.OriginalPlanFor(preview.OriginalBuilding.Find(host.LevelId));
                var a = plan?.Corners.FirstOrDefault(n => n.Id == host.StartId);
                var b = plan?.Corners.FirstOrDefault(n => n.Id == host.EndId);
                if (a == null || b == null) return snap.Position;
                start = a.Position; end = b.Position;
            }
            if (snap.IsPoint && OutlineGeometry.PointOnSegment(snap.Position, start, end)) return snap.Position;
            // Use the unsnapped cursor when no geometry attracted it: projecting a
            // separately rounded 2D grid point would lose the along-host grid again.
            return SnapOnEdge(session, snap.OnEdge ? snap.Position : raw, start, end);
        }

        internal static Vector2 PromotionPosition(HousePlan plan, bool ceiling, PlacementEdge owner, Vector2 raw, Vector2 mouse,
            HouseEditorSession session = null)
        {
            var direction = owner.End - owner.Start;
            // Owner disambiguation omits session to inspect the actual hit, without
            // letting a grid step move the click onto another owner's geometry.
            var point = session == null ? owner.Start + direction * Mathf.Clamp01(Vector2.Dot(raw - owner.Start, direction) / direction.sqrMagnitude)
                : SnapOnEdge(session, raw, owner.Start, owner.End);
            float nearest = 12;
            foreach (var intersection in DrawingIntersections(plan, new[] { owner.Start, owner.End }, false, true, ceiling))
            {
                if (Vector2.Distance(intersection, owner.Start) <= OutlineGeometry.Tolerance
                    || Vector2.Distance(intersection, owner.End) <= OutlineGeometry.Tolerance) continue;
                float pixels = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(SlabWorld(plan, ceiling, intersection)));
                if (pixels >= nearest) continue;
                nearest = pixels; point = intersection;
            }
            return point;
        }
    }
}
