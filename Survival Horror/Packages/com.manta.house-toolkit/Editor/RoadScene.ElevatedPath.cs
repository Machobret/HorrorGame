using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static partial class RoadScene
    {
        static bool ElevatedPath => Active is ProceduralPath path && path.Elevated;
        static HashSet<int> liftedPoints;
        static Vector3 liftedOrigin, liftedDelta;
        static SceneView liftedView;
        static Vector3 PathPoint(RoadNode node) => ElevatedPath ? node.position +
            (liftedPoints!=null && liftedPoints.Contains(node.id) ? liftedDelta : Vector3.zero) : Local(Flat(node.position),Active.PavementElevation);
        static void CancelElevatedMove(){liftedPoints=null;liftedDelta=Vector3.zero;liftedView=null;}

        static bool ElevatedHandles(SceneView view,Event e,Ray ray)
        {
            if(liftedView && liftedView!=view)return false;
            bool pathHit=pressedHit.part==RoadPart.Path || pressedHit.part==RoadPart.PathPoint;
            bool direct=pointerView && pathHit;
            if(direct)
            {
                if(e.button!=0 || e.rawType!=EventType.MouseDrag && e.rawType!=EventType.MouseUp)return false;
                if(liftedPoints==null && (e.mousePosition-down).sqrMagnitude<16)return false;
                if(!Picks.Has(pressedHit) || Picks.Mixed)Pick(pressedHit);
            }
            else if(pointerView || Picks.Mixed || Selected.part!=RoadPart.Path && Selected.part!=RoadPart.PathPoint || e.shift && GUIUtility.hotControl==0)return false;
            var points=liftedPoints??Picks.Items.SelectMany(h=>h.part==RoadPart.PathPoint ? new[]{h.id} :
                new[]{Active.Paths.Span(h.id).start.node,Active.Paths.Span(h.id).end.node}).ToHashSet();
            if(points.Count==0)return false;
            var pivot=liftedPoints==null ? points.Aggregate(Vector3.zero,(sum,id)=>sum+Active.Paths.Node(id).position)/points.Count : liftedOrigin;
            Vector3 target;
            int before=GUIUtility.hotControl;
            if(direct)
            {
                var plane=new Plane(Active.transform.up,World(pivot));
                if(!plane.Raycast(HandleUtility.GUIPointToWorldRay(down),out float a) || !plane.Raycast(ray,out float b))return false;
                target=pivot+Active.transform.InverseTransformVector(ray.GetPoint(b)-HandleUtility.GUIPointToWorldRay(down).GetPoint(a));
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var moved=Handles.PositionHandle(World(pivot+liftedDelta),Active.transform.rotation);
                bool changed=EditorGUI.EndChangeCheck();
                if(!changed && liftedPoints==null)return false;
                target=changed ? Active.transform.InverseTransformPoint(moved) : pivot+liftedDelta;
            }
            liftedPoints=points;liftedOrigin=pivot;liftedView=view;
            // Snap the displacement, not an off-grid starting location. Y-only drags must
            // never jump sideways to the grid, and explicit height changes are preserved.
            var displacement=target-pivot;
            var flat=PlanSnapping.FloorPoint(Flat(displacement),Vector2.zero,e.shift,e.alt,PlanSnapping.Grid);
            liftedDelta=Local(flat,displacement.y);
            bool released=direct ? e.rawType==EventType.MouseUp : before!=0 && GUIUtility.hotControl==0;
            if(released)
            {
                var delta=liftedDelta;
                if(delta.sqrMagnitude>1e-10f)
                    ChangePaths("Move "+PathName+" selection",g=>{
                        foreach(int id in points)
                        {
                            var node=g.Node(id); var desired=node.position+delta;
                            if(Mathf.Abs(delta.y)<1e-5f && Active is ProceduralMarking marking && marking.attachToSurface &&
                                MarkingPlacement.Surface(marking,desired,out var fitted))desired=fitted;
                            node.position=desired;
                        }
                    });
                CancelElevatedMove();
                if(direct){GUIUtility.hotControl=selectionControl=0;pointerView=null;pressedHit=default;}
            }
            if(direct && e.type!=EventType.Used)e.Use();
            if(e.type!=EventType.Repaint)view.Repaint();
            return true;
        }
    }
}
