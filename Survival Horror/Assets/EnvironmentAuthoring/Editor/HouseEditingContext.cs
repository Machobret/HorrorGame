using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // The context owns the editing boundary and temporary view state, never a copy of the House.
    [InitializeOnLoad, EditorToolContext("House")]
    public sealed class HouseEditingContext : EditorToolContext
    {
        static HouseEditingContext() => Selection.selectionChanged += QueueHouseRootSelection;

        private static void QueueHouseRootSelection()
        {
            if (!(EditorWindow.focusedWindow is SceneView) || Active != null) return;
            // Unity can ignore selection assignments inside its own notification.
            // Resolve the current selection once afterward, without retaining old targets.
            EditorApplication.delayCall -= SelectHouseRootsOutsideEditing;
            EditorApplication.delayCall += SelectHouseRootsOutsideEditing;
        }

        private static void SelectHouseRootsOutsideEditing()
        {
            // Unity cycles past SelectionBase on later clicks. Generated pieces are
            // not independent level objects; keep Scene selection on their House.
            // Hierarchy/Inspector selection and our semantic edit tools stay untouched.
            if (!(EditorWindow.focusedWindow is SceneView) || Active != null) return;
            var selected = Selection.objects;
            var roots = Array.ConvertAll(selected, item =>
            {
                if (!(item is GameObject go) || EditorUtility.IsPersistent(go)) return item;
                var door = go.GetComponentInParent<HouseDoorObject>();
                if (door != null) return door.gameObject;
                bool housePart = go.GetComponent<HousePlan>() != null
                    || go.GetComponentInParent<HouseWallOutput>() != null
                    || go.GetComponentInParent<HouseSlabOutput>() != null;
                var building = housePart ? go.GetComponentInParent<HouseBuilding>() : null;
                return building != null ? building.gameObject : item;
            }).Distinct().ToArray();
            // The replacement notification sees the same roots and stops here.
            if (!selected.SequenceEqual(roots)) Selection.objects = roots;
        }

        [SerializeField] private bool isolated;
        [SerializeField] private string previousContext;
        [SerializeField] private Tool previousTool = Tool.Move;
        [SerializeField] private string previousCustomTool;
        private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Renderer, EditorSelectedRenderState> nativeHighlights = new Dictionary<Renderer, EditorSelectedRenderState>();
        private bool leaving;
        private HouseEditorController controller;
        internal readonly HouseSceneCamera Camera = new HouseSceneCamera();
        internal HouseEditorController Controller
        {
            get { return controller ?? (controller = new HouseEditorController(Session)); }
        }
        internal static HousePlan SelectionPlan => Selection.activeGameObject == null
            || Selection.activeGameObject.GetComponentInParent<HouseDoorObject>() != null ? null
            : Selection.activeGameObject.GetComponentInParent<HousePlan>()
                ?? Selection.activeGameObject.GetComponentInParent<HouseBuilding>()?.Levels.Find(l => l.Plan != null)?.Plan;

        internal static HouseEditingContext Active => Array.Find(Resources.FindObjectsOfTypeAll<HouseEditingContext>(), ToolManager.IsActiveContext);
        internal HouseEditorSession Session => HouseEditorSession.instance;
        internal GameObject Root => Session?.SessionPlan == null ? null
            : HouseBuilding.For(Session.SessionPlan)?.gameObject ?? Session.SessionPlan.gameObject;
        internal bool Isolated => isolated;

        internal static bool SelectedWallFrameBounds(Component target, out Bounds bounds)
        {
            bounds = default;
            var context = Active;
            if (context?.Root == null || target == null || !target.transform.IsChildOf(context.Root.transform)) return false;
            return HouseSceneQueries.SelectedWallBounds(context.Session.SessionPlan, context.Session.Selection, out bounds);
        }

        protected override Type GetEditorToolType(Tool tool) => tool == Tool.Move ? typeof(HouseMoveTool)
            : Session.ActiveMode == HouseEditorSession.Mode.Boolean && tool == Tool.Rotate ? typeof(HouseBooleanRotateTool)
            : Session.ActiveMode == HouseEditorSession.Mode.Boolean && tool == Tool.Scale ? typeof(HouseBooleanScaleTool) : null;

        internal static bool Enter(HousePlan plan, bool revealProperties = true)
        {
            if (plan == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (!HouseEditValidation.Source(plan, out string reason))
            {
                HouseEditorSession.instance.Message = reason;
                return false;
            }
            var previous = Active;
            // Re-entering the current target reveals its properties without resetting
            // selection, drawing or typed property values.
            if (previous?.Session?.SessionPlan == plan)
            {
                if (revealProperties) HousePropertiesWindow.RevealForEditing();
                return true;
            }
            var retained = HouseEditorSession.instance.Pending;
            bool returningToDraft = retained?.Suspended == true && retained.Plan == plan;
            if (!returningToDraft
                && !HouseEditorSession.instance.ConfirmPendingChanges()) return false;
            HouseContextHistory.Change(plan);
            bool entered = Active?.Session?.SessionPlan == plan;
            if (entered && revealProperties) HousePropertiesWindow.RevealForEditing();
            return entered;
        }

        internal static void SelectLevel(HousePlan plan)
        {
            var previous = Active?.Session?.SessionPlan;
            var view = SceneView.lastActiveSceneView;
            if (!Enter(plan, revealProperties: false)) return;
            // Keep editing can reject the target change. Do not move the camera or
            // cutaway to a level which never became the active editing target.
            if (Active?.Session?.SessionPlan != plan) return;
            if (previous != null && plan != null && previous != plan && view != null)
            {
                // Track the nominal level plane, not one arbitrary room's local offset.
                view.LookAt(view.pivot + plan.transform.position - previous.transform.position,
                    view.rotation, view.size, view.orthographic);
            }
            HouseLevelView.Set(plan);
            SceneView.RepaintAll();
        }

        internal static void FollowLevelHeightChange(HousePlan plan, float previousElevation)
        {
            if (plan == null || Active?.Session.SessionPlan != plan) return;
            var view = SceneView.lastActiveSceneView;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            // Accepted spans change immediately; output moves native transforms later.
            // Follow the nominal level delta without forcing a geometry rebuild.
            var delta = building.transform.TransformVector(Vector3.up * (building.Data.Elevation(building.Find(plan)) - previousElevation));
            if (view != null && delta != Vector3.zero)
                view.LookAt(view.pivot + delta, view.rotation, view.size, view.orthographic);
        }

        internal static void ChooseLevelInheritance(HouseBuilding building, bool below, Rect anchor)
        {
            UnityEditor.PopupWindow.Show(anchor, new HouseLevelCreationPopup(building, below));
        }

        internal static void AddLevelAndSelect(HouseBuilding building, bool below, HouseLevelInheritance inheritance = HouseLevelInheritance.OutsideContours)
        {
            if (!HouseEditorSession.instance.ConfirmPendingChanges()) return;
            SelectLevel(HouseEditCommands.AddLevel(building, below, inheritance: inheritance).Plan);
        }

        internal static void DeleteLevel(HousePlan plan)
        {
            if (!HouseStructureCommands.CanDeleteLevel(plan, out string reason))
            { HouseEditorSession.instance.Message = reason; return; }
            if (!HouseEditorSession.instance.ConfirmPendingChanges()) return;
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            int index = building.Levels.FindIndex(level => level.Plan == plan);
            var fallback = building.Levels[index == 0 ? 1 : index - 1].Plan;
            var result = HouseStructureCommands.DeleteLevel(plan, message =>
            {
                if (!EditorUtility.DisplayDialog("Delete level", message, "Delete", "Cancel")) return false;
                // Change the editing target before destroying it. Storey navigation
                // stays inside the House and does not add a separate Undo step.
                if (Active?.Session.SessionPlan == plan) SelectLevel(fallback);
                Active?.Controller.CancelSceneGesture();
                return true;
            });
            HouseEditorSession.instance.Message = result.Message;
            if (result.Status == HouseEditStatus.Rejected)
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(result.Message));
        }

        internal static bool CycleLevel(HousePlan plan, int direction)
        {
            var building = HouseBuilding.For(plan);
            if (building == null) return false;
            int index = building.Levels.FindIndex(l => l.Plan == plan) + direction;
            if (index < 0 || index >= building.Levels.Count || building.Levels[index].Plan == null) return false;
            SelectLevel(building.Levels[index].Plan);
            return true;
        }

        internal static void EnterWithoutHistory(HousePlan plan, bool selectObject = true)
        {
            if (plan == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var oldContext = ToolManager.activeContextType;
            var oldTool = Tools.current;
            var oldCustom = ToolManager.activeToolType;
            bool preserveTool = oldContext == typeof(HouseEditingContext);
            bool sameHouse = preserveTool && Active?.Root == (HouseBuilding.For(plan)?.gameObject ?? plan.gameObject);
            ToolManager.SetActiveContext<HouseEditingContext>();
            var context = Active;
            if (oldContext != typeof(HouseEditingContext))
            {
                context.previousContext = oldContext.AssemblyQualifiedName;
                context.previousTool = oldCustom == typeof(HouseOutlineTool) ? Tool.Move : oldTool;
                context.previousCustomTool = oldCustom.AssemblyQualifiedName;
            }
            context.RestoreIsolation();
            // End transient property capture before changing target. Retained values
            // resume only for their original source; a window is not required.
            context.Session.Pending.Interrupt();
            context.Controller.BeginSession(plan, preserveTool);
            context.Session.Pending.Resume(plan);
            // Level changes use the Session target; reselecting a GameObject would add
            // an unrelated native selection Undo step above the last geometry action.
            if (selectObject && !sameHouse) Selection.objects = new UnityEngine.Object[] { plan.gameObject };
            context.ApplyIsolation();
            // Tool intent belongs to the context, including when Undo removed its
            // previous level. New context entry still starts with Select/Move.
            if (!preserveTool) Tools.current = Tool.Move;
            SceneView.RepaintAll();
        }

        public override void OnActivated()
        {
            leaving = false;
            // On activation Unity may still be resolving the manipulation tool.
            EditorApplication.delayCall += ResumeAfterActivation;
        }

        private void OnEnable()
        {
            SceneView.beforeSceneGui += SuppressNativeHighlight;
            SceneView.duringSceneGui += RestoreNativeHighlight;
            // Hot reload can retain private fields without calling OnActivated again.
            leaving = false;
            Selection.selectionChanged += RestrictSelection;
            // Scripted selection changes may bypass selectionChanged on this Unity version.
            EditorApplication.update += RestrictSelection;
            EditorApplication.update += CheckGestureOwner;
            Undo.undoRedoPerformed += OnUndoRedo;
            ToolManager.activeContextChanged += ContextChanged;
            HouseRebuildCoordinator.OutputsRefreshed += OnOutputsRefreshed;
            EditorApplication.hierarchyChanged += HierarchyChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.delayCall += ResumeAfterActivation;
        }

        private void OnDisable()
        {
            Session.Pending.Interrupt();
            RestoreNativeHighlight(null);
            SceneView.beforeSceneGui -= SuppressNativeHighlight;
            SceneView.duringSceneGui -= RestoreNativeHighlight;
            Camera.Restore();
            RestoreIsolation();
            Selection.selectionChanged -= RestrictSelection;
            EditorApplication.update -= RestrictSelection;
            EditorApplication.update -= CheckGestureOwner;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ToolManager.activeContextChanged -= ContextChanged;
            HouseRebuildCoordinator.OutputsRefreshed -= OnOutputsRefreshed;
            EditorApplication.delayCall -= Resume;
            EditorApplication.delayCall -= ResumeAfterActivation;
            controller?.Dispose();
            controller = null;
            EditorApplication.hierarchyChanged -= HierarchyChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
        }

        private void OnOutputsRefreshed(HousePlan plan)
        {
            if (!ToolManager.IsActiveContext(this) || leaving || plan == null || !Contains(plan.gameObject)) return;
            // Derived output knows nothing about editor visibility. Restore our temporary view here.
            HouseLevelView.Apply();
            ApplyIsolation();
        }

        private void CheckGestureOwner()
        {
            if (ToolManager.IsActiveContext(this)) controller?.CheckGestureOwnerLifetime();
        }

        private void OnUndoRedo()
        {
            if (!ToolManager.IsActiveContext(this)) return;
            Controller.OnUndoRedo();
            // Native history can restore renderers without running a builder.
            // Temporary cutaway/isolation belongs to this view, not saved output.
            HouseLevelView.Apply();
            ApplyIsolation();
        }

        private void ContextChanged()
        {
            // Unity also deactivates the retained context during script reload.
            // Only an actual context replacement is an Undoable external departure.
            if (!ToolManager.IsActiveContext(this) && leaving)
            {
                HouseContextHistory.ExternalExit();
                HousePropertiesWindow.ContextExited();
            }
        }

        private void Resume()
        {
            if (this == null || !ToolManager.IsActiveContext(this) || leaving) return;
            if (Session?.SessionPlan == null)
            {
                var plan = SelectionPlan;
                if (plan != null) Enter(plan);
                else Exit();
                return;
            }
            _ = Controller; // Recompose after reload even when Unity did not call OnActivated.
            HouseContextHistory.ResumeTarget(Session.SessionPlan);
            RestrictSelection();
            HouseLevelView.Set(Session.SessionPlan);
            ApplyIsolation();
            SceneView.RepaintAll();
        }

        private void ResumeAfterActivation()
        {
            // A delayed activation callback may arrive after a user interrupted a
            // property preview. Resume retained values only when rebuilding after reload.
            bool recompose = controller == null;
            Resume();
            if (recompose && ToolManager.IsActiveContext(this) && !leaving)
                Session.Pending.Resume(Session.SessionPlan);
        }

        public override void OnWillBeDeactivated()
        {
            Session.Pending.Interrupt();
            RestoreNativeHighlight(null);
            Camera.Restore();
            leaving = true;
            controller?.Dispose();
            controller = null;
            HouseLevelView.Restore();
            RestoreIsolation();
            SceneView.RepaintAll();
        }

        internal void Exit()
        {
            if (!HouseEditorSession.instance.ConfirmPendingChanges()) return;
            HouseContextHistory.Change(null);
        }

        internal void ExitWithoutHistory()
        {
            var closingController = Controller;
            var contextType = Type.GetType(previousContext ?? "") ?? typeof(GameObjectToolContext);
            ToolManager.SetActiveContext(contextType);
            if (previousTool == Tool.Custom)
            {
                var type = Type.GetType(previousCustomTool ?? "");
                if (type != null && type != typeof(HouseOutlineTool) && type != typeof(HouseMoveTool)) ToolManager.SetActiveTool(type);
                else Tools.current = Tool.Move;
            }
            else Tools.current = previousTool;
            closingController.CloseSession();
        }

        internal bool Contains(GameObject gameObject) => Root != null && gameObject != null
            && (gameObject == Root || gameObject.transform.IsChildOf(Root.transform));

        private void RestrictSelection()
        {
            if (!ToolManager.IsActiveContext(this) || leaving || Root == null || !HouseContextHistory.OwnsSelection(Root)) return;
            // Hierarchy clicks must not expose an unrelated scene object's editable Inspector.
            // Project assets remain selectable for material/kit authoring.
            if (Selection.gameObjects.Any(o => !EditorUtility.IsPersistent(o) && !Contains(o)))
            {
                Controller.ClearSelection();
                Selection.objects = new UnityEngine.Object[] { Session.SessionPlan.gameObject };
            }
            // Unity may clear its object selection while our Scene tool owns the click.
            // Logical House selection is cleared by explicit Scene gestures or Escape,
            // never by the native object-selection refresh.
        }

        internal void SetIsolated(bool value)
        {
            isolated = value;
            RestoreIsolation();
            ApplyIsolation();
            SceneView.RepaintAll();
        }

        private void ApplyIsolation()
        {
            if (!isolated || Root == null || !ToolManager.IsActiveContext(this) || leaving) return;
            foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded
                    || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(renderer.gameObject.scene)
                    || Contains(renderer.gameObject)) continue;
                if (!hidden.ContainsKey(renderer)) hidden.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        private void RestoreIsolation()
        {
            // Native visibility/picking commands record Undo. Temporary rendering flags do not.
            foreach (var entry in hidden)
                if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
            hidden.Clear();
        }

        private void SuppressNativeHighlight(SceneView view)
        {
            if (Event.current.type != EventType.Repaint || !ToolManager.IsActiveContext(this) || leaving || Root == null) return;
            RestoreNativeHighlight(null);
            // Unity selects the House root for context ownership. Its native child outline
            // would highlight the whole building, independently of our logical selection.
            foreach (var renderer in Root.GetComponentsInChildren<Renderer>(true))
            {
                using var serialized = new SerializedObject(renderer);
                var state = serialized.FindProperty("m_SelectedEditorRenderState");
                if (state == null) continue;
                nativeHighlights.Add(renderer, (EditorSelectedRenderState)state.intValue);
                EditorUtility.SetSelectedRenderState(renderer, EditorSelectedRenderState.Hidden);
            }
        }

        private void RestoreNativeHighlight(SceneView view)
        {
            // Restore before tools/input run. Saves and unrelated selection keep their
            // original display flags; this is not a persistent renderer modification.
            foreach (var entry in nativeHighlights)
                if (entry.Key != null) EditorUtility.SetSelectedRenderState(entry.Key, entry.Value);
            nativeHighlights.Clear();
        }

        private void HierarchyChanged()
        {
            if (ToolManager.IsActiveContext(this)) EditorApplication.delayCall += Resume;
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            // Play Mode is an interruption, not permission to apply typed scene values.
            if (state == PlayModeStateChange.ExitingEditMode && ToolManager.IsActiveContext(this)) ExitWithoutHistory();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view) || Session?.SessionPlan == null) return;
            // The context is the ONLY Scene input owner. Tool adapters never draw/claim controls.
            // Native tool changes do not exit this context. Picking belongs to
            // the context; ScenePointerInput handles camera/foreign control capture.
            Controller.OnSceneGUI(view);
        }
    }

    // W resolves to this tool inside the House context, reusing the existing Select/drag logic.
    public sealed class HouseMoveTool : EditorTool
    {
        public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("MoveTool", "Move House nodes, walls and openings");
        public override void OnActivated()
        {
            var context = HouseEditingContext.Active;
            if (context?.Session.ActiveMode == HouseEditorSession.Mode.Boolean) context.Session.BooleanGizmo = Tool.Move;
            else context?.Controller.SetMode(HouseEditorSession.Mode.Edit);
        }
        public override void OnWillBeDeactivated() => HouseEditingContext.Active?.Controller.CancelSceneGesture();
    }

    public sealed class HouseBooleanRotateTool : EditorTool
    {
        public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("RotateTool", "Rotate Boolean shape");
        public override void OnActivated() { if (HouseEditingContext.Active != null) HouseEditingContext.Active.Session.BooleanGizmo = Tool.Rotate; }
        public override void OnWillBeDeactivated() => HouseEditingContext.Active?.Controller.CancelSceneGesture();
    }

    public sealed class HouseBooleanScaleTool : EditorTool
    {
        public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("ScaleTool", "Resize Boolean shape");
        public override void OnActivated() { if (HouseEditingContext.Active != null) HouseEditingContext.Active.Session.BooleanGizmo = Tool.Scale; }
        public override void OnWillBeDeactivated() => HouseEditingContext.Active?.Controller.CancelSceneGesture();
    }
}
