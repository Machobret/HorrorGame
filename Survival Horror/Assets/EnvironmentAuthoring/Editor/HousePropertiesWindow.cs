using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    // A view of the active editing session, not another editor/controller. Closing
    // this window must never close the context or revert accepted scene changes.
    public sealed class HousePropertiesWindow : EditorWindow
    {
        private static string Preferences => "EnvironmentAuthoring.HouseProperties." + Application.dataPath + ".";
        [SerializeField] private bool selectionTab;
        private HousePendingEdit Draft => HouseEditorSession.instance.Pending;
        private bool suspended => Draft.IsProperty && (Draft.Suspended || Draft.Conflicted
            || Draft.Plan != HouseEditingContext.Active?.Session?.SessionPlan);
        private bool rebuilding;
        private bool showLevelAdvanced;
        [SerializeField] private HouseWindowDocking docking = new HouseWindowDocking();
        private HouseLevelHeightProperties height;
        private HouseSlabThicknessControls thickness;
        private string previousLevels, previousProperties, previousSelection;
        private VisualElement active, inactive, rows, levelNavigation, levelFields, sharedFields, recovery, staleNotice;
        private IMGUIContainer selectionFields, houseFields, levelProperties;
        private Label heading;
        private VisualElement townProperties;
        private Button houseTabButton, selectionTabButton;

        [MenuItem("Window/Environment/House Properties")]
        public static void Open()
        {
            var window = GetOrCreate();
            window.ShowTab(); window.Focus();
        }

        internal static HousePropertiesWindow Existing => Resources.FindObjectsOfTypeAll<HousePropertiesWindow>().FirstOrDefault();

        private static HousePropertiesWindow GetOrCreate()
        {
            if (Existing != null) return Existing;
            var window = CreateInstance<HousePropertiesWindow>();
            var rect = new Rect(100, 100, 300, 600);
            if (EditorPrefs.HasKey(Preferences + "Position"))
            {
                var saved = JsonUtility.FromJson<Rect>(EditorPrefs.GetString(Preferences + "Position"));
                if (float.IsFinite(saved.x) && float.IsFinite(saved.y) && saved.width >= 240 && saved.height >= 240) rect = saved;
            }
            window.position = rect;
            window.Show();
            return window;
        }

        internal static void RevealForEditing()
        {
            var window = GetOrCreate();
            // A newly created window has no previous dock-group tab to restore.
            window.docking.CaptureOnce(window);
            window.ShowTab(); window.Refresh();
        }

        internal static void ContextExited() => Existing?.docking.Restore(Existing);
        private bool Pending => Draft.IsPending && Draft.IsProperty;
        private void OnEnable()
        {
            titleContent = new GUIContent("House Properties");
            minSize = new Vector2(240, 240);
            selectionTab = EditorPrefs.GetBool(Preferences + "SelectionTab", selectionTab);
            saveChangesMessage = "There are unapplied House property values. Save applies those values to the scene; it does not save the scene file.";
            Draft.Changed += Refresh;
        }

        private void OnDisable()
        {
            Draft.Changed -= Refresh;
            SuspendDraft();
            if (!docked) EditorPrefs.SetString(Preferences + "Position", JsonUtility.ToJson(position));
            // Native close prompts use SaveChanges/DiscardChanges. Reload merely
            // disposes transient controls; saved geometry remains owned by the scene.
        }

        public override void SaveChanges()
        {
            if (ApplyPending()) base.SaveChanges();
        }

        private bool ApplyPending()
        {
            if (!Pending) return true;
            var result = HouseEditorSession.instance.ApplyPending();
            Refresh();
            if (result.Status == HouseEditStatus.Rejected)
                ShowNotification(new GUIContent(result.Message));
            return result.Status != HouseEditStatus.Rejected;
        }

        public override void DiscardChanges()
        {
            rebuilding = true;
            thickness?.Cancel(); height?.Discard();
            if (Pending) Draft.Discard(); rebuilding = false;
            base.DiscardChanges();
            Refresh();
        }

        private void UpdateDraftState()
        {
            if (rebuilding) return;
            hasUnsavedChanges = Pending;
            // Field adapters guard editing themselves. Disabling their whole groups
            // would also disable Cancel precisely when a stale draft needs recovery.
            levelProperties?.SetEnabled(Draft.ActiveKind == HousePendingEdit.Kind.None);
            houseTabButton?.SetEnabled(!Pending);
            selectionTabButton?.SetEnabled(!Pending);
        }

        internal void SuspendDraft()
        {
            if (Pending) Draft.Interrupt();
            thickness?.Suspend();
            hasUnsavedChanges = Pending;
        }

        public void CreateGUI()
        {
            rebuilding = true;
            previousLevels = previousProperties = null;
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = rootVisualElement.style.paddingBottom = 6;
            townProperties = TownProperties.Panel(); rootVisualElement.Add(townProperties);
            inactive = new VisualElement();
            inactive.Add(new Label("Select a House to edit its layout and properties.") { style = { whiteSpace = WhiteSpace.Normal } });
            var enter = new Button(() => HouseEditingContext.Enter(HouseEditingContext.SelectionPlan))
                { text = "Edit House", name = "house-window-enter" };
            inactive.Add(enter); rootVisualElement.Add(inactive);
            recovery = new VisualElement();
            recovery.Add(new Label { name = "house-draft-recovery", style = { whiteSpace = WhiteSpace.Normal } });
            recovery.Add(new Button(() => ApplyPending()) { name = "house-draft-apply", text = "Apply pending values" });
            recovery.Add(new Button(DiscardChanges) { name = "house-draft-discard", text = "Discard pending values" });
            recovery.Add(new Button(() => { Draft.Resume(HouseEditingContext.Active?.Session?.SessionPlan); Refresh(); })
                { name = "house-draft-review", text = "Review pending values" });
            rootVisualElement.Add(recovery);
            active = new VisualElement { name = "house-properties" };
            active.style.flexGrow = 1; active.style.minHeight = 0;
            rootVisualElement.Add(active);
            heading = new Label(); heading.style.unityFontStyleAndWeight = FontStyle.Bold; active.Add(heading);
            staleNotice = new VisualElement { name = "house-stale-output" };
            staleNotice.Add(new Label("Assets changed. Rebuild to update geometry."));
            staleNotice.Add(new Button(() => HouseRebuildCoordinator.RebuildHouse(HouseEditingContext.Active?.Session?.SessionPlan))
                { text = "Rebuild House" });
            active.Add(staleNotice);
            var levels = new ScrollView(ScrollViewMode.Vertical) { name = "house-levels" };
            levels.style.maxHeight = 160; levels.style.flexShrink = 0;
            rows = new VisualElement(); levels.Add(rows); active.Add(levels);
            levelNavigation = new VisualElement { name = "house-level-navigation" };
            levelNavigation.style.flexDirection = FlexDirection.Row;
            levelNavigation.style.flexShrink = 0;
            active.Add(levelNavigation);
            var tabs = new VisualElement(); tabs.style.flexDirection = FlexDirection.Row;
            houseTabButton = new Button(() => SetTab(false)) { text = "Defaults", name = "house-tab-house" };
            selectionTabButton = new Button(() => SetTab(true)) { text = "Selection", name = "house-tab-selection" };
            houseTabButton.style.flexGrow = selectionTabButton.style.flexGrow = 1;
            tabs.Add(houseTabButton); tabs.Add(selectionTabButton); active.Add(tabs);
            var content = new ScrollView(ScrollViewMode.Vertical) { name = "house-properties-content", viewDataKey = "house-properties-scroll" };
            content.style.flexGrow = 1; content.style.flexBasis = 0; content.style.minHeight = 0;
            active.Add(content);
            levelFields = new VisualElement();
            levelFields.Add(new Label("Current level") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            height = new HouseLevelHeightProperties(UpdateDraftState); levelFields.Add(height.Root);
            levelProperties = new IMGUIContainer(DrawLevelProperties);
            levelFields.Add(levelProperties); content.Add(levelFields);
            selectionFields = new IMGUIContainer(DrawSelectionProperties) { name = "house-property-fields" };
            content.Add(selectionFields);
            sharedFields = new VisualElement();
            sharedFields.Add(new Label("All levels") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            thickness = new HouseSlabThicknessControls(() => HouseBuilding.For(HouseEditingContext.Active?.Session?.SessionPlan), UpdateDraftState);
            sharedFields.Add(thickness.Root);
            houseFields = new IMGUIContainer(() => WithEditableFields(() =>
            {
                var result = HouseSettingsProperties.Draw(HouseBuilding.For(HouseEditingContext.Active?.Session?.SessionPlan));
                var session = HouseEditingContext.Active?.Session;
                if (session != null && result.Status != HouseEditStatus.Unchanged) session.Message = result.Message;
            }));
            sharedFields.Add(houseFields); content.Add(sharedFields);
            var done = new Button(() => HouseEditingContext.Active?.Exit())
                { text = "Done", name = "house-done", tooltip = "Leave House editing. Completed edits remain in the scene." };
            done.style.height = 26; done.style.flexShrink = 0; active.Add(done);
            // Watch the replaceable subtree, so rebuilding the window cannot leave
            // duplicate callbacks/schedules attached to its persistent root.
            HouseSceneUI.Watch(active, Refresh);
            active.RegisterCallback<KeyDownEvent>(e =>
            {
                var context = HouseEditingContext.Active;
                if (e.keyCode != KeyCode.Escape || context == null || Pending) return;
                if (context.Session.Selection.Items.Count > 0 || context.Controller.SlabTool.HasSelection) context.Controller.ClearSelection();
                else context.Exit();
                e.StopPropagation();
            });
            rebuilding = false;
            Refresh();
        }

        private void SetTab(bool selection)
        {
            if (Pending) return; // Apply/Cancel stays reachable until the draft is resolved.
            selectionTab = selection;
            EditorPrefs.SetBool(Preferences + "SelectionTab", selection);
            Refresh();
        }

        internal static void ShowSelection()
        {
            Existing?.SetTab(true);
        }

        internal void Refresh()
        {
            if (active == null) return;
            bool town = TownEditingContext.Active != null || TownEditingContext.Selected != null;
            townProperties.style.display = town ? DisplayStyle.Flex : DisplayStyle.None;
            titleContent = new GUIContent(town ? "Town Properties" : "House Properties");
            if (town)
            {
                active.style.display = inactive.style.display = recovery.style.display = DisplayStyle.None;
                return;
            }
            var plan = HouseEditingContext.Active?.Session?.SessionPlan;
            height.Refresh(); thickness.Refresh();
            recovery.style.display = suspended ? DisplayStyle.Flex : DisplayStyle.None;
            if (Pending)
            {
                recovery.Q<Label>().text = "Pending values for " + (Draft.Plan != null ? Draft.Plan.name : "a removed level") + ": "
                    + (Draft.ActiveKind == HousePendingEdit.Kind.LevelHeight ? "height " + Draft.HeightValue + " m. " : "")
                    + (Draft.ActiveKind == HousePendingEdit.Kind.SlabThickness ? "slab " + Draft.Total + " m, finish " + Draft.Finish + " m. " : "")
                    + (Draft.Error.Length > 0 ? Draft.Error : suspended
                        ? "Return to that level to review. Changed source values require explicit discard." : "");
                recovery.Q<Button>("house-draft-review").SetEnabled(Draft.Plan == plan && !Draft.Conflicted);
                recovery.Q<Button>("house-draft-apply").SetEnabled(!Draft.Conflicted);
            }
            bool editing = plan != null && !EditorApplication.isPlayingOrWillChangePlaymode;
            active.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
            inactive.style.display = editing ? DisplayStyle.None : DisplayStyle.Flex;
            inactive.Q<Button>().SetEnabled(HouseEditingContext.SelectionPlan != null && !EditorApplication.isPlayingOrWillChangePlaymode);
            if (!editing) { UpdateDraftState(); return; }
            staleNotice.style.display = HouseRebuildCoordinator.NeedsRebuild(plan) ? DisplayStyle.Flex : DisplayStyle.None;
            heading.text = (HouseEditingContext.Active.Root?.name ?? "House") + " / " + HouseSceneUI.LevelName(plan);
            var building = HouseBuilding.For(plan);
            string levelStamp = plan.GetInstanceID() + ":" + plan.name + ":" + plan.HasSupportedTransform + ":" + HouseRebuildCoordinator.NeedsSceneInstance(plan);
            if (building != null) foreach (var level in building.Levels) levelStamp += ":" + level.Id + ":" + level.Plan?.name + ":" + building.Data.Elevation(level);
            if (levelStamp != previousLevels) { previousLevels = levelStamp; DrawLevels(rows, levelNavigation, plan, building); }
            var session = HouseEditingContext.Active.Session;
            string selectionStamp = plan.GetInstanceID() + ":" + session.ActiveMode + ":"
                + string.Join("|", session.Selection.Items.Select(item => item.Type + ":" + item.BoundaryId + ":" + item.Id + ":" + item.Ceiling));
            // Follow a new selection once. A deliberate Defaults click stays put until
            // the selection changes; pending Apply/Discard must never be displaced.
            if (!Pending && selectionStamp != previousSelection)
            {
                previousSelection = selectionStamp;
                selectionTab = session.Selection.Items.Count > 0 || session.ActiveMode == HouseEditorSession.Mode.Trim
                    || session.ActiveMode == HouseEditorSession.Mode.Slab || session.ActiveMode == HouseEditorSession.Mode.Boolean
                    || session.ActiveMode == HouseEditorSession.Mode.Window || session.ActiveMode == HouseEditorSession.Mode.Door;
            }
            levelFields.style.display = selectionTab ? DisplayStyle.None : DisplayStyle.Flex;
            selectionFields.style.display = selectionTab ? DisplayStyle.Flex : DisplayStyle.None;
            var selected = HouseEditingContext.Active.Session.Selection.Items;
            bool slab = selected.Count == 1 && (selected[0].Type == HouseEditSelection.Kind.FloorSlab || selected[0].Type == HouseEditSelection.Kind.CeilingSlab);
            sharedFields.style.display = !selectionTab || slab ? DisplayStyle.Flex : DisplayStyle.None;
            houseFields.style.display = selectionTab ? DisplayStyle.None : DisplayStyle.Flex;
            HouseSceneUI.Selected(houseTabButton, !selectionTab); HouseSceneUI.Selected(selectionTabButton, selectionTab);
            string stamp = HousePropertyState.Stamp(plan, building);
            if (stamp != previousProperties)
            {
                previousProperties = stamp;
                selectionFields.MarkDirtyRepaint(); houseFields.MarkDirtyRepaint(); levelProperties.MarkDirtyRepaint();
            }
            UpdateDraftState();
        }

        private static void WithEditableFields(System.Action draw)
        {
            using (new EditorGUI.DisabledScope(HouseEditingContext.Active?.Session?.SessionPlan?.HasSupportedTransform != true)) draw();
        }

        private void DrawLevelProperties() => WithEditableFields(() =>
        {
            var plan = HouseEditingContext.Active?.Session?.SessionPlan;
            if (plan == null) return;
            var builder = plan.GetComponent<HouseWallBuilder>();
            if (builder != null)
            {
                using var settings = new SerializedObject(builder);
                var result = HouseAssetProperties.Defaults(settings);
                if (result.Status != HouseEditStatus.Unchanged) HouseEditingContext.Active.Session.Message = result.Message;
            }
            var finishes = HouseMaterialEditor.LevelWallDefaults(plan);
            if (finishes.Status != HouseEditStatus.Unchanged) HouseEditingContext.Active.Session.Message = finishes.Message;
            var defaults = HouseBuilding.For(plan)?.Data;
            EditorGUI.BeginChangeCheck();
            var material = HousePropertyFields.InheritedMaterial(new GUIContent("Floor finish"), plan.Data.FloorMaterial,
                plan.Data.FloorMaterial != null ? plan.Data.FloorMaterial : defaults?.FloorMaterial, plan.Data.FloorMaterial != null, out _);
            if (EditorGUI.EndChangeCheck())
                HouseEditingContext.Active.Controller.RunCommand(() => HouseEditCommands.SetFloorSettings(plan,
                    plan.Data.ShowFloors, material, plan.Data.FloorTextureSize, plan.Data.GenerateCollision));
            EditorGUI.BeginChangeCheck();
            var ceiling = HousePropertyFields.InheritedMaterial(new GUIContent("Ceiling finish"), plan.Data.CeilingMaterial,
                plan.Data.CeilingMaterial != null ? plan.Data.CeilingMaterial : defaults?.CeilingMaterial, plan.Data.CeilingMaterial != null, out _);
            if (EditorGUI.EndChangeCheck()) HouseEditingContext.Active.Controller.RunCommand(() => HouseFinishCommands.SetLevelCeiling(plan, ceiling));
            showLevelAdvanced = EditorGUILayout.Foldout(showLevelAdvanced, "Advanced level settings", true);
            if (showLevelAdvanced)
            {
                string displayedName = HouseSceneUI.LevelName(plan);
                string name = HousePropertyFields.DelayedText("Name", displayedName);
                if (name != displayedName && !string.IsNullOrWhiteSpace(name)) HouseEditCommands.RenameLevel(plan, name);
                EditorGUI.BeginChangeCheck();
                var repeat = new Vector2(HousePropertyFields.DelayedFloat("Repeat width (m)", plan.Data.FloorTextureSize.x),
                    HousePropertyFields.DelayedFloat("Repeat depth (m)", plan.Data.FloorTextureSize.y));
                if (EditorGUI.EndChangeCheck()) HouseEditingContext.Active.Controller.RunCommand(() =>
                    HouseEditCommands.SetFloorSettings(plan, plan.Data.ShowFloors, plan.Data.FloorMaterial, repeat, plan.Data.GenerateCollision));
            }
        });

        private static void DrawSelectionProperties() => WithEditableFields(() =>
        {
            var context = HouseEditingContext.Active;
            if (context?.Session?.SessionPlan == null) return;
            if (context.Session.ActiveMode == HouseEditorSession.Mode.Boolean
                || context.Session.Selection.Items.Count == 1 && context.Session.Selection.Items[0].Type == HouseEditSelection.Kind.BooleanShape)
                HouseBooleanProperties.Draw(context.Session, context.Controller);
            else if (context.Session.ActiveMode == HouseEditorSession.Mode.Trim)
                HouseTrimProperties.Draw(context.Session, context.Controller);
            else if (context.Session.ActiveMode == HouseEditorSession.Mode.Slab || context.Controller.SlabTool.HasSelection)
                HouseSlabProperties.Draw(context.Session, context.Controller);
            else if (context.Session.Selection.Items.Count > 0) HouseSelectionProperties.Draw(context.Session, context.Controller);
            else if (context.Session.ActiveMode == HouseEditorSession.Mode.Window || context.Session.ActiveMode == HouseEditorSession.Mode.Door)
                HouseOpeningProperties.Placement(context.Session);
            else EditorGUILayout.LabelField("Select a room, wall, corner, opening or slab cut.", EditorStyles.wordWrappedLabel);
        });
        private static void DrawLevels(VisualElement rows, VisualElement navigation, HousePlan plan, HouseBuilding building)
        {
            rows.Clear();
            navigation.Clear();
            if (plan == null) return;
            var list = new VisualElement { name = "house-level-list" };
            void Add(HousePlan target, int index)
            {
                if (target == null) return;
                var row = new VisualElement { name = "house-level-entry-" + index };
                row.style.flexDirection = FlexDirection.Row;
                var button = new Button(() => HouseEditingContext.SelectLevel(target))
                    { name = "house-level-row-" + index, tooltip = target.name };
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                button.style.minWidth = 0;
                button.style.flexDirection = FlexDirection.Row;
                button.style.minHeight = 26;
                var title = new Label(HouseSceneUI.LevelName(target));
                title.style.flexGrow = 1; title.style.minWidth = 0;
                title.style.whiteSpace = WhiteSpace.Normal;
                // Elevations are deliberately absent here: the section shows local
                // proportions/spacing. World-Y0 elevation controls are a later feature.
                button.Add(title);
                HouseSceneUI.Selected(button, target == plan);
                var delete = new Button(() => HouseEditingContext.DeleteLevel(target))
                    { text = "×", name = "house-level-delete-" + index };
                delete.style.width = 26;
                delete.style.flexShrink = 0;
                delete.style.minHeight = 26;
                bool canDelete = HouseStructureCommands.CanDeleteLevel(target, out string reason);
                delete.SetEnabled(canDelete);
                delete.tooltip = canDelete ? "Delete this level and its content" : reason;
                row.Add(button); row.Add(delete);
                list.Add(row);
            }
            void AddCreationButton(bool below)
            {
                Button add = null;
                add = new Button(() =>
                {
                    try
                    {
                        HouseEditingContext.ChooseLevelInheritance(building, below, add.worldBound);
                    }
                    catch (System.InvalidOperationException error)
                    {
                        var session = HouseEditingContext.Active?.Session;
                        if (session != null) session.Message = error.Message;
                        SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(error.Message));
                    }
                }) { text = below ? "Add Below" : "Add Above", name = below ? "house-level-add-below" : "house-level-add" };
                add.tooltip = below ? "Choose what to inherit for a new level below the lowest level. Existing elevations stay fixed."
                    : "Choose what to inherit for a new level above the highest level.";
                add.style.minHeight = 26;
                add.SetEnabled(plan.HasSupportedTransform && !HouseRebuildCoordinator.NeedsSceneInstance(plan));
                rows.Add(add);
            }
            AddCreationButton(false);
            // The model is ordered bottom-up; reverse only the displayed buttons.
            // Keep their model indices so selection and level history are unchanged.
            if (building == null) Add(plan, 0);
            else for (int i = building.Levels.Count - 1; i >= 0; i--)
                Add(building.Levels[i].Plan, i);
            rows.Add(list);
            AddCreationButton(true);
            var selectWalls = new Button(() =>
            {
                var session = HouseEditingContext.Active?.Session;
                if (session?.SessionPlan != plan) return;
                HouseEditingContext.Active.Controller.Select(plan.Data.Walls.Where(wall => wall.IsPhysical)
                    .Select(wall => new HouseEditSelection.Item(HouseEditSelection.Kind.Wall, wall.Id)));
                Existing?.Refresh();
                SceneView.RepaintAll();
            }) { text = "Select all walls", name = "house-level-select-walls" };
            selectWalls.tooltip = "Select this level's physical walls for bulk property editing.";
            selectWalls.SetEnabled(plan.Data.Walls.Any(wall => wall.IsPhysical));
            rows.Add(selectWalls);
            if (building != null)
            {
                int index = building.Levels.FindIndex(l => l.Plan == plan);
                var down = HouseSceneUI.Button("Previous", "down", "Previous level", () => HouseEditingContext.CycleLevel(HouseSceneUI.Plan, -1));
                var up = HouseSceneUI.Button("Next", "up", "Next level", () => HouseEditingContext.CycleLevel(HouseSceneUI.Plan, 1));
                down.name = "house-level-down"; up.name = "house-level-up";
                down.SetEnabled(index > 0 && building.Levels[index - 1].Plan != null);
                up.SetEnabled(index + 1 < building.Levels.Count && building.Levels[index + 1].Plan != null);
                down.style.flexGrow = up.style.flexGrow = 1;
                down.style.flexBasis = up.style.flexBasis = 0;
                navigation.Add(down); navigation.Add(up);
            }
        }

    }
}
