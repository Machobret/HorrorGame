using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class CornerJoint : MonoBehaviour
    {
        [HideInInspector] public int corner;
        // Floor-local metres, also used to read joints saved by the original toolkit.
        [HideInInspector] public Vector2[] profile;
        [HideInInspector] public float height;
        [HideInInspector] public Mesh post;
        [HideInInspector] public Vector2 postDirection;
        [HideInInspector] public float postWidth;
        SharedGeometry geometry;

        void OnEnable() => Refresh();
        void OnDisable() => SharedGeometry.Release(ref geometry);

        public void Refresh()
        {
            Mesh mesh;
            if (post && postWidth > 0)
            {
                SharedGeometry.Release(ref geometry);
                var size = post.bounds.size;
                transform.localRotation = Rotation(postDirection);
                transform.localScale = new Vector3(postWidth / size.x, height / size.y, postWidth / size.z);
                mesh = post;
            }
            else
            {
                if (profile == null || profile.Length < 3) return;
                float radius = 0;
                foreach (var point in profile) radius = Mathf.Max(radius, point.magnitude);
                if (radius <= 0) return;
                // Choose a canonical cyclic ordering independent of drawing direction or house yaw.
                // Quantize at 1/100000 of the radius to absorb rotation roundoff.
                Vector2Int[] best = null;
                Vector2 axis = Vector2.right;
                for (int i = 0; i < profile.Length; i++)
                {
                    if (profile[i].sqrMagnitude < radius * radius * .99998f) continue;
                    var x = profile[i].normalized;
                    var z = new Vector2(-x.y, x.x);
                    var candidate = new Vector2Int[profile.Length];
                    for (int j = 0; j < candidate.Length; j++)
                    {
                        var p = profile[(i + j) % profile.Length];
                        candidate[j] = Vector2Int.RoundToInt(new Vector2(Vector2.Dot(p,x), Vector2.Dot(p,z)) * (100000f / radius));
                    }
                    if (best == null || Compare(candidate, best) < 0) { best = candidate; axis = x; }
                }
                mesh = SharedGeometry.Use(ref geometry, Hash128.Compute(best), () => Prism(best));
                transform.localRotation = Rotation(axis);
                transform.localScale = new Vector3(radius, height, radius);
            }
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = GetComponent<MeshCollider>();
            if (collider.sharedMesh != mesh) collider.sharedMesh = mesh;
        }

        static Quaternion Rotation(Vector2 axis) => Quaternion.Euler(0, -Mathf.Atan2(axis.y,axis.x) * Mathf.Rad2Deg, 0);
        static int Compare(Vector2Int[] a, Vector2Int[] b)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                int x = a[i].x.CompareTo(b[i].x);
                if (x != 0) return x;
                int y = a[i].y.CompareTo(b[i].y);
                if (y != 0) return y;
            }
            return 0;
        }

        static Mesh Prism(Vector2Int[] outline)
        {
            int count = outline.Length;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 Base(int i) => new Vector3(outline[i].x, 0, outline[i].y) / 100000f;
            void Vertex(Vector3 p, Vector2 tex) { vertices.Add(p); uv.Add(tex); }
            for (int i = 0; i < count; ++i)
            {
                var a = Base(i); var b = Base((i+1)%count);
                int start = vertices.Count;
                float width = Vector3.Distance(a,b);
                Vertex(a,Vector2.zero); Vertex(a+Vector3.up,Vector2.up);
                Vertex(b+Vector3.up,new Vector2(width,1)); Vertex(b,new Vector2(width,0));
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            int bottom = vertices.Count;
            for (int level = 0; level < 2; level++)
                for (int i = 0; i < count; i++)
                {
                    var p = Base(i); Vertex(p + Vector3.up*level,new Vector2(p.x,p.z));
                }
            for (int i = 1; i < count-1; ++i)
                triangles.AddRange(new[]{bottom,bottom+i,bottom+i+1,bottom+count,bottom+count+i+1,bottom+count+i});
            var result = new Mesh {name = "Wall corner filler"};
            result.SetVertices(vertices); result.SetUVs(0,uv); result.SetTriangles(triangles,0);
            result.RecalculateNormals(); result.RecalculateTangents();
            return result;
        }
    }
}
