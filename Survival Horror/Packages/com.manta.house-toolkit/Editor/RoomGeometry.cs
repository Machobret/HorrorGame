using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class RoomGeometry
    {
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        internal static float Area(Vector2[] p)
        {
            float sum=0; for(int i=1;i<p.Length-1;i++)sum+=Cross(p[i]-p[0],p[i+1]-p[0]);
            return sum*.5f;
        }
        internal static bool Inside(Vector2 q,Vector2[] p)
        {
            bool inside=false;
            for(int i=0,j=p.Length-1;i<p.Length;j=i++)
                if((p[i].y>q.y)!=(p[j].y>q.y) && q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;
            return inside;
        }
        internal static bool Nested(Vector2[] a,Vector2[] b)
        {
            if(a.Any(p=>b.Contains(p)))return false;
            return Inside(a[0],b)||Inside(b[0],a);
        }

        internal static int[] Triangles(Vector2[] p)
        {
            var ring=Enumerable.Range(0,p.Length).ToList();
            var triangles=new List<int>();
            while(ring.Count>2)
            {
                bool clipped=false;
                for(int k=0;k<ring.Count;k++)
                {
                    int a=ring[(k+ring.Count-1)%ring.Count],b=ring[k],c=ring[(k+1)%ring.Count];
                    float turn=Cross(p[b]-p[a],p[c]-p[a]);
                    if(Mathf.Abs(turn)<1e-8f){ring.RemoveAt(k);clipped=true;break;}
                    if(turn<0 || ring.Any(i=>i!=a && i!=b && i!=c && Cross(p[b]-p[a],p[i]-p[a])>=-1e-8f &&
                        Cross(p[c]-p[b],p[i]-p[b])>=-1e-8f && Cross(p[a]-p[c],p[i]-p[c])>=-1e-8f))continue;
                    triangles.AddRange(new[]{a,b,c});ring.RemoveAt(k);clipped=true;break;
                }
                if(!clipped)return null;
            }
            return triangles.ToArray();
        }

        internal static void Publish(HouseFloor floor,List<int[]> loops,Dictionary<int,Vector2> points,string action)
        {
            var remaining=floor.GetComponentsInChildren<HouseRoom>(true).ToList();
            var contents = remaining.SelectMany(r => r.transform.Cast<Transform>())
                .SelectMany(child => child.GetComponent<RoomContents>() ? child.Cast<Transform>() : new[] { child })
                .Concat(floor.transform.Cast<Transform>().Where(t => t.GetComponent<RoomContents>())
                    .SelectMany(t => t.Cast<Transform>())).ToArray();
            // Exact matches get first choice; a split keeps the old identity on the best surviving boundary.
            foreach(var loop in loops.OrderByDescending(l=>remaining.Any(r=>r.corners!=null && r.corners.Length==l.Length && !l.Except(r.corners).Any())).ThenByDescending(l=>l.Length))
            {
                var polygon=loop.Select(i=>points[i]).ToArray();
                var room=remaining.Where(r=>r.corners!=null && r.corners.Intersect(loop).Count()>=2)
                    .OrderByDescending(r=>r.corners.Intersect(loop).Count()).FirstOrDefault();
                if(!room)
                {
                    var go=new GameObject("Room");
                    go.transform.SetParent(floor.transform,false);room=go.AddComponent<HouseRoom>();
                    room.roomId=Guid.NewGuid().ToString("N");Undo.RegisterCreatedObjectUndo(go,action);
                }
                remaining.Remove(room);
                Undo.RecordObject(room,action);room.corners=loop;room.polygon=polygon;room.topTriangles=Triangles(polygon);
                EditorUtility.SetDirty(room);
            }
            foreach(var old in remaining)
            {
                // Keep any manually nested scene content when a room merges or opens.
                foreach (Transform child in old.transform.Cast<Transform>().ToArray())
                {
                    if (child.GetComponent<RoomContents>())
                    {
                        foreach (var prop in child.Cast<Transform>().ToArray())
                            Undo.SetTransformParent(prop, floor.transform, action);
                    }
                    else Undo.SetTransformParent(child, floor.transform, action);
                }
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            RoomAssignment.Recheck(floor, contents, action);
            SlabBuilder.Rebuild(floor.House);
        }
    }
}
