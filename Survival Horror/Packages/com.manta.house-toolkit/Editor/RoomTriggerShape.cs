using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // A vertical room prism contains a convex trigger iff it contains its height interval
    // and its horizontal projection. A capsule projects to a segment swept by a disk.
    internal sealed class RoomTriggerShape
    {
        readonly Vector3[] world;
        readonly float radius;
        readonly bool emitter;
        const float Epsilon = .0001f;

        RoomTriggerShape(Vector3[] vertices, float radius = 0, bool emitter = false)
        { world = vertices; this.radius = radius; this.emitter = emitter; }

        internal static RoomTriggerShape Emitter(Vector3 position) =>
            new RoomTriggerShape(new[] {position}, emitter:true);

        internal static RoomTriggerShape From(Collider collider)
        {
            var t = collider.transform;
            Vector3 scale = t.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (collider is BoxCollider box)
            {
                var corners = new Vector3[8];
                for (int i=0; i<8; i++)
                {
                    var signs = new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1);
                    corners[i] = t.TransformPoint(box.center) + t.rotation * Vector3.Scale(Vector3.Scale(signs, box.size*.5f), scale);
                }
                return new RoomTriggerShape(corners);
            }
            if (collider is SphereCollider sphere)
                return new RoomTriggerShape(new[] {t.TransformPoint(sphere.center)}, sphere.radius * Mathf.Max(scale.x,scale.y,scale.z));
            if (collider is CapsuleCollider capsule)
            {
                int axis = capsule.direction;
                float r = capsule.radius * Mathf.Max(scale[(axis+1)%3],scale[(axis+2)%3]);
                Vector3 end = Vector3.zero;
                end[axis] = Mathf.Max(0,capsule.height * scale[axis] * .5f-r);
                end = t.rotation * end;
                var center = t.TransformPoint(capsule.center);
                return new RoomTriggerShape(new[] {center-end,center+end}, r);
            }
            if (collider is MeshCollider mesh && mesh.convex && mesh.sharedMesh && mesh.sharedMesh.vertexCount > 0)
            {
                // Read importer data in the editor, including meshes with Read/Write disabled.
                using var data = MeshUtility.AcquireReadOnlyMeshData(mesh.sharedMesh);
                using var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                data[0].GetVertices(vertices);
                var points = vertices.ToArray();
                for (int i=0; i<points.Length; i++) points[i] = t.TransformPoint(points[i]);
                return new RoomTriggerShape(points);
            }
            return null;
        }

        internal sealed class Projection
        {
            internal Vector2[] hull;
            internal float low, high, radius;
            internal bool emitter;
        }

        internal Bounds WorldBounds()
        {
            var bounds = new Bounds(world[0], Vector3.zero);
            foreach (var point in world) bounds.Encapsulate(point);
            bounds.Expand(radius * 2);
            return bounds;
        }

        internal Projection Project(HouseFloor floor)
        {
            var inverse = Quaternion.Inverse(floor.transform.rotation);
            var points = new Vector2[world.Length];
            float low=float.PositiveInfinity, high=float.NegativeInfinity;
            for (int i=0; i<world.Length; i++)
            {
                var p = inverse * (world[i]-floor.transform.position);
                low=Mathf.Min(low,p.y); high=Mathf.Max(high,p.y);
                points[i]=new Vector2(p.x,p.z);
            }
            return new Projection {hull=Hull(points), low=low-radius, high=high+radius, radius=radius, emitter=emitter};
        }

        static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;

        static Vector2[] Hull(Vector2[] points)
        {
            var sorted = points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
            if (sorted.Length<3) return sorted;
            var hull = new List<Vector2>();
            void Append(Vector2 p, int start)
            {
                while (hull.Count>=start+2 && Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)
                    hull.RemoveAt(hull.Count-1);
                hull.Add(p);
            }
            foreach (var p in sorted) Append(p,0);
            int start=hull.Count-1;
            for (int i=sorted.Length-2; i>=0; i--) Append(sorted[i],start);
            hull.RemoveAt(hull.Count-1);
            return hull.ToArray();
        }

        static float Distance(Vector2 p,Vector2 a,Vector2 b)
        {
            var ab=b-a;
            float length=ab.sqrMagnitude;
            return Vector2.Distance(p,a+ab*(length>0 ? Mathf.Clamp01(Vector2.Dot(p-a,ab)/length) : 0));
        }

        // Clips a room boundary edge against the strict interior of the convex footprint.
        // This also catches a concave notch whose two ends fall outside the trigger.
        static bool CutsInterior(Vector2[] hull, Vector2 a, Vector2 b)
        {
            if (hull.Length<3) return false;
            float enter=0, leave=1;
            for(int i=0,j=hull.Length-1;i<hull.Length;j=i++)
            {
                var edge=hull[i]-hull[j];
                float limit=Epsilon*edge.magnitude;
                float from=Cross(edge,a-hull[j])-limit, to=Cross(edge,b-hull[j])-limit;
                if (from<0 && to<0) return false;
                if (from<0) enter=Mathf.Max(enter,from/(from-to));
                else if (to<0) leave=Mathf.Min(leave,from/(from-to));
                if (enter>leave) return false;
            }
            return true;
        }

        internal static void Compare(Projection shape, Vector2[] room, float height, out bool touches, out bool fits)
        {
            touches=fits=false;
            if(shape.high < -Epsilon || shape.low > height+Epsilon) return;
            fits=shape.low >= -Epsilon && shape.high <= height+Epsilon;
            var hull=shape.hull;
            foreach(var p in hull)
            {
                bool inside=RoomGeometry.Inside(p,room);
                float clearance=float.PositiveInfinity;
                for(int i=0,j=room.Length-1;i<room.Length;j=i++)
                    clearance=Mathf.Min(clearance,Distance(p,room[j],room[i]));
                touches |= inside || clearance<=shape.radius+Epsilon;
                fits &= (inside || clearance<=Epsilon) && (shape.emitter ?
                    clearance>Epsilon : clearance+Epsilon>=shape.radius);
            }
            for(int i=0,j=room.Length-1;i<room.Length;j=i++)
            {
                var a=room[j]; var b=room[i];
                if(CutsInterior(hull,a,b)) { touches=true; fits=false; }
                if(hull.Length!=2) continue;
                // Both ends of a capsule can lie inside a concave room while its middle crosses a wall.
                var c=hull[0]; var d=hull[1];
                bool cross=Cross(b-a,c-a)*Cross(b-a,d-a)<0 && Cross(d-c,a-c)*Cross(d-c,b-c)<0;
                float gap=cross ? 0 : Mathf.Min(Distance(a,c,d),Distance(b,c,d),Distance(c,a,b),Distance(d,a,b));
                touches |= gap<=shape.radius+Epsilon;
                if(cross || gap+Epsilon<shape.radius) fits=false;
            }
        }
    }
}
