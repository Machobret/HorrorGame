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
            Pair(EnvironmentPlacementTool.Kind.Shelf,EnvironmentPlacementTool.Kind.Marking);
            creation.Add(new Button(WireTool.NewWire) { text = "Wire" });
            creation.Add(new Button(ScatterBrush.Open) { text = "Scatter" });
            var cancel=new Button(EnvironmentPlacementTool.Cancel){text="Cancel placement"};
            creation.Add(cancel);
            var enter=new Button(()=>{
                var selected=SelectedSurface();
                if(WireTool.Selected)WireTool.Begin(WireTool.Selected);else if(selected)RoadScene.Enter(selected);else HouseScene.Enter(HouseScene.SelectedFloor());
            });
            enter.style.height=26;root.Add(enter);

            var wireTools=new VisualElement();
            root.Add(wireTools);
            var wireSelect=SceneToolButtons.Add(wireTools,"Select","Edit a wire node or segment.",()=>WireTool.SetDrawing(false));
            var wireDraw=SceneToolButtons.Add(wireTools,"Draw Wire","Extend or branch from a node.",()=>WireTool.SetDrawing(true));
            wireTools.Add(new Button(HouseProperties.Open){text=HouseProperties.WindowTitle});
            wireTools.Add(new Button(WireTool.Leave){text="Done"});

            var surfaceTools=new VisualElement();
            root.Add(surfaceTools);
            var select=SceneToolButtons.Add(surfaceTools,"Select","Select a segment, point, face or boundary.",()=>RoadScene.SetDrawing(false));
            var road=SceneToolButtons.Add(surfaceTools,"Draw Road","Draw a road centreline.",()=>RoadScene.SetBrush(RoadScene.Brush.Road));
            var walk=SceneToolButtons.Add(surfaceTools,"Draw Sidewalk","Draw a sidewalk centreline.",()=>RoadScene.SetBrush(RoadScene.Brush.Sidewalk));
            var shelf=SceneToolButtons.Add(surfaceTools,"Draw Shelf","Draw a shelf centreline.",()=>RoadScene.SetBrush(RoadScene.Brush.Sidewalk));
            var marking=SceneToolButtons.Add(surfaceTools,"Draw Marking","Draw warehouse floor paint. Points support vertical movement.",()=>RoadScene.SetBrush(RoadScene.Brush.Sidewalk));
            var ground=SceneToolButtons.Add(surfaceTools,"Ground Area","Close a perimeter to fill it or draw between boundaries to divide it.",()=>RoadScene.SetBrush(RoadScene.Brush.Ground));
            var groundLabel=ground.Q<Label>();
            var fixture=SceneToolButtons.Add(surfaceTools,"Fixture","Attach a fixture to a façade wall.",()=>FixtureTool.BeginFacadeBrush(RoadScene.Active as FillerBuilding));
            surfaceTools.Add(new Button(HouseProperties.Open){text=HouseProperties.WindowTitle});
            surfaceTools.Add(new Button(RoadScene.Leave){text="Done"});

            var editing = new VisualElement();
            root.Add(editing);
            var tools = new Dictionary<HouseTool, Button>();
            AddTool(HouseTool.Select, "Select a wall, door, window, slab opening, room or corner.");
            AddTool(HouseTool.Wall, "Click to draw walls. Hold Shift for 45-degree increments or Alt for free placement.");
            AddTool(HouseTool.Door, "Place a compatible door on the active floor.");
            AddTool(HouseTool.Window, "Place a compatible window on the active floor.");

            AddTool(HouseTool.Fixture, "Attach lights and other fixtures to the active floor, ceiling or wall.");
            AddTool(HouseTool.Panel, "Place a custom panel offered by this wall family.");

            var opening = SceneToolButtons.Add(editing, "Slab opening", "Draw or edit a floor or ceiling opening", HouseProperties.DrawOpening);
            tools.Add(HouseTool.Boolean, SceneToolButtons.Add(editing, "Boolean cut", "Subtract a box or sphere from the house", () => HouseScene.SetMode(HouseTool.Boolean)));
            var cutawayRow = new VisualElement();
            cutawayRow.style.flexDirection = FlexDirection.Row;
            cutawayRow.style.marginTop = 5;
            root.Add(cutawayRow);
            var cutaway = SceneToolButtons.Add(cutawayRow, "Cutaway", "Hide the selected floor's ceiling and the floors above it.",
                () => HouseScene.ToggleCutaway(CutawayTarget()));
            cutaway.style.flexGrow = 1;
            var pin = new Button(() =>
            {
                var house = CutawayTarget();
                HouseScene.SetCutawayPinned(house, !HouseScene.IsCutawayPinned(house));
            }) { name = "pin-cutaway", text = "Pin", tooltip = "Keep this building's Cutaway setting after leaving edit mode." };
            pin.style.width = 36;
            pin.style.height = 26;
            pin.style.flexShrink = 0;
            cutawayRow.Add(pin);
            var footer = new VisualElement();
            root.Add(footer);
            footer.Add(new Button(HouseProperties.Open) {text = HouseProperties.WindowTitle});
            footer.Add(new Button(HouseScene.Leave) {text = "Done"});

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
                wireTools.style.display=WireTool.Active ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(wireSelect,!WireTool.Drawing);
                SceneToolButtons.Highlight(wireDraw,WireTool.Drawing);
                creation.style.display=!WireTool.Active && !floor && !active ? DisplayStyle.Flex : DisplayStyle.None;
                cancel.style.display=placing ? DisplayStyle.Flex : DisplayStyle.None;
                var selectedFloor = !active && !floor && !placing ? HouseScene.SelectedFloor() : null;
                enter.style.display=!WireTool.Active && !placing && !active && !floor && (WireTool.Selected || selected || selectedFloor) ? DisplayStyle.Flex : DisplayStyle.None;
                enter.text=WireTool.Selected ? "Edit Wire" : selected ? RoadScene.EditLabel(selected) : selectedFloor ? selectedFloor.EditLabel : "Edit House";
                editing.style.display=floor ? DisplayStyle.Flex : DisplayStyle.None;
                footer.style.display=editing.style.display;
                var cutawayHouse = floor ? floor.House : selectedFloor ? selectedFloor.House : null;
                bool pinned = HouseScene.IsCutawayPinned(cutawayHouse);
                cutawayRow.style.display = cutawayHouse && !cutawayHouse.wallsOnly && (floor || pinned)
                    ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(cutaway, HouseScene.CutawayFor(cutawayHouse));
                SceneToolButtons.Highlight(pin, pinned);
                surfaceTools.style.display=active ? DisplayStyle.Flex : DisplayStyle.None;
                road.style.display=active is RoadNetwork ? DisplayStyle.Flex : DisplayStyle.None;
                walk.style.display=active is ProceduralSidewalk ? DisplayStyle.Flex : DisplayStyle.None;
                marking.style.display=active is ProceduralMarking ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(marking,RoadScene.Mode==RoadScene.Brush.Sidewalk);
                shelf.style.display=active is ProceduralShelf ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(shelf,RoadScene.Mode==RoadScene.Brush.Sidewalk);
                groundLabel.text=active is FillerBuilding ? "Draw footprint" : "Ground Area";
                ground.style.display=active is GroundArea || active is FillerBuilding ? DisplayStyle.Flex : DisplayStyle.None;
                fixture.style.display=active is FillerBuilding ? DisplayStyle.Flex : DisplayStyle.None;
                SceneToolButtons.Highlight(fixture,FixtureTool.facadeBrush);
                SceneToolButtons.Highlight(select,RoadScene.Mode==RoadScene.Brush.Select && !FixtureTool.facadeBrush);
                SceneToolButtons.Highlight(road,RoadScene.Mode==RoadScene.Brush.Road);
                SceneToolButtons.Highlight(walk,RoadScene.Mode==RoadScene.Brush.Sidewalk);
                SceneToolButtons.Highlight(ground,RoadScene.Mode==RoadScene.Brush.Ground);
                if (floor)
                {
                    opening.style.display = floor.WallsOnly ? DisplayStyle.None : DisplayStyle.Flex;
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

            House CutawayTarget()
            {
                var floor = HouseScene.Floor ? HouseScene.Floor : HouseScene.SelectedFloor();
                return floor ? floor.House : null;
            }

            void AddTool(HouseTool tool, string tip)
            {
                tools.Add(tool, SceneToolButtons.Add(editing, tool.ToString(), tip, () => HouseScene.SetMode(tool)));
            }

        }
    }
}
