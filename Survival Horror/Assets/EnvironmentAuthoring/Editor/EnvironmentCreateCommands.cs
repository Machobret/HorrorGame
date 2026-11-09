using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    // Menus and the palette share initialized source creation and existing context/output owners.
    internal static class EnvironmentCreateCommands
    {
        internal enum Kind { None, House, Wall, Road, Sidewalk }

        internal static GameObject Create(Kind kind, Scene scene, Vector3 floorPoint, SceneView view)
        {
            if ((kind < Kind.House || kind > Kind.Sidewalk) || !scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode
                || !float.IsFinite(floorPoint.x) || !float.IsFinite(floorPoint.y) || !float.IsFinite(floorPoint.z)) return null;
            if (!HouseEditorSession.instance.ConfirmPendingChanges()) return null;
            // The placement tool must not become the tool restored when the new edit context closes.
            Tools.current = Tool.Move;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            string action = "Create " + kind;
            GameObject root;
            if (kind == Kind.House)
            {
                var plan = HouseStructureCommands.CreateHouse(scene, floorPoint);
                root = HouseBuilding.For(plan).gameObject;
                Selection.activeGameObject = root;
                if (HouseEditingContext.Enter(plan))
                {
                    ToolManager.SetActiveTool<HouseOutlineTool>();
                    var controller = HouseEditingContext.Active.Controller;
                    controller.SetMode(HouseEditorSession.Mode.Wall);
                    controller.BeginWallChain(view, Vector2.zero);
                }
            }
            else
            {
                root = new GameObject(kind.ToString()); SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = floorPoint;
                TownAuthoring target = kind == Kind.Road ? root.AddComponent<TownRoad>()
                    : kind == Kind.Sidewalk ? root.AddComponent<TownSidewalk>() : root.AddComponent<TownWallRun>();
                EnvironmentProjectSettings.instance.Apply(target);
                Undo.RegisterCreatedObjectUndo(root, action);
                Selection.activeGameObject = root; TownEditingContext.Enter(target);
                var context = TownEditingContext.Active;
                if (context != null)
                {
                    context.Mode = TownEditingContext.EditMode.Draw;
                    context.Controller.BeginChain(Vector3.zero);
                }
            }
            string setup = EnvironmentProjectSettings.instance.MissingSetup(kind);
            if (setup.Length > 0)
            {
                if (kind == Kind.House) HouseEditorSession.instance.Message = setup;
                else if (TownEditingContext.Active != null) TownEditingContext.Active.Message = setup;
                if (view != null) view.ShowNotification(new GUIContent(setup));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.FlushUndoRecordObjects(); Undo.SetCurrentGroupName(action);
            Undo.CollapseUndoOperations(group); Undo.IncrementCurrentGroup();
            SceneView.RepaintAll(); return root;
        }
    }
}
