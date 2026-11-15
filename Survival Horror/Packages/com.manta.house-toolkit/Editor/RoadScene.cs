using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static partial class RoadScene
    {
        internal static ProceduralSurface Active { get; private set; }
        internal static readonly SceneSelection<RoadHit> Picks = new SceneSelection<RoadHit>(Same, TypeName);
        internal static RoadHit Selected => Picks.Active;
        static string TypeName(RoadHit hit) => hit.part switch
        {
            RoadPart.None => null, RoadPart.Path => "Sidewalk segment", RoadPart.PathPoint => "Sidewalk point",
            RoadPart.GroundPoint => "Ground point", RoadPart.Ground => "Ground area", RoadPart.Sidewalk => "Roadside sidewalk", RoadPart.Boundary => Active is FillerBuilding ? "Facade wall" : "Ground boundary",
            _ => hit.part.ToString()
        };
        internal static readonly HashSet<int> SelectedRoads = new HashSet<int>();
        internal enum Brush { Select, Road, Sidewalk, Ground }
        internal static Brush Mode { get; private set; }
        internal static bool Drawing => Mode != Brush.Select;
        static bool Paving => Mode == Brush.Sidewalk || Mode == Brush.Ground;
        internal static RoadFamily DrawFamily;
        internal static string Message;
        static readonly DrawingGuides drawGuides=new DrawingGuides();
        static readonly PointPlacement press = new PointPlacement();
        static Vector3? anchor;
        static int run, anchorId, selectionControl;
        static bool hidden;
        static Vector2 down;
        static Vector3? movePreview;
        static int movingPoint;
        static RoadHit pressedHit;
        static SceneView pointerView;
        static Vector3? roadDelta;
        static float pointElevation;
        static RoadHit hover;
        static readonly Color blue=new Color(.3f,.85f,1);
        static RoadScene()
        {
            SceneView.duringSceneGui+=OnScene;
            Undo.undoRedoPerformed+=()=>{RoadEditing.RebuildLoaded();indexedPlan=null;CancelPavementMove();movePreview=null;movingPoint=0;
                if (Active && (Mode == Brush.Ground || anchorId != 0 && (Mode == Brush.Sidewalk ? Active.Paths : Active.Streets).Node(anchorId) == null)) EndChain();
                if(Active) { Picks.RemoveWhere(h=>!Exists(h)); SyncPicks(); }};
            Selection.selectionChanged+=()=>{if(Active && Selection.activeGameObject!=Active.gameObject)Leave();};
            AssemblyReloadEvents.beforeAssemblyReload+=Leave;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Leave();};
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing+=(scene,removing)=>{if(Active && Active.gameObject.scene==scene)Leave();};
        }

        [MenuItem("GameObject/House Toolkit/Create Road",false,12)]
        internal static void Create() => CreateAt(Brush.Road,Vector3.zero,false);
        internal static void CreateAt(Brush brush,Vector3 position,bool startChain)
        {
            var go=new GameObject(brush==Brush.Ground ? "Ground Area" : brush==Brush.Sidewalk ? "Sidewalk" : "Road Network");
            go.transform.position=position;
            ProceduralSurface source=brush switch
            {
                Brush.Sidewalk => go.AddComponent<ProceduralSidewalk>(),
                Brush.Ground => go.AddComponent<GroundArea>(),
                _ => go.AddComponent<RoadNetwork>()
            };
            StartDrawing(source,brush,startChain);
        }

        internal static void CreateFillerAt(Vector3 position)
        {
            var go=new GameObject("Filler Building");
            go.transform.position=position;
            var building=go.AddComponent<FillerBuilding>();
            building.seed=Guid.NewGuid().GetHashCode() & 0x7fffff;
            building.SetType(RoadInspector.StartingType(building.seed));
            building.Rebuild();
            StartDrawing(building,Brush.Ground,true);
            RoadInspector.ShowRoad();
        }
        static void StartDrawing(ProceduralSurface source,Brush brush,bool startChain)
        {
            Undo.RegisterCreatedObjectUndo(source.gameObject,"Create "+source.name);
            Enter(source); SetBrush(brush);
            if(startChain)anchor=Vector3.zero;
            if(brush==Brush.Road)RoadInspector.ShowRoad();
        }
        internal static string EditLabel(ProceduralSurface source) => source is FillerBuilding ? "Edit Filler Building" : source is GroundArea ? "Edit Ground Area" :
            source is ProceduralSidewalk ? "Edit Sidewalk" : "Edit Roads";

        internal static void Enter(ProceduralSurface road)
        {
            if(!road)return;
            if(Active)Leave();
            if(HouseScene.Floor)HouseScene.Leave();
            Selection.activeGameObject=road.gameObject;
            Active=road;hidden=Tools.hidden;Tools.hidden=true;
            SetDrawing(false);
            RoadInspector.ShowRoad();
            if (!Application.isBatchMode) HouseProperties.Open();
        }
        internal static void Leave()
        {
            if(Active)Tools.hidden=hidden;
            indexedPlan=null;corners.Clear();CancelPavementMove();
            Active=null;Mode=Brush.Select;DrawFamily=null;Picks.Pick(default,false);SelectedRoads.Clear();EndChain();
            if(selectionControl!=0 && GUIUtility.hotControl==selectionControl)GUIUtility.hotControl=0;
            selectionControl=0;pressedHit=default;pointerView=null;roadDelta=null;movePreview=null;movingPoint=0;Message=null;SceneView.RepaintAll();
        }
        internal static void SetDrawing(bool draw) => SetBrush(draw ? Brush.Road : Brush.Select);
        internal static void SetBrush(Brush brush)
        {
            if (brush!=Brush.Select && ((Active is GroundArea || Active is FillerBuilding) && brush!=Brush.Ground ||
                Active is ProceduralSidewalk && brush!=Brush.Sidewalk)) return;
            CancelPavementMove();Mode=brush;EndChain();Picks.Pick(default,false);SelectedRoads.Clear();hover=default;Message=null;
            RoadInspector.ShowTool(Drawing); Repaint();
        }
        static void EndChain(){drawGuides.Clear();anchor=null;anchorId=run=0;press.Cancel();}
        internal static void Repaint(){SceneView.RepaintAll();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();}
        internal static void Pick(RoadHit hit,bool additive=false)
        {
            Picks.Pick(hit,additive);
            SyncPicks();
        }
        internal static void SyncPicks()
        {
            SelectedRoads.Clear();
            foreach(var hit in Picks.Items)
                if(hit.part==RoadPart.Road)SelectedRoads.Add(hit.id);
            Message=null;RoadInspector.ShowTool(false);Repaint();
        }
        internal static bool Change(string label,Action<RoadGraph> apply)
        {
            bool ok=RoadEditing.Change(Active,label,apply,out Message);Repaint();return ok;
        }
        internal static bool ChangePaths(string label, Action<RoadGraph> apply)
        {
            bool ok = RoadEditing.Change(Active,label,apply,out Message,true);
            Repaint();
            return ok;
        }
        internal static bool ChangeGround(string label, Action<GroundPlan> apply)
        {
            bool ok = GroundEditing.Change(Active,label,apply,out Message);
            Repaint();
            return ok;
        }
        static Vector2 Flat(Vector3 point)=>RoadGeometry.Flat(point);
        static Vector3 Local(Vector2 point,float elevation)=>new Vector3(point.x,elevation,point.y);
        static Vector3 World(Vector3 point)=>Active.transform.TransformPoint(point);
        static Vector2 Pixel(Vector3 point)=>HandleUtility.WorldToGUIPoint(World(point));

        static Vector3 Snap(Vector3 point,Vector2? axis,bool free,int ignore=0, bool pavement=false,Vector2? from=null)
        {
            var graph=Active.Streets;
            var nodes=graph.nodes.Where(n=>n.id!=ignore);
            var spans=graph.spans.Where(s=>s.start.node!=ignore && s.end.node!=ignore);
            var endpoints = nodes.Select(n=>Flat(n.position));
            var segments = spans.Select(s=>(Flat(graph.Node(s.start.node).position),Flat(graph.Node(s.end.node).position)));
            if (pavement)
            {
                endpoints = endpoints.Concat(Active.Paths.nodes.Select(n=>Flat(n.position)))
                    .Concat(Active.Areas.edges.SelectMany(e=>new[]{e.a,e.b}));
                segments = segments.Concat(Active.Paths.spans.Select(s=>(Flat(Active.Paths.Node(s.start.node).position),
                    Flat(Active.Paths.Node(s.end.node).position)))).Concat(Active.Areas.edges.Select(e=>(e.a,e.b)));
            }
            // Legacy networks can contain three independent layouts. Only extend guides from the
            // current layout, while real endpoints and intersections remain eligible across layers.
            IEnumerable<(Vector2,Vector2)> guideEdges=null;
            if(from.HasValue && pavement && Active is RoadNetwork)
                guideEdges=Mode==Brush.Ground ? Active.Areas.edges.Select(e=>(e.a,e.b)) :
                    Active.Paths.spans.Select(s=>(Flat(Active.Paths.Node(s.start.node).position),Flat(Active.Paths.Node(s.end.node).position)));
            var plan = PlanSnapping.Snap(Flat(point),free,axis,PlanSnapping.Grid,endpoints,segments,
                (a,b)=>Vector2.Distance(Pixel(Local(a,point.y)),Pixel(Local(b,point.y))),from.HasValue ? drawGuides : null,from,guideEdges);
            if (pavement) return Local(plan,Active.PavementElevation);
            if(!free)
            {
                foreach(var n in nodes)if((plan-Flat(n.position)).sqrMagnitude<1e-8f)return n.position;
                foreach(var s in spans)
                {
                    var a=graph.Node(s.start.node).position;var b=graph.Node(s.end.node).position;
                    if((PlanSnapping.Project(plan,Flat(a),Flat(b))-plan).sqrMagnitude>1e-8f) continue;
                    return Vector3.Lerp(a,b,Vector2.Dot(plan-Flat(a),Flat(b-a))/Flat(b-a).sqrMagnitude);
                }
            }
            return Local(plan,point.y);
        }
        static void Place(Vector2 point,bool finishes)
        {
            drawGuides.Clear();
            var p=Local(point,pointElevation);
            if(!anchor.HasValue){anchor=p;return;}
            var a=anchor.Value;
            if((p-a).sqrMagnitude<1e-6f){EndChain();return;}
            if (Mode == Brush.Ground)
            {
                if (ChangeGround("Draw ground boundary",g=>GroundEditing.Line(g,Flat(a),point)))
                {
                    if(Active is FillerBuilding building && building.footprint.faces.Count==1){SetBrush(Brush.Select);RoadInspector.ShowRoad();}
                    else if (finishes) EndChain(); else anchor=p;
                }
                return;
            }
            if (Mode == Brush.Sidewalk)
            {
                int endId=0, pathRun=0;
                if (ChangePaths("Draw sidewalk",g=>{
                    var start=RoadEditing.Point(g,Local(Flat(a),0));
                    var end=RoadEditing.Point(g,Local(point,0));
                    endId=end.id;
                    pathRun=RoadEditing.Extend(g,start.id,end.id,run,null).run;
                }))
                {
                    if (finishes) EndChain(); else { anchor=p; anchorId=endId; run=pathRun; }
                }
                return;
            }
            int nextRun=0,nextId=0;
            if(Change("Draw road",g=>{
                var start=RoadEditing.Point(g,a); var end=RoadEditing.Point(g,p);
                nextId=end.id; nextRun=RoadEditing.Extend(g,start.id,end.id,run,DrawFamily).run;
            }))
            {
                if(finishes)EndChain();
                else {anchor=p;anchorId=nextId;run=nextRun;}
            }
        }
        internal static bool InsertPoint(int id,Vector3 position)
        {
            int point=0;
            bool ok=Change("Insert road point",g=>{
                var s=g.Span(id);var a=g.Node(s.start.node).position;var b=g.Node(s.end.node).position;
                var projected=Vector3.Lerp(a,b,Vector2.Dot(Flat(position-a),Flat(b-a))/Flat(b-a).sqrMagnitude);
                var n=RoadEditing.Point(g,projected);point=n.id;RoadEditing.Split(g,s,n);
            });
            if(ok) Pick(new RoadHit(RoadPart.Point,point));
            return ok;
        }
        internal static void Delete()
        {
            var hit=Selected;
            if (Picks.Items.Count>1)
            {
                if (!Picks.Mixed) DeleteBatch();
                return;
            }
            if(hit.part==RoadPart.GroundPoint){DeleteBatch();return;}
            if (DeletePavement(hit)) return;
            if(hit.part==RoadPart.None)return;
            if(Change("Delete road selection",g=>{
                if(SelectedRoads.Count>0)g.spans.RemoveAll(s=>SelectedRoads.Contains(s.id));
                else RoadEditing.Remove(g,hit);
            }))Pick(default);
        }

        static bool Exists(RoadHit hit) => hit.part switch
        {
            RoadPart.Ground => Active.Areas.faces.Any(f=>f.id==hit.id),
            RoadPart.GroundPoint or RoadPart.Boundary => Active.Areas.edges.Any(e=>e.id==hit.id),
            RoadPart.Path => Active.Paths.Span(hit.id)!=null,
            RoadPart.PathPoint => Active.Paths.Node(hit.id)!=null,
            RoadPart.Corner => Active.Streets.Node(hit.id) is RoadNode n && Active.Streets.spans.SelectMany(s=>new[]{s.start,s.end})
                .Count(e=>e.node==n.id && (e.port==hit.arm || e.port==hit.next))==2,
            RoadPart.Point => Active.Streets.Node(hit.id)!=null,
            _ => Active.Streets.Span(hit.id)!=null
        };

        internal static void DeleteBatch()
        {
            if(Picks.Mixed)return;
            var items=Picks.Items.ToArray();
            var part=Selected.part;
            bool ok;
            if(part==RoadPart.GroundPoint)
            {
                var points=PavementPoints(items);
                ok=ChangeGround("Remove ground points",g=>GroundEditing.RemovePoints(g,points));
            }
            else if(part==RoadPart.Ground || part==RoadPart.Boundary)
                ok=ChangeGround("Remove ground selection",g=>{
                    if(part==RoadPart.Boundary)g.edges.RemoveAll(e=>items.Any(h=>h.id==e.id));
                    else
                    {
                        var chosen=g.faces.Where(f=>items.Any(h=>h.id==f.id)).ToArray();
                        var others=g.faces.Except(chosen).ToArray();
                        if(g.edges.RemoveAll(e=>chosen.Any(f=>GroundEditing.Borders(e,f)) && !others.Any(f=>GroundEditing.Borders(e,f)))==0)
                            throw new InvalidOperationException("Delete a boundary to merge enclosed faces.");
                    }
                });
            else if(part==RoadPart.Path || part==RoadPart.PathPoint)
                ok=ChangePaths("Delete sidewalks",g=>{foreach(var h in items)
                    RoadEditing.Remove(g,new RoadHit(part==RoadPart.Path?RoadPart.Road:RoadPart.Point,h.id));});
            else if(part==RoadPart.Road || part==RoadPart.Point || part==RoadPart.Approach)
                ok=Change("Delete road selection",g=>{foreach(var h in items)RoadEditing.Remove(g,h);});
            else return;
            if(ok)Pick(default);
        }

        static bool Same(RoadHit a,RoadHit b)=>a.part==b.part && a.id==b.id && a.arm==b.arm && a.next==b.next && a.left==b.left;
        internal static bool Frame(out Bounds bounds)
        {
            bounds=default;
            if (!Active) return false;
            bool found=false;
            foreach (var hit in Picks.Items)
            {
                if (!FrameHit(hit,out var part)) continue;
                if(found)bounds.Encapsulate(part);else bounds=part;
                found=true;
            }
            return found;
        }
        static bool FrameHit(RoadHit selected,out Bounds bounds)
        {
            bounds=default;
            if (selected.part == RoadPart.PathPoint && Active.Paths.Node(selected.id) is RoadNode point)
            {
                bounds = new Bounds(World(Local(Flat(point.position),Active.PavementElevation)),Vector3.one);
                return true;
            }
            if(selected.part==RoadPart.GroundPoint && Active.Areas.edges.Find(e=>e.id==selected.id) is GroundEdge line)
            {
                bounds=new Bounds(PavementWorld(selected.arm==0 ? line.a : line.b),Vector3.one);
                return true;
            }
            if (selected.part == RoadPart.Boundary && Active.Areas.edges.Find(e=>e.id==selected.id) is GroundEdge edge)
            {
                bounds = new Bounds(World(Local(edge.a,Active.PavementElevation)),Vector3.zero);
                bounds.Encapsulate(World(Local(edge.b,Active.PavementElevation)));
                if(Active is FillerBuilding building)
                {
                    bounds.Encapsulate(World(Local(edge.a,building.Elevation(building.floors))));
                    bounds.Encapsulate(World(Local(edge.b,building.Elevation(building.floors))));
                }
                return true;
            }
            if(selected.part==RoadPart.Point && Active.Streets.Node(selected.id) is RoadNode node)
            {
                bounds=new Bounds(World(node.position),Vector3.one);
                foreach(var a in RoadGeometry.Arms(Active.Streets,node,Active.StreetFamily))
                    if(a.trim>0){bounds.Encapsulate(World(Local(a.Left,node.position.y)));bounds.Encapsulate(World(Local(a.Right,node.position.y)));}
                return true;
            }
            var mesh=Active.GetComponent<MeshCollider>().sharedMesh;
            if(!mesh)return false;
            var vertices=mesh.vertices;var indices=mesh.triangles;bool found=false;
            for(int i=0;i<Active.faces.Length;i++)
            {
                var h=Active.faces[i];
                if(!Same(selected,h) && !(selected.part==RoadPart.Road && selected.id==h.id && (h.part==RoadPart.Approach || h.part==RoadPart.Sidewalk)))continue;
                for(int j=0;j<3;j++)
                {
                    var world=World(vertices[indices[3*i+j]]);
                    if(!found){bounds=new Bounds(world,Vector3.zero);found=true;}else bounds.Encapsulate(world);
                }
            }
            return found;
        }

        static void OnScene(SceneView view)
        {
            if(!Active || EditorApplication.isPlayingOrWillChangePlaymode)return;
            var e=Event.current;
            if(e.type==EventType.Repaint)
                using(new Handles.DrawingScope(Color.white))DrawPavementOutlines();
            if(Drawing && press.Track(view,e,Place))return;
            if(pointerView && pointerView!=view)return;
            if(e.rawType==EventType.Ignore || e.rawType==EventType.MouseLeaveWindow || e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape)
            {
                if(selectionControl!=0 && GUIUtility.hotControl==selectionControl) GUIUtility.hotControl=0;
                CancelPavementMove();pressedHit=default;selectionControl=0;pointerView=null;movingPoint=0;movePreview=null;roadDelta=null;
            }
            if(e.button==1 && e.isMouse || Tools.viewToolActive && !e.alt || Tools.current==Tool.View)return;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape)
            {
                EndChain();movePreview=null;movingPoint=0;Pick(default);e.Use();return;
            }
            bool delete=e.commandName=="Delete" || e.commandName=="SoftDelete";
            if(Selected.part!=RoadPart.None && (delete && (e.type==EventType.ExecuteCommand || e.type==EventType.ValidateCommand)
                || e.type==EventType.KeyDown && (e.keyCode==KeyCode.Delete || e.keyCode==KeyCode.Backspace)))
            {
                if(e.type!=EventType.ValidateCommand)Delete();e.Use();return;
            }
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout && !e.alt)HandleUtility.AddDefaultControl(control);
            var graph=Active.Streets;
            var selected=Selected.part==RoadPart.Point?graph.Node(Selected.id):null;
            if(!Drawing && Picks.Items.Count==1 && selected!=null && !pointerView)
            {
                int before=GUIUtility.hotControl;
                var pos=movePreview??selected.position;
                EditorGUI.BeginChangeCheck();
                var moved=Handles.PositionHandle(World(pos),Active.transform.rotation);
                if(EditorGUI.EndChangeCheck())
                {
                    var local=Active.transform.InverseTransformPoint(moved);
                    movePreview=Snap(local,e.shift?Flat(selected.position):(Vector2?)null,e.alt,selected.id);
                    movingPoint=selected.id;view.Repaint();
                }
                if(movingPoint!=0 && before!=0 && GUIUtility.hotControl==0)
                {
                    if(movePreview.HasValue)Change("Move road point",g=>RoadEditing.Move(g,movingPoint,movePreview.Value));
                    movePreview=null;movingPoint=0;
                }
            }
            bool movingRoads=!Picks.Mixed && Selected.part==RoadPart.Road && SelectedRoads.Count>0;
            bool Included(int id)=>SelectedRoads.Any(key=>graph.Span(key) is RoadSpan span && (span.start.node==id || span.end.node==id));
            if(!Drawing && movingRoads && (!e.shift || GUIUtility.hotControl!=0))
            {
                var points=graph.nodes.Where(n=>Included(n.id)).ToArray();
                if(points.Length!=0)
                {
                    var center=points.Aggregate(Vector3.zero,(v,n)=>v+n.position)/points.Length;
                    int before=GUIUtility.hotControl;
                    EditorGUI.BeginChangeCheck();
                    var handle=Handles.PositionHandle(World(center+(roadDelta??Vector3.zero)),Active.transform.rotation);
                    if(EditorGUI.EndChangeCheck())
                    {
                        var desired=Active.transform.InverseTransformPoint(handle);
                        var snapped=PlanSnapping.FloorPoint(Flat(desired),Flat(center),e.shift,e.alt,PlanSnapping.Grid);
                        roadDelta=Local(snapped,desired.y)-center;view.Repaint();
                    }
                    if(roadDelta.HasValue && before!=0 && GUIUtility.hotControl==0)
                    {
                        var delta=roadDelta.Value;
                        Change("Move roads",g=>{foreach(var n in points)g.Node(n.id).position+=delta;});
                        roadDelta=null;
                    }
                }
            }
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if(pointerView && pressedHit.part==RoadPart.Point && selectionControl!=0 &&
                (e.rawType==EventType.MouseDrag || e.rawType==EventType.MouseUp) && e.button==0 &&
                (movePreview.HasValue || (e.mousePosition-down).sqrMagnitude>=16))
            {
                var node=graph.Node(pressedHit.id);
                if(node!=null)
                {
                    if(!movePreview.HasValue) Pick(pressedHit);
                    movingPoint=node.id;
                    var plane=new Plane(Active.transform.up,World(node.position));
                    var startRay=HandleUtility.GUIPointToWorldRay(down);
                    if(plane.Raycast(ray,out var endDepth) && plane.Raycast(startRay,out var startDepth))
                    {
                        var local=node.position+Active.transform.InverseTransformVector(ray.GetPoint(endDepth)-startRay.GetPoint(startDepth));
                        movePreview=Snap(local,e.shift?Flat(node.position):(Vector2?)null,e.alt,node.id);
                    }
                    if(e.rawType==EventType.MouseUp)
                    {
                        if(movePreview.HasValue)Change("Move road point",g=>RoadEditing.Move(g,node.id,movePreview.Value));
                        GUIUtility.hotControl=selectionControl=0;pointerView=null;pressedHit=default;movingPoint=0;movePreview=null;
                    }
                    e.Use();view.Repaint();return;
                }
            }
            if(!Drawing && PavementHandles(view,e,ray))return;
            float y=anchor?.y??(Paving ? Active.PavementElevation : 0);
            bool onPlane=new Plane(Active.transform.up,World(Vector3.up*y)).Raycast(ray,out var depth);
            if(!onPlane && (Drawing || !(Active is FillerBuilding)))return;
            var position=Active.transform.InverseTransformPoint(ray.GetPoint(depth));
            var collider=Active.GetComponent<MeshCollider>();
            var hit=default(RoadHit);
            if(collider.sharedMesh && collider.Raycast(ray,out var info,float.PositiveInfinity) &&
                info.triangleIndex>=0 && info.triangleIndex<Active.faces.Length)
            {
                hit=Active.faces[info.triangleIndex];
                if(!Drawing)position=Active.transform.InverseTransformPoint(info.point);
            }
            float nearest=10;
            foreach(var n in graph.nodes)
            {
                float distance=Vector2.Distance(Pixel(n.position),e.mousePosition);
                if(distance<nearest){nearest=distance;hit=new RoadHit(RoadPart.Point,n.id);}
            }
            if (!Drawing) PickPavementOutline(ref hit,ref nearest,e.mousePosition);
            if(!Drawing && Picks.Items.Count==1 && selected!=null && !e.shift)
            {
                var arms=RoadGeometry.Arms(graph,selected,Active.StreetFamily);
                if(arms.Count>=3)
                    for(int i=0;i<arms.Count;i++)
                    {
                        var a=arms[i];var b=arms[(i+1)%arms.Count];
                        var mid=World(Local((a.Left+b.Right)*.5f,selected.position.y+.2f));
                        if(Handles.Button(mid,view.rotation,HandleUtility.GetHandleSize(mid)*.06f,HandleUtility.GetHandleSize(mid)*.1f,Handles.DotHandleCap))
                            Pick(new RoadHit(RoadPart.Corner,selected.id,a.end.port,b.end.port));
                    }
            }
            if(Drawing)
            {
                var point=Snap(position,e.shift && anchor.HasValue?Flat(anchor.Value):(Vector2?)null,e.alt,pavement:Paving,from:anchor.HasValue ? Flat(anchor.Value) : (Vector2?)null);
                CursorGrid.Draw(view,Active.transform,Flat(point),point.y);
                if(anchor.HasValue)drawGuides.Draw(Flat(point),p=>World(Local(p,point.y)));
                if(e.type==EventType.Repaint)
                {
                    Handles.color=Color.green;
                    Handles.DotHandleCap(0,World(point),Quaternion.identity,HandleUtility.GetHandleSize(World(point))*.07f,EventType.Repaint);
                    if(anchor.HasValue)
                    {
                        Handles.DrawAAPolyLine(3,World(anchor.Value),World(point));
                        if (Mode == Brush.Sidewalk) PreviewSidewalk(anchor.Value,point);
                    }
                }
                if(e.rawType==EventType.MouseDown && e.button==0)
                {
                    pointElevation=point.y;
                    bool finish=Paving ? OnPavement(Flat(point)) : graph.spans.Any(s=>(Flat(point)-PlanSnapping.Project(Flat(point),
                        Flat(graph.Node(s.start.node).position),Flat(graph.Node(s.end.node).position))).sqrMagnitude<1e-8f);
                    press.Press(view,e,control,Flat(point),finish,Place);
                }
            }
            else if(!e.alt)
            {
                if(e.type==EventType.MouseDown && e.button==0 && GUIUtility.hotControl==0 && HandleUtility.nearestControl==control)
                {
                    GUIUtility.hotControl=selectionControl=control;down=e.mousePosition;pressedHit=hit;pointerView=view;
                    if(e.clickCount==2 && hit.part==RoadPart.Path)
                    {
                        SplitPath(hit.id, Flat(position));selectionControl=GUIUtility.hotControl=0;pointerView=null;pressedHit=default;
                    }
                    else if(e.clickCount==2 && (hit.part==RoadPart.Road || hit.part==RoadPart.Approach))
                    {InsertPoint(hit.id,position);selectionControl=GUIUtility.hotControl=0;pointerView=null;pressedHit=default;}
                    e.Use();
                }
                if(e.rawType==EventType.MouseUp && e.button==0 && selectionControl==control)
                {
                    selectionControl=GUIUtility.hotControl=0;pointerView=null;pressedHit=default;
                    if((e.mousePosition-down).sqrMagnitude<16)Pick(hit,e.shift);
                    e.Use();
                }
            }
            if(e.type==EventType.MouseMove){hover=hit;view.Repaint();}
            if(e.type!=EventType.Repaint)return;
            foreach(var span in graph.spans)
            {
                bool IsSelected(RoadHit h)=>h.id==span.id && (h.part==RoadPart.Road || h.part==RoadPart.Approach || h.part==RoadPart.Sidewalk);
                if(!Drawing && !IsSelected(hover) && !SelectedRoads.Contains(span.id) && !Picks.Items.Any(IsSelected) && movingPoint==0 && !roadDelta.HasValue)continue;
                Vector3 End(int id)=>World(movingPoint==id && movePreview.HasValue?movePreview.Value:graph.Node(id).position+(roadDelta.HasValue && Included(id)?roadDelta.Value:Vector3.zero));
                Handles.color=SelectedRoads.Contains(span.id)||Picks.Items.Any(IsSelected)?Color.yellow:blue;
                Handles.DrawAAPolyLine(3,End(span.start.node),End(span.end.node));
            }
            foreach(var n in graph.nodes)
            {
                var world=World(n.position);
                Handles.color=Picks.Has(new RoadHit(RoadPart.Point,n.id))?Color.yellow:blue;
                Handles.DotHandleCap(0,world,Quaternion.identity,HandleUtility.GetHandleSize(world)*.055f,EventType.Repaint);
            }
        }
    }
}
