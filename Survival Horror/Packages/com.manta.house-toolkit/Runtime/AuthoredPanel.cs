using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    // Whole bays use the FBX mesh directly. Only cropped bays lease a shared variant.
    [ExecuteAlways, RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
    public sealed class AuthoredPanel : MonoBehaviour
    {
        [HideInInspector] public Mesh source;
        [HideInInspector] public Vector3 limits; // left, right, top in source space
        [HideInInspector] public Vector2 glazing;
        SharedGeometry lease;
        void OnEnable()=>Refresh();
        void OnDisable()=>SharedGeometry.Release(ref lease);
        public void Refresh()
        {
            if(!source)return;
            var b=source.bounds;
            Mesh result;
            if(limits.x<=b.min.x+.00001f && limits.y>=b.max.x-.00001f && limits.z>=b.max.y-.00001f &&
                (source.subMeshCount<2 || (glazing.x<=b.min.x+.00001f && glazing.y>=b.max.x-.00001f)))
            {
                SharedGeometry.Release(ref lease);result=source;
            }
            else
            {
                var key=Hash128.Compute(source.GetInstanceID());key.Append(ref limits);key.Append(ref glazing);
                result=SharedGeometry.Use(ref lease,key,Crop);
            }
            GetComponent<MeshFilter>().sharedMesh=result;
            GetComponent<MeshCollider>().sharedMesh=result.vertexCount>0 ? result : null;
        }
        struct Vertex
        {
            public Vector3 p,n; public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),
                n=Vector3.Lerp(a.n,b.n,t),uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        Mesh Crop()
        {
            var positions=source.vertices;var normals=source.normals;var uv=source.uv;
            var verts=new List<Vector3>();var ns=new List<Vector3>();var uvs=new List<Vector2>();
            var slots=new[]{new List<int>(),new List<int>()};
            var polygon=new List<Vertex>(12);var scratch=new List<Vertex>(12);
            Vertex Read(int i)=>new Vertex{p=positions[i],n=normals.Length==positions.Length?normals[i]:Vector3.zero,
                uv=uv.Length==positions.Length?uv[i]:Vector2.zero};
            void Clip(int axis,float edge,int sign)
            {
                if(polygon.Count==0)return;
                scratch.Clear();var a=polygon[polygon.Count-1];float da=(a.p[axis]-edge)*sign;
                foreach(var v in polygon)
                {
                    float dv=(v.p[axis]-edge)*sign;
                    if((da<0)!=(dv<0))scratch.Add(Vertex.Lerp(a,v,da/(da-dv)));
                    if(dv>=0)scratch.Add(v);
                    a=v;da=dv;
                }
                var tmp=polygon;polygon=scratch;scratch=tmp;
            }
            void Emit(Vertex a,Vertex b,Vertex c,int slot,float left,float right)
            {
                if(right-left<.000001f)return;
                polygon.Clear();polygon.Add(a);polygon.Add(b);polygon.Add(c);
                Clip(0,left,1);Clip(0,right,-1);Clip(1,limits.z,-1);
                if(polygon.Count<3)return;
                int start=verts.Count;
                foreach(var v in polygon){verts.Add(v.p);ns.Add(v.n.normalized);uvs.Add(v.uv);}
                for(int i=1;i<polygon.Count-1;i++)
                {
                    if(Vector3.Cross(polygon[i].p-polygon[0].p,polygon[i+1].p-polygon[0].p).sqrMagnitude<1e-18f)continue;
                    slots[slot].Add(start);slots[slot].Add(start+i);slots[slot].Add(start+i+1);
                }
            }
            for(int slot=0;slot<source.subMeshCount;slot++)
            {
                var indices=source.GetTriangles(slot);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=Read(indices[i]);var b=Read(indices[i+1]);var c=Read(indices[i+2]);
                    if(slot==0 || glazing.x>=glazing.y)Emit(a,b,c,0,limits.x,limits.y);
                    else
                    {
                        Emit(a,b,c,0,limits.x,glazing.x);
                        Emit(a,b,c,1,glazing.x,glazing.y);
                        Emit(a,b,c,0,glazing.y,limits.y);
                    }
                }
            }
            var mesh=new Mesh{name=source.name+" (cropped)"};
            if(verts.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);mesh.SetUVs(0,uvs);mesh.SetNormals(ns);mesh.subMeshCount=source.subMeshCount;
            for(int i=0;i<mesh.subMeshCount;i++)mesh.SetTriangles(slots[i],i);
            mesh.RecalculateBounds();if(normals.Length==0)mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }
    }
}
