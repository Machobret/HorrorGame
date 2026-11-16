using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal enum HouseTool { Select, Wall, Door, Window, Boolean }

    [InitializeOnLoad]
    internal static partial class HouseScene
    {
        internal static HouseFloor Floor { get; private set; }
        internal static House House => Floor ? Floor.House : null;
        internal static HouseWall Wall { get; private set; }
        internal static HouseWall PaletteWall { get; private set; }
        internal static string Opening;
        internal static HouseInsert Choice;
        internal static float Sill=1;
        internal static WallFamily DrawFamily;
        internal static bool FlipDoor;
        internal static string Message;
        internal static HouseTool Mode;
        internal static bool Cutaway;
        internal static float Grid { get => PlanSnapping.Grid; set => PlanSnapping.Grid=value; }
        internal static HouseRoom Room {get;private set;}
        static int corner;
        static readonly HouseScenePicker picker=new HouseScenePicker();
        static readonly HouseHighlight selectedHighlight=new HouseHighlight(), hoverHighlight=new HouseHighlight();
        internal static HousePick Selected {get;private set;}
        internal static HousePick Hover=>picker.hover;
        static Vector2 hoverMouse;
        const string refreshHoverCommand="HouseToolkit.RefreshHover";
        static void InvalidatePicking(bool keepPointer=false)
        {
            picker.Invalidate();
            selectedHighlight.Clear();hoverHighlight.Clear();
            foreach (var entry in extraHighlights) entry.Clear();
            EditorApplication.delayCall-=RefreshStationaryHover;
            if(keepPointer && picker.hoverView)EditorApplication.delayCall+=RefreshStationaryHover;
            else {picker.hover=default;picker.hoverView=null;}
        }
        static void RefreshStationaryHover()
        {
            // Picking renders object IDs. Run it in an input event outside the Scene repaint.
            if(picker.hoverView && Floor && Mode==HouseTool.Select)
                picker.hoverView.SendEvent(EditorGUIUtility.CommandEvent(refreshHoverCommand));
        }
        internal static readonly SceneSelection<HousePick> Picks = new SceneSelection<HousePick>(
            (a,b)=>a.OpeningId!=null ? a.wall==b.wall && a.OpeningId==b.OpeningId && (a.segmentId==null)==(b.segmentId==null) :
                a.cutter!=null ? a.cutter==b.cutter : a.cut!=null ? a.cutOwner==b.cutOwner && a.cut.id==b.cut?.id :
                a.room ? a.room==b.room && a.ceiling==b.ceiling : a.Same(b), p=>p.Kind);
        static readonly List<HouseHighlight> extraHighlights = new();
        internal static IEnumerable<HouseWall> SelectedWalls => Picks.Items.Where(p=>p.Kind=="Wall").Select(p=>p.wall);
        internal static bool MultipleWalls => Picks.Items.Count>1 && !Picks.Mixed && Selected.Kind=="Wall";

        internal static void ToggleWall(HouseWall wall,bool positiveFace=false) =>
            Pick(new HousePick{wall=wall,positiveFace=positiveFace},true);
        internal static void Select(HousePick pick) => Pick(pick,false);
        internal static void Pick(HousePick pick,bool additive)
        {
            if(pick.insert) pick.insertId=pick.insert.openingId;
            Picks.Pick(pick,additive);
            SyncPicks();
        }
        internal static void SyncPicks()
        {
            var pick=Picks.Active;
            BooleanTool.SetActive(pick.cutter);
            pick.insert=pick.ResolveInsert();
            Selected=pick;Room=pick.room;Wall=pick.wall;Opening=pick.OpeningId;corner=pick.corner;
            extraHighlights.Clear();
            for(int i=1;i<Picks.Items.Count;i++) extraHighlights.Add(new HouseHighlight());
            Repaint();
        }
        internal static void ClearArchitectureHover()
        {
            picker.hover=default;
            picker.hoverView=null;
        }

        internal static void ClearSelectionForCutter()
        {
            Select(default);
            ClearArchitectureHover();
            Selection.activeGameObject=House.gameObject;
            Tools.hidden=true;
        }

        internal static bool TryFrameSelection(out Bounds bounds)
        {
            bounds = default;
            if (!Floor) return false;
            if (DrawingOpening) return openingTool.TryFrameOutline(out bounds);
            var cutter = BooleanTool.DisplayedSelection;
            if (cutter != null && Picks.Items.Count<=1)
            {
                bounds = HouseBooleanMesh.PrimitiveBounds(House.transform.localToWorldMatrix * cutter.Matrix);
                return true;
            }
            if (Selected.cut != null && SelectedCutIndex() < 0) return false;
            if (Picks.Items.Count<=1) return HouseSelection.TryFrame(Selected, Floor, out bounds);
            bool found = false;
            foreach (var item in Picks.Items)
            {
                Bounds part;
                if(item.cutter!=null && House.booleanCuts.Find(c=>c.id==item.cutter) is BooleanCut cut)
                    part=HouseBooleanMesh.PrimitiveBounds(House.transform.localToWorldMatrix*cut.Matrix);
                else if(!HouseSelection.TryFrame(item,Floor,out part))continue;
                if (found) bounds.Encapsulate(part);
                else { bounds = part; found = true; }
            }
            return found;
        }

        internal static int SelectedCutIndex()
        {
            var pick=Selected;
            if(!pick.cutOwner || pick.cut==null)return -1;
            var cuts=pick.roofCut ? pick.cutOwner.roofOpenings : pick.cutOwner.floorOpenings;
            int index=cuts.FindIndex(c => c.id==pick.cut.id);
            if(index>=0 && pick.cut!=cuts[index])
            {
                pick.cut=cuts[index];
                Selected=pick;
            }
            return index;
        }
        internal static void EditSelectedCut()
        {
            int index=SelectedCutIndex();
            if(index>=0)ToggleOpening(Selected.ceiling,false,index);
        }
        static Vector2? chain, dragOrigin, dragPoint;
        static Vector2 pressed;
        static HousePick pressedTarget;
        static bool directDrag;
        static FloorEdit previewEdit;
        static Vector2? validatedPoint;
        static bool validDrop;
        static string dragIssue;
        static int clickControl;
        static bool altPress, navigating, oldToolsHidden;
        static int pressedClicks;
        static bool editing;
        static SceneView dragView;
        static readonly List<GameObject> hidden=new List<GameObject>();
        static readonly List<Renderer> houseRenderers=new List<Renderer>();
        static readonly List<EditorSelectedRenderState> renderStates=new List<EditorSelectedRenderState>();
        static bool refreshRenderStates=true, nativeOutlineSuppressed;

        static void BeforeSceneDraw(SceneView view)
        {
            if(!editing || !Floor || Event.current.type!=EventType.Repaint || Selection.activeGameObject!=House.gameObject)return;
            if(refreshRenderStates)
            {
                House.GetComponentsInChildren(true,houseRenderers);
                renderStates.Clear();
                foreach(var renderer in houseRenderers)
                {
                    using var serialized=new SerializedObject(renderer);
                    var state=serialized.FindProperty("m_SelectedEditorRenderState");
                    renderStates.Add(state!=null?(EditorSelectedRenderState)state.intValue:EditorSelectedRenderState.Hidden);
                }
                refreshRenderStates=false;
            }
            for(int i=0;i<houseRenderers.Count;i++)
                if(houseRenderers[i] && renderStates[i]!=EditorSelectedRenderState.Hidden)
                    EditorUtility.SetSelectedRenderState(houseRenderers[i],EditorSelectedRenderState.Hidden);
            nativeOutlineSuppressed=true;
        }

        static void RestoreNativeOutline()
        {
            if(!nativeOutlineSuppressed)return;
            for(int i=0;i<houseRenderers.Count;i++)
                if(houseRenderers[i] && renderStates[i]!=EditorSelectedRenderState.Hidden)
                    EditorUtility.SetSelectedRenderState(houseRenderers[i],renderStates[i]);
            nativeOutlineSuppressed=false;
        }
        static readonly List<(Vector2 start,Vector2 end)> chainSteps=new List<(Vector2,Vector2)>();
        static int chainCursor;
        static string lastUndo;
        static readonly Dictionary<int,string> recentFloors=new Dictionary<int,string>();
        static void Remember()
        {
            if(!Floor || !House)return;
            recentFloors[House.GetInstanceID()]=Floor.floorId;
            if(!string.IsNullOrEmpty(House.gameObject.scene.path))EditorPrefs.SetString(Key(House),Floor.floorId);
        }

        static HouseScene()
        {
            SceneView.beforeSceneGui+=BeforeSceneDraw;
            SceneView.duringSceneGui+=OnScene;
            Selection.selectionChanged += OnNativeSelection;
            Undo.undoRedoEvent+=OnUndo;
            AssemblyReloadEvents.beforeAssemblyReload+=Leave;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Leave();};
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing+=(scene,removing)=>{if(Floor && Floor.gameObject.scene==scene)Leave();};
            EditorApplication.hierarchyChanged+=()=>{refreshRenderStates=true;InvalidatePicking(keepPointer:true);if(Cutaway && Floor)ApplyCutaway();};
        }
        static void OnNativeSelection()
        {
            if (Floor)
            {
                if (Mode == HouseTool.Select && !DrawingOpening && Selection.activeGameObject != House.gameObject)
                {
                    Select(default);
                    Tools.hidden = oldToolsHidden;
                }
                return;
            }
            if (!(EditorWindow.focusedWindow is SceneView)) return;
            EditorApplication.delayCall -= SelectHouseRoots;
            EditorApplication.delayCall += SelectHouseRoots;
        }

        static void SelectHouseRoots()
        {
            if (Floor || (EditorWindow.focusedWindow && !(EditorWindow.focusedWindow is SceneView)) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var selected = Selection.objects;
            var roots = selected.Select(item =>
            {
                if (!(item is GameObject go) || EditorUtility.IsPersistent(go)) return item;
                // Only generated architecture belongs to the house selection. Prefab inserts
                // and hand-placed room contents retain their own editable selection.
                if (go.GetComponentInParent<InsertAttachment>() is InsertAttachment attachment)
                {
                    var root = go.transform;
                    while (root != attachment.transform && root.parent != attachment.transform) root = root.parent;
                    return root.gameObject;
                }
                if (!(go.GetComponent<HouseWall>() || go.GetComponent<HouseFloor>() || go.GetComponent<HouseRoom>() ||
                    go.GetComponent<BooleanCutResult>() || go.GetComponent<HouseSlab>() || go.GetComponent<CornerJoint>() || go.GetComponentInParent<WallOutput>())) return item;
                var house = go.GetComponentInParent<House>();
                return house ? house.gameObject : item;
            }).Distinct().ToArray();
            if (!selected.SequenceEqual(roots)) Selection.objects = roots;
        }

        internal static void Repaint(){SceneView.RepaintAll();HouseProperties.RepaintOpen();}
        internal static void Enter(HouseFloor requested)
        {
            if(!requested)return;
            if(RoadScene.Active)RoadScene.Leave();
            if(editing)Leave();
            var house=requested.House;
            if(!house || string.IsNullOrEmpty(requested.floorId)) house=HouseEditing.Adopt(requested,out Message);
            if(!house){Repaint();return;}
            if(!recentFloors.TryGetValue(house.GetInstanceID(),out var saved))saved=EditorPrefs.GetString(Key(house));
            var selected=HouseEditing.Floors(house).FirstOrDefault(f=>f.floorId==saved)??requested;
            if (!WallJunctions.Refresh(selected,out Message)){Repaint();return;}
            editingHouse=house; Floor=selected; Cutaway=true; SlabBuilder.Rebuild(house);
            focusedPlane=Floor.transform.position;
            refreshRenderStates=true;oldToolsHidden=Tools.hidden;Tools.hidden=true;editing=true;
            Selection.activeGameObject=house.gameObject;
            Mode=HouseTool.Select;ResetSelection();if(!Application.isBatchMode)HouseProperties.Open();Repaint();
        }
        internal static string Key(House h)=>"HouseToolkit.Floor."+Application.dataPath+"."+GlobalObjectId.GetGlobalObjectIdSlow(h);
        internal static void SwitchFloor(HouseFloor f)
        {
            if(!f)return;
            if(!WallJunctions.Refresh(f,out Message)){Repaint();return;}
            ResetSelection();Selection.activeGameObject=f.House.gameObject;Tools.hidden=true;Floor=f;Select(new HousePick { storey=f });FocusFloor();Remember();ApplyCutaway();HouseProperties.ShowFloor();Repaint();
        }
        static House editingHouse;
        static Vector3 focusedPlane;
        static void FocusFloor()
        {
            focusedPlane=Floor.transform.position;
            var view=SceneView.lastActiveSceneView;
            if(!view)return;
            var pivot=Floor.World(Floor.Local(view.pivot));
            view.LookAtDirect(pivot,view.rotation,view.size);
            view.Repaint();
        }
        internal static void RefocusIfMoved()
        {
            if(Floor && Mathf.Abs(Vector3.Dot(Floor.transform.position-focusedPlane,Floor.transform.up))>.00001f)FocusFloor();
        }
        internal static void Leave()
        {
            RestoreNativeOutline();houseRenderers.Clear();renderStates.Clear();refreshRenderStates=true;
            Remember();
            if(editing)Tools.hidden=oldToolsHidden;
            editing=false;editingHouse=null;
            DrawFamily=null; FlipDoor=false;
            RestoreVisibility(); Cutaway=false;Floor=null;ResetSelection();Repaint();
        }
        static void RestoreVisibility()
        {
            foreach(var go in hidden)if(go)SceneVisibilityManager.instance.Show(go,false);
            hidden.Clear();
        }
        internal static void ApplyCutaway()
        {
            if(!Floor || !Cutaway || Floor.WallsOnly){RestoreVisibility();return;}
            var above=new HashSet<GameObject>(HouseEditing.Floors(House).Where(f=>f.transform.localPosition.y>Floor.transform.localPosition.y)
                .SelectMany(f=>f.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject));
            foreach(var slab in Floor.GetComponentsInChildren<HouseSlab>(true))
                if (slab.overhead)
                    foreach (var child in slab.GetComponentsInChildren<Transform>(true)) above.Add(child.gameObject);
            var manager=SceneVisibilityManager.instance;
            foreach(var go in hidden.ToArray())if(!go || !above.Contains(go)){if(go)manager.Show(go,false);hidden.Remove(go);}
            foreach(var go in above)if(!manager.IsHidden(go)){manager.Hide(go,false);hidden.Add(go);}
        }
        static readonly SlabOpeningTool openingTool=new SlabOpeningTool();
        internal static bool DrawingOpening=>openingTool.Active;
        internal static SlabOpeningTool OpeningTool => openingTool;
        internal static void ToggleOpening(bool ceiling=false,bool rectangle=false,int selected=-1)
        {
            if (!Floor || Floor.WallsOnly) return;
            ResetSelection();Mode=HouseTool.Select;
            openingTool.Begin(Floor,ceiling,rectangle,selected);Repaint();
        }
        internal static void RefreshOpeningTarget(){if(DrawingOpening)openingTool.RefreshTarget();}
        internal static void EndOpening(){openingTool.Cancel();Message=null;Repaint();}
        static void ResetSelection(){BooleanTool.Reset();Select(default);InvalidatePicking();openingTool.Cancel();PaletteWall=null;Choice=null;Message=null;EndChain();CancelDrag();}
        static readonly DrawingGuides wallGuides=new DrawingGuides();
        static readonly PointPlacement wallPress=new PointPlacement();
        static void EndChain(){wallGuides.Clear();wallPress.Cancel();chain=null;chainSteps.Clear();chainCursor=0;lastUndo=null;}
        static void CancelDrag()
        {
            if(directDrag && GUIUtility.hotControl==clickControl)GUIUtility.hotControl=0;
            if(directDrag)clickControl=0;
            directDrag=false;pressedTarget=default;previewEdit=null;validatedPoint=null;dragIssue=null;
            dragOrigin=dragPoint=null;dragView=null;
        }
        internal static void SelectWall(HouseWall w){Select(new HousePick{wall=w});Mode=HouseTool.Select;Repaint();}
        internal static void SelectDoorOrWindow()
        {
            if(!Wall || Opening==null)return;
            var attachment=Array.Find(Wall.GetComponentsInChildren<InsertAttachment>(),a=>a.openingId==Opening);
            if(attachment)Select(new HousePick{wall=Wall,insert=attachment});
            Repaint();
        }
        internal static void SelectSurround()
        {
            if(!Wall || Opening==null)return;
            Select(new HousePick{wall=Wall,segmentId=Opening});Repaint();
        }
        static void OnUndo(in UndoRedoInfo info)
        {
            if(!Floor)
            {
                if(!editing)return;
                var survivor=editingHouse?HouseEditing.Floors(editingHouse)
                    .OrderBy(f=>(f.transform.position-focusedPlane).sqrMagnitude).FirstOrDefault():null;
                if(survivor)SwitchFloor(survivor);else Leave();
                return;
            }
            CancelDrag();
            BooleanTool.CancelDrag();
            Picks.RemoveWhere(p=>p.Kind==null || p.cutter!=null && !House.booleanCuts.Any(c=>c.id==p.cutter) ||
                p.corner!=0 && !Floor.corners.Any(c=>c.id==p.corner) ||
                p.cut!=null && (!p.cutOwner || !(p.roofCut?p.cutOwner.roofOpenings:p.cutOwner.floorOpenings).Any(c=>c.id==p.cut.id)));
            SyncPicks();
            if(Mode==HouseTool.Wall && info.undoName==lastUndo && chainSteps.Count>0)
            {
                chainCursor=Mathf.Clamp(chainCursor+(info.isRedo?1:-1),0,chainSteps.Count);
                chain=chainCursor==0?chainSteps[0].start:chainSteps[chainCursor-1].end;
            }
            else if(chainSteps.Count>0 && info.undoName!=lastUndo)EndChain();
            if(corner!=0 && !Floor.corners.Any(c=>c.id==corner) || Selected.cut!=null && SelectedCutIndex()<0)Select(default);
            else if(Opening!=null && (!Wall || !Wall.openings.Any(o=>o.id==Opening)))Select(new HousePick{wall=Wall});
            if(DrawingOpening)openingTool.OnUndo();
            InvalidatePicking();
            RefocusIfMoved();ApplyCutaway();Repaint();
        }
        internal static HouseFloor SelectedFloor()
        {
            var go=Selection.activeGameObject;
            if(!go || go.GetComponentInParent<InsertAttachment>())return null;
            return go.GetComponentInParent<HouseFloor>() ?? go.GetComponentInChildren<HouseFloor>();
        }
        [MenuItem("GameObject/House Toolkit/Create House",false,10)]
        internal static void CreateHouse()
        {
            var pivot=SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            CreateAt(new Vector3(pivot.x,0,pivot.z),false);
        }
        internal static void CreateAt(Vector3 position,bool startChain,bool wallsOnly = false)
        {
            var defaultFamily=AssetDatabase.FindAssets("t:WallFamily").Select(g=>AssetDatabase.LoadAssetAtPath<WallFamily>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(f=>f && !f.IsComplex);
            if(!defaultFamily)
            {
                defaultFamily=ScriptableObject.CreateInstance<WallFamily>();defaultFamily.surface=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.defaultMaterial;
                AssetDatabase.CreateAsset(defaultFamily,AssetDatabase.GenerateUniqueAssetPath("Assets/Plain Wall.asset"));
            }
            var house=HouseEditing.Create(position,defaultFamily,wallsOnly);
            Enter(HouseEditing.Floors(house)[0]);Mode=HouseTool.Wall;
            DrawFamily=null;
            if(startChain)chain=Vector2.zero;
        }
        internal static void SetMode(HouseTool mode)
        {
            if (!Floor || mode == Mode && !DrawingOpening) return;
            ResetSelection();
            Selection.activeGameObject = House.gameObject;
            Tools.hidden = true;
            Mode = mode;
            if (mode == HouseTool.Boolean) HouseProperties.Open();
            HouseProperties.ShowTool();
            Repaint();
        }

        static HouseWall PickSurface(Ray ray,out Vector2 position,out string opening)
        {
            position=default;opening=null;HouseWall nearest=null;float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(ray,Mathf.Infinity,~0,QueryTriggerInteraction.Ignore))
            {
                var w=hit.collider.GetComponentInParent<HouseWall>();
                if(!w || w.Floor!=Floor || hit.distance>=distance || SceneVisibilityManager.instance.IsHidden(w.gameObject))continue;
                nearest=w;distance=hit.distance;position=Floor.Local(hit.point);
                opening=hit.collider.GetComponentInParent<InsertAttachment>()?.openingId;
            }
            return nearest;
        }
        static void OnScene(SceneView view)
        {
            RestoreNativeOutline();
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!Floor)
            {
                if(editing)Leave();
                if (Event.current.rawType == EventType.MouseUp && Event.current.button == 0)
                {
                    EditorApplication.delayCall -= SelectHouseRoots;
                    EditorApplication.delayCall += SelectHouseRoots;
                }
                return;
            }
            var e=Event.current;
            var rawType=e.rawType;
            if(DrawingOpening){openingTool.Handle(view,e);return;}
            if(Mode==HouseTool.Wall && wallPress.Track(view,e,PlaceWallPoint))return;
            if(rawType==EventType.MouseDown && e.button==1){navigating=true;CancelDrag();}
            if(rawType==EventType.MouseUp && e.button==1)navigating=false;
            if(e.type==EventType.MouseLeaveWindow && GUIUtility.hotControl==0){navigating=false;CancelDrag();}
            if((e.type==EventType.Ignore || e.type==EventType.MouseLeaveWindow) && dragView==view){CancelDrag();clickControl=0;}
            if(picker.hoverView==view && (navigating || rawType==EventType.MouseLeaveWindow || e.alt && Mode==HouseTool.Select))
            {
                picker.hover=default;picker.hoverView=null;view.Repaint();
            }
            if(navigating || e.button==1 && e.isMouse || e.type==EventType.ScrollWheel || Tools.current==Tool.View)return;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape){BooleanTool.CancelDrag();openingTool.Cancel();EndChain();CancelDrag();Message=null;e.Use();Repaint();return;}
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout && !e.alt && !Tools.viewToolActive)HandleUtility.AddDefaultControl(control);
            Ray ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Mode == HouseTool.Boolean || Mode == HouseTool.Select && BooleanTool.SelectBooleans)
            {
                BooleanTool.Scene(view, e, ray, control);
                if (Mode == HouseTool.Boolean || e.type == EventType.Used) return;
            }
            bool editingArchitecture = Mode==HouseTool.Select && Selection.activeGameObject==House.gameObject;
            if(editingArchitecture && e.type==EventType.ValidateCommand && (e.commandName=="Delete" || e.commandName=="SoftDelete"))
            {e.Use();return;}
            if(editingArchitecture && (e.type==EventType.KeyDown && (e.keyCode==KeyCode.Delete || e.keyCode==KeyCode.Backspace) ||
                e.type==EventType.ExecuteCommand && (e.commandName=="Delete" || e.commandName=="SoftDelete")))
            {DeleteSelection();e.Use();return;}
            if(Mode==HouseTool.Select){HandleSelection(view,e,ray,control);return;}
            if(!new Plane(Floor.transform.up,Floor.transform.position).Raycast(ray,out var depth))return;
            var raw=Floor.Local(ray.GetPoint(depth));
            var walls=Floor.GetComponentsInChildren<HouseWall>(true);
            foreach(var w in walls)
            {
                Handles.color=w==Wall?Color.yellow:new Color(.45f,.85f,1,.85f);
                Handles.DrawAAPolyLine(3,Floor.World(Floor.Position(w.startCorner)),Floor.World(Floor.Position(w.endCorner)));
            }
            var near=NearestWall(walls,raw,out var projected,out float wallPixels);
            bool constrained=Mode==HouseTool.Wall && chain.HasValue && e.shift && !e.alt;
            var point=Snap(raw,e.alt,constrained?chain:null,walls,0,Mode==HouseTool.Wall ? chain : null);
            if (Mode == HouseTool.Wall)
            {
                CursorGrid.Draw(view, Floor, point);
                if(chain.HasValue)wallGuides.Draw(point,Floor.World);
            }
            bool drawValid=true;string drawIssue=null;
            if(Mode==HouseTool.Wall && chain.HasValue && Vector2.Distance(chain.Value,point)>.001f)
            {
                var draft=new FloorEdit(Floor);draft.Draw(chain.Value,point,DrawFamily);
                drawValid=draft.Prepare(out drawIssue);
            }
            bool placing=Mode==HouseTool.Door||Mode==HouseTool.Window;
            if(placing)
            {
                var surface=PickSurface(ray,out var surfacePoint,out _);
                if(surface){near=surface;DistanceTo(near,surfacePoint,out projected);wallPixels=0;}
            }
            HouseInsert insert=null;float distance=0;float sill=0;
            if(placing && near && wallPixels<28 && !near.boundaryOnly)
            {
                if(PaletteWall!=near){PaletteWall=near;HouseProperties.RepaintOpen();}
                insert=PaletteChoice();
                distance=Vector2.Distance(Floor.Position(near.startCorner),projected);
                distance=SnapInsertDistance(near,insert,distance,null,e.alt);
                CursorGrid.Draw(view, Floor, Floor.Local(near.transform.TransformPoint(Vector3.right * distance)));
                sill=Mode==HouseTool.Window?Sill:0;
                if(insert)
                {
                    var o=new PlacedOpening{distance=distance,sill=sill,insert=insert,clearSize=GarageDoorFields.BrushSize,finishTile=GarageDoorFields.BrushTile,reversed=Mode==HouseTool.Door && FlipDoor};
                    var fits=new List<FittedOpening>();
                    bool valid=near.TryLayout(Floor.height,fits,out var issue,o) &&
                        WallJunctions.CheckOpening(near,new List<PlacedOpening>(near.openings){o},out issue);
                    var surround=near.Family.panel?near.Family.FindSurround(insert):null;
                    var fit=new FittedOpening(o,surround);
                    var center=near.transform.TransformPoint(new Vector3(distance,fit.sill+o.Size.y*.5f,0));
                    using(new Handles.DrawingScope(valid?Color.green:Color.red,Matrix4x4.TRS(center,near.transform.rotation,Vector3.one)))
                    {
                        Handles.DrawWireCube(Vector3.zero,new Vector3(o.Size.x,o.Size.y,near.Thickness));
                        if (Mode == HouseTool.Door && e.type == EventType.Repaint)
                        {
                            var facing = o.reversed ? Vector3.back : Vector3.forward;
                            Handles.ArrowHandleCap(0, facing * near.Thickness * .5f,
                                Quaternion.LookRotation(facing), Mathf.Min(o.Size.x * .5f, .5f), EventType.Repaint);
                        }
                    }
                    if(valid && insert is GarageDoorInsert garage && e.type==EventType.Repaint)
                        GarageDoorFields.Preview(near,o,garage);
                    if(!valid)Handles.Label(center,issue);
                }
            }
            if(Mode==HouseTool.Wall)
            {
                Handles.color=drawValid?Color.green:Color.red;var world=Floor.World(point);
                if(!drawValid)Handles.Label(world+Floor.transform.up*.15f,drawIssue);
                Handles.DotHandleCap(0,world,Quaternion.identity,HandleUtility.GetHandleSize(world)*.08f,EventType.Repaint);
                if(chain.HasValue){Handles.DrawAAPolyLine(3,Floor.World(chain.Value),world);Handles.Label(world,Vector2.Distance(chain.Value,point).ToString("0.##")+" m");}
            }
            if(Mode==HouseTool.Wall)
            {
                if(rawType==EventType.MouseDown && e.button==0)
                    wallPress.Press(view,e,control,point,walls.Any(w=>DistanceTo(w,point,out _) < .0002f),PlaceWallPoint);
                if(e.type==EventType.MouseMove || rawType==EventType.MouseDrag)view.Repaint();
                return;
            }
            // Alt-drag belongs to orbit. Observe the click without capturing Unity's navigation control.
            if(rawType==EventType.MouseDown && e.button==0)
            {
                pressed=e.mousePosition;pressedClicks=e.clickCount;altPress=e.alt;clickControl=control;
                if(!e.alt && e.type!=EventType.Used && GUIUtility.hotControl==0 && HandleUtility.nearestControl==control)
                {GUIUtility.hotControl=control;e.Use();}
            }
            if(rawType==EventType.MouseUp && e.button==0 && clickControl==control)
            {
                bool owned=GUIUtility.hotControl==control;
                if(owned)GUIUtility.hotControl=0;
                clickControl=0;
                if((owned || altPress) && Vector2.Distance(pressed,e.mousePosition)<4)
                {
                    if(placing && near && wallPixels<28 && insert &&
                        WallBuilder.Place(near,insert,distance,sill,out Message,Mode==HouseTool.Door && FlipDoor,
                            insert is GarageDoorInsert ? GarageDoorFields.BrushSize : default,insert is GarageDoorInsert ? GarageDoorFields.BrushTile : 0)
                        && insert is GarageDoorInsert) GarageDoorFields.Advance();
                    if(owned && e.type!=EventType.Used)e.Use();Repaint();
                }
            }
            if(e.type==EventType.MouseMove)view.Repaint();
        }

        static void PlaceWallPoint(Vector2 point,bool endOnWall)
        {
            wallGuides.Clear();
            if(!chain.HasValue){chain=point;chainSteps.Clear();chainCursor=0;Message=null;}
            else if((chain.Value-point).sqrMagnitude<1e-6f)EndChain();
            else
            {
                var edit=new FloorEdit(Floor);
                edit.Draw(chain.Value,point,DrawFamily);
                const string label="Draw house wall";
                if(edit.Commit(label,out Message))
                {
                    if(chainCursor<chainSteps.Count)chainSteps.RemoveRange(chainCursor,chainSteps.Count-chainCursor);
                    chainSteps.Add((chain.Value,point));chainCursor++;lastUndo=label;
                    chain=endOnWall?(Vector2?)null:point;
                }
            }
            Repaint();
        }

        static void HandleSelection(SceneView view,Event e,Ray ray,int control)
        {
            if(e.type==EventType.Repaint)
            {
                if(Selected.cut!=null && SelectedCutIndex()<0)Select(default);
                picker.DrawGuides(Floor);
                for (int i=0;i<extraHighlights.Count;i++)
                    extraHighlights[i].Draw(Picks.Items[i],Floor,new Color(1f,.67f,.12f));
                selectedHighlight.Draw(Selected,Floor,new Color(1f,.67f,.12f));
                if(picker.hoverView==view && !picker.hover.Same(Selected))
                    hoverHighlight.Draw(picker.hover,Floor,new Color(.24f,.85f,1f));
            }
            bool showHandle=!e.shift || GUIUtility.hotControl!=0;
            if (showHandle && (dragView==view || !e.alt && !Tools.viewToolActive) && Picks.Items.Count>1 && BatchHandle(view,e))return;
            if ((Picks.Items.Count<=1 && (HandleDirectDrag(view,e,ray,control) || showHandle && Gizmo(view,e))) || e.alt || Tools.viewToolActive) return;
            // A selected short wall's move handle can cover one of its own endpoints.
            // Give the visible endpoint the click before the handle starts a wall drag.
            if (e.type == EventType.Layout && Wall && GUIUtility.hotControl == 0 && picker.hoverView == view)
            {
                int hovered = picker.hover.corner;
                if (hovered != 0 && (hovered == Wall.startCorner || hovered == Wall.endCorner) &&
                    Vector2.Distance(HandleUtility.WorldToGUIPoint(Floor.World(Floor.Position(hovered))), e.mousePosition) < 8f)
                    HandleUtility.AddControl(control, 0f);
            }
            bool repick=e.type==EventType.ExecuteCommand && e.commandName==refreshHoverCommand && picker.hoverView==view;
            if((e.type==EventType.MouseMove || repick) && GUIUtility.hotControl==0)
            {
                if(!repick)hoverMouse=e.mousePosition;
                var hit=picker.Pick(Floor,hoverMouse,repick?HandleUtility.GUIPointToWorldRay(hoverMouse):ray);
                if(repick){e.Use();view.Repaint();}
                if(picker.hoverView!=view || !picker.hover.Same(hit))
                {
                    picker.hoverView=view;picker.hover=hit;view.Repaint();
                }
            }
            if(e.type==EventType.MouseDown && e.button==0 && GUIUtility.hotControl==0 && HandleUtility.nearestControl==control)
            {
                pressedTarget=picker.Pick(Floor,e.mousePosition,ray);
                GUIUtility.hotControl=clickControl=control;pressed=e.mousePosition;pressedClicks=e.clickCount;e.Use();
            }
            if(e.rawType!=EventType.MouseUp || e.button!=0 || clickControl!=control || GUIUtility.hotControl!=control)return;
            GUIUtility.hotControl=clickControl=0;pressedTarget=default;
            if(Vector2.Distance(pressed,e.mousePosition)<4)
            {
                var hit=picker.Pick(Floor,e.mousePosition,ray);
                if (hit.prop && !e.shift)
                {
                    Select(default);
                    Selection.activeGameObject = hit.prop;
                    Tools.hidden = oldToolsHidden;
                    picker.hover=default; picker.hoverView=null; Message=null;
                    Repaint(); e.Use(); return;
                }
                Selection.activeGameObject = House.gameObject;
                Tools.hidden = true;
                Pick(hit,e.shift);
                picker.hover=hit;picker.hoverView=view;hoverMouse=e.mousePosition;Message=null;
                if(!e.shift && pressedClicks>=2 && hit.cut!=null)EditSelectedCut();
                else if(!e.shift && pressedClicks>=2 && hit.wall && !hit.insert && hit.segmentId==null)
                {
                    var edit=new FloorEdit(Floor);edit.Split(hit.wall,hit.point);edit.Commit("Insert wall corner",out Message);
                }
                if(!DrawingOpening)HouseProperties.ShowSelection();
                Repaint();
            }
            e.Use();
        }

        static readonly List<HouseInsert> hoverInserts = new List<HouseInsert>();
        internal static HouseInsert PaletteChoice()
        {
            if(!PaletteWall)return null;
            OpeningPalette.Fill(PaletteWall,Mode==HouseTool.Door?OpeningKind.Door:OpeningKind.Window,hoverInserts);
            if(!hoverInserts.Contains(Choice))Choice=hoverInserts.Count>0 ? hoverInserts[0] : null;
            return Choice;
        }
        static float ScreenDistance(Vector2 a,Vector2 b)=>Vector2.Distance(HandleUtility.WorldToGUIPoint(Floor.World(a)),HandleUtility.WorldToGUIPoint(Floor.World(b)));
        static float DistanceTo(HouseWall w,Vector2 p,out Vector2 foot)
        {
            var a=Floor.Position(w.startCorner);var v=Floor.Position(w.endCorner)-a;
            foot=a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(1e-10f,v.sqrMagnitude));return Vector2.Distance(p,foot);
        }
        static HouseWall NearestWall(HouseWall[] walls,Vector2 p,out Vector2 at,out float pixels)
        {
            HouseWall best=null;at=p;pixels=float.PositiveInfinity;
            foreach(var wall in walls){DistanceTo(wall,p,out var q);float d=ScreenDistance(p,q);if(d<pixels){pixels=d;at=q;best=wall;}}
            return best;
        }
        static Vector2 Snap(Vector2 p,bool free,Vector2? axis,HouseWall[] walls,int ignore,Vector2? from=null) =>
            PlanSnapping.Snap(p,free,axis,Grid,Floor.corners.Where(n=>n.id!=ignore).Select(n=>n.point),
                walls.Where(w=>ignore==0 || w.startCorner!=ignore && w.endCorner!=ignore)
                    .Select(w=>(Floor.Position(w.startCorner),Floor.Position(w.endCorner))),ScreenDistance,from.HasValue ? wallGuides : null,from);

        static bool HandleDirectDrag(SceneView view,Event e,Ray ray,int control)
        {
            if(clickControl!=control || GUIUtility.hotControl!=control || dragView && dragView!=view)return false;
            bool isCorner=pressedTarget.corner!=0;
            if(!isCorner && !pressedTarget.insert && pressedTarget.segmentId==null)return false;
            if(!directDrag)
            {
                if(e.rawType!=EventType.MouseDrag || (e.mousePosition-pressed).sqrMagnitude<16)return false;
                Select(pressedTarget);directDrag=true;dragView=view;
                dragOrigin=isCorner?Floor.Position(corner):
                    Floor.Local(Wall.transform.TransformPoint(Vector3.right*Wall.openings.Find(o=>o.id==Opening).distance));
                dragPoint=dragOrigin;HouseProperties.ShowSelection();
            }
            var placement=isCorner?null:Wall.openings.Find(o=>o.id==Opening);
            bool release=e.rawType==EventType.MouseUp && e.button==0;
            if(e.rawType==EventType.MouseDrag || release)
            {
                if(isCorner)
                {
                    var startRay=HandleUtility.GUIPointToWorldRay(pressed);
                    var plane=new Plane(Floor.transform.up,Floor.World(dragOrigin.Value));
                    if(Mathf.Abs(Vector3.Dot(plane.normal,startRay.direction))<.05f)
                        plane=new Plane(view.camera.transform.forward,Floor.World(dragOrigin.Value));
                    if(plane.Raycast(startRay,out var start) && plane.Raycast(ray,out var end))
                        dragPoint=Snap(dragOrigin.Value+Floor.Local(ray.GetPoint(end))-Floor.Local(startRay.GetPoint(start)),
                            e.alt,e.shift ? dragOrigin : null,Floor.GetComponentsInChildren<HouseWall>(),corner);
                }
                else
                {
                    var anchor=Wall.transform.TransformPoint(new Vector3(placement.distance,OpeningSill(placement)+placement.Size.y*.5f,0));
                    var distance=placement.distance+HandleUtility.CalcLineTranslation(pressed,e.mousePosition,anchor,Wall.transform.right)/Wall.transform.TransformVector(Vector3.right).magnitude;
                    distance=SnapInsertDistance(Wall,placement.insert,distance,placement,e.alt);
                    dragPoint=Floor.Local(Wall.transform.TransformPoint(Vector3.right*distance));
                }
            }
            PreviewDrag(view,placement,release);
            if(e.isMouse && e.type!=EventType.Used)e.Use();
            return true;
        }

        internal static float SnapInsertDistance(HouseWall wall,HouseInsert insert,float distance,PlacedOpening moving,bool free)
        {
            if(free)return distance;
            float grid=Grid>0?Mathf.Round(distance/Grid)*Grid:distance;
            if(!insert || insert.kind!=OpeningKind.Window || wall.openings.Count==0)return grid;
            var family=wall.Family;
            var surround=family && family.panel?family.FindSurround(insert):null;
            float leftOffset=surround?surround.aperture.center.x:insert.opening.x*.5f;
            float rightOffset=(surround?surround.size.x:insert.opening.x)-leftOffset;
            float best=grid, radius=.15f;
            void Try(float candidate)
            {
                float delta=Mathf.Abs(candidate-distance);
                if(delta>radius || candidate-leftOffset<-.0005f || candidate+rightOffset>wall.length+.0005f)return;
                best=candidate;radius=delta;
            }
            foreach(var other in wall.openings)
            {
                if(other==moving || !other.insert || other.insert.kind!=OpeningKind.Window)continue;
                var fit=new FittedOpening(other,family && family.panel?family.FindSurround(other.insert):null);
                Try(fit.left-rightOffset);
                Try(fit.Right+leftOffset);
            }
            return best;
        }

        static float OpeningSill(PlacedOpening opening)
        {
            var family=Wall.Family;
            var surround=family && family.panel?family.FindSurround(opening.insert):null;
            return surround?surround.aperture.y:opening.sill;
        }

        static bool BatchHandle(SceneView view, Event e)
        {
            if(Picks.Mixed || dragView && dragView!=view)return false;
            bool released=e.rawType==EventType.MouseUp && e.button==0;
            bool openings=Selected.OpeningId!=null;
            if(!openings && !MultipleWalls && Selected.corner==0)return false;
            var items=Picks.Items;
            var ids=openings ? Array.Empty<int>() : items.SelectMany(p=>p.corner!=0 ? new[]{p.corner} :
                new[]{p.wall.startCorner,p.wall.endCorner}).Distinct().ToArray();
            Vector3 OpeningCenter(HousePick p,float offset)
            {
                var o=p.wall.openings.Find(x=>x.id==p.OpeningId);
                return p.wall.transform.TransformPoint(new Vector3(o.distance+offset,o.sill+o.Size.y*.5f,0));
            }
            var pivot=openings ? items.Aggregate(Vector3.zero,(v,p)=>v+OpeningCenter(p,0))/items.Count :
                Floor.World(ids.Aggregate(Vector2.zero,(v,id)=>v+Floor.Position(id))/ids.Length);
            var origin=Floor.Local(pivot);
            var delta=(dragPoint??origin)-origin;
            var direction=openings ? (Floor.Position(Wall.endCorner)-Floor.Position(Wall.startCorner)).normalized : Vector2.zero;
            var shown=pivot+Floor.World(delta)-Floor.World(Vector2.zero);
            EditorGUI.BeginChangeCheck();
            var moved=openings ? Handles.Slider(shown,Wall.transform.right,HandleUtility.GetHandleSize(shown)*.7f,Handles.ArrowHandleCap,0) :
                Handles.PositionHandle(shown,Floor.transform.rotation);
            if(EditorGUI.EndChangeCheck())
            {
                delta=Floor.Local(moved)-origin;
                if(openings)
                {
                    float distance=Vector2.Dot(delta,direction);
                    if(!e.alt && Grid>0)distance=Mathf.Round(distance/Grid)*Grid;
                    delta=direction*distance;
                }
                else delta=PlanSnapping.FloorPoint(delta,Vector2.zero,e.shift,e.alt,Grid);
                dragPoint=origin+delta;dragView=view;
            }
            if(!dragPoint.HasValue)return false;
            float offset=Vector2.Dot(delta,direction);
            if(e.type==EventType.Repaint)
            {
                using(new Handles.DrawingScope(Color.yellow))
                {
                    if(openings)
                        foreach(var p in items)
                        {
                            var o=p.wall.openings.Find(x=>x.id==p.OpeningId);
                            using(new Handles.DrawingScope(p.wall.transform.localToWorldMatrix))
                                Handles.DrawWireCube(p.wall.transform.InverseTransformPoint(OpeningCenter(p,offset)),
                                    new Vector3(o.Size.x,o.Size.y,p.wall.Thickness));
                        }
                    else foreach(var w in Floor.GetComponentsInChildren<HouseWall>())
                        if(ids.Contains(w.startCorner)||ids.Contains(w.endCorner))
                            Handles.DrawLine(Floor.World(Floor.Position(w.startCorner)+(ids.Contains(w.startCorner)?delta:Vector2.zero)),
                                Floor.World(Floor.Position(w.endCorner)+(ids.Contains(w.endCorner)?delta:Vector2.zero)));
                }
            }
            if(released)
            {
                if(delta.sqrMagnitude>1e-9f)
                {
                    if(openings)HouseProperties.EditOpenings(items.ToArray(),o=>o.distance+=offset);
                    else {var edit=new FloorEdit(Floor);edit.Move(ids,delta);edit.Commit("Move selection",out Message);}
                }
                CancelDrag();Repaint();
            }
            return true;
        }

        static bool Gizmo(SceneView view,Event e)
        {
            if(corner==0 && !Wall)return false;
            if(dragView && dragView!=view)return false;
            var placement=Wall && Opening!=null?Wall.openings.Find(o=>o.id==Opening):null;
            Vector2 center=corner!=0?Floor.Position(corner):placement!=null?
                Floor.Local(Wall.transform.TransformPoint(Vector3.right*placement.distance)):
                (Floor.Position(Wall.startCorner)+Floor.Position(Wall.endCorner))*.5f;
            var prior=dragPoint??center;
            var world=Floor.World(prior);
            if(placement!=null)
                world+=Wall.transform.TransformVector(Vector3.up*(OpeningSill(placement)+placement.Size.y*.5f));
            var rot=Floor.transform.rotation;
            float size=HandleUtility.GetHandleSize(world)*.72f;
            bool release=e.rawType==EventType.MouseUp && e.button==0;
            EditorGUI.BeginChangeCheck();
            var updated=placement==null ? PlanarMoveHandle.Draw(world,rot) :
                Handles.Slider(world,Wall.transform.right,size,Handles.ArrowHandleCap,0);
            if(EditorGUI.EndChangeCheck())
            {
                dragOrigin??=center;dragView=view;
                var p=Floor.Local(updated);
                if(placement!=null)
                {
                    DistanceTo(Wall,p,out p);
                    var start=Floor.Position(Wall.startCorner);
                    var direction=(Floor.Position(Wall.endCorner)-start).normalized;
                    p=start+direction*SnapInsertDistance(Wall,placement.insert,Vector2.Dot(p-start,direction),placement,e.alt);
                }
                else if(corner!=0)p=Snap(p,e.alt,e.shift ? dragOrigin : null,Floor.GetComponentsInChildren<HouseWall>(),corner);
                else p=center+PlanSnapping.FloorPoint(p-center,Vector2.zero,e.shift,e.alt,Grid);
                dragPoint=p;
            }
            if(!dragOrigin.HasValue)return false;
            PreviewDrag(view,placement,release);
            return true;
        }

        static void PreviewDrag(SceneView view,PlacedOpening placement,bool release)
        {
            CursorGrid.Draw(view, Floor, dragPoint.Value);
            var delta=dragPoint.Value-dragOrigin.Value;
            float distance=placement==null?0:Wall.transform.InverseTransformPoint(Floor.World(dragPoint.Value)).x;
            if(validatedPoint!=dragPoint)
            {
                validatedPoint=dragPoint;
                if(placement!=null)
                    validDrop=EditOpening(Wall,placement,placement.insert,distance,placement.sill,false,out dragIssue);
                else
                {
                    previewEdit=new FloorEdit(Floor);
                    previewEdit.Move(corner!=0?new[]{corner}:new[]{Wall.startCorner,Wall.endCorner},delta);
                    validDrop=previewEdit.Prepare(out dragIssue);
                }
                view.Repaint();
            }
            if(Event.current.type==EventType.Repaint)
            {
                using(new Handles.DrawingScope(validDrop?Color.green:Color.red))
                {
                    if(placement!=null)
                    {
                        float y=OpeningSill(placement)+placement.Size.y*.5f;
                        var at=new Vector3(distance,y,0);
                        using(new Handles.DrawingScope(Wall.transform.localToWorldMatrix))
                        {
                            Handles.DrawWireCube(at,new Vector3(placement.Size.x,placement.Size.y,Wall.Thickness));
                            Handles.DrawLine(new Vector3(placement.distance,y,0),at,2);
                        }
                        Handles.Label(Wall.transform.TransformPoint(at+Vector3.up*(placement.Size.y*.5f+.12f)),
                            $"{distance-placement.distance:+0.00;-0.00;0.00} m   ({distance:0.00} m from start)"+
                            (validDrop?"":"\n"+dragIssue));
                    }
                    else
                    {
                        bool Moves(int id)=>corner!=0?id==corner:id==Wall.startCorner || id==Wall.endCorner;
                        foreach(var w in Floor.GetComponentsInChildren<HouseWall>())
                            if(Moves(w.startCorner)||Moves(w.endCorner))
                                Handles.DrawAAPolyLine(4,Floor.World(Floor.Position(w.startCorner)+(Moves(w.startCorner)?delta:Vector2.zero)),
                                    Floor.World(Floor.Position(w.endCorner)+(Moves(w.endCorner)?delta:Vector2.zero)));
                    }
                }
            }
            if(release)
            {
                Message=dragIssue;
                if(validDrop && delta.sqrMagnitude>1e-9f)
                {
                    if(placement!=null)EditOpening(Wall,placement,placement.insert,distance,placement.sill,true,out Message);
                    else previewEdit.Commit("Move wall points",out Message);
                }
                CancelDrag();Repaint();
            }
            else if(GUIUtility.hotControl==0)CancelDrag();
        }
        internal static bool EditOpening(HouseWall wall,PlacedOpening o,HouseInsert insert,float distance,float sill,bool commit,out string reason,Vector2? clearSize=null,int? tile=null)
        {
            var copy=o.Copy();
            copy.insert=insert;copy.distance=distance;copy.sill=sill;
            if(clearSize.HasValue)copy.clearSize=clearSize.Value;
            if(tile.HasValue)copy.finishTile=tile.Value;
            var list=wall.openings.Select(x=>x==o?copy:x).ToList();
            if(!HouseWall.Validate(wall.Family,wall.length,wall.Floor.height,list,new List<FittedOpening>(),out reason,resolvedThickness:wall.Thickness))return false;
            if(!WallJunctions.CheckOpening(wall,list,out reason))return false;
            if(!commit)return true;
            Undo.RecordObject(wall,"Edit opening");wall.openings=list;
            if (!WallBuilder.Rebuild(wall,out reason)) return false;
            return WallJunctions.Refresh(wall.Floor,out reason);
        }
        internal static void DissolveEndpoint(int endpoint, bool selectMergedWall)
        {
            if (!Floor || endpoint == 0) return;
            var edit = new FloorEdit(Floor);
            if (edit.Dissolve(endpoint, MultipleWalls ? Wall : null, out var merged, out Message) && edit.Commit("Dissolve wall endpoint", out Message))
                Select(selectMergedWall ? new HousePick { wall = merged } : default);
            Repaint();
        }

        internal static void DeleteSelection()
        {
            if(!Floor)return;
            if(Picks.Items.Count>1 && !MultipleWalls){ HouseProperties.DeleteHouseBatch();return; }
            if(Selected.cut!=null)
            {
                int index=SelectedCutIndex();
                if(index>=0)SlabBuilder.SetOpening(Selected.cutOwner,Selected.roofCut,index,null,out Message);
                Select(default);
            }
            else if (corner != 0) { DissolveEndpoint(corner, selectMergedWall: false); return; }
            else if(Opening!=null && Wall){Undo.RecordObject(Wall,"Remove opening");Wall.openings.RemoveAll(o=>o.id==Opening);WallBuilder.Rebuild(Wall,out Message);WallJunctions.Refresh(Wall.Floor,out Message);Select(new HousePick{wall=Wall});}
            else
            {
                var edit=new FloorEdit(Floor);
                if (MultipleWalls) { foreach (var wall in SelectedWalls) edit.Remove(wall); }
                else if(Wall)edit.Remove(Wall);else return;
                if(edit.Commit("Delete selection",out Message))Select(default);
            }
            Repaint();
        }
    }
}
