using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static partial class RoadScene
    {
        static Vector3 PavementWorld(Vector2 point) => World(Local(point,Active.PavementElevation));

        static GroundPlan indexedPlan;
        static readonly Dictionary<Vector2,RoadHit> corners=new Dictionary<Vector2,RoadHit>();
        static HashSet<Vector2> movingPavement;
        static Vector2 pavementOrigin, pavementDelta;
        static SceneView pavementView;

        static Dictionary<Vector2,RoadHit> Corners
        {
            get
            {
                var plan=Active.Areas;
                if(ReferenceEquals(indexedPlan,plan))return corners;
                corners.Clear();
                foreach(var edge in plan.edges)
                {
                    Add(edge.a,edge.id,0);
                    Add(edge.b,edge.id,1);
                }
                indexedPlan=plan;
                return corners;
            }
        }

        // Shared endpoints in existing ground plans already have identical local coordinates.
        static void Add(Vector2 point,int edge,int end)
        {
            if(!corners.TryGetValue(point,out var hit) || edge<hit.id)
                corners[point]=new RoadHit(RoadPart.GroundPoint,edge,end);
        }

        static bool MovablePavement(RoadPart part) => part==RoadPart.GroundPoint ||
            part==RoadPart.Boundary || part==RoadPart.PathPoint;

        static HashSet<Vector2> PavementPoints(IEnumerable<RoadHit> hits)
        {
            var points=new HashSet<Vector2>();
            foreach(var hit in hits)
            {
                if(hit.part==RoadPart.PathPoint)
                {
                    if(Active.Paths.Node(hit.id) is RoadNode node)points.Add(Flat(node.position));
                }
                else if(Active.Areas.edges.Find(e=>e.id==hit.id) is GroundEdge edge)
                {
                    if(hit.part==RoadPart.Boundary || hit.arm==0)points.Add(edge.a);
                    if(hit.part==RoadPart.Boundary || hit.arm==1)points.Add(edge.b);
                }
            }
            return points;
        }

        static bool OnPavement(Vector2 point)
        {
            bool On(Vector2 a, Vector2 b) => (point-PlanSnapping.Project(point,a,b)).sqrMagnitude < 1e-8f;
            return Mode == Brush.Ground ? Active.Areas.edges.Any(e=>On(e.a,e.b)) :
                Active.Paths.spans.Any(s=>On(Flat(Active.Paths.Node(s.start.node).position),Flat(Active.Paths.Node(s.end.node).position)));
        }

        static void PreviewSidewalk(Vector3 start, Vector3 end)
        {
            var width=Active.Paths.RunWidth(run,Active.PathWidth);
            var offset = RoadGeometry.LeftOf(Flat(end-start).normalized) * (width * .5f);
            var a=Flat(start); var b=Flat(end);
            Handles.DrawAAPolyLine(2,PavementWorld(a+offset),PavementWorld(b+offset),PavementWorld(b-offset),
                PavementWorld(a-offset),PavementWorld(a+offset));
        }

        static void PickPavementOutline(ref RoadHit hit, ref float nearest, Vector2 mouse)
        {
            foreach(var corner in Corners)
            {
                float d=Vector2.Distance(HandleUtility.WorldToGUIPoint(PavementWorld(corner.Key)),mouse);
                if(d>=nearest)continue;
                nearest=d;hit=corner.Value;
            }
            foreach (var n in Active.Paths.nodes)
            {
                var pixel=HandleUtility.WorldToGUIPoint(PavementWorld(Flat(n.position)));
                float d=Vector2.Distance(pixel,mouse);
                if (d >= nearest) continue;
                nearest=d; hit=new RoadHit(RoadPart.PathPoint,n.id);
            }
            if(hit.part==RoadPart.Point || hit.part==RoadPart.PathPoint || hit.part==RoadPart.GroundPoint)return;
            foreach (var edge in Active.Areas.edges)
            {
                var a = HandleUtility.WorldToGUIPoint(PavementWorld(edge.a));
                var b = HandleUtility.WorldToGUIPoint(PavementWorld(edge.b));
                float d = Vector2.Distance(mouse,PlanSnapping.Project(mouse,a,b));
                if (d >= Mathf.Min(5,nearest)) continue;
                nearest=d; hit=new RoadHit(RoadPart.Boundary,edge.id);
            }
        }

        static void DrawPavementOutlines()
        {
            Vector2 Preview(Vector2 p,bool path) => movingPavement!=null &&
                path==(Selected.part==RoadPart.PathPoint) && movingPavement.Contains(p) ? p+pavementDelta : p;
            void Line(Vector2 a, Vector2 b) => Handles.DrawAAPolyLine(2,PavementWorld(a),PavementWorld(b));
            foreach (var edge in Active.Areas.edges)
            {
                var hit=new RoadHit(RoadPart.Boundary,edge.id);
                bool selected=Picks.Has(hit), hovered=Same(hit,hover);
                if (Mode!=Brush.Ground && !selected && !hovered && Drawing) continue;
                Handles.color=selected ? Color.yellow : hovered ? blue : new Color(.4f,.8f,.9f,.45f);
                var start=Preview(edge.a,false);var end=Preview(edge.b,false);
                Line(start,end);
                if(Active is FillerBuilding building && (selected || hovered))
                {
                    var up=building.transform.TransformVector(Vector3.up*(building.Elevation(building.floors)));
                    var a=PavementWorld(start);var b=PavementWorld(end);
                    Handles.DrawAAPolyLine(2,a,a+up,b+up,b);
                }
            }
            foreach (var face in Active.Areas.faces)
            {
                var hit=new RoadHit(RoadPart.Ground,face.id);
                if (!Picks.Has(hit) && !Same(hit,hover)) continue;
                Handles.color=Picks.Has(hit) ? Color.yellow : blue;
                for(int i=0;i<face.polygon.Length;i++) Line(Preview(face.polygon[i],false),Preview(face.polygon[(i+1)%face.polygon.Length],false));
            }
            foreach(var corner in Corners)
            {
                var point=PavementWorld(Preview(corner.Key,false));
                Handles.color=Picks.Has(corner.Value) ? Color.yellow : Same(corner.Value,hover) ? Color.white : blue;
                Handles.DotHandleCap(0,point,Quaternion.identity,HandleUtility.GetHandleSize(point)*.055f,EventType.Repaint);
            }
            foreach (var span in Active.Paths.spans)
            {
                var hit=new RoadHit(RoadPart.Path,span.id);
                if (Mode!=Brush.Sidewalk && !Picks.Has(hit) && !Same(hit,hover) && !(movingPavement!=null && Selected.part==RoadPart.PathPoint)) continue;
                Handles.color=Picks.Has(hit) ? Color.yellow : blue;
                Line(Preview(Flat(Active.Paths.Node(span.start.node).position),true),Preview(Flat(Active.Paths.Node(span.end.node).position),true));
            }
            foreach (var n in Active.Paths.nodes)
            {
                var point=PavementWorld(Preview(Flat(n.position),true));
                Handles.color=Picks.Has(new RoadHit(RoadPart.PathPoint,n.id)) ? Color.yellow : blue;
                Handles.DotHandleCap(0,point,Quaternion.identity,HandleUtility.GetHandleSize(point)*.05f,EventType.Repaint);
            }
        }

        static void SplitPath(int id, Vector2 position)
        {
            int inserted=0;
            if(ChangePaths("Insert sidewalk point",g=>{
                var span=g.Span(id);
                var plan=PlanSnapping.Project(position,Flat(g.Node(span.start.node).position),Flat(g.Node(span.end.node).position));
                var node=RoadEditing.Point(g,Local(plan,0));
                RoadEditing.Split(g,span,node);
                inserted=node.id;
            })) Pick(new RoadHit(RoadPart.PathPoint,inserted));
        }

        static bool DeletePavement(RoadHit hit)
        {
            bool ok;
            switch(hit.part)
            {
                case RoadPart.Path:
                case RoadPart.PathPoint:
                    var kind=hit.part==RoadPart.Path ? RoadPart.Road : RoadPart.Point;
                    ok=ChangePaths("Delete sidewalk selection",g=>RoadEditing.Remove(g,new RoadHit(kind,hit.id)));
                    break;
                case RoadPart.Ground:
                    ok=ChangeGround("Delete ground area",g=>GroundEditing.RemoveFace(g,hit.id));
                    break;
                case RoadPart.Boundary:
                    ok=ChangeGround("Delete ground boundary",g=>g.edges.RemoveAll(e=>e.id==hit.id));
                    break;
                default: return false;
            }
            if(ok)Pick(default);
            return true;
        }

        static void CancelPavementMove()
        {
            movingPavement=null;
            pavementView=null;
            pavementDelta=Vector2.zero;
        }

        static bool PavementHandles(SceneView view, Event e, Ray ray)
        {
            bool released=e.rawType==EventType.MouseUp && e.button==0;
            if(pavementView && pavementView!=view)return false;
            bool direct=pointerView && MovablePavement(pressedHit.part);
            if(direct)
            {
                if(e.button!=0 || e.rawType!=EventType.MouseDrag && e.rawType!=EventType.MouseUp)return false;
                if(movingPavement==null && (e.mousePosition-down).sqrMagnitude<16)return false;
                if(!Picks.Has(pressedHit) || Picks.Mixed)Pick(pressedHit);
            }
            else if(pointerView || Picks.Mixed || !MovablePavement(Selected.part) || e.shift && GUIUtility.hotControl==0)
                return false;

            var points=movingPavement??PavementPoints(Picks.Items);
            if(points.Count==0)return false;
            var pivot=movingPavement==null ? points.Aggregate(Vector2.zero,(a,b)=>a+b)/points.Count : pavementOrigin;
            Vector2 target;
            if(direct)
            {
                var plane=new Plane(Active.transform.up,PavementWorld(pivot));
                var start=HandleUtility.GUIPointToWorldRay(down);
                if(!plane.Raycast(start,out var a) || !plane.Raycast(ray,out var b))return false;
                target=pivot+Flat(Active.transform.InverseTransformVector(ray.GetPoint(b)-start.GetPoint(a)));
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var moved=PlanarMoveHandle.Draw(PavementWorld(pivot+pavementDelta),Active.transform.rotation);
                bool changed=EditorGUI.EndChangeCheck();
                if(!changed && movingPavement==null)return false;
                target=changed ? Flat(Active.transform.InverseTransformPoint(moved)) : pivot+pavementDelta;
            }
            movingPavement=points;pavementOrigin=pivot;pavementView=view;
            pavementDelta=PlanSnapping.FloorPoint(target,pivot,e.shift,e.alt,PlanSnapping.Grid)-pivot;
            if(released)
            {
                var delta=pavementDelta;
                if(delta.sqrMagnitude>1e-9f)
                {
                    if(Selected.part==RoadPart.PathPoint)
                        ChangePaths("Move sidewalk points",g=>{
                            if(Picks.Items.Count==1)RoadEditing.Move(g,Selected.id,g.Node(Selected.id).position+Local(delta,0));
                            else foreach(var hit in Picks.Items)g.Node(hit.id).position+=Local(delta,0);
                        });
                    else if(ChangeGround("Move ground selection",g=>GroundEditing.Move(g,points,delta)) &&
                        Selected.part==RoadPart.GroundPoint)
                    {
                        // Noding can split the edge that previously identified this corner.
                        Picks.Pick(default,false);
                        foreach(var point in points)
                        {
                            var targetPoint=point+delta;
                            var corner=Corners.FirstOrDefault(c=>(c.Key-targetPoint).sqrMagnitude<1e-8f);
                            if(corner.Value.part!=RoadPart.None && !Picks.Has(corner.Value))Picks.Pick(corner.Value,true);
                        }
                    }
                }
                Picks.RemoveWhere(h=>!Exists(h));
                CancelPavementMove();
                if(direct){GUIUtility.hotControl=selectionControl=0;pointerView=null;pressedHit=default;}
            }
            if(direct && e.type!=EventType.Used)e.Use();
            if(e.type!=EventType.Repaint)view.Repaint();
            return true;
        }
    }
}
