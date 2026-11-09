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
    internal static class HouseSceneUI
    {
        internal static HousePlan Plan => HouseEditingContext.Active?.Session?.SessionPlan ?? HouseEditingContext.SelectionPlan;
        internal static string LevelName(HousePlan plan)
        {
            if (plan == null) return "";
            var building = HouseBuilding.For(plan);
            if (building == null) return "Level 1";
            // Default names follow order relative to Level 1; custom names stay authored.
            bool defaultName = plan.name.Contains("draw and edit here") || plan.name.StartsWith("Level ")
                && (int.TryParse(plan.name.Substring(6), out _)
                    || plan.name.StartsWith("Level B") && int.TryParse(plan.name.Substring(7), out _));
            if (!defaultName) return plan.name;
            var level = building.Find(plan);
            int index = building.Levels.IndexOf(level), anchor = building.Levels.IndexOf(building.Data.Anchor);
            bool below = index < anchor;
            int number = below ? anchor - index : index - anchor + 1;
            return "Level " + (below ? "B" : "") + number;
        }
        internal static bool Visible => HouseEditingContext.Active?.Session?.SessionPlan != null && !EditorApplication.isPlayingOrWillChangePlaymode;
        internal static void Activate(HouseEditorSession.Mode mode)
        {
            if (HouseEditingContext.Active?.Session == null) HouseEditingContext.Enter(Plan);
            if (HouseEditingContext.Active?.Session == null) return;
            if (mode == HouseEditorSession.Mode.Edit) Tools.current = Tool.Move;
            else ToolManager.SetActiveTool<HouseOutlineTool>();
            HouseEditingContext.Active?.Controller.SetMode(mode);
            SceneView.RepaintAll();
        }
        internal static void AddViewActions(VisualElement parent, Func<SceneView> view)
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(.5f, .5f, .5f, .35f);
            separator.style.marginTop = separator.style.marginBottom = 5;
            parent.Add(separator);
            var top = Button("Top view", "view", "Toggle top view; return to the previous projection and pitch, retaining pan and heading.",
                () => HouseEditingContext.Active?.Camera.Toggle(view()));
            top.name = "house-top-view";
            var cutaway = Button("Cutaway", "layers", "Hide the active ceiling and upper levels.", () =>
            {
                HouseLevelView.Cutaway = !HouseLevelView.Cutaway;
                HouseLevelView.Set(Plan);
            });
            cutaway.name = "house-cutaway";
            var isolate = Button("Isolate", "view", "Hide outside renderers while editing. Outside objects cannot be edited in this context.",
                () => { var context = HouseEditingContext.Active; context?.SetIsolated(!context.Isolated); });
            isolate.name = "house-isolate";
            var snap = Button("Grid snap", "grid", "Toggle snapping. Use > beside it to set the grid spacing.", () =>
            {
                var session = HouseEditingContext.Active?.Session;
                if (session != null) session.SnapToGrid = !session.SnapToGrid;
                SceneView.RepaintAll();
            });
            snap.name = "house-grid-snap";
            var spacing = new Button { text = ">", name = "house-grid-spacing", tooltip = "Grid spacing in metres" };
            spacing.style.width = 24;
            spacing.style.height = 26;
            spacing.style.flexShrink = 0;
            spacing.clicked += () =>
            {
                var session = HouseEditingContext.Active?.Session;
                if (session != null) UnityEditor.PopupWindow.Show(spacing.worldBound, new HouseSnapPopup(session));
            };
            foreach (var button in new[] { top, cutaway, isolate }) parent.Add(button);
            var gridRow = new VisualElement { name = "house-grid-controls" };
            gridRow.style.flexDirection = FlexDirection.Row;
            snap.style.flexGrow = 1;
            snap.style.flexShrink = 1;
            gridRow.Add(snap);
            gridRow.Add(spacing);
            parent.Add(gridRow);
            Watch(parent, () =>
            {
                var context = HouseEditingContext.Active;
                Selected(top, context?.Camera.IsTop(view()) == true);
                Selected(cutaway, HouseBuilding.For(Plan) != null && HouseLevelView.Cutaway);
                cutaway.SetEnabled(HouseBuilding.For(Plan) != null);
                Selected(isolate, context?.Isolated == true);
                Selected(snap, context?.Session?.SnapToGrid == true);
            });
        }

        internal static void Watch(VisualElement root, Action update)
        {
            // Detached panels stop scheduling. Updating in place preserves focused controls.
            root.schedule.Execute(update).Every(100);
            root.RegisterCallback<AttachToPanelEvent>(_ => update());
        }
        internal static void Selected(Button button, bool selected)
        {
            button.style.backgroundColor = selected ? new StyleColor(new Color(.22f, .43f, .61f)) : new StyleColor(StyleKeyword.Null);
        }
        internal static Button Button(string text, string icon, string tooltip, Action action)
        {
            var button = new Button(action) { tooltip = tooltip };
            button.style.height = 26;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = string.IsNullOrEmpty(text) ? Justify.Center : Justify.FlexStart;
            if (string.IsNullOrEmpty(text)) button.style.width = 28;
            var image = new VisualElement();
            image.style.width = image.style.height = 16;
            image.style.flexShrink = 0;
            image.generateVisualContent += context => DrawIcon(context.painter2D, icon);
            button.Add(image);
            if (!string.IsNullOrEmpty(text)) { var label = new Label(text); label.style.marginLeft = 7; button.Add(label); }
            return button;
        }
        private static void DrawIcon(Painter2D painter, string icon)
        {
            painter.strokeColor = EditorGUIUtility.isProSkin ? new Color(.85f, .85f, .85f) : new Color(.22f, .22f, .22f);
            painter.lineWidth = 1.3f;
            void Line(params float[] points)
            {
                painter.BeginPath(); painter.MoveTo(new Vector2(points[0], points[1]));
                for (int i = 2; i < points.Length; i += 2) painter.LineTo(new Vector2(points[i], points[i + 1]));
                painter.Stroke();
            }
            switch (icon)
            {
                case "select": Line(3, 2, 13, 8, 8, 9, 6, 14, 3, 2); break;
                case "wall": Line(2, 13, 2, 3, 14, 3, 14, 13, 2, 13); Line(2, 8, 14, 8); Line(6, 3, 6, 8); Line(10, 8, 10, 13); break;
                case "divider": Line(2, 3, 14, 3, 14, 13, 2, 13, 2, 3); Line(8, 3, 8, 5); Line(8, 7, 8, 9); Line(8, 11, 8, 13); break;
                case "door": Line(3, 14, 3, 2, 12, 2, 12, 14); Line(6, 14, 6, 4, 12, 2); Line(8, 9, 9, 9); break;
                case "window": Line(2, 3, 14, 3, 14, 13, 2, 13, 2, 3); Line(8, 3, 8, 13); Line(2, 8, 14, 8); break;
                case "cut": Line(2, 6, 2, 2, 6, 2); Line(10, 2, 14, 2, 14, 6); Line(14, 10, 14, 14, 10, 14); Line(6, 14, 2, 14, 2, 10); break;
                case "delete": Line(2, 10, 9, 3, 14, 8, 7, 15, 2, 10); Line(6, 6, 11, 11); Line(7, 15, 14, 15); break;
                case "up": Line(3, 10, 8, 5, 13, 10); break;
                case "down": Line(3, 6, 8, 11, 13, 6); break;
                case "menu": Line(2, 8, 4, 8); Line(7, 8, 9, 8); Line(12, 8, 14, 8); break;
                case "layers": Line(1, 6, 8, 2, 15, 6, 8, 10, 1, 6); Line(1, 10, 8, 14, 15, 10); break;
                case "view": Line(2, 6, 2, 2, 6, 2); Line(10, 2, 14, 2, 14, 6); Line(14, 10, 14, 14, 10, 14); Line(6, 14, 2, 14, 2, 10); Line(5, 5, 11, 5, 11, 11, 5, 11, 5, 5); break;
                case "grid": Line(2, 2, 14, 2, 14, 14, 2, 14, 2, 2); Line(8, 2, 8, 14); Line(2, 8, 14, 8); break;
            }
        }
    }

    internal sealed class HouseSnapPopup : PopupWindowContent
    {
        private readonly HouseEditorSession tool;
        internal HouseSnapPopup(HouseEditorSession tool) { this.tool = tool; }
        public override Vector2 GetWindowSize() => new Vector2(210, 62);
        public override void OnGUI(Rect rect)
        {
            if (tool == null) { editorWindow.Close(); return; }
            tool.SnapToGrid = EditorGUILayout.ToggleLeft(new GUIContent("Grid snap", "Snap free endpoints and along-wall movement."), tool.SnapToGrid);
            float previous = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 90;
            tool.GridSize = EditorGUILayout.FloatField("Spacing (m)", tool.GridSize);
            EditorGUIUtility.labelWidth = previous;
        }
    }
}
