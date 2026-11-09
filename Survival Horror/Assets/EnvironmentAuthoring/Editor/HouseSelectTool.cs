using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseSelectTool
    {
        private static readonly Unity.Profiling.ProfilerMarker moveSample = new Unity.Profiling.ProfilerMarker("House.SelectionMovePreview");
        private readonly HouseEditorSession session;
        private readonly HouseEditorController controller;
        private readonly HouseSceneFeedback feedback;
        private HouseEditSelection editSelection => session.Selection;

        internal HouseSelectTool(HouseEditorSession session, HouseSceneFeedback feedback, HouseEditorController controller)
        {
            this.session = session;
            this.controller = controller;
            this.feedback = feedback;
        }

        internal bool HasGesture => controller.CapturedBy(HouseEditorController.PointerOwner.Selection);
        internal bool HasPreview => editPreview != null;
        private Vector2 mouseDown, mouseNow, dragOrigin;
        private float dragHeight;
        private bool boxSelecting, boxAdd, boxSubtract, dragging, canDrag;
        private bool roomMouseDown;
        private HouseSceneHit? singlePickOnRelease;
        private bool geometryClaimed;
        private bool movePreviewRequested;
        private bool previewCurrent = true;
        private HouseEditPreview editPreview => geometryClaimed && session.Pending.ActiveKind == HousePendingEdit.Kind.Geometry ? session.Pending.Preview : null;
        private Dictionary<string, Vector2> movingNodes;
        private Dictionary<string, float> movingOpenings;
        private Dictionary<string, Vector2> movingDividers;
        private string dividerBoundary;
        private string dragInputs;

        internal void Handle(HousePlan plan, SceneView view, ScenePointerInput input, HouseSceneHit? hover)
        {
            var e = input.Event;
            int control = input.ControlId;
            if (input.Navigating) return;
            if (e.type == EventType.ContextClick && !HasGesture
                && !HouseSceneQueries.PickControl(plan, e.mousePosition, editSelection).HasValue)
            {
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (RoomSelectionGeometry.PickSegment(plan, ray, true, out var surface, out _, out float distance)
                    && HouseSceneQueries.PatchAreas(plan, surface)[0].Floor.Count == 0
                    && HouseSceneQueries.VisibleWorld(plan, ray.GetPoint(distance)))
                {
                    var menu = new GenericMenu();
                    menu.AddDisabledItem(new GUIContent(HouseSlabProperties.BoundaryLabel(HouseBuilding.For(plan), surface.Boundary)));
                    menu.AddSeparator("");
                    var cut = HouseSceneQueries.PickSlabOpening(plan, surface.Boundary, false, e.mousePosition);
                    if (cut.HasValue)
                    {
                        string cutId = cut.Value.OpeningId;
                        menu.AddItem(new GUIContent("Select slab opening"), false,
                            () => { if (session.SessionPlan != plan) return; session.PickSlabOpening(cutId, false); SyncSelection(); });
                        menu.AddSeparator("");
                    }
                    menu.AddItem(new GUIContent("Select cut-away floor area"), false,
                        () => { if (session.SessionPlan != plan) return; session.Pick(HouseEditSelection.Kind.SurfaceRegion, surface.Floor.Id,
                            boundaryId: surface.Boundary.Id); SyncSelection(); });
                    menu.ShowAsContext(); e.Use(); return;
                }
            }
            if (e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == control)
            {
                mouseDown = mouseNow = e.mousePosition;
                boxAdd = e.shift; boxSubtract = e.control || e.command;
                boxSelecting = !hover.HasValue;
                roomMouseDown = hover.HasValue && (hover.Value.Kind == HouseEditSelection.Kind.Room || hover.Value.Kind == HouseEditSelection.Kind.SurfaceRegion);
                canDrag = false;
                if (hover.HasValue)
                {
                    var hit = hover.Value;
                    // Clicking an already selected group member narrows selection on release;
                    // dragging it still moves the group without requiring Shift to stay held.
                    if (!boxAdd && !boxSubtract && editSelection.Items.Count > 1 && editSelection.Contains(hit.Kind, hit.Id, hit.BoundaryId))
                    {
                        singlePickOnRelease = hit;
                        session.ClearSlabSelection();
                    }
                    else session.Pick(hit.Kind, hit.Id, boxAdd, boxSubtract, hit.BoundaryId);
                    dragHeight = hit.Height;
                    canDrag = !boxSubtract && editSelection.Items.All(i => i.Type == HouseEditSelection.Kind.Node
                        || i.Type == HouseEditSelection.Kind.Wall || i.Type == HouseEditSelection.Kind.Opening
                        || i.Type == HouseEditSelection.Kind.DividerNode || i.Type == HouseEditSelection.Kind.Divider)
                        && editSelection.Contains(hit.Kind, hit.Id, hit.BoundaryId)
                        && HouseSceneQueries.PointOnEditPlane(plan, mouseDown, dragHeight, out dragOrigin);
                }
                if (canDrag)
                    dragInputs = HouseEditInputs.SourceInputs(plan);
                SyncSelection();
                controller.CapturePointer(HouseEditorController.PointerOwner.Selection, view, control);
                e.Use(); view.Repaint();
            }
            else if (HasGesture)
            {
                if (e.type == EventType.MouseDrag || dragging && input.SnapModifierChanged)
                {
                    if (e.type == EventType.MouseDrag) mouseNow = e.mousePosition;
                    if ((mouseNow - mouseDown).sqrMagnitude >= 16)
                    {
                        dragging = true;
                        // A floor click selects a segment; dragging there still box-selects nodes.
                        // It never moves its owning room as an accidental group operation.
                        if (roomMouseDown) boxSelecting = true;
                        if (!boxSelecting && canDrag)
                        {
                            // A click owns selection only. Allocate a candidate only
                            // after crossing the drag threshold, against the click's source.
                            if (!geometryClaimed)
                            {
                                if (dragInputs != HouseEditInputs.SourceInputs(plan))
                                {
                                    Cancel();
                                    session.Message = "The House or its kit changed during this gesture. Try the edit again.";
                                    e.Use(); return;
                                }
                                geometryClaimed = session.Pending.TryBeginGeometry(plan, out string reason);
                                if (!geometryClaimed) { Cancel(); session.Message = reason; e.Use(); return; }
                                if (!editSelection.Items.Any(item => item.Type == HouseEditSelection.Kind.Divider
                                    || item.Type == HouseEditSelection.Kind.DividerNode) && !editPreview.CaptureMoveBaseline(out reason))
                                { Cancel(); session.Message = reason; e.Use(); return; }
                            }
                            movePreviewRequested = true;
                        }
                    }
                    e.Use(); view.Repaint();
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    bool prepared = false;
                    // Pointer events can arrive faster than geometry validation.
                    // Never replay their old positions; the release is authoritative.
                    if (dragging && !boxSelecting && canDrag)
                    {
                        mouseNow = e.mousePosition;
                        movePreviewRequested = false;
                        prepared = PreviewSelectionMove(plan, prepareNodes: true);
                    }
                    if (boxSelecting && dragging)
                    {
                        var rect = SelectionRectangle();
                        var targets = HouseSceneQueries.NodesInRectangle(plan, view, rect, editSelection)
                            .Select(id => new HouseEditSelection.Item(HouseEditSelection.Kind.Node, id))
                            .Concat(HouseSceneQueries.DividerNodesInRectangle(plan, view, rect, editSelection)).ToArray();
                        session.MarqueeItems(targets, boxAdd, boxSubtract);
                    }
                    else if (dragging && editPreview != null && prepared)
                        session.Message = movingDividers != null
                            ? HouseEditCommit.PublishBuildingGeometry(editPreview, "Move floor divider").Message
                            : HouseEditCommands.CommitPreview(editPreview, "Move House selection").Message;
                    // Keep the current room's handle scope while dragging a marquee
                    // from empty space. Only an ordinary empty click clears selection.
                    else if (boxSelecting && !boxAdd && !boxSubtract) ClearSelection();
                    else if (!dragging && singlePickOnRelease.HasValue)
                        session.Pick(singlePickOnRelease.Value.Kind, singlePickOnRelease.Value.Id, boundaryId: singlePickOnRelease.Value.BoundaryId);
                    Cancel(); SyncSelection(); e.Use(); view.Repaint();
                }
            }
            if (e.type == EventType.Repaint && movePreviewRequested)
            {
                movePreviewRequested = false;
                PreviewSelectionMove(plan, prepareNodes: false);
            }
            if (e.type == EventType.MouseMove) { session.Message = ""; view.Repaint(); }
        }

        private Rect SelectionRectangle() => Rect.MinMaxRect(Mathf.Min(mouseDown.x, mouseNow.x), Mathf.Min(mouseDown.y, mouseNow.y),
            Mathf.Max(mouseDown.x, mouseNow.x), Mathf.Max(mouseDown.y, mouseNow.y));

        private bool PreviewSelectionMove(HousePlan plan, bool prepareNodes)
        {
            using var sample = moveSample.Auto();
            // The pending owner captures source inputs at mouse-down. A changed transform
            // must not reinterpret the stored drag origin during the gesture.
            if (editPreview == null || editPreview.HasConflict)
            {
                Cancel();
                session.Message = "The House or its kit changed during this gesture. Try the edit again.";
                return false;
            }
            if (editSelection.Items.Any(i => i.Type == HouseEditSelection.Kind.Room || i.Type == HouseEditSelection.Kind.SurfaceRegion
                || i.Type == HouseEditSelection.Kind.FloorSlab || i.Type == HouseEditSelection.Kind.CeilingSlab)) return false;
            bool openings = editSelection.Items.Any(i => i.Type == HouseEditSelection.Kind.Opening);
            if (openings && editSelection.Items.Any(i => i.Type != HouseEditSelection.Kind.Opening))
            { session.Message = "Move openings separately from nodes and walls."; return false; }
            if (!HouseSceneQueries.PointOnEditPlane(plan, mouseNow, dragHeight, out var current))
            { session.Message = "Use Top view to move along the floor plane."; return false; }
            var delta = current - dragOrigin;
            bool dividers = editSelection.Items.Any(i => i.Type == HouseEditSelection.Kind.DividerNode || i.Type == HouseEditSelection.Kind.Divider);
            if (dividers)
            {
                if (editSelection.Items.Any(i => i.Type != HouseEditSelection.Kind.DividerNode && i.Type != HouseEditSelection.Kind.Divider)
                    || editSelection.Items.Select(i => i.BoundaryId).Distinct().Count() != 1)
                { session.Message = "Move floor dividers from one slab separately from wall objects."; return false; }
                dividerBoundary = editSelection.Items[0].BoundaryId;
                var boundary = HouseBuilding.For(plan).Boundaries.First(b => b.Id == dividerBoundary);
                var ids = editSelection.MovingDividerNodes(boundary);
                if (ids.Count == 0) return false;
                var anchorNode = boundary.DividerNodes.First(n => ids.Contains(n.Id));
                var anchor = anchorNode.Position;
                bool ceiling = boundary.LowerId == HouseBuilding.For(plan).Find(plan).Id;
                var snap = HouseSceneQueries.ResolvePlacement(plan, session, anchor + delta,
                    UnityEditor.HandleUtility.WorldToGUIPoint(HouseSceneQueries.SlabWorld(plan, ceiling, anchor + delta)), ceiling: ceiling,
                    excludedNodes: ids);
                delta = HouseSceneQueries.SnapDividerAnchor(editPreview, session, boundary, anchorNode, ids, anchor + delta, snap) - anchor;
                movingDividers = boundary.DividerNodes.Where(n => ids.Contains(n.Id)).ToDictionary(n => n.Id, n => n.Position + delta);
                editPreview.MoveDividerNodes(dividerBoundary, movingDividers, out string reason);
                if (editPreview.Valid)
                {
                    var resolved = editPreview.Building.Boundaries.First(b => b.Id == dividerBoundary);
                    movingDividers = resolved.DividerNodes.ToDictionary(n => n.Id, n => n.Position);
                }
                session.Message = reason;
            }
            else if (openings)
            {
                var anchorWall = plan.Data.Walls.First(w => w.Openings.Any(o => editSelection.Contains(HouseEditSelection.Kind.Opening, o.Id)));
                var anchor = anchorWall.Openings.First(o => editSelection.Contains(HouseEditSelection.Kind.Opening, o.Id));
                var direction = HouseSceneQueries.Direction(plan, anchorWall);
                float shiftAlong = Vector2.Dot(delta, direction);
                // Attract one anchor, then carry the same translation through the group.
                // Snapping every opening independently would change their separation.
                delta += direction * (HouseSceneQueries.SnapOpeningDistance(plan, session, anchorWall, anchor.Distance + shiftAlong)
                    - anchor.Distance - shiftAlong);
                movingOpenings = new Dictionary<string, float>();
                foreach (var wall in plan.Data.Walls) foreach (var opening in wall.Openings)
                    if (editSelection.Contains(HouseEditSelection.Kind.Opening, opening.Id))
                    {
                        float shift = Vector2.Dot(delta, HouseSceneQueries.Direction(plan, wall));
                        movingOpenings[opening.Id] = opening.Distance + shift;
                    }
                editPreview.MoveOpenings(movingOpenings, out string reason);
                session.Message = reason;
            }
            else
            {
                var ids = editSelection.MovingNodes(plan);
                if (ids.Count == 0) return false;
                // Snap one shared offset, preserving distances within a multi-selection.
                var anchor = plan.Data.GetPosition(ids.First());
                delta = HouseSceneQueries.ResolvePlacement(plan, session, anchor + delta,
                    UnityEditor.HandleUtility.WorldToGUIPoint(HouseSceneQueries.NodeWorld(plan, anchor + delta)), excludedNodes: ids).Position - anchor;
                movingNodes = ids.ToDictionary(id => id, id => plan.Data.GetPosition(id) + delta);
                previewCurrent = false;
                // The outline follows the latest pointer without fitting the whole
                // House. Prepare at release; publication owns exact validation.
                if (!prepareNodes)
                {
                    session.Message = "";
                    return false;
                }
                bool ready = editPreview.PrepareMoveNodes(movingNodes, out string reason);
                session.Message = reason;
                return ready;
            }
            if (editPreview.Valid && editPreview.RemovedOpenings.Count > 0)
                session.Message = $"{editPreview.RemovedOpenings.Count} orange opening(s) will be removed on release. Escape cancels.";
            return editPreview.Valid;
        }

        internal void Cancel()
        {
            controller.ReleasePointer(HouseEditorController.PointerOwner.Selection);
            singlePickOnRelease = null;
            dragging = boxSelecting = canDrag = roomMouseDown = false;
            if (geometryClaimed) session.Pending.ReleaseGeometry();
            geometryClaimed = false; movingNodes = null; movingOpenings = null; movingDividers = null; dividerBoundary = null;
            movePreviewRequested = false; previewCurrent = true;
            dragInputs = null;
        }

        internal void SyncSelection()
        {
            if (session.SessionPlan != null) editSelection.Prune(session.SessionPlan);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        internal void ClearSelection()
        {
            session.ClearSelection();
            SyncSelection();
            SceneView.RepaintAll();
        }

        internal void InsertNode(HousePlan plan, string wallId, float fraction)
        {
            var result = HouseEditCommands.InsertNode(plan, wallId, fraction, out string added);
            session.Message = result.Message;
            if (!result.Applied) return;
            session.ClearSelection();
            session.Pick(HouseEditSelection.Kind.Node, added);
            SyncSelection();
        }

        internal void DrawFeedback(HousePlan plan, HouseSceneHit? hover)
        {
            bool? validity = previewCurrent ? editPreview == null || editPreview.Valid : (bool?)null;
            feedback.DrawSelection(plan, editSelection, hover, movingNodes, movingOpenings,
                validity);
            feedback.DrawDividers(plan, editSelection, hover, dividerBoundary, movingDividers,
                validity, movingNodes, editPreview);
            if (editPreview != null && previewCurrent) feedback.DrawRemovedOpenings(plan, editPreview.RemovedOpenings);
            if (boxSelecting && dragging) feedback.DrawMarquee(SelectionRectangle());
        }

        internal bool TrySnapCursor(HousePlan plan, out Vector2 point, out float height)
        {
            height = dragHeight;
            point = default;
            return dragging && canDrag && !boxSelecting
                && HouseSceneQueries.PointOnEditPlane(plan, mouseNow, dragHeight, out point);
        }
    }
}
