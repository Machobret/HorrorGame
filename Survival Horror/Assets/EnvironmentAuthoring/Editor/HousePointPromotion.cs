using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Promotion edits one highlighted owner. Derived crossings are never saved on both contours.
    internal static class HousePointPromotion
    {
        internal static bool Handle(HouseEditorSession session, HousePlan plan, SceneView view,
            ScenePointerInput input, HouseSceneTarget target)
        {
            var e = input.Event;
            if (session.ActiveMode != HouseEditorSession.Mode.Edit || !session.CanEditSource
                || !input.LeftMouseDown || e.clickCount != 2 || e.shift || e.control || e.command
                || input.Navigating) return false;
            var owners = Owners(plan, target, e.mousePosition, out bool ceiling);
            if (owners.Count == 0) return false;
            if (!TryPoint(plan, target, owners[0], e.mousePosition, out var raw)) return false;
            int revision = session.ContextRevision;
            string inputs = HouseEditInputs.SourceInputs(plan);
            if (owners.Count == 1)
            {
                var point = HouseSceneQueries.PromotionPosition(plan, ceiling, owners[0], raw, e.mousePosition, session);
                Insert(session, plan, owners[0], point, ceiling, revision, inputs);
            }
            else
            {
                var context = HouseEditingContext.Active;
                var menu = new GenericMenu();
                foreach (var owner in owners)
                {
                    var point = HouseSceneQueries.PromotionPosition(plan, ceiling, owner, raw, e.mousePosition, session);
                    menu.AddItem(new GUIContent("Insert point on " + OwnerLabel(plan, owner, ceiling)), false, () =>
                    {
                        // A menu may outlive its Scene context; it cannot revive an old edit target.
                        if (context == null || HouseEditingContext.Active != context) return;
                        Insert(session, plan, owner, point, ceiling, revision, inputs);
                    });
                }
                menu.ShowAsContext();
            }
            e.Use(); view.Repaint();
            return true;
        }

        internal static List<HouseSceneQueries.PlacementEdge> Owners(HousePlan plan, HouseSceneTarget target,
            Vector2 mouse, out bool ceiling)
        {
            ceiling = target.Ceiling;
            HouseSceneFeedback.PointKind kind;
            string id, boundaryId = null;
            int index = 0;
            if (target.Cut.HasValue && target.Cut.Value.Vertex < 0 && target.Cut.Value.Edge >= 0)
            {
                kind = HouseSceneFeedback.PointKind.Opening;
                id = target.Cut.Value.OpeningId;
                index = target.Cut.Value.Edge;
            }
            else if (target.Element.HasValue && (target.Element.Value.Kind == HouseEditSelection.Kind.Wall
                || target.Element.Value.Kind == HouseEditSelection.Kind.Divider))
            {
                var hit = target.Element.Value;
                kind = hit.Kind == HouseEditSelection.Kind.Wall ? HouseSceneFeedback.PointKind.Wall : HouseSceneFeedback.PointKind.Divider;
                id = hit.Id; boundaryId = hit.BoundaryId;
                if (kind == HouseSceneFeedback.PointKind.Divider)
                {
                    var building = HouseBuilding.For(plan);
                    var level = building?.Find(plan);
                    ceiling = level != null && building.Boundaries.FirstOrDefault(b => b.Id == boundaryId)?.LowerId == level.Id;
                }
            }
            else return new List<HouseSceneQueries.PlacementEdge>();
            var candidates = HouseSceneQueries.PlacementCandidates(plan, ceiling).Edges;
            var selected = candidates.FirstOrDefault(edge => edge.Kind == kind && edge.Id == id && edge.Index == index
                && (boundaryId == null || edge.BoundaryId == boundaryId));
            var owners = new List<HouseSceneQueries.PlacementEdge>();
            if (selected == null) return owners;
            owners.Add(selected);
            // A visible hover wins unless another owner occupies the same projected edge
            // at this click. Merely being within the attraction radius is not ambiguity.
            if (!HouseSceneQueries.PlacementEdgeProjection(plan, ceiling, selected, mouse, out var selectedGui, out float selectedPixels)
                || selectedPixels > 12) return owners;
            if (!TryPoint(plan, target, selected, mouse, out var raw)) return owners;
            var position = HouseSceneQueries.PromotionPosition(plan, ceiling, selected, raw, mouse);
            foreach (var edge in candidates)
            {
                if (edge == selected || edge.Kind == selected.Kind && edge.Id == selected.Id && edge.BoundaryId == selected.BoundaryId) continue;
                if (!OutlineGeometry.PointOnSegment(position, edge.Start, edge.End)
                    || Vector2.Distance(position, edge.Start) <= OutlineGeometry.Tolerance
                    || Vector2.Distance(position, edge.End) <= OutlineGeometry.Tolerance) continue;
                if (!HouseSceneQueries.PlacementEdgeProjection(plan, ceiling, edge, mouse, out var gui, out float pixels)
                    || Mathf.Abs(pixels - selectedPixels) > 1 || Vector2.Distance(gui, selectedGui) > 1) continue;
                owners.Add(edge);
            }
            return owners;
        }

        private static bool TryPoint(HousePlan plan, HouseSceneTarget target, HouseSceneQueries.PlacementEdge owner,
            Vector2 mouse, out Vector2 point)
        {
            if (target.Element?.Kind == HouseEditSelection.Kind.Wall)
            {
                // The visible wall hit already supplies its along-wall position. Projecting
                // that ray onto the floor would shift it in a perspective or side view.
                point = owner.Start + (owner.End - owner.Start).normalized * target.Element.Value.Along;
                return true;
            }
            float height = target.Cut?.Height ?? target.Element.Value.Height;
            return HouseSceneQueries.PointOnEditPlane(plan, mouse, height, out point);
        }

        internal static HouseEditResult Insert(HouseEditorSession session, HousePlan plan,
            HouseSceneQueries.PlacementEdge owner, Vector2 point, bool ceiling, int expectedContext, string expectedInputs)
        {
            if (plan == null || session.SessionPlan != plan || session.ContextRevision != expectedContext
                || session.ActiveMode != HouseEditorSession.Mode.Edit || !session.CanEditSource
                || HouseEditInputs.SourceInputs(plan) != expectedInputs)
            {
                session.Message = "The House or editing context changed. Choose the point again.";
                return HouseEditResult.Reject(session.Message);
            }
            HouseEditResult result;
            string nodeId = null;
            if (owner.Kind == HouseSceneFeedback.PointKind.Wall)
            {
                var direction = owner.End - owner.Start;
                float fraction = Vector2.Dot(point - owner.Start, direction) / direction.sqrMagnitude;
                result = HouseEditCommands.InsertNode(plan, owner.Id, fraction, out nodeId);
            }
            else if (owner.Kind == HouseSceneFeedback.PointKind.Divider)
                result = HouseSurfaceCommands.InsertDividerNode(plan, owner.BoundaryId, owner.Id, point, out nodeId);
            else result = HouseSlabCommands.InsertOpeningVertex(plan, owner.BoundaryId, owner.Id, owner.Index, point);
            session.Message = result.Message;
            if (result.Applied)
            {
                session.ClearSelection();
                if (owner.Kind == HouseSceneFeedback.PointKind.Opening) session.PickSlabOpening(owner.Id, ceiling);
                else session.Pick(owner.Kind == HouseSceneFeedback.PointKind.Wall ? HouseEditSelection.Kind.Node
                    : HouseEditSelection.Kind.DividerNode, nodeId, boundaryId: owner.BoundaryId);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            return result;
        }

        private static string OwnerLabel(HousePlan plan, HouseSceneQueries.PlacementEdge owner, bool ceiling)
        {
            string label = owner.Kind == HouseSceneFeedback.PointKind.Opening ? "slab opening"
                : owner.Kind == HouseSceneFeedback.PointKind.Divider ? "floor divider"
                : plan.Data.Walls.First(w => w.Id == owner.Id).IsPhysical ? "wall" : "room boundary";
            var ids = HouseSceneQueries.PlacementCandidates(plan, ceiling).Edges.Where(e => e.Kind == owner.Kind)
                .Select(e => e.Id).Distinct().ToList();
            return label + " " + (ids.IndexOf(owner.Id) + 1);
        }
    }
}
