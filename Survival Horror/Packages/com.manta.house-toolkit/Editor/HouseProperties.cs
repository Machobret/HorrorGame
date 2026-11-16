using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal sealed class FloorCreationDialog : EditorWindow
    {
        System.Action<bool> choose;
        string source;
        internal static FloorCreationDialog Open(string floorName,bool below,Action<bool> onChoice)
        {
            var dialog=GetWindow<FloorCreationDialog>(true,below?"Add floor below":"Add floor above",true);
            dialog.titleContent=new GUIContent(below?"Add floor below":"Add floor above");
            dialog.minSize=dialog.maxSize=new Vector2(340,80);
            dialog.source=floorName;
            dialog.choose=onChoice;
            dialog.Repaint();
            return dialog;
        }

        void OnGUI()
        {
            GUILayout.Label("Copy the layout of "+source+" or start empty?",EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Copy layout")){Close();choose?.Invoke(true);GUIUtility.ExitGUI();}
            if(GUILayout.Button("Empty floor")){Close();choose?.Invoke(false);GUIUtility.ExitGUI();}
            if(GUILayout.Button("Cancel") || (Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Escape))Close();
            GUILayout.EndHorizontal();
        }
    }

    public sealed partial class HouseProperties : EditorWindow
    {
        internal const string WindowTitle = "Procedural Env Properties";
        static HouseProperties window;
        UnityEditor.Editor roadEditor;
        Vector2 roadScroll;
        Vector2 scroll;
        int tab;
        static readonly string[] Tabs = {"House","Floors","Selection","Tool Properties"};
        static readonly string[] WallTabs = {"Wall","Selection","Tool Properties"};
        Vector2 floorsScroll;
        bool ceilingCuts,rectangleCuts;
        [MenuItem("Window/House Toolkit/" + WindowTitle)]
        public static void Open(){window=GetWindow<HouseProperties>(WindowTitle);}
        internal static void RepaintOpen(){if(window)window.Repaint();}
        public static void OpenFloors(){Open(); ShowFloor();}
        internal static void DrawOpening()
        {
            Open();
            if (!HouseScene.DrawingOpening)
                HouseScene.ToggleOpening(window.ceilingCuts, window.rectangleCuts);
            ShowTool();
        }
        void OnEnable()
        {
            window = this;
            titleContent = new GUIContent(WindowTitle);
        }

        void OnDisable()
        {
            if (roadEditor) DestroyImmediate(roadEditor);
            if (window == this) window = null;
        }

        void OnSelectionChange() => Repaint();
        void OnGUI()
        {
            var floor=HouseScene.Floor;
            var selected = Selection.activeGameObject;
            var road = RoadScene.Active;
            if (!road && selected && selected.scene.IsValid()) road = selected.GetComponent<ProceduralSurface>();
            if (road)
            {
                UnityEditor.Editor.CreateCachedEditor(road, typeof(RoadInspector), ref roadEditor);
                roadScroll = EditorGUILayout.BeginScrollView(roadScroll);
                ((RoadInspector)roadEditor).DrawProperties();
                EditorGUILayout.EndScrollView();
                return;
            }
            if (roadEditor) DestroyImmediate(roadEditor);
            if (!floor)
            {
                var selectedFloor = selected && selected.scene.IsValid() ? HouseScene.SelectedFloor() : null;
                if (selectedFloor)
                {
                    if (GUILayout.Button(selectedFloor.EditLabel)) HouseScene.Enter(selectedFloor);
                }
                else EditorGUILayout.HelpBox("Select a procedural environment object in the Scene.", MessageType.Info);
                return;
            }
            if (floor.WallsOnly)
            {
                int index = GUILayout.Toolbar(Mathf.Max(0, tab - 1), WallTabs);
                tab = index == 0 ? 0 : index + 1;
            }
            else tab=GUILayout.Toolbar(tab,Tabs);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            var wall=HouseScene.Wall;
            if(tab==0 && floor.WallsOnly) StandaloneFields(floor);
            else if(tab==0)HouseFields(floor.House);
            else if(tab==1)DrawFloors(floor);
            else if(tab==3) DrawToolProperties();
            else if(HouseScene.Picks.Items.Count>1) DrawHouseBatch();
            else if(BooleanTool.Current!=null) BooleanTool.SelectionProperties();
            else if(HouseScene.Mode==HouseTool.Boolean)
            {
                if(GUILayout.Button("Tool Properties")) ShowTool();
            }
            else if(HouseScene.SelectedCutIndex()>=0)
            {
                EditorGUILayout.LabelField(HouseScene.Selected.ceiling?"Ceiling opening":"Floor opening",EditorStyles.boldLabel);
                if(GUILayout.Button("Edit outline"))HouseScene.EditSelectedCut();
                if(GUILayout.Button("Remove opening"))HouseScene.DeleteSelection();
            }
            else if (HouseScene.Mode == HouseTool.Select && HouseScene.Selected.corner != 0)
            {
                EditorGUILayout.LabelField("Wall endpoint", EditorStyles.boldLabel);
                DrawDissolve(HouseScene.Selected.corner);
            }
            else if (HouseScene.MultipleWalls) DrawWallSelection();
            else if(wall)WallFields(wall);
            else if(HouseScene.Room)
            {
                var room=HouseScene.Room;
                bool ceiling=HouseScene.Selected.ceiling;
                EditorGUILayout.LabelField(ceiling?"Room ceiling":"Room floor",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var ceilingOverride=(RoomCeiling)EditorGUILayout.EnumPopup("Ceiling",room.ceilingOverride);
                if(EditorGUI.EndChangeCheck()) Change(room,"Change room ceiling",()=>room.ceilingOverride=ceilingOverride,()=>{SlabBuilder.Rebuild(floor.House);return null;});
                EditorGUI.BeginChangeCheck();
                var finish=FinishPicker.Draw("Material override",ceiling?room.ceilingFinish:room.floorFinish);
                if(EditorGUI.EndChangeCheck())SetRoomFinish(room,ceiling,finish);
                EditorGUI.BeginChangeCheck();
                var walls = FinishPicker.Draw("Room wall finish", room.wallFinish);
                if (EditorGUI.EndChangeCheck()) Change(room, "Change room wall finish", () => room.wallFinish = walls, () => {
                    foreach (var member in floor.GetComponentsInChildren<HouseWall>(true)) WallBuilder.ApplyMaterials(member);
                    return null;
                });
            }
            else if(HouseScene.Mode==HouseTool.Select)GUILayout.Label("Select a room, wall, endpoint or opening in the Scene.");
            if(!string.IsNullOrEmpty(HouseScene.Message))EditorGUILayout.HelpBox(HouseScene.Message,MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void DrawToolProperties()
        {
            if (HouseScene.DrawingOpening)
            {
                var opening = HouseScene.OpeningTool;
                if (!opening.EditingOutline)
                {
                    using (new EditorGUI.DisabledScope(!opening.CanConfigure))
                    {
                        bool ceiling = GUILayout.Toolbar(opening.Ceiling ? 1 : 0, new[] { "Floor", "Ceiling" }) == 1;
                        bool rectangle = GUILayout.Toolbar(opening.Rectangle ? 1 : 0, new[] { "Polygon", "Rectangle" }) == 1;
                        if (ceiling != opening.Ceiling || rectangle != opening.Rectangle)
                        {
                            ceilingCuts = ceiling;
                            rectangleCuts = rectangle;
                            HouseScene.ToggleOpening(ceiling, rectangle);
                        }
                    }
                }
            }
            else if (HouseScene.Mode == HouseTool.Wall)
            {
                HouseScene.DrawFamily = (WallFamily)EditorGUILayout.ObjectField(
                    new GUIContent("Family override", "Leave empty to inherit the interior or exterior family for each new wall."),
                    HouseScene.DrawFamily, typeof(WallFamily), false);
            }
            else if (HouseScene.Mode == HouseTool.Select || HouseScene.Mode == HouseTool.Boolean)
            {
                BooleanTool.ToolProperties();
                if (HouseScene.Mode == HouseTool.Boolean) return;
            }
            else if (HouseScene.Mode == HouseTool.Door || HouseScene.Mode == HouseTool.Window)
            {
                GUILayout.Label("Opening palette",EditorStyles.boldLabel);
                var host=HouseScene.PaletteWall;
                if(host)
                {
                    var kind=HouseScene.Mode==HouseTool.Door?OpeningKind.Door:OpeningKind.Window;
                    var choices=OpeningPalette.Choices(host,kind);
                    if(choices.Count>0)
                    {
                        int i=Mathf.Max(0,choices.IndexOf(HouseScene.Choice));
                        HouseScene.Choice=choices[EditorGUILayout.Popup(host.Family.name,i,choices.Select(x=>x.name+(System.Array.IndexOf(host.Family.defaultInserts ?? System.Array.Empty<HouseInsert>(),x) >= 0?" (default)":"")).ToArray())];
                        if(HouseScene.Choice is GarageDoorInsert garage) GarageDoorFields.Brush(garage);
                        if(kind==OpeningKind.Window && !host.Family.panel)HouseScene.Sill=EditorGUILayout.FloatField("Sill (m)",HouseScene.Sill);
                    }
                    else EditorGUILayout.HelpBox("No compatible inserts.",MessageType.Info);
                }
                else GUILayout.Label("Point at a wall to choose an insert.");
                if (HouseScene.Mode == HouseTool.Door)
                    HouseScene.FlipDoor = EditorGUILayout.Toggle("Flip facing", HouseScene.FlipDoor);
            }
            HouseScene.Grid=EditorGUILayout.DelayedFloatField("Grid spacing (m)",HouseScene.Grid);
        }

        static readonly GUIContent dissolveButton = new GUIContent("Dissolve endpoint",
            "Connect the outer endpoints. If both walls are selected, the last-selected wall supplies the settings.");

        static void DrawDissolve(int endpoint)
        {
            string issue = endpoint == 0 ? "The selected walls must share an endpoint." :
                FloorEdit.DissolveIssue(HouseScene.Floor, endpoint);
            using (new EditorGUI.DisabledScope(issue != null))
                if (GUILayout.Button(dissolveButton)) HouseScene.DissolveEndpoint(endpoint, selectMergedWall: true);
            if (issue != null) EditorGUILayout.HelpBox(issue, MessageType.Info);
        }

        static void DrawWallSelection()
        {
            var walls = HouseScene.SelectedWalls.ToArray();
            GUILayout.Label($"{walls.Length} walls selected", EditorStyles.boldLabel);
            if (walls.Length == 2)
            {
                var a = walls[0]; var b = walls[1];
                int shared = a.startCorner == b.startCorner || a.startCorner == b.endCorner ? a.startCorner :
                    a.endCorner == b.startCorner || a.endCorner == b.endCorner ? a.endCorner : 0;
                DrawDissolve(shared);
                if (!HouseScene.MultipleWalls) return;
            }
            DrawFaceFinish(walls);
            DrawCommonWalls(walls);
            var targets = walls.Where(w => !w.boundaryOnly && w.Family && w.Family.IsComplex).ToArray();
            if (targets.Length == 0) return;
            if (targets.Length != walls.Length) GUILayout.Label($"Panel layout applies to {targets.Length} Complex walls.");
            var first = targets[0].panelLayoutOverride;
            EditorGUI.showMixedValue = targets.Any(w => w.panelLayoutOverride != first);
            EditorGUI.BeginChangeCheck();
            int choice = EditorGUILayout.Popup("Panel layout", (int)first + 1, new[] { "Inherit", "Best Fit", "Even Spacing" });
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (changed) SetWallLayouts(targets, (PanelLayoutOverride)(choice - 1));
            // This also covers a mixed selection whose first wall already inherits.
            if (targets.Any(w => w.panelLayoutOverride != PanelLayoutOverride.Inherit) && GUILayout.Button("Reset to Inherit"))
                SetWallLayouts(targets, PanelLayoutOverride.Inherit);
        }

        static void DrawFaceFinish(HouseWall[] walls)
        {
            var picked = HouseScene.Selected;
            if (!picked.wall || picked.wall.boundaryOnly) return;
            var rooms = picked.wall.Floor.GetComponentsInChildren<HouseRoom>(true);
            var room = WallFaces.RoomOnSide(picked.wall, picked.positiveFace, rooms);
            var faces = SelectedFaces(walls, picked.positiveFace, room, rooms);
            if (faces.Length == 0) return;
            Material Current(HouseWall wall, bool side)
            {
                var value = side ? wall.positiveFinish : wall.negativeFinish;
                return value ? value : wall.surfaceOverride;
            }
            var first = Current(faces[0].wall, faces[0].side);
            EditorGUI.showMixedValue = faces.Any(f => Current(f.wall, f.side) != first);
            EditorGUI.BeginChangeCheck();
            string label = room ? "Room-facing finish" : "Outside face finish";
            var finish = FinishPicker.Draw(walls.Length == 1 ? label : $"{label} ({faces.Length} faces)", first);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (!changed) return;
            SetFaceFinishes(faces, finish);
        }

        internal static (HouseWall wall, bool side)[] SelectedFaces(HouseWall[] walls, bool positive, HouseRoom room, HouseRoom[] rooms) =>
            (from wall in walls where !wall.boundaryOnly && wall.Family && !wall.Family.IsComplex
             from side in new[]{false, true}
             where walls.Length == 1 ? side == positive : WallFaces.RoomOnSide(wall, side, rooms) == room
             select (wall, side)).ToArray();

        internal static void SetFaceFinishes((HouseWall wall, bool side)[] faces, Material finish)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change wall faces");
            var targets = faces.Select(f => f.wall).Distinct().ToArray();
            Undo.RecordObjects(targets, "Change wall faces");
            foreach (var (wall, side) in faces)
            {
                if (wall.surfaceOverride)
                {
                    if (!wall.positiveFinish) wall.positiveFinish = wall.surfaceOverride;
                    if (!wall.negativeFinish) wall.negativeFinish = wall.surfaceOverride;
                    wall.surfaceOverride = null;
                }
                if (side) wall.positiveFinish = finish; else wall.negativeFinish = finish;
                EditorUtility.SetDirty(wall);
            }
            foreach (var wall in targets) WallBuilder.ApplyMaterials(wall);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            HouseScene.Repaint();
        }

        internal static void SetWallLayouts(HouseWall[] selection, PanelLayoutOverride value)
        {
            var walls = selection.Where(w => w && !w.boundaryOnly && w.Family && w.Family.IsComplex && w.panelLayoutOverride != value).Distinct().ToArray();
            if (walls.Length == 0) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change wall panel layouts");
            try
            {
                Undo.RecordObjects(walls, "Change wall panel layouts");
                foreach (var wall in walls) wall.panelLayoutOverride = value;
                Undo.FlushUndoRecordObjects();
                foreach (var wall in walls)
                {
                    if (!WallBuilder.Rebuild(wall, out var issue)) throw new InvalidOperationException(issue);
                    EditorUtility.SetDirty(wall);
                }
                Undo.CollapseUndoOperations(group);
                HouseScene.Message = null;
            }
            catch (Exception ex) { Undo.RevertAllDownToGroup(group); HouseScene.Message = ex.Message; }
            HouseScene.Repaint();
        }

        internal static PanelLayoutOverride PanelLayoutField(HouseWall wall, WallFamily familyOverride, bool boundary)
        {
            var family = familyOverride ? familyOverride : wall.Floor.FamilyFor(wall.interior);
            if (boundary || !family || !family.IsComplex) return wall.panelLayoutOverride;
            string inherited = family.defaultPanelLayout == PanelLayout.EvenSpacing ? "Even Spacing" : "Best Fit";
            var labels = new[] { $"Inherit ({inherited})", "Best Fit", "Even Spacing" };
            int choice = EditorGUILayout.Popup("Panel layout", (int)wall.panelLayoutOverride + 1, labels);
            return (PanelLayoutOverride)(choice - 1);
        }

        internal static void SetRoomFinish(HouseRoom room,bool ceiling,Material finish)
        {
            Change(room,"Change room material",()=>{
                if(ceiling)room.ceilingFinish=finish;
                else room.floorFinish=finish;
            },()=>{SlabBuilder.Rebuild(room.GetComponentInParent<House>());return null;});
        }
        internal static void ShowTool(){if(window){window.tab=3;window.Repaint();}}
        internal static void ShowFloor(){if(window){window.tab=1;window.Repaint();}}
        internal static void ShowSelection(){if(window){window.tab=2;window.Repaint();}}

        static T Field<T>(string label,T value) where T:UnityEngine.Object =>
            (T)EditorGUILayout.ObjectField(label,value,typeof(T),false);

        static void Change(UnityEngine.Object target,string label,Action apply,Func<string> validate)
        {
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(label);
            Undo.RecordObject(target,label);apply();Undo.FlushUndoRecordObjects();
            string error=validate();
            if(error!=null)Undo.RevertAllDownToGroup(group);
            else {Undo.CollapseUndoOperations(group);EditorUtility.SetDirty(target);}
            HouseScene.Message=error;HouseScene.Repaint();
        }
        static string Refresh(House house)=>HouseEditing.Refresh(house,out var error)?null:error;
        static Material FinishField(string label, WallFamily family, Material selected, bool glass = false)
        {
            if (!glass) return FinishPicker.Draw(label, selected, family ? family.surfaceFinishes : null);
            if(!family)return Field(label,selected);
            var extras=family.glassFinishes;
            if(extras==null || extras.Length==0)return Field(label,selected);
            var values=new[]{family.glass}.Concat(extras).Append(selected).Where(m=>m).Distinct().ToList();
            var names=new[]{"Inherit"}.Concat(values.Select(m=>m.name)).ToArray();
            int i=EditorGUILayout.Popup(label,selected?values.IndexOf(selected)+1:0,names);
            return i==0?null:values[i-1];
        }
        static float ThicknessField(string label,float value)
        {
            var entry=EditorGUILayout.DelayedFloatField(new GUIContent(label+" (m)","0 inherits the next default. Applies to plain walls."),value);
            return float.IsFinite(entry) && entry>0 ? Mathf.Max(.01f,entry) : 0;
        }

        static void StandaloneFields(HouseFloor floor)
        {
            var house = floor.House;
            EditorGUI.BeginChangeCheck();
            var family = Field("Wall family",house.exteriorFamily);
            var thickness = family && family.IsComplex ? house.exteriorThickness : ThicknessField("Thickness",house.exteriorThickness);
            var finish = FinishField("Surface",family,house.exteriorFinish);
            var glass = family && family.HasGlass ? FinishField("Glass",family,house.exteriorGlass,true) : house.exteriorGlass;
            if (EditorGUI.EndChangeCheck())
                Change(house,"Change standalone wall",() => {
                    house.exteriorFamily = family;
                    house.exteriorThickness = thickness;
                    house.exteriorFinish = finish;
                    house.exteriorGlass = glass;
                }, () => Refresh(house));
            EditorGUI.BeginChangeCheck();
            var height = EditorGUILayout.DelayedFloatField("Height (m)",floor.height);
            if (EditorGUI.EndChangeCheck()) WallBuilder.SetHeight(floor,height,out HouseScene.Message);
        }

        static void HouseFields(House house)
        {
            EditorGUI.BeginChangeCheck();
            var exteriorDepth=ThicknessField("Plain exterior thickness",house.exteriorThickness);
            var interiorDepth=ThicknessField("Plain interior thickness",house.interiorThickness);
            var exterior=Field("Exterior family",house.exteriorFamily);var interior=Field("Interior family",house.interiorFamily);
            var outside=FinishField("Exterior finish",exterior,house.exteriorFinish);var inside=FinishField("Interior finish",interior,house.interiorFinish);
            var exteriorGlass=house.exteriorGlass;var interiorGlass=house.interiorGlass;
            if(exterior && exterior.HasGlass)exteriorGlass=FinishField("Exterior glass",exterior,exteriorGlass,true);
            if(interior && interior.HasGlass)interiorGlass=FinishField("Interior glass",interior,interiorGlass,true);
            var floorMat=FinishPicker.Draw("Floor finish",house.floorFinish);
            var ceilingMat=FinishPicker.Draw("Ceiling finish",house.ceilingFinish);
            float thickness=EditorGUILayout.DelayedFloatField("Slab thickness (m)",house.slabThickness);var trim=Field("Trim",house.trim);
            if(EditorGUI.EndChangeCheck())Change(house,"Edit house defaults",()=>{
                house.exteriorThickness=exteriorDepth;house.interiorThickness=interiorDepth;
                house.exteriorFamily=exterior;house.interiorFamily=interior;house.exteriorFinish=outside;house.interiorFinish=inside;
                house.floorFinish=floorMat;house.ceilingFinish=ceilingMat;house.trim=trim;house.exteriorGlass=exteriorGlass;house.interiorGlass=interiorGlass;},()=>{
                    if(!float.IsFinite(thickness)||thickness<.01f)return "Slab thickness must be at least 1 cm.";
                    float delta=thickness-house.slabThickness;
                    if(delta!=0)
                    {
                        var levels=HouseEditing.Floors(house);
                        for(int i=1;i<levels.Length;i++)
                        {
                            Undo.RecordObject(levels[i].transform,"Adjust floor elevation");
                            levels[i].transform.localPosition+=Vector3.up*(i*delta);
                        }
                        Undo.RecordObject(house,"Change slab thickness");house.slabThickness=thickness;
                    }
                    var issue=Refresh(house); HouseScene.RefocusIfMoved(); return issue;
                });
            BooleanTool.CutMaterialField(house);
        }
        RoomAssignment.Result assignment;
        House assignmentHouse;
        bool showUnassigned;

        void DrawFloors(HouseFloor active)
        {
            var floors = HouseEditing.Floors(active.House);
            floorsScroll = EditorGUILayout.BeginScrollView(floorsScroll, GUILayout.Height(Mathf.Min(floors.Length, 6) * 26 + 4));
            for (int i = floors.Length - 1; i >= 0; --i)
            {
                var floor = floors[i];
                var rect = EditorGUILayout.GetControlRect(false, 24);
                bool selected = floor == active;
                if (selected) EditorGUI.DrawRect(rect, new Color(.22f,.43f,.61f));
                if (GUI.Button(rect, new GUIContent("  " + floor.name, "Edit " + floor.name), EditorStyles.label))
                {
                    HouseScene.SwitchFloor(floor);
                    active = floor;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            bool above=GUILayout.Button("Add above"), below=GUILayout.Button("Add below");
            if(GUILayout.Button("Delete floor") && EditorUtility.DisplayDialog("Delete "+active.name+"?",
                "Remove this floor and its contents? You can undo this change.", "Delete floor", "Cancel"))
            {
                try
                {
                    active=HouseEditing.DeleteFloor(active);
                    if(active)HouseScene.SwitchFloor(active);
                    else HouseScene.Leave();
                }
                catch(InvalidOperationException error){HouseScene.Message=error.Message;}
            }
            EditorGUILayout.EndHorizontal();
            if(!active)return;
            if (above || below)
            {
                var source=active;
                FloorCreationDialog.Open(source.name,below,copy=>
                {
                    if(!source || HouseScene.House!=source.House)return;
                    try { HouseScene.SwitchFloor(HouseEditing.AddFloor(source.House,source,copy,below)); }
                    catch(InvalidOperationException error){HouseScene.Message=error.Message;}
                });
            }
            if (GUILayout.Button("Assign Room Contents"))
            {
                assignmentHouse = active.House;
                assignment = RoomAssignment.Assign(active.House);
                HouseScene.ApplyCutaway();
                HouseScene.Repaint();
            }
            if (assignment != null && assignmentHouse == active.House)
            {
                if (assignment.review.Count == 0) GUILayout.Label($"{assignment.moved} assigned");
                else
                {
                    showUnassigned = EditorGUILayout.Foldout(showUnassigned,
                        $"{assignment.moved} assigned, {assignment.review.Count} need review");
                    if (showUnassigned)
                        foreach (var item in assignment.review)
                            if (item && GUILayout.Button(item.name, EditorStyles.linkLabel)) EditorGUIUtility.PingObject(item);
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(active.name, EditorStyles.boldLabel);
            FloorFields(active);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slab openings",EditorStyles.boldLabel);
            bool ceiling=ceilingCuts=GUILayout.Toolbar(ceilingCuts?1:0,new[]{"Floor","Ceiling"})==1;
            var owner=SlabOpeningTool.Owner(active,ceiling,out bool roof);
            var openings=roof?owner.roofOpenings:owner.floorOpenings;
            for(int i=0;i<openings.Count;i++)
            {
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button($"Edit opening {i+1}"))HouseScene.ToggleOpening(ceiling,rectangleCuts,i);
                bool remove=GUILayout.Button("Remove",GUILayout.Width(64));
                EditorGUILayout.EndHorizontal();
                if(remove)
                {
                    SlabBuilder.SetOpening(owner,roof,i,null,out HouseScene.Message);
                    HouseScene.RefreshOpeningTarget();
                    HouseScene.Repaint();break;
                }
            }
            if(GUILayout.Button(HouseScene.DrawingOpening?"Done":"Draw opening"))
            {
                if(HouseScene.DrawingOpening)HouseScene.EndOpening();
                else DrawOpening();
            }
        }

        static void FloorFields(HouseFloor floor)
        {
            EditorGUI.BeginChangeCheck();float height=EditorGUILayout.DelayedFloatField("Clear height (m)",floor.height);
            if(EditorGUI.EndChangeCheck()){WallBuilder.SetHeight(floor,height,out HouseScene.Message);HouseScene.Repaint();}
            EditorGUI.BeginChangeCheck();
            bool hasCeiling=EditorGUILayout.Toggle("Generate ceiling",!floor.openCeiling);
            var exteriorDepth=ThicknessField("Plain exterior thickness",floor.exteriorThickness);
            var interiorDepth=ThicknessField("Plain interior thickness",floor.interiorThickness);
            var ext=Field("Exterior family override",floor.defaultFamily);var ins=Field("Interior family override",floor.interiorFamily);
            var outside=FinishField("Exterior finish override",floor.FamilyFor(false),floor.exteriorFinish);var inside=FinishField("Interior finish override",floor.FamilyFor(true),floor.interiorFinish);
            var exteriorGlass=floor.exteriorGlass;var interiorGlass=floor.interiorGlass;
            if(floor.FamilyFor(false) && floor.FamilyFor(false).HasGlass)exteriorGlass=FinishField("Exterior glass override",floor.FamilyFor(false),exteriorGlass,true);
            if(floor.FamilyFor(true) && floor.FamilyFor(true).HasGlass)interiorGlass=FinishField("Interior glass override",floor.FamilyFor(true),interiorGlass,true);
            var mat=FinishPicker.Draw("Floor finish override",floor.floorFinish);
            var ceiling=FinishPicker.Draw("Ceiling finish override",floor.ceilingFinish);var trim=Field("Trim override",floor.trim);
            if(EditorGUI.EndChangeCheck())Change(floor,"Edit floor defaults",()=>{
                floor.exteriorThickness=exteriorDepth;floor.interiorThickness=interiorDepth;
                floor.openCeiling=!hasCeiling;floor.defaultFamily=ext;floor.interiorFamily=ins;floor.exteriorFinish=outside;floor.interiorFinish=inside;
                floor.floorFinish=mat;floor.ceilingFinish=ceiling;floor.trim=trim;floor.exteriorGlass=exteriorGlass;floor.interiorGlass=interiorGlass;},()=>Refresh(floor.House));

        }
        static void WallFields(HouseWall wall)
        {
            var opening=wall.openings.Find(o=>o.id==HouseScene.Opening);
            if(opening!=null)
            {
                var surround=wall.Family && wall.Family.panel?wall.Family.FindSurround(opening.insert):null;
                if(HouseScene.Selected.segmentId!=null)
                {
                    EditorGUILayout.LabelField(surround?surround.name:"Opening segment",EditorStyles.boldLabel);
                    if(GUILayout.Button("Select "+opening.insert.kind.ToString().ToLowerInvariant()))HouseScene.SelectDoorOrWindow();
                    if(GUILayout.Button("Select whole wall"))HouseScene.SelectWall(wall);
                    float position=EditorGUILayout.DelayedFloatField("Distance from start (m)",opening.distance);
                    if(position!=opening.distance)HouseScene.EditOpening(wall,opening,opening.insert,position,opening.sill,true,out HouseScene.Message);
                    if(GUILayout.Button("Remove segment and insert"))HouseScene.DeleteSelection();
                    return;
                }
                EditorGUILayout.LabelField(opening.insert.name,EditorStyles.boldLabel);
                if(surround && GUILayout.Button("Select opening segment"))HouseScene.SelectSurround();
                if(GUILayout.Button("Select whole wall"))HouseScene.SelectWall(wall);
                var items=OpeningPalette.Choices(wall,opening.insert.kind);
                if(!items.Contains(opening.insert))items.Add(opening.insert);
                EditorGUI.BeginChangeCheck();
                var choice=items[EditorGUILayout.Popup("Insert",items.IndexOf(opening.insert),items.Select(x=>x.name).ToArray())];
                float distance=EditorGUILayout.DelayedFloatField("Distance from start (m)",opening.distance);
                float sill=opening.sill;
                if(choice.kind==OpeningKind.Window && !wall.Family.panel)sill=EditorGUILayout.DelayedFloatField("Sill (m)",sill);
                var size=opening.clearSize; int tile=opening.finishTile;
                if(choice is GarageDoorInsert garage)GarageDoorFields.Selection(garage,ref size,ref tile);
                if(EditorGUI.EndChangeCheck())HouseScene.EditOpening(wall,opening,choice,distance,sill,true,out HouseScene.Message,size,tile);
                if(opening.insert is GarageDoorInsert selectedGarage && selectedGarage.operable && GUILayout.Button("Select gameplay prefab"))
                {
                    var attachment=wall.GetComponentsInChildren<InsertAttachment>().FirstOrDefault(a=>a.openingId==opening.id);
                    if(attachment && attachment.GetComponentInChildren<GarageDoor>() is GarageDoor door)Selection.activeGameObject=door.gameObject;
                }
                if(GUILayout.Button("Remove opening"))HouseScene.DeleteSelection();
                return;
            }
            if (wall.Family && !wall.Family.IsComplex && !wall.boundaryOnly) DrawFaceFinish(new[]{wall});
            GUILayout.Label(wall.interior?"Interior wall":"Exterior wall",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var boundary=wall.Floor.WallsOnly ? wall.boundaryOnly : EditorGUILayout.Toggle("Boundary only",wall.boundaryOnly);
            var family=Field("Family override",wall.familyOverride);
            var effectiveFamily=family ? family : wall.Floor.FamilyFor(wall.interior);
            float depth=effectiveFamily && !effectiveFamily.IsComplex && !boundary ? ThicknessField("Thickness override",wall.thicknessOverride) : wall.thicknessOverride;
            var layout = PanelLayoutField(wall, family, boundary);
            var surface=wall.Family && wall.Family.IsComplex ? FinishField("Frame override",wall.Family,wall.surfaceOverride) : wall.surfaceOverride;
            var glass=wall.glassOverride;
            if(wall.Family && wall.Family.HasGlass)glass=FinishField("Glass override",wall.Family,glass,true);
            var suppress=EditorGUILayout.Toggle("Disable trim",wall.suppressTrim);
            var trim=suppress?wall.trimOverride:Field("Trim override",wall.trimOverride);
            if(EditorGUI.EndChangeCheck())Change(wall,"Edit wall properties",()=>{
                wall.thicknessOverride=depth;wall.panelLayoutOverride=layout;wall.familyOverride=family;wall.surfaceOverride=surface;wall.glassOverride=glass;wall.boundaryOnly=boundary;
                wall.suppressTrim=suppress;wall.trimOverride=trim;},()=>{
                    if(!WallBuilder.Rebuild(wall,out var error))return error;
                    return WallJunctions.Refresh(wall.Floor,out error) ? null : error;
                });
        }
    }

    [CustomEditor(typeof(House))]
    class HouseInspector : UnityEditor.Editor
    {
        Bounds frameBounds;

        // SceneView.FrameSelected asks the active inspector for bounds at command time.
        bool HasFrameBounds() => HouseScene.House == target && Selection.count == 1 &&
            Selection.activeGameObject == HouseScene.House.gameObject && HouseScene.TryFrameSelection(out frameBounds);
        Bounds OnGetFrameBounds() => frameBounds;

        public override void OnInspectorGUI()
        {
            var house=(House)target;
            var first=HouseEditing.Floors(house).FirstOrDefault();
            if(GUILayout.Button(!first?"Add ground floor":HouseScene.House==house?HouseProperties.WindowTitle:first.EditLabel))
            {
                var floor=first?first:HouseEditing.AddFloor(house);
                if(HouseScene.House!=house)HouseScene.Enter(floor);else HouseProperties.Open();
            }
        }
    }
}
