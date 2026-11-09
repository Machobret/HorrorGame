using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Editor intent survives closing a view or leaving a context. It is not scene data
    // and is deliberately separate from native context-entry and authoring Undo.
    public sealed class HouseEditorSession : ScriptableSingleton<HouseEditorSession>
    {
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private float gridSize = 0.5f;
        internal enum Mode { Wall, Edit, Delete, Door, Window, Slab, Divider, Boundary, Trim, Boolean }
        [SerializeField] private Mode mode = Mode.Edit;
        [SerializeField] private HousePlan editingPlan;
        [SerializeField] private HouseEditSelection editSelection = new HouseEditSelection();
        [SerializeField] private bool slabCeiling, slabPolygon = true;
        [SerializeField] private float openingWidth;
        [SerializeField] internal OpeningFamily OpeningFamily;
        [SerializeField] internal OpeningFamily.Condition OpeningCondition;
        [SerializeField] internal WallOpeningInsert OpeningModel;
        [SerializeField] internal bool OpeningOnly;
        internal WallFamily OpeningHostFamily;
        internal WallOpeningInsert PendingOpeningModel;
        [SerializeField] private TrimFamily trimFamily;
        [SerializeField] private bool showBooleanShapes = true, placeBooleanShape;
        [SerializeField] private HouseStack.BooleanShape booleanShape;
        [SerializeField] private Tool booleanGizmo = Tool.Move;
        [SerializeField] private HousePendingEdit pending = new HousePendingEdit();
        private string message = "";
        private double nextSourceCheck;
        internal int ContextRevision { get; set; }

        internal HousePendingEdit Pending => pending;
        internal bool CanEditSource => pending.ActiveKind == HousePendingEdit.Kind.None;
        internal HousePlan SessionPlan { get => editingPlan; set => editingPlan = value; }
        internal Mode ActiveMode { get => mode; set => mode = value; }
        internal bool SnapToGrid { get => snapToGrid; set => snapToGrid = value; }
        internal float GridSize
        {
            get => gridSize;
            set => gridSize = !float.IsFinite(value) ? 0.5f : Mathf.Max(0.01f, value);
        }
        internal HouseEditSelection Selection => editSelection;
        internal TrimFamily TrimFamily { get => trimFamily; set => trimFamily = value; }
        internal bool ShowBooleanShapes
        {
            get => showBooleanShapes;
            set { if (showBooleanShapes == value) return; showBooleanShapes = value; SceneView.RepaintAll(); }
        }
        internal bool PlaceBooleanShape { get => placeBooleanShape; set => placeBooleanShape = value; }
        internal HouseStack.BooleanShape BooleanShape
        {
            get => booleanShape;
            set => booleanShape = value;
        }
        internal Tool BooleanGizmo { get => booleanGizmo; set => booleanGizmo = value; }
        internal string Message { get => message; set => message = value ?? ""; }
        internal bool SlabCeiling { get => slabCeiling; set => slabCeiling = value; }
        internal bool SlabPolygon { get => slabPolygon; set => slabPolygon = value; }
        // Zero uses the family default of 2 m; 1 m and 2 m choices are explicit.
        internal float OpeningWidth { get => openingWidth; set => openingWidth = value == 1 || value == 2 ? value : 0; }
        internal HouseEditSelection.Item SelectedSlabCut => editSelection.Items.Count == 1
            && editSelection.Items[0].Type == HouseEditSelection.Kind.SlabCut ? editSelection.Items[0] : null;
        internal string SlabSelection => SelectedSlabCut?.Id;
        internal bool SelectedSlabCeiling => SelectedSlabCut?.Ceiling ?? slabCeiling;

        private void OnEnable()
        {
            pending ??= new HousePendingEdit();
            pending.Changed += OnPendingChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Interrupt;
            Undo.undoRedoPerformed += Interrupt;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SceneView.duringSceneGui += DrawPending;
            EditorApplication.update += CheckSource;
        }
        private void OnDisable()
        {
            Interrupt();
            pending.Changed -= OnPendingChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Interrupt;
            Undo.undoRedoPerformed -= Interrupt;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SceneView.duringSceneGui -= DrawPending;
            EditorApplication.update -= CheckSource;
        }
        private void CheckSource()
        {
            if (pending.ActiveKind == HousePendingEdit.Kind.None || EditorApplication.timeSinceStartup < nextSourceCheck) return;
            nextSourceCheck = EditorApplication.timeSinceStartup + .1;
            pending.CheckSource();
        }
        internal HouseEditResult ApplyPending()
        {
            var target = pending.Plan;
            var building = target != null ? HouseBuilding.For(target) : null;
            float previousElevation = building != null ? building.Data.Elevation(building.Find(target)) : 0;
            var result = pending.Apply();
            Message = result.Message;
            if (result.Applied) HouseEditingContext.FollowLevelHeightChange(target, previousElevation);
            return result;
        }
        private void OnPendingChanged() => SceneView.RepaintAll();
        private void Interrupt() => pending.Interrupt();
        private void OnPlayModeChanged(PlayModeStateChange state) => Interrupt();
        private void DrawPending(SceneView view) => pending.DrawSlabPreview(view);

        // Transitions resolve intent through this owner; a Properties window need not exist.
        internal bool ConfirmPendingChanges()
        {
            if (!pending.IsProperty) return true;
            if (!pending.IsPending) { pending.Discard(); return true; }
            int choice = EditorUtility.DisplayDialogComplex("Unapplied House properties",
                "Apply or discard the pending property values before continuing? Completed scene edits are preserved.",
                "Apply", "Keep editing", "Discard pending values");
            if (choice == 1) return false;
            if (choice == 2) { pending.Discard(); return true; }
            var result = ApplyPending();
            return result.Status != HouseEditStatus.Rejected && !pending.IsPending;
        }

        internal void Pick(HouseEditSelection.Kind kind, string id, bool add = false, bool subtract = false,
            string boundaryId = null, bool ceiling = false)
            => editSelection.Pick(kind, id, add, subtract, editingPlan, boundaryId, ceiling);

        internal void PickSlabOpening(string id, bool ceiling)
        {
            var building = HouseBuilding.For(editingPlan);
            var level = building?.Find(editingPlan);
            var boundary = ceiling ? building?.Above(level) : building?.Below(level);
            if (boundary == null) return;
            editSelection.Pick(HouseEditSelection.Kind.SlabCut, id, false, false, editingPlan, boundary.Id, ceiling);
        }
        internal void Marquee(IEnumerable<string> nodes, bool add, bool subtract)
            => editSelection.Marquee(nodes, add, subtract, editingPlan);
        internal void MarqueeItems(IEnumerable<HouseEditSelection.Item> items, bool add, bool subtract)
            => editSelection.MarqueeItems(items, add, subtract, editingPlan);
        internal void ClearSlabSelection() => editSelection.Items.RemoveAll(item => item.Type == HouseEditSelection.Kind.SlabCut);
        internal void ClearSelection() => editSelection.Clear();
        internal string SelectedWallId => SingleSelectedId(HouseEditSelection.Kind.Wall);
        internal string SelectedCornerId => SingleSelectedId(HouseEditSelection.Kind.Node);
        private string SingleSelectedId(HouseEditSelection.Kind kind) => editSelection.Items.Count == 1
            && editSelection.Items[0].Type == kind ? editSelection.Items[0].Id : null;
    }
}
