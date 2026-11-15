using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static class RoadScene
    {
        internal static RoadNetwork Active { get; private set; }
        internal static RoadHit Selected { get; private set; }
        internal static readonly HashSet<int> SelectedRoads = new HashSet<int>();
        internal static bool Drawing { get; private set; }
        internal static RoadFamily DrawFamily;
        internal static string Message;
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
            Undo.undoRedoPerformed+=()=>{RoadEditing.RebuildLoaded();movePreview=null;movingPoint=0;
                if(Active && anchorId!=0 && Active.graph.Node(anchorId)==null) EndChain();
                if(Active) { SelectedRoads.RemoveWhere(i=>Active.graph.Span(i)==null); Repaint(); }};
            Selection.selectionChanged+=()=>{if(Active && Selection.activeGameObject!=Active.gameObject)Leave();};
            AssemblyReloadEvents.beforeAssemblyReload+=Leave;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Leave();};
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing+=(scene,removing)=>{if(Active && Active.gameObject.scene==scene)Leave();};
        }

        [MenuItem("GameObject/House Toolkit/Create Road",false,12)]
        internal static void Create()
        {
            var go=new GameObject("Road Network");
            var road=go.AddComponent<RoadNetwork>();
            Undo.RegisterCreatedObjectUndo(go,"Create road network");
            Enter(road); SetDrawing(true);
        }
        internal static void Enter(RoadNetwork road)
        {
            if(!road)return;
            if(Active)Leave();
            if(HouseScene.Floor)HouseScene.Leave();
            Selection.activeGameObject=road.gameObject;
            Active=road;hidden=Tools.hidden;Tools.hidden=true;
            SetDrawing(false);
        }
        internal static void Leave()
        {
            if(Active)Tools.hidden=hidden;
            Active=null;Drawing=false;DrawFamily=null;Selected=default;SelectedRoads.Clear();EndChain();
            if(selectionControl!=0 && GUIUtility.hotControl==selectionControl)GUIUtility.hotControl=0;
            selectionControl=0;pressedHit=default;pointerView=null;roadDelta=null;movePreview=null;movingPoint=0;Message=null;SceneView.RepaintAll();
        }
        internal static void SetDrawing(bool draw)
        {
            Drawing=draw;EndChain();Selected=default;SelectedRoads.Clear();hover=default;Message=null;
            RoadInspector.ShowTool(draw); Repaint();
        }
        static void EndChain(){anchor=null;anchorId=run=0;press.Cancel();}
        internal static void Repaint(){SceneView.RepaintAll();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();}
        internal static void Pick(RoadHit hit,bool additive=false)
        {
            if(!additive || hit.part!=RoadPart.Road)SelectedRoads.Clear();
            if(hit.part==RoadPart.Road && !SelectedRoads.Add(hit.id))SelectedRoads.Remove(hit.id);
            Selected=hit.part==RoadPart.Road && !SelectedRoads.Contains(hit.id)
                ? (SelectedRoads.Count==0?default:new RoadHit(RoadPart.Road,SelectedRoads.First())) : hit;
            Message=null; RoadInspector.ShowTool(false);Repaint();
        }
        internal static bool Change(string label,Action<RoadGraph> apply)
        {
            bool ok=RoadEditing.Change(Active,label,apply,out Message);Repaint();return ok;
        }
        static Vector2 Flat(Vector3 point)=>RoadGeometry.Flat(point);
        static Vector3 Local(Vector2 point,float elevation)=>new Vector3(point.x,elevation,point.y);
        static Vector3 World(Vector3 point)=>Active.transform.TransformPoint(point);
        static Vector2 Pixel(Vector3 point)=>HandleUtility.WorldToGUIPoint(World(point));

        static Vector3 Snap(Vector3 point,Vector2? axis,bool free,int ignore=0)
        {
            var graph=Active.graph;
            var nodes=graph.nodes.Where(n=>n.id!=ignore);
            var spans=graph.spans.Where(s=>s.start.node!=ignore && s.end.node!=ignore);
            var plan=PlanSnapping.Snap(Flat(point),free,axis,PlanSnapping.Grid,nodes.Select(n=>Flat(n.position)),
                spans.Select(s=>(Flat(graph.Node(s.start.node).position),Flat(graph.Node(s.end.node).position))),
                (a,b)=>Vector2.Distance(Pixel(Local(a,point.y)),Pixel(Local(b,point.y))));
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
            var p=Local(point,pointElevation);
            if(!anchor.HasValue){anchor=p;return;}
            var a=anchor.Value;
            if((p-a).sqrMagnitude<1e-6f){EndChain();return;}
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
            if(hit.part==RoadPart.None)return;
            if(Change("Delete road selection",g=>{
                if(SelectedRoads.Count>0)g.spans.RemoveAll(s=>SelectedRoads.Contains(s.id));
                else RoadEditing.Remove(g,hit);
            }))Pick(default);
        }

        static bool Same(RoadHit a,RoadHit b)=>a.part==b.part && a.id==b.id && a.arm==b.arm && a.next==b.next && a.left==b.left;
        internal static bool Frame(out Bounds bounds)
        {
            bounds=default;if(!Active || Selected.part==RoadPart.None)return false;
            if(Selected.part==RoadPart.Point && Active.graph.Node(Selected.id) is RoadNode node)
            {
                bounds=new Bounds(World(node.position),Vector3.one);
                foreach(var a in RoadGeometry.Arms(Active.graph,node,Active.family))
                    if(a.trim>0){bounds.Encapsulate(World(Local(a.Left,node.position.y)));bounds.Encapsulate(World(Local(a.Right,node.position.y)));}
                return true;
            }
            var mesh=Active.GetComponent<MeshCollider>().sharedMesh;
            if(!mesh)return false;
            var vertices=mesh.vertices;var indices=mesh.triangles;bool found=false;
            for(int i=0;i<Active.faces.Length;i++)
            {
                var h=Active.faces[i];
                if(!Same(Selected,h) && !(SelectedRoads.Contains(h.id) && h.part!=RoadPart.Point && h.part!=RoadPart.Corner))continue;
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
            if(Drawing && press.Track(view,e,Place))return;
            if(pointerView && pointerView!=view)return;
            if(e.rawType==EventType.Ignore || e.rawType==EventType.MouseLeaveWindow || e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape)
            {
                if(selectionControl!=0 && GUIUtility.hotControl==selectionControl) GUIUtility.hotControl=0;
                pressedHit=default;selectionControl=0;pointerView=null;movingPoint=0;movePreview=null;roadDelta=null;
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
            var graph=Active.graph;
            var selected=Selected.part==RoadPart.Point?graph.Node(Selected.id):null;
            if(!Drawing && selected!=null && !pointerView)
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
            bool movingRoads=Selected.part==RoadPart.Road && SelectedRoads.Count>0;
            bool Included(int id)=>SelectedRoads.Any(key=>graph.Span(key) is RoadSpan span && (span.start.node==id || span.end.node==id));
            if(!Drawing && movingRoads)
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
            float y=anchor?.y??0;
            if(!new Plane(Active.transform.up,World(Vector3.up*y)).Raycast(ray,out var depth))return;
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
            if(!Drawing && selected!=null)
            {
                var arms=RoadGeometry.Arms(graph,selected,Active.family);
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
                var point=Snap(position,e.shift && anchor.HasValue?Flat(anchor.Value):(Vector2?)null,e.alt);
                CursorGrid.Draw(view,Active.transform,Flat(point),point.y);
                if(e.type==EventType.Repaint)
                {
                    Handles.color=Color.green;
                    Handles.DotHandleCap(0,World(point),Quaternion.identity,HandleUtility.GetHandleSize(World(point))*.07f,EventType.Repaint);
                    if(anchor.HasValue)Handles.DrawAAPolyLine(3,World(anchor.Value),World(point));
                }
                if(e.rawType==EventType.MouseDown && e.button==0)
                {
                    pointElevation=point.y;
                    bool finish=graph.spans.Any(s=>(Flat(point)-PlanSnapping.Project(Flat(point),
                        Flat(graph.Node(s.start.node).position),Flat(graph.Node(s.end.node).position))).sqrMagnitude<1e-8f);
                    press.Press(view,e,control,Flat(point),finish,Place);
                }
            }
            else if(!e.alt)
            {
                if(e.type==EventType.MouseDown && e.button==0 && GUIUtility.hotControl==0 && HandleUtility.nearestControl==control)
                {
                    GUIUtility.hotControl=selectionControl=control;down=e.mousePosition;pressedHit=hit;pointerView=view;
                    if(e.clickCount==2 && (hit.part==RoadPart.Road || hit.part==RoadPart.Approach))
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
                if(!Drawing && !IsSelected(hover) && !SelectedRoads.Contains(span.id) && !IsSelected(Selected) && movingPoint==0 && !roadDelta.HasValue)continue;
                Vector3 End(int id)=>World(movingPoint==id && movePreview.HasValue?movePreview.Value:graph.Node(id).position+(roadDelta.HasValue && Included(id)?roadDelta.Value:Vector3.zero));
                Handles.color=SelectedRoads.Contains(span.id)||IsSelected(Selected)?Color.yellow:blue;
                Handles.DrawAAPolyLine(3,End(span.start.node),End(span.end.node));
            }
            foreach(var n in graph.nodes)
            {
                var world=World(n.position);
                Handles.color=Selected.part==RoadPart.Point && n.id==Selected.id?Color.yellow:blue;
                Handles.DotHandleCap(0,world,Quaternion.identity,HandleUtility.GetHandleSize(world)*.055f,EventType.Repaint);
            }
        }
    }
}
