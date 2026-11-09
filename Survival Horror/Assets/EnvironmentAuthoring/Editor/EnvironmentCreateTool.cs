using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    // Only first-click placement lives here. After creation, the existing context owns all drawing/input.
    [EditorTool("Place environment object")]
    public sealed class EnvironmentCreateTool : EditorTool
    {
        private EnvironmentCreateCommands.Kind kind;
        private SceneView owner;
        private Vector3? cursor;
        internal static EnvironmentCreateTool Active => ToolManager.activeToolType == typeof(EnvironmentCreateTool)
            ? Resources.FindObjectsOfTypeAll<EnvironmentCreateTool>().FirstOrDefault(ToolManager.IsActiveTool) : null;
        internal EnvironmentCreateCommands.Kind Pending => kind;
        public override GUIContent toolbarIcon => new GUIContent(EditorGUIUtility.IconContent("Prefab Icon").image, "Place a House, Wall, Road or Sidewalk from Environment Tools");
        public override bool IsAvailable() => !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static void Arm(EnvironmentCreateCommands.Kind kind, SceneView view)
        {
            if (kind == EnvironmentCreateCommands.Kind.None || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!HouseEditorSession.instance.ConfirmPendingChanges()) return;
            view ??= EditorWindow.GetWindow<SceneView>();
            HouseEditingContext.Active?.Exit();
            TownEditingContext.Active?.Exit();
            ToolManager.SetActiveContext<GameObjectToolContext>();
            ToolManager.SetActiveTool<EnvironmentCreateTool>();
            var tool = Active; tool.kind = kind; tool.owner = view; tool.cursor = null;
            SceneView.RepaintAll();
        }
        internal void Cancel() { Tools.current = Tool.Move; SceneView.RepaintAll(); }
        public override void OnWillBeDeactivated() { kind = EnvironmentCreateCommands.Kind.None; owner = null; cursor = null; }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view)) return;
            if (owner == null || kind == EnvironmentCreateCommands.Kind.None) { Cancel(); return; }
            if (owner != view) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); Cancel(); return; }
            var input = new ScenePointerInput(e); input.ClaimLayout(true);
            if (input.Navigating) { cursor = null; return; }
            if (e.type == EventType.MouseMove || input.LeftMouseDown)
            {
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (HandleUtility.PlaceObject(e.mousePosition, out var point, out _)) cursor = point;
                else cursor = new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance) ? ray.GetPoint(distance) : null;
                if (cursor.HasValue && HouseEditorSession.instance.SnapToGrid && !e.shift)
                {
                    var snapped = cursor.Value; float grid = HouseEditorSession.instance.GridSize;
                    snapped.x = Mathf.Round(snapped.x / grid) * grid; snapped.z = Mathf.Round(snapped.z / grid) * grid;
                    cursor = snapped;
                }
                view.Repaint();
            }
            if (cursor.HasValue && e.type == EventType.Repaint)
                using (new Handles.DrawingScope(Color.green))
                    Handles.DrawWireDisc(cursor.Value, Vector3.up, HandleUtility.GetHandleSize(cursor.Value) * .12f);
            if (input.LeftMouseDown)
            {
                e.Use();
                if (!cursor.HasValue) return;
                var point = cursor.Value; var chosen = kind;
                EnvironmentCreateCommands.Create(chosen, SceneManager.GetActiveScene(), point, view);
            }
        }
    }
}
