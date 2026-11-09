using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Native handles borrow the session's one copied candidate. Cutting happens
    // once on release; a drag only updates a wire preview, not generated meshes.
    internal sealed class HouseBooleanTool
    {
        private readonly HouseEditorSession session;
        private readonly HouseEditorController controller;
        private string dragId;
        private Vector3? placement;
        internal HouseBooleanTool(HouseEditorSession session, HouseEditorController controller)
        { this.session = session; this.controller = controller; }
        internal bool HasGesture => controller.CapturedBy(HouseEditorController.PointerOwner.Boolean);
        internal HouseStack.BooleanVolume Selected => session.Selection.Items.Count == 1
            && session.Selection.Items[0].Type == HouseEditSelection.Kind.BooleanShape
            ? HouseBuilding.For(session.SessionPlan)?.Data.BooleanVolumes.FirstOrDefault(volume => volume.Id == session.Selection.Items[0].Id) : null;

        internal void Arm(HouseStack.BooleanShape shape)
        {
            if (!session.ConfirmPendingChanges()) return;
            controller.CancelSceneGesture();
            session.BooleanShape = shape;
            session.PlaceBooleanShape = true;
            session.ShowBooleanShapes = true;
            session.ClearSelection();
            HouseSceneUI.Activate(HouseEditorSession.Mode.Boolean);
            HousePropertiesWindow.ShowSelection();
            SceneView.RepaintAll();
        }

        internal void Cancel()
        {
            bool owned = HasGesture;
            controller.ReleasePointer(HouseEditorController.PointerOwner.Boolean);
            dragId = null; placement = null;
            if (owned) session.Pending.ReleaseGeometry();
        }

        internal void OnSceneGUI(HousePlan plan, SceneView view, ScenePointerInput input)
        {
            var e = input.Event;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            var raw = e.rawType;
            if (!plan.HasSupportedTransform) { Cancel(); DrawHelpers(); return; }
            if (HasGesture && !controller.IsGestureOwner(view)) { DrawHelpers(); return; }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && !EditorGUIUtility.editingTextField)
            {
                if (HasGesture) Cancel();
                else if (session.PlaceBooleanShape) { session.PlaceBooleanShape = false; placement = null; }
                else if (session.Selection.Items.Count > 0) controller.ClearSelection();
                else HouseEditingContext.Active?.Exit();
                e.Use(); view.Repaint(); return;
            }
            if (input.LeftMouseDown && session.Pending.IsProperty && !session.Pending.IsPending) session.Pending.Discard();
            if (input.Navigating)
            { placement = null; DrawHelpers(); return; }

            var selected = Selected;
            if (!session.PlaceBooleanShape && selected != null && !session.Pending.IsProperty)
            {
                var displayed = HasGesture ? session.Pending.Preview?.Building.BooleanVolumes.FirstOrDefault(volume => volume.Id == dragId) : selected;
                if (displayed != null)
                {
                    var center = displayed.Center; var rotation = displayed.Rotation; var size = displayed.Size;
                    int previousControl = GUIUtility.hotControl;
                    EditorGUI.BeginChangeCheck();
                    using (new Handles.DrawingScope(building.transform.localToWorldMatrix))
                    {
                        if (session.BooleanGizmo == Tool.Scale)
                            size = Handles.ScaleHandle(size, center, rotation, HandleUtility.GetHandleSize(center));
                        else if (session.BooleanGizmo == Tool.Rotate) rotation = Handles.RotationHandle(rotation, center);
                        else center = Handles.PositionHandle(center, Tools.pivotRotation == PivotRotation.Local
                            ? rotation : Quaternion.Inverse(building.transform.rotation));
                    }
                    bool changed = EditorGUI.EndChangeCheck();
                    if (!HasGesture && previousControl == 0 && raw == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl != 0)
                    {
                        controller.CapturePointer(HouseEditorController.PointerOwner.Boolean, view, GUIUtility.hotControl);
                        if (session.Pending.TryBeginGeometry(plan, out string reason))
                            dragId = selected.Id;
                        else { controller.ReleasePointer(HouseEditorController.PointerOwner.Boolean); session.Message = reason; }
                    }
                    if (changed && HasGesture)
                    {
                        size = Vector3.Max(Vector3.one * .01f, size);
                        if (!session.Pending.Preview.Building.TrySetBooleanVolume(dragId, selected.Shape, center, rotation, size, out string reason))
                            session.Message = reason;
                    }
                    if (HasGesture && raw == EventType.MouseUp && e.button == 0)
                    {
                        var result = HouseBooleanCommands.PublishTransform(session.Pending.Preview);
                        Cancel(); session.Message = result.Message;
                        view.Repaint();
                    }
                }
            }
            DrawHelpers();
            if (HasGesture || e.type == EventType.Layout || !input.CanHover) return;
            if (session.PlaceBooleanShape)
            {
                if (session.Pending.IsProperty)
                {
                    placement = null;
                    if (input.LeftMouseDown)
                    {
                        session.Message = "Apply or discard the pending property values before changing geometry.";
                        e.Use();
                    }
                    return;
                }
                // Query visible surfaces only on pointer input, never on camera
                // repaints. Hidden floors and empty space are not placement targets.
                if (e.type == EventType.MouseMove || input.LeftMouseDown)
                    placement = Placement(plan, e.mousePosition);
                if (placement.HasValue && e.type == EventType.Repaint)
                    Draw(building, session.BooleanShape, placement.Value, Quaternion.identity, Vector3.one, new Color(1, .65f, .2f));
                if (input.LeftMouseDown && placement.HasValue)
                {
                    Vector3 center = placement.Value;
                    var result = controller.RunCommand(() => HouseBooleanCommands.Set(plan, null, session.BooleanShape, center, Quaternion.identity, Vector3.one));
                    if (result.Applied)
                    {
                        session.PlaceBooleanShape = false;
                        controller.Select(HouseEditSelection.Kind.BooleanShape, building.Data.BooleanVolumes.Last().Id);
                        HousePropertiesWindow.ShowSelection();
                    }
                    e.Use();
                }
            }
            else if (input.LeftMouseDown)
            {
                string id = session.ShowBooleanShapes ? Pick(building, HandleUtility.GUIPointToWorldRay(e.mousePosition)) : null;
                if (id == null) controller.ClearSelection();
                else { controller.Select(HouseEditSelection.Kind.BooleanShape, id); HousePropertiesWindow.ShowSelection(); }
                e.Use();
            }
            if (e.type == EventType.MouseMove) view.Repaint();
        }

        private Vector3? Placement(HousePlan plan, Vector2 mouse)
        {
            var building = HouseBuilding.For(plan);
            var world = HouseSceneQueries.VisibleSurfacePoint(plan, mouse);
            if (!world.HasValue) return null;
            var local = building.transform.InverseTransformPoint(world.Value);
            if (HouseSceneQueries.GridSnapping(session))
            {
                local.x = Mathf.Round(local.x / session.GridSize) * session.GridSize;
                local.z = Mathf.Round(local.z / session.GridSize) * session.GridSize;
            }
            return local;
        }

        internal void DrawHelpers()
        {
            if (Event.current.type != EventType.Repaint || !session.ShowBooleanShapes) return;
            var building = HouseBuilding.For(session.SessionPlan);
            if (building == null) return;
            var volumes = HasGesture && session.Pending.Preview != null ? session.Pending.Preview.Building.BooleanVolumes : building.Data.BooleanVolumes;
            foreach (var volume in volumes)
                Draw(building, volume.Shape, volume.Center, volume.Rotation, volume.Size,
                    session.Selection.Contains(HouseEditSelection.Kind.BooleanShape, volume.Id) ? new Color(1, .75f, .2f) : new Color(.9f, .5f, .25f, .65f));
        }

        private static void Draw(HouseBuilding building, HouseStack.BooleanShape shape, Vector3 center, Quaternion rotation, Vector3 size, Color color)
        {
            using (new Handles.DrawingScope(color, building.transform.localToWorldMatrix * Matrix4x4.TRS(center, rotation, size)))
                if (shape == HouseStack.BooleanShape.Box) Handles.DrawWireCube(Vector3.zero, Vector3.one);
                else if (shape == HouseStack.BooleanShape.Icosphere) Handles.DrawLines(HouseBooleanMesh.IcosphereEdges);
        }

        internal static string Pick(HouseBuilding building, Ray ray)
        {
            string id = null; float nearest = float.PositiveInfinity;
            foreach (var volume in building.Data.BooleanVolumes)
            {
                var matrix = building.transform.localToWorldMatrix * volume.Matrix;
                var inverse = matrix.inverse;
                var localRay = new Ray(inverse.MultiplyPoint3x4(ray.origin), inverse.MultiplyVector(ray.direction).normalized);
                float distance;
                if (volume.Shape == HouseStack.BooleanShape.Box)
                {
                    if (!new Bounds(Vector3.zero, Vector3.one).IntersectRay(localRay, out distance)) continue;
                }
                else if (volume.Shape == HouseStack.BooleanShape.Icosphere)
                {
                    if (!HouseBooleanMesh.RaycastIcosphere(localRay, out distance)) continue;
                }
                else continue;
                float worldDistance = Vector3.Distance(ray.origin, matrix.MultiplyPoint3x4(localRay.GetPoint(distance)));
                if (worldDistance >= nearest) continue;
                nearest = worldDistance; id = volume.Id;
            }
            return id;
        }
    }
}
