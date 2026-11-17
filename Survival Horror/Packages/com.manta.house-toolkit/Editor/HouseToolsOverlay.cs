using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseToolkit.Editor
{
    [Overlay(typeof(SceneView), "house-toolkit-tools", "Environment Tools", true,
        defaultDockZone = DockZone.LeftColumn, defaultDockPosition = DockPosition.Top)]
    public sealed class HouseToolsOverlay : Overlay, ITransientOverlay
    {
        public bool visible => !EditorApplication.isPlayingOrWillChangePlaymode;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.width = 168;
            var creation=new VisualElement {name="environment-create-actions"};
            root.Add(creation);
            creation.Add(new Label("Create"));
            var createButtons=new Dictionary<EnvironmentPlacementTool.Kind,Button>();
            void Pair(params EnvironmentPlacementTool.Kind[] kinds)
            {
                var row=new VisualElement();
                row.style.flexDirection=FlexDirection.Row;creation.Add(row);
                foreach(var kind in kinds)
                {
                    string label=kind==EnvironmentPlacementTool.Kind.GroundArea ? "Ground Area" : kind==EnvironmentPlacementTool.Kind.FillerBuilding ? "Filler Building" : kind.ToString();
                    var button=new Button(()=>EnvironmentPlacementTool.Arm(kind,containerWindow as SceneView))
                        {name="create-"+kind,text=label,tooltip="Click in the Scene to start a new "+label+"."};
                    button.style.flexGrow=1;button.style.flexBasis=0;button.style.height=26;row.Add(button);createButtons.Add(kind,button);
                }
            }
            Pair(EnvironmentPlacementTool.Kind.House,EnvironmentPlacementTool.Kind.Road);
            Pair(EnvironmentPlacementTool.Kind.Sidewalk,EnvironmentPlacementTool.Kind.GroundArea);
            Pair(EnvironmentPlacementTool.Kind.FillerBuilding,EnvironmentPlacementTool.Kind.Wall);
            Pair(EnvironmentPlacementTool.Kind.Shelf);
            var cancel=new Button(EnvironmentPlacementTool.Cancel){text="Cancel placement"};
            creation.Add(cancel);
            var enter=new Button(()=>{
                var selected=SelectedSurface();
                if(selected)RoadScene.Enter(selected);else HouseScene.Enter(HouseScene.SelectedFloor());
            });
            enter.style.height=26;root.Add(enter);

            var surfaceTools=new VisualElement();
            root.Add(surfaceTools);
            var select=SceneToolButtons.Add(surfaceTools,"Select","Select a segment, point, face or boundary.",()=>RoadScene.SetDrawing(false));
            var road=SceneToolButtons.Add(surfaceTools,"Draw Road","Draw a road centreline.",()=>RoadScene.SetBrush(RoadScene.Brush.Road));
            var walk=SceneToolButtons.Add(surfaceTools,"Draw Sidewalk","Draw a sidewalk centreline.",()=>RoadScene.SetBrush(RoadScene.Brush.Sidewalk));
            var shelf=SceneToolButtons.Add(surfaceTools,"Draw Shelf","Draw a shelf centreline.",()=>RoadScene.SetBrush(RoadScene.Brush.Sidewalk));
            var ground=SceneToolButtons.Add(surfaceTools,"Ground Area","Close a perimeter to fill it or draw between boundaries to divide it.",()=>RoadScene.SetBrush(RoadScene.Brush.Ground));
            var groundLabel=ground.Q<Label>();
            surfaceTools.Add(new Button(HouseProperties.Open){text=HouseProperties.WindowTitle});
            surfaceTools.Add(new Button(RoadScene.Leave){text="Done"});

            var editing = new VisualElement();
            root.Add(editing);
            var tools = new Dictionary<HouseTool, Button>();
            AddTool(HouseTool.Select, "Select a wall, door, window, slab opening, room or corner.");
            AddTool(HouseTool.Wall, "Click to draw walls. Hold Shift for 45-degree increments or Alt for free placement.");
            AddTool(HouseTool.Door, "Place a compatible door on the active floor.");
            AddTool(HouseTool.Window, "Place a compatible window on the active floor.");

            AddTool(HouseTool.Panel, "Place a custom panel offered by this wall family.");

            var opening = SceneToolButtons.Add(editing, "Slab opening", "Draw or edit a floor or ceiling opening", HouseProperties.DrawOpening);
            tools.Add(HouseTool.Boolean, SceneToolButtons.Add(editing, "Boolean cut", "Subtract a box or sphere from the house", () => HouseScene.SetMode(HouseTool.Boolean)));
            var cutaway = SceneToolButtons.Add(editing, "Cutaway", "Hide the active ceiling and upper floors", () =>
            {
                HouseScene.Cutaway = !HouseScene.Cutaway;
                HouseScene.ApplyCutaway();
                HouseScene.Repaint();
            });
            cutaway.style.marginTop = 5;
            editing.Add(new Button(HouseProperties.Open) {text = HouseProperties.WindowTitle});
            editing.Add(new Button(HouseScene.Leave) {text = "Done"});

            var message = new Label();
            message.style.whiteSpace = WhiteSpace.Normal;
            root.Add(message);

            void Update()
            {
                var floor = HouseScene.Floor;
                var active=RoadScene.Active;
                var selected=SelectedSurface();
                var pending=EnvironmentPlacementTool.Pending;
                bool placing=pending;
                foreach(var pair in createButtons)SceneToolButtons.Highlight(pair.Value,placing && pending.choice==pair.Key);
                displayName="Environment Tools";
                creation.style.display=!floor && !active ? DisplayStyle.Flex : DisplayStyle.None;
                cancel.style.display=placing ? DisplayStyle.Flex : DisplayStyle.None;
                var selectedFloor = !active && !floor && !placing ? HouseScene.SelectedFloor() : null;
                enter.style.display=!placing && !active && !floor && (selected || selectedFloor) ? DisplayStyle.Flex : DisplayStyle.None;
                enter.text=selected ? RoadScene.EditLabel(selected) : selectedFloor ? selectedFloor.EditLabel : "Edit House";
                editing.style.display=floor ? DisplayStyle.Flex : DisplayStyle.None;
                surfaceTools.style.display=active ? DisplayStyle.Flex : DisplayStyle.None;
                road.style.display=active is RoadNetwork ? DisplayStyle.Flex : DisplayStyle.None;
                walk.style.display=active is ProceduralSidewalk ? DisplayStyle.Flex : DisplayStyle.None;
                shelf.style.display=active is ProceduralShelf ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(shelf,RoadScene.Mode==RoadScene.Brush.Sidewalk);
                groundLabel.text=active is FillerBuilding ? "Draw footprint" : "Ground Area";
                ground.style.display=active is GroundArea || active is FillerBuilding ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(select,RoadScene.Mode==RoadScene.Brush.Select);
                SceneToolButtons.Highlight(road,RoadScene.Mode==RoadScene.Brush.Road);
                SceneToolButtons.Highlight(walk,RoadScene.Mode==RoadScene.Brush.Sidewalk);
                SceneToolButtons.Highlight(ground,RoadScene.Mode==RoadScene.Brush.Ground);
                if (floor)
                {
                    cutaway.style.display = opening.style.display = floor.WallsOnly ? DisplayStyle.None : DisplayStyle.Flex;
                    SceneToolButtons.Highlight(cutaway, HouseScene.Cutaway);
                    foreach (var pair in tools) SceneToolButtons.Highlight(pair.Value, !HouseScene.DrawingOpening && pair.Key == HouseScene.Mode);
                    SceneToolButtons.Highlight(opening,HouseScene.DrawingOpening);
                }
                message.text = placing ? "Click to start drawing. Esc cancels." : active ? RoadScene.Message : HouseScene.Message;
                message.style.display = string.IsNullOrEmpty(message.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            Update();
            root.schedule.Execute(Update).Every(120);
            return root;

            ProceduralSurface SelectedSurface() => Selection.activeGameObject && Selection.activeGameObject.scene.IsValid()
                ? Selection.activeGameObject.GetComponent<ProceduralSurface>() : null;

            void AddTool(HouseTool tool, string tip)
            {
                tools.Add(tool, SceneToolButtons.Add(editing, tool.ToString(), tip, () => HouseScene.SetMode(tool)));
            }

        }
    }
}
