using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class PlanSnapping
    {
        internal static float Grid { get=>EditorPrefs.GetFloat("HouseToolkit.Grid",.25f); set { if(value!=Grid)EditorPrefs.SetFloat("HouseToolkit.Grid",Mathf.Max(0,value)); } }

        internal static Vector2 Project(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta=b-a;
            return a+delta*Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(1e-10f,delta.sqrMagnitude));
        }

        internal static Vector2 Direction(Vector2 delta)
        {
            const float threshold=.41421356237f; // tan(22.5 degrees)
            float x=Mathf.Abs(delta.x), y=Mathf.Abs(delta.y);
            if(y<=threshold*x)return new Vector2(Mathf.Sign(delta.x),0);
            if(x<=threshold*y)return new Vector2(0,Mathf.Sign(delta.y));
            return new Vector2(Mathf.Sign(delta.x),Mathf.Sign(delta.y));
        }

        internal static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

        // A sloping or offset wall may pass between grid nodes. Its intersections
        // with either family of grid lines are still exact attachment points.
        internal static Vector2 GridPointOnWall(Vector2 foot, Vector2 a, Vector2 b, float grid)
        {
            if (grid <= 0) return foot;
            var best = foot;
            float nearest = float.PositiveInfinity;
            CheckAxis(a.x, b.x, foot.x);
            CheckAxis(a.y, b.y, foot.y);
            return best;

            void CheckAxis(float start, float end, float coordinate)
            {
                if (start == end) return;
                float first = Mathf.Ceil(Mathf.Min(start, end) / grid);
                float last = Mathf.Floor(Mathf.Max(start, end) / grid);
                if (first > last) return;
                float line = Mathf.Clamp(Mathf.Round(coordinate / grid), first, last) * grid;
                var crossing = Vector2.Lerp(a, b, (line - start) / (end - start));
                float distance = (crossing - foot).sqrMagnitude;
                if (distance >= nearest) return;
                best = crossing;
                nearest = distance;
            }
        }

        internal static Vector2 FloorPoint(Vector2 raw,Vector2? from,bool constrain,bool bypass,float grid)
        {
            if(bypass)return raw;
            if(constrain && from.HasValue)
            {
                var origin=from.Value;var delta=raw-origin;var dir=Direction(delta);
                float step=Vector2.Dot(delta,dir)/dir.sqrMagnitude;
                if(grid>0)
                {
                    if(dir.y==0)return new Vector2(Mathf.Round(raw.x/grid)*grid,origin.y);
                    if(dir.x==0)return new Vector2(origin.x,Mathf.Round(raw.y/grid)*grid);
                    step=Mathf.Round(step/grid)*grid;
                }
                return origin+dir*step;
            }
            return grid>0 ? new Vector2(Mathf.Round(raw.x/grid)*grid,Mathf.Round(raw.y/grid)*grid) : raw;
        }

        internal static Vector2 Snap(Vector2 p, bool free, Vector2? axis, float grid,
            IEnumerable<Vector2> nodes, IEnumerable<(Vector2 a, Vector2 b)> segments, Func<Vector2,Vector2,float> screenDistance)
        {
            if(free)return p;
            var raw=p;
            var direction=axis.HasValue ? Direction(raw-axis.Value) : Vector2.zero;
            p=FloorPoint(raw,axis,axis.HasValue,false,0);
            var best=p;float distance=12;
            bool snapped=false;
            bool OnRay(Vector2 q) => !axis.HasValue ||
                (Mathf.Abs(Cross(q-axis.Value,direction))<=.0001f && Vector2.Dot(q-axis.Value,direction)>=-.0001f);
            bool Consider(Vector2 q)
            {
                if(!OnRay(q))return false;
                float d=screenDistance(q,raw);
                if(d>=distance)return false;
                best=q;distance=d;snapped=true;
                return true;
            }
            foreach(var node in nodes) Consider(node);
            if(snapped)return best;
            (Vector2 a, Vector2 b)? gridEdge=null;
            foreach(var segment in segments)
            {
                var (a,b)=segment;
                var q=Project(p,a,b);
                bool fixedIntersection=false;
                if(axis.HasValue)
                {
                    float denominator=Cross(b-a,direction);
                    if(Mathf.Abs(denominator)>.00001f)
                    {
                        float t=Cross(axis.Value-a,direction)/denominator;
                        if(t<0 || t>1)continue;
                        q=Vector2.Lerp(a,b,t);
                        fixedIntersection=true;
                    }
                }
                // Capture distance is measured to the wall, even at coarse grid settings.
                if(Consider(q))gridEdge=fixedIntersection ? null : segment;
            }
            if(gridEdge.HasValue)
            {
                var crossing=GridPointOnWall(best,gridEdge.Value.a,gridEdge.Value.b,grid);
                if(OnRay(crossing))return crossing;
            }
            if(snapped)return best;
            return FloorPoint(raw,axis,axis.HasValue,false,grid);
        }
    }
}
