using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class MarkingPlacement
    {
        static bool Architecture(Collider collider) => collider.GetComponentInParent<HouseWall>() || collider.GetComponentInParent<InsertAttachment>();
        static bool Supporting(RaycastHit hit, Vector3 up)
        {
            var collider=hit.collider;
            return Vector3.Dot(hit.normal,up)>.3f && !SceneVisibilityManager.instance.IsHidden(collider.gameObject) &&
                !Architecture(collider) &&
                !collider.GetComponentInParent<FixtureAttachment>() && !collider.GetComponentInParent<ProceduralShelf>();
        }

        internal static bool Cursor(ProceduralMarking marking,Ray ray,out Vector3 local)
        {
            bool found=Cursor(ray,marking.transform.up,out var world);
            local=marking.transform.InverseTransformPoint(world);return found;
        }

        internal static bool Cursor(Ray ray,Vector3 up,out Vector3 world)
        {
            world=default;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(ray,Mathf.Infinity,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.distance>=nearest || !Supporting(hit,up))continue;
                nearest=hit.distance;world=hit.point;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        internal static bool Surface(ProceduralMarking marking, Vector3 local, out Vector3 fitted)
        {
            var up = marking.transform.up;
            var point = marking.transform.TransformPoint(local);
            fitted = local;
            float nearest=float.PositiveInfinity;
            // Examine all supports, not just the first collider. Door leaves, wall caps,
            // frames and fixtures are obstacles above the paint, never its support.
            foreach(var hit in Physics.RaycastAll(point+up*.75f,-up,1.5f,~0,QueryTriggerInteraction.Ignore))
            {
                if(!Supporting(hit,up))continue;
                var candidate=marking.transform.InverseTransformPoint(hit.point);
                float distance=Mathf.Abs(candidate.y-local.y);
                if(distance>=nearest)continue;
                nearest=distance;fitted=candidate;
            }
            if(!float.IsPositiveInfinity(nearest))return true;
            // Room slabs can stop at wall/door boundaries. Preserve the intended floor
            // plane beneath that architecture instead of requiring a slab inside the wall.
            foreach(var collider in Physics.OverlapSphere(point+up*.01f,.02f,~0,QueryTriggerInteraction.Ignore))
                if(Architecture(collider))return true;
            return false;
        }

        // Surface fitting is authoring-only. Explicit vertical moves remain under the artist's
        // control. Clip only unsupported stretches, preserving the rest in the editable graph.
        internal static List<(Vector3 a,Vector3 b)> Stretches(ProceduralMarking marking,Vector3 a,Vector3 b)
        {
            Physics.SyncTransforms();
            var result=new List<(Vector3,Vector3)>();
            float length=Vector3.Distance(a,b);
            if(length<.001f)return result;
            var side=Vector3.Cross(Vector3.up,b-a).normalized*(marking.PathWidth*.475f);
            bool At(float t)
            {
                var point=Vector3.Lerp(a,b,t);
                for(int across=-1;across<=1;across++)
                    if(!Surface(marking,point+side*across,out var fitted) || Mathf.Abs(fitted.y-point.y)>.025f)return false;
                return true;
            }
            int count=Mathf.Clamp(Mathf.CeilToInt(length/.05f),1,16384);
            bool previous=At(0);float start=0;
            for(int i=1;i<=count;i++)
            {
                float t=i/(float)count;bool valid=At(t);
                if(valid!=previous)
                {
                    float lo=(i-1)/(float)count,hi=t;
                    for(int step=0;step<8;step++)
                    {
                        float mid=(lo+hi)*.5f;if(At(mid)==previous)lo=mid;else hi=mid;
                    }
                    if(previous && (lo-start)*length>.005f)result.Add((Vector3.Lerp(a,b,start),Vector3.Lerp(a,b,lo)));
                    if(valid)start=hi;
                }
                if(i==count && valid && (t-start)*length>.005f)result.Add((Vector3.Lerp(a,b,start),b));
                previous=valid;
            }
            return result;
        }

        internal static void Refit(ProceduralMarking marking)
        {
            RoadEditing.Change(marking,"Refit marking to surface",graph => {
                Physics.SyncTransforms();
                foreach (var node in graph.nodes)
                {
                    if (Surface(marking,node.position,out var point))node.position = point;
                }
                foreach (var span in graph.spans.ToArray())
                {
                    var stretches=Stretches(marking,graph.Node(span.start.node).position,graph.Node(span.end.node).position);
                    graph.spans.Remove(span);
                    for(int i=0;i<stretches.Count;i++)
                    {
                        int a=RoadEditing.Point(graph,stretches[i].a).id,b=RoadEditing.Point(graph,stretches[i].b).id;
                        if(i==0){span.start.node=a;span.end.node=b;graph.spans.Add(span);}
                        else graph.Connect(a,b,span.run,null);
                    }
                }
            },out RoadScene.Message,true);
            RoadScene.Repaint();
        }

        internal static bool Pick(ProceduralMarking marking, Ray worldRay, out RoadHit hit, out Vector3 point, out float distance)
        {
            hit = default; point = default; distance = float.PositiveInfinity;
            var ray = new Ray(marking.transform.InverseTransformPoint(worldRay.origin), marking.transform.InverseTransformVector(worldRay.direction));
            for (int i=0; i<marking.Patches.Count; i++)
            {
                var q = marking.Patches[i];
                Check(q[0],q[3],q[2],i,ray,ref hit,ref distance,marking);
                Check(q[0],q[2],q[1],i,ray,ref hit,ref distance,marking);
            }
            if (hit.part == RoadPart.None) return false;
            point = ray.GetPoint(distance); return true;
        }

        static void Check(Vector3 a,Vector3 b,Vector3 c,int index,Ray ray,ref RoadHit hit,ref float nearest,ProceduralMarking marking)
        {
            var ab=b-a; var ac=c-a; var p=Vector3.Cross(ray.direction,ac); float determinant=Vector3.Dot(ab,p);
            if (Mathf.Abs(determinant)<1e-8f) return;
            var t=ray.origin-a; float u=Vector3.Dot(t,p)/determinant;
            if(u<0 || u>1)return;
            var q=Vector3.Cross(t,ab); float v=Vector3.Dot(ray.direction,q)/determinant;
            if(v<0 || u+v>1)return;
            float d=Vector3.Dot(ac,q)/determinant;
            if(d<0 || d>=nearest)return;
            nearest=d;hit=new RoadHit(RoadPart.Path,marking.paths.spans[index].id);
        }
    }

    // Shader-deformed strips need matching editor picking, without adding runtime colliders.
    [InitializeOnLoad]
    internal static class MarkingPicking
    {
        static readonly List<ProceduralMarking> visible = new List<ProceduralMarking>();
        static bool indexed;
        static MarkingPicking()
        {
            EditorApplication.hierarchyChanged += () => {visible.Clear();indexed=false;};
            SceneView.duringSceneGui += OnScene;
        }
        static void OnScene(SceneView view)
        {
            if (RoadScene.Active || HouseScene.Floor || EnvironmentPlacementTool.Pending || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var e=Event.current;
            if(e.alt || e.button==1 || Tools.viewToolActive)return;
            if(!indexed){visible.AddRange(UnityEngine.Object.FindObjectsByType<ProceduralMarking>(FindObjectsSortMode.None));indexed=true;}
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            ProceduralMarking best=null;float nearest=float.PositiveInfinity;
            foreach(var marking in visible)
                if(marking && marking.isActiveAndEnabled && !SceneVisibilityManager.instance.IsHidden(marking.gameObject) &&
                    !SceneVisibilityManager.instance.IsPickingDisabled(marking.gameObject) &&
                    MarkingPlacement.Pick(marking,ray,out _,out _,out float d) && d<nearest){best=marking;nearest=d;}
            if(!best)return;
            if(Physics.Raycast(ray,out var obstruction,nearest-.01f,~0,QueryTriggerInteraction.Ignore))return;
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddControl(control,0);
            if(e.type==EventType.MouseDown && e.button==0 && GUIUtility.hotControl==0 && HandleUtility.nearestControl==control)
            {
                Selection.activeGameObject=best.gameObject;
                if(e.clickCount==2)RoadScene.Enter(best);
                e.Use();
            }
        }
    }
}
