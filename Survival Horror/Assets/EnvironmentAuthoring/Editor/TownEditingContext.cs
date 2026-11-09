using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    [EditorToolContext("Town")]
    public sealed class TownEditingContext : EditorToolContext
    {
        internal enum EditMode { Select, Draw, Delete }
        [SerializeField] private TownAuthoring owner;
        [SerializeField] private EditMode mode;
        private TownSceneController controller;
        internal static TownEditingContext Active => ToolManager.activeContextType == typeof(TownEditingContext)
            ? Resources.FindObjectsOfTypeAll<TownEditingContext>().FirstOrDefault(ToolManager.IsActiveContext) : null;
        internal static TownAuthoring Selected => Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<TownAuthoring>() : null;
        internal TownAuthoring Owner => owner;
        internal TownSceneController Controller => controller ??= new TownSceneController(this);
        internal EditMode Mode { get => mode; set { Controller.Cancel(); mode = value; SceneView.RepaintAll(); } }
        internal string Message = "";
        [SerializeField] internal FenceFamily FenceBrowser;
        internal static void Enter(TownAuthoring target)
        {
            if (!TownCommands.CanEdit(target, out var reason)) { Debug.LogWarning(reason); return; }
            if (!HouseEditorSession.instance.ConfirmPendingChanges()) return;
            HouseEditingContext.Active?.ExitWithoutHistory();
            if (Active != null) Active.Exit();
            ToolManager.SetActiveContext<TownEditingContext>();
            var context = Active; context.owner = target; context.Mode = EditMode.Select;
            context.FenceBrowser = target is TownWallRun wall ? AssetDatabase.FindAssets("t:FenceFamily")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FenceFamily>)
                .FirstOrDefault(family => family.Panels != null && family.Panels.Contains(wall.Model)) : null;
            Selection.activeGameObject = target.gameObject; Tools.current = Tool.Move;
            HousePropertiesWindow.RevealForEditing(); SceneView.RepaintAll();
        }
        internal void Exit() => ToolManager.SetActiveContext<GameObjectToolContext>();
        protected override Type GetEditorToolType(Tool tool) => tool == Tool.Move ? typeof(TownMoveTool) : null;
        public override void OnActivated()
        {
            Undo.undoRedoPerformed += UndoChanged;
            Selection.selectionChanged += SelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        }
        public override void OnWillBeDeactivated()
        {
            Cancel(); Undo.undoRedoPerformed -= UndoChanged; Selection.selectionChanged -= SelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel; controller = null;
            HousePropertiesWindow.Existing?.Refresh(); SceneView.RepaintAll();
            HousePropertiesWindow.ContextExited();
        }
        private void Cancel() => controller?.Cancel();
        private void UndoChanged()
        {
            Cancel(); controller?.Prune();
            // Undoing creation removes the owner too; don't leave a dead editing context active.
            if (owner == null) { Exit(); return; }
            TownRebuild.Request(owner);
        }
        private void SelectionChanged()
        {
            // Native object selection can briefly clear during tool input. Only an
            // explicit selection of another scene object leaves this context.
            if (Selection.activeGameObject != null && owner != null && Selection.activeGameObject != owner.gameObject
                && !Selection.activeGameObject.transform.IsChildOf(owner.transform) && !EditorUtility.IsPersistent(Selection.activeGameObject)) Exit();
        }
        public override void OnToolGUI(EditorWindow window)
        {
            if (owner == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (window is SceneView view) Controller.Draw(view);
        }
    }
    public sealed class TownMoveTool : EditorTool
    {
        public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("MoveTool", "Move town points or spans");
        public override void OnWillBeDeactivated() => TownEditingContext.Active?.Controller.Cancel();
    }

    internal sealed class TownSceneController
    {
        private readonly TownEditingContext context;
        internal readonly HashSet<string> Points = new HashSet<string>(), Spans = new HashSet<string>();
        private string hoverPoint, hoverSpan, chain;
        private Vector3? draft;
        private Vector3 pointer;
        private TownPath preview;
        private string stamp;
        private SceneView captureView;
        private int capture;
        private bool nativeHandle, box;
        private Vector2 press;
        private Vector3 origin;
        private Dictionary<string, Vector3> moving;
        private Rect boxRect;
        private TownPath sampledPath;
        private readonly Dictionary<string, Vector3[]> sampled = new Dictionary<string, Vector3[]>();
        private TownAuthoring Target => context.Owner;
        internal bool HasGesture => capture != 0;
        internal void BeginChain(Vector3 point) { Cancel(); draft = point; }
        internal TownSceneController(TownEditingContext context) { this.context = context; }
        internal void Prune()
        {
            sampledPath = null;
            if (Target == null) return;
            Points.RemoveWhere(id => Target.Path.Find(id) == null); Spans.RemoveWhere(id => Target.Path.FindSpan(id) == null);
        }
        internal void Cancel()
        {
            int control = capture; var view = captureView;
            preview = null; capture = 0; captureView = null; moving = null; box = false; nativeHandle = false;
            draft = null; chain = null;
            ScenePointerInput.Release(view, control);
            SceneView.RepaintAll();
        }
        internal IEnumerable<string> SelectedPoints() => Points.Concat(Spans.SelectMany(id =>
        {
            var span = Target.Path.FindSpan(id); return span == null ? Array.Empty<string>() : new[] { span.Start, span.End };
        })).Distinct();
        internal void DeleteSelection()
        {
            if (Points.Count == 0 && Spans.Count == 0) return;
            TownCommands.Edit(Target, "Delete town selection", path => { path.Remove(Points, Spans); return true; }, out context.Message);
            Points.Clear(); Spans.Clear();
        }
        internal void Draw(SceneView view)
        {
            var current = Event.current; var input = new ScenePointerInput(current);
            input.ClaimLayout(true);
            // A destroyed view has no native control left to release. In another
            // live view, do not compare or release its unrelated control IDs.
            if (HasGesture && captureView == null) { capture = 0; Cancel(); }
            if (HasGesture && captureView != view) return;
            if (!TownCommands.CanEdit(Target, out var reason)) { if (HasGesture) Cancel(); context.Message = reason; return; }
            if (current.type == EventType.Ignore || current.type == EventType.MouseLeaveWindow
                || HasGesture && (input.CancelsPointerCapture || GUIUtility.hotControl != capture))
            { Cancel(); hoverPoint = hoverSpan = null; }
            // Capture owns keyboard input; idle navigation intent only suspends
            // pointer picking. The Hand tool must still allow logical selection commands.
            if (input.CameraOwnsInput)
            {
                if (input.DeleteRequested) current.Use();
                hoverPoint = hoverSpan = null; DrawLines(); return;
            }
            if (input.DeleteRequested)
            {
                if (current.type != EventType.ValidateCommand) DeleteSelection();
                current.Use(); return;
            }
            // Native navigation gets its own release/cancel events; path selection
            // and drawing intent are independent of that temporary camera capture.
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                if (HasGesture || draft.HasValue || chain != null) Cancel();
                else if (Points.Count + Spans.Count > 0) { Points.Clear(); Spans.Clear(); }
                else context.Exit();
                current.Use(); return;
            }
            if (input.Navigating) { hoverPoint = hoverSpan = null; DrawLines(); return; }
            if (HasGesture && stamp != JsonUtility.ToJson(Target.Path)) { Cancel(); return; }

            if (context.Mode == TownEditingContext.EditMode.Select && SelectedPoints().Any()) Handle(view, current);
            // Render picking must not recurse into Unity's Repaint pass. Only
            // pointer movement/press changes the hover target or placement point.
            if (input.CanHover && !HasGesture && (current.type == EventType.MouseMove || current.type == EventType.MouseDown))
            {
                Pick(current.mousePosition); pointer = Place(current.mousePosition, current.shift);
            }
            if (HasGesture && !nativeHandle)
            {
                if (current.type == EventType.MouseDrag)
                {
                    if (box) boxRect = Rect.MinMaxRect(Mathf.Min(press.x, current.mousePosition.x), Mathf.Min(press.y, current.mousePosition.y), Mathf.Max(press.x, current.mousePosition.x), Mathf.Max(press.y, current.mousePosition.y));
                    else if (Vector2.Distance(press, current.mousePosition) > 3)
                    {
                        preview ??= Target.Path.Copy(); var delta = Place(current.mousePosition, current.shift) - origin;
                        foreach (var pair in moving) preview.Find(pair.Key).Position = pair.Value + delta;
                    }
                    current.Use(); view.Repaint();
                }
                if (current.rawType == EventType.MouseUp && current.button == 0)
                {
                    if (!box && moving != null && Vector2.Distance(press, current.mousePosition) > 3)
                    {
                        preview ??= Target.Path.Copy(); var delta = Place(current.mousePosition, current.shift) - origin;
                        foreach (var pair in moving) preview.Find(pair.Key).Position = pair.Value + delta;
                    }
                    if (box && boxRect.width + boxRect.height > 5)
                    {
                        if (!current.shift && !current.control) { Points.Clear(); Spans.Clear(); }
                        foreach (var point in Target.Path.Points)
                            if (boxRect.Contains(HandleUtility.WorldToGUIPoint(Target.transform.TransformPoint(point.Position))))
                            { if (current.control) Points.Remove(point.Id); else Points.Add(point.Id); }
                    }
                    Commit(); current.Use();
                }
            }
            else if (input.LeftMouseDown && !HasGesture)
            {
                if (context.Mode == TownEditingContext.EditMode.Draw) DrawClick();
                else
                {
                    bool empty = hoverPoint == null && hoverSpan == null;
                    if (!current.shift && !current.control && (empty || !(hoverPoint != null ? Points.Contains(hoverPoint) : Spans.Contains(hoverSpan))))
                    { Points.Clear(); Spans.Clear(); }
                    if (hoverPoint != null) { if (current.control) Points.Remove(hoverPoint); else Points.Add(hoverPoint); }
                    else if (hoverSpan != null) { if (current.control) Spans.Remove(hoverSpan); else Spans.Add(hoverSpan); }
                    if (context.Mode == TownEditingContext.EditMode.Delete) DeleteSelection();
                    else
                    {
                        capture = input.ControlId; GUIUtility.hotControl = capture; captureView = view;
                        stamp = JsonUtility.ToJson(Target.Path); press = current.mousePosition; origin = pointer; box = empty;
                        moving = SelectedPoints().ToDictionary(id => id, id => Target.Path.Find(id).Position);
                    }
                }
                current.Use(); view.Repaint();
            }
            if (input.RightMouseDown && context.Mode == TownEditingContext.EditMode.Draw)
            { draft = null; chain = null; current.Use(); }
            DrawLines();
            if (box && HasGesture && current.type == EventType.Repaint)
            { Handles.BeginGUI(); GUI.Box(boxRect, GUIContent.none, "SelectionRect"); Handles.EndGUI(); }
            if (current.type == EventType.MouseMove) view.Repaint();
        }
        private void Handle(SceneView view, Event current)
        {
            var ids = SelectedPoints().ToArray(); var data = preview ?? Target.Path;
            var center = ids.Aggregate(Vector3.zero, (sum, id) => sum + data.Find(id).Position) / ids.Length;
            int before = GUIUtility.hotControl;
            EditorGUI.BeginChangeCheck();
            var world = Target.transform.TransformPoint(center);
            var moved = Handles.PositionHandle(world, Tools.pivotRotation == PivotRotation.Global ? Quaternion.identity : Target.transform.rotation);
            var next = Target.transform.InverseTransformPoint(moved);
            bool changed = EditorGUI.EndChangeCheck();
            if (before == 0 && GUIUtility.hotControl != 0)
            {
                capture = GUIUtility.hotControl; nativeHandle = true; captureView = view; stamp = JsonUtility.ToJson(Target.Path);
                preview = Target.Path.Copy();
            }
            if (changed && nativeHandle && preview != null)
            {
                var delta = next - center;
                foreach (var id in ids) preview.Find(id).Position += delta;
            }
            if (nativeHandle && capture != 0 && GUIUtility.hotControl == 0) Commit();
        }
        private void Commit()
        {
            var accepted = preview;
            if (accepted != null)
                TownCommands.Edit(Target, "Move town selection", path =>
                {
                    foreach (var point in accepted.Points) path.Find(point.Id).Position = point.Position;
                    return true;
                }, out context.Message);
            int control = capture; var view = captureView;
            capture = 0; nativeHandle = false; captureView = null; preview = null; moving = null; box = false; boxRect = default;
            ScenePointerInput.Release(view, control);
            Prune(); SceneView.RepaintAll();
        }
        private void DrawClick()
        {
            if (chain == null && !draft.HasValue)
            {
                if (hoverPoint != null) chain = hoverPoint; else draft = pointer;
                return;
            }
            var start = chain != null ? Target.Path.Find(chain).Position : draft.Value;
            if (Vector3.Distance(start, pointer) < .001f) { draft = null; chain = null; return; }
            string last = null;
            if (TownCommands.Edit(Target, "Draw town span", path =>
            {
                string from = chain ?? path.AddPoint(start); last = hoverPoint ?? path.AddPoint(pointer);
                return path.Connect(from, last) != null;
            }, out context.Message)) { chain = last; draft = null; }
        }
        private Vector3 Place(Vector2 mouse, bool bypassGrid)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            Vector3 world;
            var hit = Physics.RaycastAll(ray, 10000, ~0, QueryTriggerInteraction.Ignore)
                .Where(result => result.collider.gameObject.scene == Target.gameObject.scene && !result.collider.transform.IsChildOf(Target.transform))
                .OrderBy(result => result.distance).FirstOrDefault();
            if (hit.collider != null) world = hit.point;
            else if (new Plane(Vector3.up, Target.transform.position).Raycast(ray, out float distance)) world = ray.GetPoint(distance);
            else return pointer;
            var local = Target.transform.InverseTransformPoint(world);
            var session = HouseEditorSession.instance;
            if (session.SnapToGrid && !bypassGrid)
            { local.x = Mathf.Round(local.x / session.GridSize) * session.GridSize; local.z = Mathf.Round(local.z / session.GridSize) * session.GridSize; }
            // Shift bypasses only the grid. Source lines remain available, including
            // the selected span's original direction during a drag.
            float best = 10;
            foreach (var point in Target.Path.Points)
            {
                float d = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(Target.transform.TransformPoint(point.Position)));
                if (d < best) { best = d; local = point.Position; }
            }
            if (best < 10) return local;
            foreach (var span in Target.Path.Spans)
            {
                var samples = Sample(Target.Path, span);
                for (int i = 0; i + 1 < samples.Length; i++)
                {
                    var a = HandleUtility.WorldToGUIPoint(Target.transform.TransformPoint(samples[i]));
                    var b = HandleUtility.WorldToGUIPoint(Target.transform.TransformPoint(samples[i + 1])); var delta = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(mouse - a, delta) / Mathf.Max(.00001f, delta.sqrMagnitude));
                    float d = Vector2.Distance(mouse, a + t * delta);
                    if (d < best) { best = d; local = Vector3.Lerp(samples[i], samples[i + 1], t); }
                }
            }
            return local;
        }
        private void Pick(Vector2 mouse)
        {
            hoverPoint = hoverSpan = null; float best = 10;
            foreach (var point in Target.Path.Points)
            {
                float distance = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(Target.transform.TransformPoint(point.Position)));
                if (distance < best) { best = distance; hoverPoint = point.Id; }
            }
            if (hoverPoint != null) return;
            foreach (var span in Target.Path.Spans)
            {
                var samples = Sample(Target.Path, span);
                for (int i = 0; i + 1 < samples.Length; i++)
                {
                    float distance = HandleUtility.DistanceToLine(Target.transform.TransformPoint(samples[i]), Target.transform.TransformPoint(samples[i + 1]));
                    if (distance < best) { best = distance; hoverSpan = span.Id; }
                }
            }
            if (hoverSpan == null && context.Mode != TownEditingContext.EditMode.Draw) PickBody(HandleUtility.GUIPointToWorldRay(mouse));
        }
        private void PickBody(Ray ray)
        {
            var output = Target.Output;
            if (output == null || !output.gameObject.activeInHierarchy) return;
            // Native render picking respects the actual triangles and visibility.
            // A large combined road/sidewalk bounding box would select empty gaps.
            var picked = HandleUtility.PickGameObject(Event.current.mousePosition, false);
            var hit = picked != null ? picked.GetComponentInParent<TownOutputPart>() : null;
            if (hit == null || !hit.transform.IsChildOf(output.transform)) return;
            var localRay = new Ray(hit.transform.InverseTransformPoint(ray.origin), hit.transform.InverseTransformVector(ray.direction));
            if (!hit.Source.bounds.IntersectRay(localRay, out float boxDistance)) return;
            var position = Target.transform.InverseTransformPoint(hit.transform.TransformPoint(localRay.GetPoint(boxDistance)));
            string id = hit.Key.Split('/')[0];
            if (Target.Path.FindSpan(id) != null) { hoverSpan = id; return; }
            if (hit.Key.Contains("/post")) { hoverPoint = id; return; }
            // Joined sidewalk surfaces have one mesh, but selection remains a
            // logical span. Choose its nearest path instead of a component's ID.
            float nearest = float.PositiveInfinity;
            foreach (var span in Target.Path.Spans)
            {
                var samples = Sample(Target.Path, span);
                for (int i = 0; i + 1 < samples.Length; i++)
                {
                    var a = samples[i]; var delta = samples[i + 1] - a;
                    float t = Mathf.Clamp01(Vector3.Dot(position - a, delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                    float distance = (position - a - t * delta).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; hoverSpan = span.Id; }
                }
            }
        }
        private void DrawLines()
        {
            if (Event.current.type != EventType.Repaint) return;
            var data = preview ?? Target.Path;
            using var scope = new Handles.DrawingScope(Target.transform.localToWorldMatrix);
            foreach (var span in data.Spans)
            {
                Handles.color = Spans.Contains(span.Id) ? Color.white : hoverSpan == span.Id ? Color.yellow : new Color(.4f, .75f, .9f, .7f);
                Handles.DrawAAPolyLine(Spans.Contains(span.Id) || hoverSpan == span.Id ? 4 : 2, Sample(data, span));
            }
            foreach (var point in data.Points)
            {
                Handles.color = Points.Contains(point.Id) ? Color.yellow : hoverPoint == point.Id ? Color.white : Color.cyan;
                Handles.DotHandleCap(0, point.Position, Quaternion.identity, HandleUtility.GetHandleSize(Target.transform.TransformPoint(point.Position)) * .06f, EventType.Repaint);
            }
            Vector3? start = chain != null ? data.Find(chain)?.Position : draft;
            if (context.Mode == TownEditingContext.EditMode.Draw && start.HasValue)
            { Handles.color = Color.yellow; Handles.DrawAAPolyLine(3, start.Value, pointer); }
            if (!(Target is TownWallRun))
            {
                var outline = data;
                if (context.Mode == TownEditingContext.EditMode.Draw && start.HasValue && Vector3.Distance(start.Value, pointer) > .001f)
                {
                    outline = data.Copy();
                    outline.Connect(outline.AddPoint(start.Value), outline.AddPoint(pointer));
                }
                float half = Target is TownRoad road ? road.Width / 2 : ((TownSidewalk)Target).Width / 2;
                var elevation = TownPathGeometry.Heights(outline);
                void DrawOutline(float left, float right, Color color, float height)
                {
                    Handles.color = color;
                    foreach (var polygon in TownPathGeometry.Footprint(outline, left, right))
                    {
                        var points = polygon.Select(p => new Vector3((float)p.x,
                            elevation(new Vector2((float)p.x, (float)p.y)) + height + .015f, (float)p.y)).ToList();
                        if (points.Count > 0) { points.Add(points[0]); Handles.DrawAAPolyLine(2, points.ToArray()); }
                    }
                }
                DrawOutline(half, half, Color.yellow, 0);
                if (Target is TownRoad street && (street.LeftSidewalk || street.RightSidewalk))
                    DrawOutline(half + (street.LeftSidewalk ? street.SidewalkWidth : 0),
                        half + (street.RightSidewalk ? street.SidewalkWidth : 0), Color.cyan, street.SidewalkHeight);
            }
        }
        private Vector3[] Sample(TownPath path, TownPath.Span span)
        {
            if (path != Target.Path) return TownPathGeometry.Samples(path, span);
            if (sampledPath != path) { sampled.Clear(); sampledPath = path; }
            if (!sampled.TryGetValue(span.Id, out var values)) sampled[span.Id] = values = TownPathGeometry.Samples(path, span);
            return values;
        }
    }
}
