using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseWallTool
    {
        private readonly HouseEditorSession session;
        private readonly HouseSceneFeedback feedback;
        private readonly HouseEditorController controller;
        private HouseEditPreview Preview => session.Pending.ActiveKind == HousePendingEdit.Kind.Geometry
            ? session.Pending.Preview : null;
        private Vector2? previewEnd;
        private string previewReason;
        private bool previewConnects;
        private Vector2? chainStart;
        private string chainInputs;
        private readonly List<DrawingStep> steps = new List<DrawingStep>();
        private readonly List<Vector2> dividerPoints = new List<Vector2>();
        private bool consumeRightRelease;
        private sealed class DrawingStep
        {
            internal Vector2 Start;
            internal string PlanJson, BuildingJson;
        }
        internal HouseWallTool(HouseEditorSession session, HouseSceneFeedback feedback, HouseEditorController controller)
        { this.session = session; this.feedback = feedback; this.controller = controller; }
        internal IReadOnlyCollection<string> RemovedOpenings => Preview?.RemovedOpenings;
        internal bool HasChain => chainStart.HasValue;
        internal bool BeginChain(HousePlan plan, Vector2 point)
        {
            if (!BeginGeometry(plan)) return false;
            chainStart = point; chainInputs = HouseEditInputs.SourceInputs(plan); previewEnd = null;
            return true;
        }
        internal void Cancel()
        {
            chainStart = null; chainInputs = null; steps.Clear(); dividerPoints.Clear(); consumeRightRelease = false;
            session.Pending.ReleaseGeometry();
            previewEnd = null;
        }
        internal void Handle(HousePlan plan, SceneView view, ScenePointerInput input)
        {
            Event current = input.Event;
            bool divider = session.ActiveMode == HouseEditorSession.Mode.Divider;
            bool boundaryTool = session.ActiveMode == HouseEditorSession.Mode.Boundary;
            string subject = divider ? "floor divider" : boundaryTool ? "room boundary" : "wall";
            if (current.type == EventType.MouseUp && current.button == 1 && consumeRightRelease)
            {
                consumeRightRelease = false;
                current.Use();
                return;
            }
            if (chainStart.HasValue && (Preview == null || Preview.HasConflict))
            {
                Cancel();
                session.Message = $"The House or its kit changed. Start the next {subject} again; committed segments stay.";
                if (input.LeftMouseDown) current.Use();
                view.Repaint();
                return;
            }
            if (input.RightMouseDown && chainStart.HasValue)
            {
                if (divider && dividerPoints.Count > 1)
                {
                    dividerPoints.RemoveAt(dividerPoints.Count - 1);
                    chainStart = dividerPoints[dividerPoints.Count - 1];
                    previewEnd = null;
                    session.Message = "Last draft point removed. Continue the divider or right-click again.";
                }
                else if (steps.Count == 0)
                {
                    Cancel();
                    session.Message = $"Proposed {subject} cancelled.";
                }
                else
                {
                    var step = steps[steps.Count - 1];
                    session.Pending.ReleaseGeometry();
                    previewEnd = null;
                    var result = HouseEditCommands.RestoreDrawingStep(plan, chainInputs, step.PlanJson, step.BuildingJson);
                    session.Message = result.Message;
                    if (result.Applied)
                    {
                        steps.RemoveAt(steps.Count - 1);
                        chainStart = step.Start;
                        chainInputs = HouseEditInputs.SourceInputs(plan);
                        session.Message = "Last drawing point removed. Continue drawing, or right-click to remove another point.";
                    }
                    if (result.Status == HouseEditStatus.Rejected || !BeginGeometry(plan)) Cancel();
                }
                consumeRightRelease = true;
                current.Use(); view.Repaint();
                return;
            }
            if (input.Navigating)
                return;
            int control = input.ControlId;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!HouseSceneQueries.RaycastFloor(plan, ray, out var localPoint))
                return;
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = divider && level != null ? building.Below(level) : null;
            if (divider && boundary == null)
            {
                session.Message = "Choose a House level before drawing a floor divider.";
                return;
            }
            feedback.DrawPlacementGuides(plan, localPoint);
            var snapped = HouseSceneQueries.ResolvePlacement(plan, session, localPoint, current.mousePosition,
                pending: chainStart.HasValue ? new[] { chainStart.Value } : null);
            Vector2 point = snapped.Position;
            bool finish = chainStart.HasValue
                && Vector2.Distance(chainStart.Value, point) <= OutlineGeometry.Tolerance;
            bool endsOnWall = false;
            string reason = "";
            bool valid = finish || !chainStart.HasValue && (divider || plan.Data.CanStartWall(point, out reason));
            if (chainStart.HasValue && !finish)
            {
                bool currentProposal = Preview != null && previewEnd == point;
                if (!currentProposal && (input.LeftMouseDown
                    || current.type == EventType.Repaint && controller.CanValidateScenePreview(view)))
                {
                    // Both operations reset the same isolated candidate before each proposal.
                    if (Preview == null)
                    {
                        if (!BeginGeometry(plan)) return;
                    }
                    previewEnd = point;
                    if (divider)
                    {
                        var stroke = new List<Vector2>(dividerPoints) { point };
                        valid = Preview.AddDividerStroke(boundary.Id, stroke, false, out previewReason);
                        previewConnects = Preview.DividerPartitionsFloor;
                    }
                    else valid = Preview.AddEdge(chainStart.Value, point,
                        boundaryTool ? FloorPlan.PhysicalPresence.None : FloorPlan.PhysicalPresence.Wall, out previewConnects, out previewReason);
                    controller.ScenePreviewValidated();
                    currentProposal = true;
                }
                valid = currentProposal && Preview.Valid;
                endsOnWall = currentProposal && previewConnects;
                reason = currentProposal ? previewReason : "Click to place the endpoint.";
            }
            session.Message = reason;
            if (valid)
                session.Message = finish ? divider ? "This divider is unfinished. Connect it across the floor or close a loop; right-click removes a draft point."
                    : "Click this endpoint again to finish. Committed segments stay."
                    : !chainStart.HasValue ? $"Click to start a {subject}."
                    : endsOnWall ? "Click to connect and finish drawing."
                    : divider ? "Click to add a draft point. The divider is saved only when it divides the floor."
                    : "Click to add a segment and keep drawing. Crossing an existing edge does not stop the run.";
            bool checkedEndpoint = !chainStart.HasValue || finish || previewEnd == point;
            if (!divider && checkedEndpoint)
                feedback.DrawRemovedOpenings(plan, chainStart.HasValue && !finish && Preview != null
                    ? Preview.RemovedOpenings : HouseSceneQueries.OpeningsAtWallPoint(plan, point));
            if (!divider && checkedEndpoint && valid && Preview?.RemovedOpenings.Count > 0 && !finish)
                session.Message += $" {Preview.RemovedOpenings.Count} orange opening(s) will be removed on this click.";
            using (new Handles.DrawingScope(!checkedEndpoint ? HouseSceneFeedback.HoverColor : valid ? Color.green : Color.red))
            {
                Vector3 world = DrawingWorld(plan, divider, point);
                if (!divider)
                    Handles.DrawWireDisc(world, plan.transform.up, HandleUtility.GetHandleSize(world) * 0.08f);
                if (chainStart.HasValue)
                    Handles.DrawAAPolyLine(4f, DrawingWorld(plan, divider, chainStart.Value), world);
            }
            if (divider && current.type == EventType.Repaint && dividerPoints.Count > 1)
                using (new Handles.DrawingScope(HouseSceneFeedback.HoverColor))
                    for (int index = 1; index < dividerPoints.Count; index++)
                        foreach (var piece in HouseSceneQueries.ConstructionPieces(plan, dividerPoints[index - 1], dividerPoints[index]))
                            Handles.DrawAAPolyLine(2, piece.Start, piece.End);
            if (chainStart.HasValue)
            {
                Vector3 start = DrawingWorld(plan, divider, chainStart.Value);
                if (!divider)
                    using (new Handles.DrawingScope(HouseSceneFeedback.SelectionColor))
                        Handles.DrawWireDisc(start, plan.transform.up, HandleUtility.GetHandleSize(start) * 0.08f);
                if (current.type == EventType.Repaint)
                    feedback.DrawIntersectionPoints(plan, HouseSceneQueries.DrawingIntersections(plan, new[]{chainStart.Value, point}, false, true), divider ? false : (bool?)null);
            }
            if (divider)
                feedback.DrawSlabPoints(plan, false, chainStart.HasValue ? new[] { chainStart.Value, point } : new[] { point },
                    HouseSceneFeedback.HoverColor, HouseSceneFeedback.PointKind.Divider);
            if (current.type == EventType.MouseDown && current.button == 0
                && HandleUtility.nearestControl == control)
            {
                if (finish && !divider)
                    Cancel();
                else if (valid && !chainStart.HasValue)
                {
                    if (BeginChain(plan, point))
                    {
                        if (divider) dividerPoints.Add(point);
                    }
                }
                else if (valid)
                {
                    if (divider)
                    {
                        if (!finish && !Preview.DividerPartitionsFloor)
                        {
                            dividerPoints.Add(point);
                            chainStart = point;
                            previewEnd = null;
                            session.Message = "Draft point added. Continue across the floor or close a loop.";
                        }
                        else if (!finish)
                        {
                            var dividerResult = HouseEditCommit.PublishBuildingGeometry(Preview, "Draw floor divider", requireEditedFit: false);
                            if (dividerResult.Status != HouseEditStatus.Rejected) Cancel();
                            session.Message = dividerResult.Message;
                        }
                        // Clicking an unfinished endpoint cannot publish a dangling
                        // chain. Escape/right-click discards only this transient draft.
                        current.Use(); view.Repaint();
                        return;
                    }
                    var step = new DrawingStep { Start = chainStart.Value, PlanJson = JsonUtility.ToJson(plan),
                        BuildingJson = building == null ? null : JsonUtility.ToJson(building) };
                    var result = HouseEditCommit.PublishPlanGeometry(Preview, "Draw House wall");
                    session.Message = result.Message;
                    if (result.Status != HouseEditStatus.Rejected)
                    {
                        chainStart = endsOnWall ? (Vector2?)null : point;
                        chainInputs = endsOnWall ? null : HouseEditInputs.SourceInputs(plan);
                        session.Message = "";
                        session.Pending.ReleaseGeometry();
                        previewEnd = null;
                        if (endsOnWall) steps.Clear();
                        else
                        {
                            if (result.Applied) steps.Add(step);
                            if (!BeginGeometry(plan)) Cancel();
                        }
                    }
                }
                current.Use();
                view.Repaint();
            }
        }

        private bool BeginGeometry(HousePlan plan)
        {
            if (session.Pending.TryBeginGeometry(plan, out string reason)) return true;
            session.Message = reason;
            return false;
        }

        private static Vector3 DrawingWorld(HousePlan plan, bool divider, Vector2 point) => divider
            ? HouseSceneQueries.SlabWorld(plan, false, point) : HouseSceneQueries.NodeWorld(plan, point);

    }
}
