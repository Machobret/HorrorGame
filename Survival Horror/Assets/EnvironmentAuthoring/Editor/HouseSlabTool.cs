using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseSlabTool
    {
        private readonly HouseEditorSession session;
        private readonly HouseEditorController controller;
        private readonly HouseSceneFeedback feedback;
        private bool slabCeiling => session.SlabCeiling;
        private bool slabPolygon => session.SlabPolygon;
        private string slabSelection => session.SlabSelection;
        private bool geometryClaimed;
        private HouseEditPreview preview => geometryClaimed && session.Pending.ActiveKind == HousePendingEdit.Kind.Geometry
            ? session.Pending.Preview : null;
        private readonly List<Vector2> slabPending = new List<Vector2>();
        private Vector2? slabStart;
        private int slabVertex = -1;
        private Vector2 slabDragOrigin;
        private Vector2 mouseDown;
        private string dragInputs;
        private float slabDragHeight;
        private List<Vector2> slabOriginal, slabPreview;
        private bool consumeRightRelease;
        internal HouseSlabTool(HouseEditorSession session, HouseSceneFeedback feedback, HouseEditorController controller)
        {
            this.session = session;
            this.controller = controller;
            this.feedback = feedback;
        }

        internal bool HasGesture => HasPointerCapture || slabStart.HasValue || slabPending.Count > 0;
        internal bool HasPointerCapture => controller.CapturedBy(HouseEditorController.PointerOwner.Slab);
        internal IReadOnlyList<Vector2> PendingPoints => slabPending;

        internal void Cancel()
        {
            controller.ReleasePointer(HouseEditorController.PointerOwner.Slab);
            slabPending.Clear();
            slabStart = null;
            slabOriginal = null;
            slabPreview = null;
            dragInputs = null;
            ReleaseGeometry();
            consumeRightRelease = false;
        }

        internal void ClearSelection() => session.ClearSlabSelection();

        internal bool HasSelection
        {
            get
            {
                var building = HouseBuilding.For(session.SessionPlan);
                if (building == null || string.IsNullOrEmpty(slabSelection)) return false;
                return SelectionBoundary(building, session.SessionPlan)?.Openings.Any(o => o.Id == slabSelection) == true;
            }
        }

        internal HouseStack.Boundary SlabBoundary(HouseBuilding building, HousePlan plan)
        {
            var level = building.Find(plan);
            return slabCeiling ? building.Above(level) : building.Below(level);
        }

        internal bool Handle(HousePlan plan, SceneView view, bool drawing, ScenePointerInput input, HouseSceneTarget target)
        {
            var building = HouseBuilding.For(plan);
            if (building == null)
                return false;
            // Hovering the opposite boundary must not change the selected cut's side.
            // Capture a new side only when beginning an actual cut interaction.
            bool ceiling = drawing ? slabCeiling : HasPointerCapture ? session.SelectedSlabCeiling
                : target.Cut.HasValue ? target.Ceiling : session.SelectedSlabCeiling;
            var level = building.Find(plan);
            var boundary = ceiling ? building.Above(level) : building.Below(level);
            if (boundary == null)
                return false;
            var e = input.Event;
            int control = input.ControlId;
            if (e.type == EventType.MouseUp && e.button == 1 && consumeRightRelease)
            {
                consumeRightRelease = false;
                e.Use();
                return true;
            }
            if (drawing && input.RightMouseDown && (slabPending.Count > 0 || slabStart.HasValue))
            {
                if (SourceUnchanged())
                {
                    if (slabPending.Count > 0) slabPending.RemoveAt(slabPending.Count - 1);
                    if (slabPending.Count == 0) Cancel();
                    session.Message = HasGesture ? "Last point removed. Continue drawing." : "Proposed slab opening cancelled.";
                }
                consumeRightRelease = true;
                e.Use(); view.Repaint();
                return true;
            }
            var hit = target.Cut;
            var hover = hit.HasValue ? boundary.Openings.Find(o => o.Id == hit.Value.OpeningId) : null;
            int vertex = hit?.Vertex ?? -1;

            var selectedBoundary = SelectionBoundary(building, plan);
            var selected = selectedBoundary?.Openings.Find(o => o.Id == slabSelection);
            bool engaged = drawing || HasPointerCapture || hover != null;
            if (!engaged)
                return false;
            if (input.Navigating)
                return false;

            float height = HasPointerCapture ? slabDragHeight
                : hit?.Height ?? HouseSceneQueries.SlabHeight(plan, ceiling);
            Vector2 point;
            bool onPlane = HasPointerCapture || hit.HasValue
                ? HouseSceneQueries.PointOnEditPlane(plan, e.mousePosition, height, out point)
                : HouseSceneQueries.PointOnSlab(plan, slabCeiling, e.mousePosition, out point, out height);
            if (!onPlane)
            {
                // Releasing outside the edit plane cannot produce a position, but
                // must still end capture. Keep the accepted cut unchanged.
                if (e.type == EventType.MouseUp && e.button == 0 && HasPointerCapture)
                { Cancel(); e.Use(); }
                return engaged;
            }
            var rawPoint = point;
            feedback.DrawPlacementGuides(plan, point, ceiling);
            bool movingVertex = HasPointerCapture && slabOriginal != null && slabVertex >= 0;
            // Preserve the grip offset first, then attract the authored vertex. Snapping
            // the cursor delta leaves an offset when Shift changes during the drag.
            var proposedPoint = movingVertex ? slabOriginal[slabVertex] + rawPoint - slabDragOrigin : rawPoint;
            var proposedMouse = movingVertex ? HandleUtility.WorldToGUIPoint(HouseSceneQueries.SlabWorld(plan, ceiling, proposedPoint)) : e.mousePosition;
            var snap = HouseSceneQueries.ResolvePlacement(plan, session, proposedPoint, proposedMouse, ceiling,
                drawing && slabPolygon ? slabPending : null, HasPointerCapture ? slabSelection : null);
            point = snap.Position;
            int pendingPoint = snap.PendingIndex;
            if (HouseSceneQueries.GridSnapping(session) && (drawing || HasPointerCapture))
                feedback.DrawSnapGrid(plan, point, height, session.GridSize);
            if (input.LeftMouseDown)
            {
                if (hover != null && slabPending.Count == 0)
                {
                    session.PickSlabOpening(hover.Id, ceiling);
                    // A cut click is selection only. Borrow a candidate only if
                    // the pointer actually moves; retained property values stay intact.
                    dragInputs = HouseEditInputs.SourceInputs(plan);
                    mouseDown = e.mousePosition;
                    slabOriginal = hover.Points.ToList();
                    slabDragOrigin = vertex >= 0 ? rawPoint : point;
                    // Keep the plane captured at the hovered edge stable for the whole drag.
                    slabDragHeight = height;
                    slabVertex = vertex;
                    controller.CapturePointer(HouseEditorController.PointerOwner.Slab, view, control);
                }
                else if (drawing && slabPolygon)
                {
                    if (slabPending.Count == 0 && !BeginGeometry(plan)) { e.Use(); return true; }
                    bool first = slabPending.Count >= 3 && pendingPoint == 0;
                    bool last = slabPending.Count >= 3 && pendingPoint == slabPending.Count - 1;
                    if (first || last)
                        CommitSlab(building, boundary, slabPending);
                    else if (slabPending.Count == 0 || (point - slabPending.Last()).sqrMagnitude > OutlineGeometry.Tolerance * OutlineGeometry.Tolerance)
                        slabPending.Add(point);
                }
                else if (drawing)
                {
                    if (!BeginGeometry(plan)) { e.Use(); return true; }
                    slabStart = point;
                    slabDragHeight = height;
                    controller.CapturePointer(HouseEditorController.PointerOwner.Slab, view, control);
                    slabOriginal = null;
                }

                e.Use();
                view.Repaint();
                return true;
            }

            if ((e.type == EventType.MouseDrag || input.SnapModifierChanged)
                && HasPointerCapture)
            {
                if (slabOriginal != null)
                {
                    if (!geometryClaimed)
                    {
                        if ((e.mousePosition - mouseDown).sqrMagnitude < 16) { e.Use(); return true; }
                        if (dragInputs != HouseEditInputs.SourceInputs(plan))
                        {
                            Cancel();
                            session.Message = "The House changed during this gesture. Try the edit again.";
                            e.Use(); return true;
                        }
                        if (!BeginGeometry(plan)) { Cancel(); e.Use(); return true; }
                    }
                    var delta = point - slabDragOrigin;
                    slabPreview = slabOriginal.Select((p, i) => slabVertex < 0 ? p + delta : i == slabVertex ? point : p).ToList();
                }

                e.Use();
                view.Repaint();
                return true;
            }

            if (e.type == EventType.MouseUp && e.button == 0 && HasPointerCapture)
            {
                if (slabOriginal != null && slabPreview != null)
                {
                    if (SourceUnchanged())
                    {
                        var result = HouseSlabCommands.SetSlabOpening(preview, boundary.Id, slabSelection, slabPreview, "Move slab opening");
                        session.Message = result.Message;
                    }
                }
                else if (slabStart.HasValue)
                    CommitSlab(building, boundary, Rectangle(slabStart.Value, point));
                slabStart = null;
                slabOriginal = null;
                slabPreview = null;
                ReleaseGeometry();
                controller.ReleasePointer(HouseEditorController.PointerOwner.Slab);
                e.Use();
                return true;
            }

            if (drawing && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && slabPending.Count >= 3)
            {
                CommitSlab(building, boundary, slabPending);
                e.Use();
                return true;
            }

            if (slabStart.HasValue)
            {
                feedback.DrawSlabLine(plan, slabCeiling, Rectangle(slabStart.Value, point), HouseSceneFeedback.HoverColor, true);
                if (e.type == EventType.Repaint)
                    feedback.DrawIntersectionPoints(plan, HouseSceneQueries.DrawingIntersections(plan, Rectangle(slabStart.Value, point), true, false, slabCeiling), slabCeiling);
            }
            if (slabPending.Count > 0)
            {
                feedback.DrawSlabLine(plan, slabCeiling, slabPending.Concat(new[]{point}).ToArray(), HouseSceneFeedback.HoverColor, false);
                feedback.DrawSlabPoints(plan, slabCeiling, slabPending, HouseSceneFeedback.HoverColor);
                if (e.type == EventType.Repaint)
                    feedback.DrawIntersectionPoints(plan, HouseSceneQueries.DrawingIntersections(plan, slabPending.Concat(new[]{point}).ToArray(), false, false, slabCeiling), slabCeiling);
                feedback.DrawSlabPoints(plan, slabCeiling, new[]{point}, HouseSceneFeedback.HoverColor);
                session.Message = slabPending.Count >= 3 && (pendingPoint == 0 || pendingPoint == slabPending.Count - 1)
                    ? "Click this point to complete the slab opening." : "Click to add a point; click the first point or press Enter to finish.";
            }
            if (e.type == EventType.MouseMove)
                view.Repaint();
            return engaged;
        }

        internal HouseStack.Boundary SelectionBoundary(HouseBuilding building, HousePlan plan)
        {
            var item = session.SelectedSlabCut;
            return item == null ? null : building.Boundaries.FirstOrDefault(boundary => boundary.Id == item.BoundaryId);
        }

        internal void DrawFeedback(HousePlan plan, bool drawing, HouseSceneTarget target)
        {
            if (Event.current.type != EventType.Repaint) return;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            var level = building.Find(plan);
            var selectedBoundary = SelectionBoundary(building, plan);
            foreach (bool ceiling in new[] { false, true })
            {
                var boundary = ceiling ? building.Above(level) : building.Below(level);
                if (boundary == null) continue;
                foreach (var opening in boundary.Openings)
                {
                    bool selected = boundary.Id == selectedBoundary?.Id && opening.Id == slabSelection;
                    bool hovered = target.Cut.HasValue && target.Ceiling == ceiling && opening.Id == target.Cut.Value.OpeningId;
                    if (!selected && !hovered && !(drawing && ceiling == slabCeiling)) continue;
                    var points = selected && slabPreview != null ? slabPreview : opening.Points;
                    feedback.DrawSlabLine(plan, ceiling, points, selected ? HouseSceneFeedback.SelectionColor : HouseSceneFeedback.HoverColor, true);
                    if (selected || hovered) feedback.DrawSlabArea(plan, boundary, ceiling, opening);
                    if (selected) feedback.DrawSlabPoints(plan, ceiling, points, HouseSceneFeedback.SelectionColor);
                }
            }
        }

        private static List<Vector2> Rectangle(Vector2 a, Vector2 b) => new List<Vector2>{a, new Vector2(b.x, a.y), b, new Vector2(a.x, b.y)};
        private void CommitSlab(HouseBuilding building, HouseStack.Boundary boundary, IReadOnlyList<Vector2> points)
        {
            if (!SourceUnchanged())
                return;
            var result = HouseSlabCommands.SetSlabOpening(preview, boundary.Id, null, points);
            session.Message = result.Message;
            if (!result.Applied)
                return;
            session.PickSlabOpening(building.Boundaries.Find(b => b.Id == boundary.Id).Openings.Last().Id, slabCeiling);
            slabPending.Clear();
            ReleaseGeometry();
        }

        private bool BeginGeometry(HousePlan plan)
        {
            if (geometryClaimed) return true;
            geometryClaimed = session.Pending.TryBeginGeometry(plan, out string reason);
            if (!geometryClaimed) session.Message = reason;
            return geometryClaimed;
        }

        private void ReleaseGeometry()
        {
            if (geometryClaimed) session.Pending.ReleaseGeometry();
            geometryClaimed = false;
        }

        private bool SourceUnchanged()
        {
            // A gesture belongs to the inputs at its start, including the kit settings.
            if (preview != null && !preview.HasConflict) return true;
            session.Message = "The House changed while editing this opening. Please start again.";
            Cancel();
            return false;
        }
    }
}
