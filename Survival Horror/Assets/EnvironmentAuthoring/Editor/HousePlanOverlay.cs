using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    // Keep the existing overlay ID so saved docking survives the shared palette.
    [Overlay(typeof(SceneView), "house-tools-docked", "Environment Tools", true, defaultDockZone = DockZone.LeftColumn, defaultDockPosition = DockPosition.Top)]
    public sealed class HousePlanOverlay : Overlay, ITransientOverlay
    {
        public bool visible => !EditorApplication.isPlayingOrWillChangePlaymode;
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { name = "house-tools" };
            root.style.width = 168; // Leave room for full labels, icons and the vertical scrollbar.
            var creation = new VisualElement { name = "environment-create-actions" }; root.Add(creation);
            creation.Add(new Label("Create") { name = "environment-create-heading" });
            var creationButtons = new Dictionary<EnvironmentCreateCommands.Kind, Button>();
            var kinds = new[] { EnvironmentCreateCommands.Kind.House, EnvironmentCreateCommands.Kind.Wall,
                EnvironmentCreateCommands.Kind.Road, EnvironmentCreateCommands.Kind.Sidewalk };
            for (int row = 0; row < 2; row++)
            {
                var line = new VisualElement(); line.style.flexDirection = FlexDirection.Row; creation.Add(line);
                for (int column = 0; column < 2; column++)
                {
                    var kind = kinds[row * 2 + column];
                    var button = new Button(() => EnvironmentCreateTool.Arm(kind, containerWindow as SceneView))
                        { text = kind.ToString(), name = "environment-create-" + kind,
                            tooltip = "Place a new " + kind.ToString().ToLowerInvariant() + (kind == EnvironmentCreateCommands.Kind.Wall ? " or fence" : "") + ". Click in the Scene to start drawing; Escape cancels placement." };
                    button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.minWidth = 0; button.style.height = 26;
                    line.Add(button); creationButtons.Add(kind, button);
                }
            }
            creation.Add(new Button(EnvironmentProjectSettings.Open)
                { text = "Project defaults", name = "environment-project-defaults",
                    tooltip = "Choose explicit default families, models and finishes for new objects." });
            var placement = new Label { name = "environment-placement-hint" };
            placement.style.whiteSpace = WhiteSpace.Normal; creation.Add(placement);
            var cancel = new Button(() => EnvironmentCreateTool.Active?.Cancel())
                { text = "Cancel placement", name = "environment-cancel-placement" }; creation.Add(cancel);
            var townTools = TownProperties.Toolbar();
            var enter = new Button(() => HouseEditingContext.Enter(HouseSceneUI.Plan))
                { text = "Edit House", name = "house-enter", tooltip = "Enter this House's editing context. Edits are ordinary scene changes." };
            enter.style.height = 26;
            root.Add(enter);
            var tools = new ScrollView(ScrollViewMode.Vertical) { name = "house-edit-tools",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            tools.style.maxHeight = 520;
            root.Add(tools);
            // Both editors share one scrolling body; Done stays outside it.
            var houseTools = new VisualElement { name = "house-edit-actions" };
            tools.Add(houseTools); tools.Add(townTools);
            var buttons = new Dictionary<HouseEditorSession.Mode, Button>();
            void Add(HouseEditorSession.Mode mode, string name, string icon, string tip)
            {
                var button = HouseSceneUI.Button(name, icon, tip, () => HouseSceneUI.Activate(mode));
                button.name = "house-tool-" + mode;
                houseTools.Add(button); buttons.Add(mode, button);
            }
            Add(HouseEditorSession.Mode.Edit, "Select", "select", "Select rooms through floors or holes. Drag nodes, walls or openings. Drag empty space to box-select nodes. Shift adds; Ctrl removes.");
            Add(HouseEditorSession.Mode.Wall, "Wall", "wall", "Click to chain walls. Close a loop to create a room. Click the last endpoint again or finish on a wall to stop.");
            Add(HouseEditorSession.Mode.Divider, "Floor divider", "divider", "Draw across a floor region or close a loop without creating a wall or room. Points stay temporary until they divide the floor. Right-click removes a draft point.");
            Add(HouseEditorSession.Mode.Boundary, "Room boundary", "divider", "Draw a room perimeter without physical wall geometry. Drawing over a wall replaces only the aligned interval.");
            Add(HouseEditorSession.Mode.Door, "Door", "door", "Hover a wall to preview a doorway; click to place. Escape clears selection, then exits House editing.");
            Add(HouseEditorSession.Mode.Window, "Window", "window", "Hover a wall to preview a window; click to place. Escape clears selection, then exits House editing.");
            var openingWidth = new PopupField<string>(new List<string> { "Kit default", "1 m segment", "2 m segment" }, 0)
                { name = "house-opening-width", tooltip = "Choose the full rigid module width, including the frame, rather than the clear aperture." };
            openingWidth.RegisterValueChangedCallback(e =>
            {
                var session = HouseEditingContext.Active?.Session;
                if (session != null) session.OpeningWidth = openingWidth.index;
                SceneView.RepaintAll();
            });
            houseTools.Add(openingWidth);
            Add(HouseEditorSession.Mode.Slab, "Slab cut", "cut", "Click to chain a floor cut; click its first or last point, or press Enter, to finish. Options appear in House properties.");
            Add(HouseEditorSession.Mode.Trim, "Trim", "wall", "Select exterior wall bottom bands, then choose a trim mesh and material in House Properties. Shift adds; Ctrl removes.");
            Add(HouseEditorSession.Mode.Boolean, "Boolean cut", "cut", "Place boxes or icospheres that cut generated walls and slabs. Edit their handles in the Scene view; options appear in House Properties.");
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(.5f, .5f, .5f, .35f);
            separator.style.marginTop = separator.style.marginBottom = 5;
            houseTools.Add(separator);
            Add(HouseEditorSession.Mode.Delete, "Delete", "delete", HouseEditorStatus.DeleteHint);
            HouseSceneUI.AddViewActions(houseTools, () => containerWindow as SceneView);
            var done = new Button(() =>
            {
                if (TownEditingContext.Active != null) TownEditingContext.Active.Exit();
                else HouseEditingContext.Active?.Exit();
            }) { text = "Done", name = "environment-done" };
            root.Add(done);
            VisualElement viewRoot = null;
            void FitHeight()
            {
                if (root.panel == null || viewRoot == null || viewRoot.worldBound.height <= 0) return;
                // Geometry events follow actual layout; a polling tick can still
                // see the previous viewport immediately after a native resize.
                float footer = done.resolvedStyle.display == DisplayStyle.None ? 0
                    : done.layout.height + done.resolvedStyle.marginTop + done.resolvedStyle.marginBottom;
                float available = viewRoot.worldBound.yMax - tools.worldBound.yMin
                    - footer - EditorGUIUtility.standardVerticalSpacing * 2;
                if (!float.IsFinite(available)) return;
                float height = Mathf.Clamp(available, 0, 520);
                if (!Mathf.Approximately(tools.style.maxHeight.value.value, height)) tools.style.maxHeight = height;
            }
            void LayoutChanged(GeometryChangedEvent _) => FitHeight();
            root.RegisterCallback<GeometryChangedEvent>(LayoutChanged);
            done.RegisterCallback<GeometryChangedEvent>(LayoutChanged);
            root.RegisterCallback<AttachToPanelEvent>(_ => root.schedule.Execute(() =>
            {
                // The window's root getter initializes overlays. Read it only after
                // attachment has returned, when saved-layout restoration is complete.
                viewRoot?.UnregisterCallback<GeometryChangedEvent>(LayoutChanged);
                viewRoot = (containerWindow as SceneView)?.rootVisualElement;
                viewRoot?.RegisterCallback<GeometryChangedEvent>(LayoutChanged);
                FitHeight();
            }));
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                viewRoot?.UnregisterCallback<GeometryChangedEvent>(LayoutChanged);
                viewRoot = null;
            });
            HouseSceneUI.Watch(root, () =>
            {
                var plan = HouseSceneUI.Plan;
                var tool = HouseEditingContext.Active?.Session;
                bool town = TownEditingContext.Active != null || TownEditingContext.Selected != null;
                displayName = "Environment Tools";
                var pending = EnvironmentCreateTool.Active;
                bool placing = pending != null && pending.Pending != EnvironmentCreateCommands.Kind.None;
                creation.style.display = tool != null || TownEditingContext.Active != null ? DisplayStyle.None : DisplayStyle.Flex;
                foreach (var pair in creationButtons) HouseSceneUI.Selected(pair.Value, placing && pending.Pending == pair.Key);
                placement.text = placing ? "Click to start " + pending.Pending.ToString().ToLowerInvariant() + ". Escape cancels." : "";
                placement.style.display = cancel.style.display = placing ? DisplayStyle.Flex : DisplayStyle.None;
                townTools.style.display = !placing && town ? DisplayStyle.Flex : DisplayStyle.None;
                enter.style.display = !placing && !town && tool == null && plan != null ? DisplayStyle.Flex : DisplayStyle.None;
                houseTools.style.display = placing || town || tool == null ? DisplayStyle.None : DisplayStyle.Flex;
                tools.style.display = placing || !town && tool == null ? DisplayStyle.None : DisplayStyle.Flex;
                done.style.display = !placing && (tool != null || TownEditingContext.Active != null) ? DisplayStyle.Flex : DisplayStyle.None;
                FitHeight();
                openingWidth.style.display = tool != null && (tool.ActiveMode == HouseEditorSession.Mode.Door
                    || tool.ActiveMode == HouseEditorSession.Mode.Window) ? DisplayStyle.Flex : DisplayStyle.None;
                if (tool != null) openingWidth.SetValueWithoutNotify(openingWidth.choices[(int)tool.OpeningWidth]);
                foreach (var pair in buttons)
                {
                    bool openings = pair.Key == HouseEditorSession.Mode.Door || pair.Key == HouseEditorSession.Mode.Window || pair.Key == HouseEditorSession.Mode.Delete;
                    pair.Value.SetEnabled(plan != null && plan.HasSupportedTransform
                        && (!openings || plan.Data.Walls.Count > 0)
                        && (pair.Key != HouseEditorSession.Mode.Divider || HouseBuilding.For(plan)?.Find(plan) != null)
                        && (pair.Key != HouseEditorSession.Mode.Slab || !HouseRebuildCoordinator.NeedsSceneInstance(plan)));
                    HouseSceneUI.Selected(pair.Value, tool != null && tool.ActiveMode == pair.Key);
                }
            });
            return root;
        }
    }

    [Overlay(typeof(SceneView), "House view", true, defaultDockZone = DockZone.TopToolbar, defaultLayout = Layout.HorizontalToolbar)]
    public sealed class HouseViewOverlay : Overlay, ITransientOverlay, ICreateHorizontalToolbar
    {
        public bool visible => HouseSceneUI.Visible;
        public override VisualElement CreatePanelContent() => CreateHorizontalToolbarContent();
        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            var root = new OverlayToolbar { name = "house-view" };
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexWrap = Wrap.Wrap;
            root.style.alignItems = Align.Center;
            var breadcrumb = new VisualElement { name = "house-breadcrumb" };
            breadcrumb.style.flexDirection = FlexDirection.Row;
            breadcrumb.style.alignItems = Align.Center;
            breadcrumb.style.maxWidth = 160;
            breadcrumb.style.marginRight = 8;
            var houseName = new Label();
            houseName.style.maxWidth = 70;
            houseName.style.minWidth = 0;
            houseName.style.flexShrink = 1;
            houseName.style.whiteSpace = WhiteSpace.NoWrap;
            houseName.style.overflow = Overflow.Hidden;
            houseName.style.textOverflow = TextOverflow.Ellipsis;
            var level = new Label { name = "house-active-level", tooltip = "Choose levels in House properties" };
            level.style.flexShrink = 0;
            breadcrumb.Add(houseName);
            breadcrumb.Add(level);
            root.Add(breadcrumb);
            HouseSceneUI.Watch(root, () =>
            {
                var plan = HouseSceneUI.Plan;
                var building = HouseBuilding.For(plan);
                var context = HouseEditingContext.Active;
                houseName.text = context?.Root?.name ?? "House";
                level.text = " > " + HouseSceneUI.LevelName(plan);
                breadcrumb.tooltip = "Scene â€º " + houseName.text + level.text;
            });
            return root;
        }
    }

    // Instructions and diagnostics share a short strip instead of crowding property fields.
    [Overlay(typeof(SceneView), "House status", true, defaultDockZone = DockZone.BottomToolbar, defaultLayout = Layout.HorizontalToolbar)]
    public sealed class HouseStatusOverlay : Overlay, ITransientOverlay, ICreateHorizontalToolbar
    {
        public bool visible => HouseSceneUI.Visible;
        public override VisualElement CreatePanelContent() => CreateHorizontalToolbarContent();
        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            var root = new OverlayToolbar();
            var label = new Label { name = "house-status" };
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = label.style.paddingRight = 8;
            label.style.maxWidth = 600;
            HouseSceneUI.Watch(label, () =>
            {
                var plan = HouseSceneUI.Plan;
                var tool = HouseEditingContext.Active?.Session;
                var building = HouseBuilding.For(plan);
                string problem = building != null ? HouseSlabBuilder.Output(building)?.Problem : null;
                string fit = plan != null ? plan.GetComponent<HouseWallBuilder>()?.Output?.Problem : null;
                label.text = plan != null && !plan.HasSupportedTransform ? "Keep the House upright at world scale (1, 1, 1)."
                    : !string.IsNullOrEmpty(problem) ? problem : !string.IsNullOrEmpty(fit) ? fit
                    : tool == null ? "Choose a House tool to begin editing." : HouseEditorStatus.StatusHint(tool);
                label.tooltip = tool == null ? label.text : HouseEditorStatus.Instructions(tool);
            });
            root.Add(label);
            return root;
        }
    }

}
