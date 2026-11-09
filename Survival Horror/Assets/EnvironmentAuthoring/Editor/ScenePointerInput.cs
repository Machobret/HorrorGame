using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Shared native pointer policy for the active House or Town context.
    // One default control per event; controllers own capture, tools consume input.
    internal readonly struct ScenePointerInput
    {
        private static readonly Dictionary<SceneView, int> pendingReleases = new Dictionary<SceneView, int>();
        static ScenePointerInput() => SceneView.beforeSceneGui += ReleaseInOwner;

        private static void ReleaseInOwner(SceneView view)
        {
            if (pendingReleases.Count == 0) return;
            foreach (var closed in pendingReleases.Keys.Where(owner => owner == null).ToArray()) pendingReleases.Remove(closed);
            if (pendingReleases.Remove(view, out int control) && GUIUtility.hotControl == control)
                GUIUtility.hotControl = 0;
        }

        private static readonly int Hint = "EnvironmentAuthoring.SceneInput".GetHashCode();
        internal readonly int ControlId;
        internal readonly Event Event;
        internal readonly bool LeftMouseUp;
        internal ScenePointerInput(Event current)
        {
            Event = current;
            // Handles can consume the event while running. Keep the release
            // signal for the controller's final cleanup, without authorizing a commit.
            LeftMouseUp = current.type == EventType.MouseUp && current.button == 0;
            ControlId = GUIUtility.GetControlID(Hint, FocusType.Passive);
        }

        internal bool LeftMouseDown => Event.type == EventType.MouseDown
            && Event.button == 0 && HandleUtility.nearestControl == ControlId;
        internal bool RightMouseDown => Event.type == EventType.MouseDown && Event.button == 1
            && !Event.alt && (HandleUtility.nearestControl == ControlId || GUIUtility.hotControl == ControlId);
        internal bool SnapModifierChanged => (Event.type == EventType.KeyDown || Event.type == EventType.KeyUp)
            && (Event.keyCode == KeyCode.LeftShift || Event.keyCode == KeyCode.RightShift);
        internal bool DeleteRequested => !EditorGUIUtility.editingTextField &&
            ((Event.type == EventType.KeyDown && (Event.keyCode == KeyCode.Delete || Event.keyCode == KeyCode.Backspace))
            || (Event.type == EventType.ValidateCommand || Event.type == EventType.ExecuteCommand)
                && (Event.commandName == "Delete" || Event.commandName == "SoftDelete"));

        // Navigation is a native input state, not an inference from camera movement.
        // In particular, the last eased camera frame must not disable picking.
        // MouseMove carries no held button, even if its last-button field is still set.
        internal bool Navigating => Event.alt || Tools.viewToolActive || Event.type == EventType.ScrollWheel
            || (Event.type == EventType.MouseDown || Event.type == EventType.MouseDrag) && Event.button == 2;
        // The idle Hand tool suspends pointer picking, but does not own keyboard
        // cancellation/deletion until a native camera control actually captures.
        internal bool CameraOwnsInput => Tools.viewToolActive && (Tools.current != Tool.View || GUIUtility.hotControl != 0);
        internal bool CanHover => !Navigating && GUIUtility.hotControl == 0
            && Event.type != EventType.Used && Event.type != EventType.Ignore && Event.type != EventType.MouseLeaveWindow;

        // Used can replace rawType too: an earlier handler's consumed event
        // cancels our capture, never publishes it. Move/enter/leave mean no button is held.
        internal bool CancelsPointerCapture => Event.type == EventType.Used || Event.type == EventType.Ignore
            || Event.type == EventType.MouseMove
            || Event.type == EventType.MouseEnterWindow || Event.type == EventType.MouseLeaveWindow
            || Event.type == EventType.MouseDown && Event.button == 0
            || Event.type != EventType.Repaint && Event.type != EventType.Layout && Navigating;

        internal void ClaimLayout(bool editing)
        {
            // Default control is only a fallback; native handles keep their capture.
            // Register it even during a captured handle drag so a replacement press
            // can cancel that gesture and select normally when a release was missed.
            if (editing && Event.type == EventType.Layout && !Navigating)
                HandleUtility.AddDefaultControl(ControlId);
        }

        // Native GUI control IDs are scoped to their view. Controllers retain
        // ownership; this adapter releases only that view's matching control.
        internal static void Release(SceneView view, int control)
        {
            if (control == 0) return;
            if (view == null) return; // Closing a native view destroys its GUI state.
            if (SceneView.currentDrawingSceneView == view)
            {
                if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
                return;
            }
            // Re-entering another view's OnGUI can overwrite the caller's control.
            // Release once in the owner's next native event, even after context exit.
            pendingReleases[view] = control;
            view.Repaint();
        }
    }
}
