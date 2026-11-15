using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseToolkit.Editor
{
    [Overlay(typeof(SceneView), "house-toolkit-tools", "House Tools", true,
        defaultDockZone = DockZone.LeftColumn, defaultDockPosition = DockPosition.Top)]
    public sealed class HouseToolsOverlay : Overlay, ITransientOverlay
    {
        public bool visible => !EditorApplication.isPlayingOrWillChangePlaymode && !RoadScene.Active &&
            !(Selection.activeGameObject && Selection.activeGameObject.GetComponent<RoadNetwork>());

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.width = 168;
            var create = new Button(HouseScene.CreateHouse) {text = "Create House"};
            var road = new Button(RoadScene.Create) {text = "Create Road"};
            var enter = new Button(() => HouseScene.Enter(HouseScene.SelectedFloor())) {text = "Edit House"};
            create.style.height = road.style.height = enter.style.height = 26;
            root.Add(create);
            root.Add(road);
            root.Add(enter);

            var editing = new VisualElement();
            root.Add(editing);
            var tools = new Dictionary<HouseTool, Button>();
            AddTool(HouseTool.Select, "Select a wall, door, window, slab opening, room or corner.");
            AddTool(HouseTool.Wall, "Click to draw walls. Hold Shift for 45-degree increments or Alt for free placement.");
            AddTool(HouseTool.Door, "Place a compatible door on the active floor.");
            AddTool(HouseTool.Window, "Place a compatible window on the active floor.");

            var opening = SceneToolButtons.Add(editing, "Slab opening", "Draw or edit a floor or ceiling opening", HouseProperties.DrawOpening);
            tools.Add(HouseTool.Boolean, SceneToolButtons.Add(editing, "Boolean cut", "Subtract a box or sphere from the house", () => HouseScene.SetMode(HouseTool.Boolean)));
            var cutaway = SceneToolButtons.Add(editing, "Cutaway", "Hide the active ceiling and upper floors", () =>
            {
                HouseScene.Cutaway = !HouseScene.Cutaway;
                HouseScene.ApplyCutaway();
                HouseScene.Repaint();
            });
            cutaway.style.marginTop = 5;
            editing.Add(new Button(HouseProperties.Open) {text = "Properties"});
            editing.Add(new Button(HouseScene.Leave) {text = "Done"});

            var message = new Label();
            message.style.whiteSpace = WhiteSpace.Normal;
            root.Add(message);

            void Update()
            {
                var floor = HouseScene.Floor;
                create.style.display = road.style.display = floor ? DisplayStyle.None : DisplayStyle.Flex;
                enter.style.display = !floor && HouseScene.SelectedFloor() ? DisplayStyle.Flex : DisplayStyle.None;
                editing.style.display = floor ? DisplayStyle.Flex : DisplayStyle.None;
                if (floor)
                {
                    SceneToolButtons.Highlight(cutaway, HouseScene.Cutaway);
                    foreach (var pair in tools) SceneToolButtons.Highlight(pair.Value, !HouseScene.DrawingOpening && pair.Key == HouseScene.Mode);
                    SceneToolButtons.Highlight(opening,HouseScene.DrawingOpening);
                }
                message.text = HouseScene.Message;
                message.style.display = string.IsNullOrEmpty(message.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            Update();
            root.schedule.Execute(Update).Every(120);
            return root;

            void AddTool(HouseTool tool, string tip)
            {
                tools.Add(tool, SceneToolButtons.Add(editing, tool.ToString(), tip, () => HouseScene.SetMode(tool)));
            }

        }
    }
}
