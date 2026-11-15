using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // Locks are measured in Scene-view points, independent of plan scale and rotation.
    internal sealed class DrawingGuides
    {
        const float capture=8, release=15;
        Vector2? xSource,ySource,origin;
        (Vector2 a,Vector2 b)? parallel;

        internal void Clear() { xSource=ySource=origin=null; parallel=null; }

        internal Vector2 Align(Vector2 raw,Vector2 from,Vector2? shiftDirection,float grid,
            IEnumerable<Vector2> nodes,IEnumerable<(Vector2 a,Vector2 b)> segments,Func<Vector2,Vector2,float> pixels)
        {
            if(origin!=from)Clear();
            origin=from;
            var ray=shiftDirection;
            var delta=raw-from;
            var previous=parallel;
            parallel=null;
            if(!ray.HasValue && pixels(raw,from)>release)
            {
                float best=capture;
                foreach(var edge in segments)
                {
                    var d=(edge.b-edge.a).normalized;
                    if(Mathf.Abs(d.x*d.y)<.0001f)continue; // Local axis guides cover these.
                    var projected=from+d*Vector2.Dot(delta,d);
                    float distance=pixels(projected,raw);
                    bool held=previous.HasValue && previous.Value==edge && distance<release;
                    if(!held && distance>=best)continue;
                    ray=d;parallel=edge;best=distance;
                    if(held)break;
                }
            }

            Vector2 aim=ray.HasValue ? from+ray.Value*(Vector2.Dot(delta,ray.Value)/ray.Value.sqrMagnitude) : raw;
            var fallback=PlanSnapping.FloorPoint(raw,from,shiftDirection.HasValue,false,grid);
            if(parallel.HasValue)
            {
                var d=ray.Value;
                int axis=Mathf.Abs(d.x)>=Mathf.Abs(d.y)?0:1;
                fallback=grid>0 ? aim+d*((Mathf.Round(aim[axis]/grid)*grid-aim[axis])/d[axis]) : aim;
            }
            var lastX=xSource;var lastY=ySource;
            xSource=ySource=null;
            float xDistance=float.PositiveInfinity,yDistance=float.PositiveInfinity;
            void Consider(Vector2 source,int axis,Vector2? last,ref Vector2? chosen,ref float best)
            {
                float offset=source[axis]-aim[axis];
                Vector2 crossing=aim;
                if(ray.HasValue)
                {
                    if(Mathf.Abs(ray.Value[axis])<.0001f)return;
                    crossing+=ray.Value*(offset/ray.Value[axis]);
                    if(Vector2.Dot(crossing-from,aim-from)<0)return;
                }
                else crossing[axis]=source[axis];
                float distance=pixels(crossing,aim);
                bool held=last.HasValue && last.Value[axis]==source[axis] && distance<release;
                if(distance>=capture && !held)return;
                float priority=held ? distance-release : distance;
                if(priority>=best)return;
                best=priority;chosen=source;
            }
            void Visit(Vector2 source)
            {
                Consider(source,0,lastX,ref xSource,ref xDistance);
                Consider(source,1,lastY,ref ySource,ref yDistance);
            }
            Visit(from);
            foreach(var node in nodes)Visit(node);
            if(ray.HasValue && (xSource.HasValue || ySource.HasValue))
            {
                int axis=xDistance<=yDistance?0:1;
                var source=axis==0?xSource.Value:ySource.Value;
                if(axis==0)ySource=null;else xSource=null;
                var crossing=from+ray.Value*((source[axis]-from[axis])/ray.Value[axis]);
                crossing[axis]=source[axis];
                return crossing;
            }
            return new Vector2(xSource?.x??fallback.x,ySource?.y??fallback.y);
        }

        internal void Draw(Vector2 point,Func<Vector2,Vector3> world)
        {
            if(Event.current.type!=EventType.Repaint)return;
            using(new Handles.DrawingScope(new Color(.4f,.83f,1,.85f)))
            {
                if(xSource.HasValue)Handles.DrawDottedLine(world(xSource.Value),world(point),4);
                if(ySource.HasValue)Handles.DrawDottedLine(world(ySource.Value),world(point),4);
                if(parallel.HasValue)Handles.DrawAAPolyLine(2,world(parallel.Value.a),world(parallel.Value.b));
            }
        }
    }
}