using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Input routing and native capture. The session owns pending data; tools own pointer coordinates.
    internal sealed class HouseEditorController : IDisposable
    {
        private static readonly Unity.Profiling.ProfilerMarker sceneSample = new Unity.Profiling.ProfilerMarker("House.SceneGUI");
        private static readonly Unity.Profiling.ProfilerMarker hoverSample = new Unity.Profiling.ProfilerMarker("House.ResolveHover");
        private static readonly Unity.Profiling.ProfilerMarker feedbackSample = new Unity.Profiling.ProfilerMarker("House.DrawFeedback");
        internal HouseEditorSession Session { get; }
        internal HouseSelectTool SelectionTool { get; }
        internal HouseSlabTool SlabTool { get; }
        internal HouseWallTool WallTool { get; }
        internal HouseBooleanTool BooleanTool { get; }
        private readonly HouseOpeningTool openingTool;
        private readonly HouseDeleteTool deleteTool;
        private readonly HouseSceneFeedback feedback;
        private SceneView gestureView;
        internal enum PointerOwner { None, Selection, Slab, Boolean }
        private PointerOwner pointerOwner;
        private int pointerControl;
        private bool selectionNeedsPrune = true;
        private double nextPreviewValidation;

        internal HouseEditorController(HouseEditorSession session)
        {
            Session = session;
            feedback = new HouseSceneFeedback();
            SlabTool = new HouseSlabTool(session, feedback, this);
            SelectionTool = new HouseSelectTool(session, feedback, this);
            WallTool = new HouseWallTool(session, feedback, this);
            BooleanTool = new HouseBooleanTool(session, this);
            openingTool = new HouseOpeningTool(session, feedback);
            deleteTool = new HouseDeleteTool(session, feedback);
            Session.Pending.Interrupted += CancelSceneGesture;
            HouseEditCommit.AuthoredChanged += InvalidateSelection;
            HouseRebuildCoordinator.OutputsRefreshed += OnOutputsRefreshed;
        }

        // Wall-drawing outlines follow every repaint. Full drawing proposals run
        // at most ten times per second, measured after validation finishes.
        // Clicks validate exactly; selection-node drags validate only at release.
        internal bool CanValidateScenePreview(SceneView view)
        {
            if (EditorApplication.timeSinceStartup >= nextPreviewValidation) return true;
            view.Repaint(); // Finish the latest pending proposal even if the pointer stops.
            return false;
        }
        internal void ScenePreviewValidated() => nextPreviewValidation = EditorApplication.timeSinceStartup + .1;

        private void InvalidateSelection() => selectionNeedsPrune = true;
        private void OnOutputsRefreshed(HousePlan plan) => InvalidateSelection();

        internal void RefreshSelection()
        {
            if (!selectionNeedsPrune || Session.SessionPlan == null) return;
            using var queries = HouseSceneQueries.BeginEvent();
            Session.Selection.Prune(Session.SessionPlan);
            selectionNeedsPrune = false;
        }

        // Selection-only clicks and marquees also own native capture, even though
        // they have no authored candidate. Their view lifetime still needs protection.
        internal bool HasSceneGesture => HasPointerCapture || SlabTool.HasGesture || WallTool.HasChain;
        private bool HasPointerCapture => pointerOwner != PointerOwner.None;
        internal bool IsGestureOwner(SceneView view) => HasSceneGesture && gestureView == view;
        internal bool CapturedBy(PointerOwner owner) => pointerOwner == owner;

        internal void CapturePointer(PointerOwner owner, SceneView view, int control)
        {
            if (HasPointerCapture) throw new InvalidOperationException("A House pointer is already captured.");
            pointerOwner = owner;
            pointerControl = control;
            gestureView = view;
            GUIUtility.hotControl = control;
        }

        internal void ReleasePointer(PointerOwner owner)
        {
            if (!HasPointerCapture || pointerOwner != owner) return;
            int control = pointerControl;
            var view = gestureView;
            pointerOwner = PointerOwner.None;
            pointerControl = 0;
            ScenePointerInput.Release(view, control);
        }

        internal void BeginSession(HousePlan plan, bool preserveTool)
        {
            bool changedTarget = Session.SessionPlan != plan;
            Session.ContextRevision++;
            CancelSceneGesture();
            feedback.Dispose();
            Session.SessionPlan = plan;
            if (changedTarget) Session.ClearSelection();
            InvalidateSelection();
            HouseLevelView.Set(plan);
            if (changedTarget && !preserveTool) Session.ActiveMode = HouseEditorSession.Mode.Edit;
        }

        internal void SetMode(HouseEditorSession.Mode mode)
        {
            if (Session.ActiveMode == mode || !Session.ConfirmPendingChanges()) return;
            CancelSceneGesture();
            if (mode == HouseEditorSession.Mode.Boolean || Session.ActiveMode == HouseEditorSession.Mode.Boolean)
                Session.ClearSelection();
            if (mode == HouseEditorSession.Mode.Slab)
            {
                SlabTool.ClearSelection();
                Session.SlabCeiling = false;
                Session.SlabPolygon = true;
                HouseLevelView.Set(Session.SessionPlan);
            }
            Session.ActiveMode = mode;
            if (mode == HouseEditorSession.Mode.Boolean && HouseBuilding.For(Session.SessionPlan)?.Data.BooleanVolumes.Count == 0)
                Session.PlaceBooleanShape = true;
            if (mode == HouseEditorSession.Mode.Trim || mode == HouseEditorSession.Mode.Boolean) HousePropertiesWindow.ShowSelection();
        }

        internal void BeginWallChain(SceneView view, Vector2 point)
        {
            // Creation starts drawing outside this controller's normal input dispatch.
            // Give that chain the same view lifetime as one started by a wall-tool click.
            if (view != null && WallTool.BeginChain(Session.SessionPlan, point)) gestureView = view;
        }

        internal void CancelSceneGesture()
        {
            nextPreviewValidation = 0;
            SelectionTool.Cancel();
            SlabTool.Cancel();
            WallTool.Cancel();
            BooleanTool.Cancel();
            Session.Pending.ReleaseGeometry();
            gestureView = null;
            Session.Message = "";
        }

        internal HouseStack.Boundary SelectedSlabBoundary(HouseBuilding building) => SlabTool.SelectionBoundary(building, Session.SessionPlan)
            ?? SlabTool.SlabBoundary(building, Session.SessionPlan);
        internal void SetSlabOptions(bool ceiling, bool polygon)
        {
            if (Session.SlabCeiling == ceiling && Session.SlabPolygon == polygon || !Session.ConfirmPendingChanges()) return;
            CancelSceneGesture();
            if (Session.SlabCeiling != ceiling) SlabTool.ClearSelection();
            Session.SlabCeiling = ceiling;
            Session.SlabPolygon = polygon;
        }

        internal bool HasSelectionGesture => SelectionTool.HasGesture;
        internal void InsertNode(HousePlan plan, string wallId, float fraction) => SelectionTool.InsertNode(plan, wallId, fraction);
        internal void ClearSelection()
        {
            CancelSceneGesture();
            SelectionTool.ClearSelection();
        }

        // Property views replace the complete selection and cancel unfinished Scene gestures.
        internal void Select(HouseEditSelection.Kind kind, string id, string boundaryId = null)
            => Select(new[] { new HouseEditSelection.Item(kind, id, boundaryId) });

        internal bool Select(IEnumerable<HouseEditSelection.Item> items)
        {
            CancelSceneGesture();
            Session.ClearSelection();
            foreach (var item in items)
                Session.Pick(item.Type, item.Id, add: true, boundaryId: item.BoundaryId, ceiling: item.Ceiling);
            SelectionTool.SyncSelection();
            SceneView.RepaintAll();
            return true;
        }

        internal HouseEditResult RunCommand(Func<HouseEditResult> command)
        {
            if (!Session.ConfirmPendingChanges())
                return new HouseEditResult(HouseEditStatus.Unchanged, "The edit was cancelled.");
            CancelSceneGesture();
            var result = command();
            Session.Message = result.Message;
            if (Session.SessionPlan != null) Session.Selection.Prune(Session.SessionPlan);
            SceneView.RepaintAll();
            return result;
        }

        internal void CloseSession()
        {
            CancelSceneGesture();
            feedback.Dispose();
            // Leaving the context does not erase the user's target or semantic selection.
        }

        internal void OnUndoRedo()
        {
            CancelSceneGesture();
            if (Session.SessionPlan != null) Session.Selection.Prune(Session.SessionPlan);
            SceneView.RepaintAll();
        }

        internal void CheckGestureOwnerLifetime()
        {
            // A candidate is not a gesture. If no tool owns pointer/chain state,
            // it must not suppress picking forever merely because a preview survived.
            if (!HasSceneGesture) Session.Pending.ReleaseGeometry();
            // A closed view compares equal to null. Never let its unfinished gesture migrate to another view.
            if (HasSceneGesture && (gestureView == null || Session.SessionPlan == null || !Session.SessionPlan.HasSupportedTransform)) CancelSceneGesture();
            else if (!HasSceneGesture) gestureView = null;
        }

        internal void OnSceneGUI(SceneView view)
        {
            using var sample = sceneSample.Auto();
            var plan = Session.SessionPlan;
            if (plan == null) return;
            // Allocate before conditional handles on every event, including events in other views.
            var input = new ScenePointerInput(Event.current);
            input.ClaimLayout(plan.HasSupportedTransform);
            CheckGestureOwnerLifetime();
            CancelInterruptedPointer(view, input);
            try
            {
                RefreshSelection();
                // The camera owns navigation input, including Escape. Consuming its
                // cancellation can strand native capture. Delete still protects the
                // House root, but cannot change authored elements while navigating.
                if (input.CameraOwnsInput && input.Event.type != EventType.Layout && input.Event.type != EventType.Repaint)
                {
                    HandleDeleteInput(plan, view, input, allowEdit: false);
                    return;
                }
                // Native transform handles need Layout too. They still share
                // the controller's interruption and release cleanup.
                if (Session.ActiveMode == HouseEditorSession.Mode.Boolean)
                {
                    if (!HandleDeleteInput(plan, view, input)) BooleanTool.OnSceneGUI(plan, view, input);
                    return;
                }
                // Layout only registers the session control; picking here as
                // well as during input doubles the repaint's geometry work.
                if (input.Event.type == EventType.Layout) return;
                using var queries = HouseSceneQueries.BeginEvent();
                HousePlanSceneDrawing.DrawPlan(plan);
                BooleanTool.DrawHelpers();
                HouseSceneTarget hover = default;
                HandleSceneInput(plan, view, input, ref hover);
                // Returning from an input handler still reaches this shared drawing
                // pass. A drawing exception uses the same capture cleanup as input.
                bool navigationOnly = input.Navigating && !HasSceneGesture && !Session.Pending.IsPending
                    && Session.Selection.Items.Count == 0 && !SlabTool.HasSelection;
                if (plan != null && plan.HasSupportedTransform && Event.current.type == EventType.Repaint && !navigationOnly)
                    DrawFeedback(plan, hover);
                else if (plan != null && plan.HasSupportedTransform && Event.current.type == EventType.Repaint)
                    feedback.DrawNavigationDividers(plan);
            }
            catch
            {
                CancelSceneGesture();
                throw;
            }
            finally
            {
                // An early return or consumed release cannot strand native capture.
                // Tools publish only through their ordinary, unconsumed release path.
                if (gestureView == view && HasPointerCapture && input.LeftMouseUp)
                    CancelSceneGesture();
            }
        }

        private void CancelInterruptedPointer(SceneView view, ScenePointerInput input)
        {
            // Native control IDs belong to the drawing view. A foreign view's
            // event cannot cancel or publish this view's drag. Chains do not hold a button.
            if (gestureView != view || !HasPointerCapture) return;
            if (!Session.SessionPlan.HasSupportedTransform || input.CancelsPointerCapture
                || GUIUtility.hotControl != pointerControl)
                CancelSceneGesture();
        }

        private void HandleSceneInput(HousePlan plan, SceneView view, ScenePointerInput input, ref HouseSceneTarget hover)
        {
            // Unity keeps the level GameObject selected while we select logical elements.
            // Own deletion before tool-specific routing, even when no element can be deleted.
            if (HandleDeleteInput(plan, view, input)) return;
            // A property drag may reserve its slot while unchanged. A later Scene
            // click ends that empty reservation; changed values still require Apply/Discard.
            if (input.LeftMouseDown && Session.Pending.IsProperty && !Session.Pending.IsPending)
                Session.Pending.Discard();
            if (Session.Pending.IsProperty && input.Event.type == EventType.KeyDown && input.Event.keyCode == KeyCode.Escape
                && !EditorGUIUtility.editingTextField)
            {
                Session.ConfirmPendingChanges(); input.Event.Use();
                return;
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape
                && !EditorGUIUtility.editingTextField && (!HasSceneGesture || gestureView == view))
            {
                if (HasSceneGesture)
                {
                    CancelSceneGesture();
                    Event.current.Use();
                    view.Repaint();
                    return;
                }
                bool selected = Session.Selection.Items.Count > 0 || SlabTool.HasSelection;
                ClearSelection();
                Event.current.Use();
                if (!selected) HouseEditingContext.Active?.Exit();
                view.Repaint();
                return;
            }
            if (Event.current.type == EventType.KeyDown && EditorGUIUtility.editingTextField) return;
            if (!plan.HasSupportedTransform)
            {
                if (HasSceneGesture) CancelSceneGesture();
                return;
            }
            if (HasSceneGesture && gestureView != view)
            {
                return;
            }
            // Native camera gestures do not need a House hover target. Keep authored
            // gestures on their existing path so their release/cancellation still runs.
            if (!HasSceneGesture && !input.CanHover) return;
            hover = ResolveHover(plan, input.Event.mousePosition);
            DrawSnapGrid(plan, input);
            // Retained values reserve an authored edit, not the pointer. Select
            // can still inspect targets; placement/delete modes only show feedback.
            if (Session.Pending.IsProperty && Session.ActiveMode != HouseEditorSession.Mode.Edit)
            {
                if (input.LeftMouseDown)
                {
                    Session.Message = "Apply or discard the pending property values before changing geometry.";
                    input.Event.Use();
                }
                return;
            }
            if (!HasSceneGesture && HousePointPromotion.Handle(Session, plan, view, input, hover)) return;
            switch (Session.ActiveMode)
            {
                case HouseEditorSession.Mode.Wall:
                case HouseEditorSession.Mode.Divider: WallTool.Handle(plan, view, input); break;
                case HouseEditorSession.Mode.Boundary: WallTool.Handle(plan, view, input); break;
                case HouseEditorSession.Mode.Door:
                case HouseEditorSession.Mode.Window: openingTool.Handle(plan, view, input); break;
                case HouseEditorSession.Mode.Slab: SlabTool.Handle(plan, view, true, input, hover); break;
                case HouseEditorSession.Mode.Trim: HouseTrimTool.Handle(Session, input, hover.Element); break;
                case HouseEditorSession.Mode.Delete:
                    deleteTool.Handle(plan, view, input, hover);
                    break;
                default:
                    if (SelectionTool.HasGesture
                        || !HasSceneGesture && input.Event.type == EventType.ContextClick
                        || hover.Element?.Kind == HouseEditSelection.Kind.Node
                        || hover.Element?.Kind == HouseEditSelection.Kind.DividerNode
                        || hover.Element?.Kind == HouseEditSelection.Kind.Divider)
                        SelectionTool.Handle(plan, view, input, hover.Element);
                    else
                    {
                        if (!SlabTool.Handle(plan, view, false, input, hover)) SelectionTool.Handle(plan, view, input, hover.Element);
                    }
                    break;
            }
            gestureView = HasSceneGesture ? view : null;
            if (Event.current.type == EventType.MouseMove) view.Repaint();
        }

        private bool HandleDeleteInput(HousePlan plan, SceneView view, ScenePointerInput input, bool allowEdit = true)
        {
            if (!input.DeleteRequested) return false;
            var current = input.Event;
            bool execute = current.type != EventType.ValidateCommand;
            current.Use();
            if (!execute) return true;
            if (!allowEdit || HasSceneGesture || Session.Pending.IsProperty)
                Session.Message = "Finish or cancel the current edit before deleting an element.";
            else
                RunCommand(() => HouseEditCommands.DeleteSelection(plan, Session.Selection));
            view.Repaint();
            return true;
        }

        private HouseSceneTarget ResolveHover(HousePlan plan, Vector2 mouse)
        {
            using var sample = hoverSample.Auto();
            // A proposal already owns its target. Continue snapping that target;
            // do not rebuild unrelated hover geometry during the captured gesture.
            if (HasSceneGesture) return default;
            if (Session.ActiveMode == HouseEditorSession.Mode.Trim)
                return HouseSceneTarget.ForElement(HouseSceneQueries.PickTrim(plan, mouse));
            if (Session.ActiveMode == HouseEditorSession.Mode.Delete)
            {
                var trim = HouseSceneQueries.PickAttachedTrim(plan, mouse);
                if (trim.HasValue) return HouseSceneTarget.ForElement(trim);
            }
            bool select = Session.ActiveMode == HouseEditorSession.Mode.Edit || Session.ActiveMode == HouseEditorSession.Mode.Delete;
            if (!select && Session.ActiveMode != HouseEditorSession.Mode.Slab && Session.ActiveMode != HouseEditorSession.Mode.Delete)
                return default;
            if (select && !SlabTool.HasGesture)
            {
                // Delete targets edges, not their draggable endpoint handles.
                var control = Session.ActiveMode == HouseEditorSession.Mode.Delete
                    ? HouseSceneQueries.PickFloorBand(plan, mouse)
                    : HouseSceneQueries.PickControl(plan, mouse, Session.Selection);
                if (control.HasValue)
                    return HouseSceneTarget.ForElement(control);
            }
            var building = HouseBuilding.For(plan);
            if (building != null)
            {
                if (Session.ActiveMode == HouseEditorSession.Mode.Slab)
                {
                    bool ceiling = Session.SlabCeiling;
                    var level = building.Find(plan);
                    var boundary = ceiling ? building.Above(level) : building.Below(level);
                    var cut = boundary == null ? null : HouseSceneQueries.PickSlabOpening(plan, boundary, ceiling, mouse,
                        !ceiling || !HouseLevelView.CeilingHidden(plan));
                    return HouseSceneTarget.ForCut(cut, ceiling);
                }
                var target = HouseSceneQueries.PickSlabTarget(plan, mouse);
                if (target.Cut.HasValue) return target;
            }
            return select && !SlabTool.HasGesture
                ? HouseSceneTarget.ForElement(HouseSceneQueries.PickEditSurface(plan, mouse)) : default;
        }

        internal void DrawFeedback(HousePlan plan, HouseSceneTarget hover)
        {
            using var sample = feedbackSample.Auto();
            // Surface overlays first, target outlines last. Selection survives unrelated
            // hover and input owners; plain-click selection replacement still clears it.
            feedback.BeginOutlines();
            try
            {
                SlabTool.DrawFeedback(plan, Session.ActiveMode == HouseEditorSession.Mode.Slab, hover);
                SelectionTool.DrawFeedback(plan, hover.Element);
                HouseTrimTool.Draw(Session, feedback, hover.Element);
                if (Session.ActiveMode == HouseEditorSession.Mode.Delete) deleteTool.DrawFeedback(plan, hover);
            }
            finally { feedback.EndOutlines(); }
        }

        public void Dispose()
        {
            Session.Pending.Interrupted -= CancelSceneGesture;
            HouseEditCommit.AuthoredChanged -= InvalidateSelection;
            HouseRebuildCoordinator.OutputsRefreshed -= OnOutputsRefreshed;
            CancelSceneGesture();
            feedback.Dispose();
        }

        private void DrawSnapGrid(HousePlan plan, ScenePointerInput input)
        {
            if (Event.current.type != EventType.Repaint || !HouseSceneQueries.GridSnapping(Session) || input.Navigating) return;
            var mouse = input.Event.mousePosition;
            Vector2 point;
            float height;
            if (SelectionTool.TrySnapCursor(plan, out point, out height))
                feedback.DrawSnapGrid(plan, point, height, Session.GridSize);
            else if ((Session.ActiveMode == HouseEditorSession.Mode.Wall || Session.ActiveMode == HouseEditorSession.Mode.Divider || Session.ActiveMode == HouseEditorSession.Mode.Boundary)
                || Session.ActiveMode == HouseEditorSession.Mode.Door || Session.ActiveMode == HouseEditorSession.Mode.Window)
            {
                if (!HouseSceneQueries.RaycastFloor(plan, HandleUtility.GUIPointToWorldRay(mouse), out point)) return;
                height = 0;
                feedback.DrawSnapGrid(plan, point, height, Session.GridSize);
            }
        }
    }
}
