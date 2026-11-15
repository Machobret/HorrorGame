using System;
using System.Linq;
using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class HouseSlab : MonoBehaviour
    {
        [HideInInspector] public bool overhead;
        [HideInInspector] public Vector3[] vertices;
        [HideInInspector] public int[] triangles, faceSlots;
        [HideInInspector] public int slots;
        SharedGeometry geometry;

        void OnEnable() => Refresh();
        void OnDisable() => SharedGeometry.Release(ref geometry);
        public void Refresh()
        {
            var filter=GetComponent<MeshFilter>(); var collider=GetComponent<MeshCollider>();
            if (vertices==null || vertices.Length==0)
            {
                filter.sharedMesh=collider.sharedMesh=null;
                SharedGeometry.Release(ref geometry);
                return;
            }
            var key=Hash128.Compute("House slab");
            key.Append(vertices); key.Append(triangles); key.Append(faceSlots); key.Append(slots);
            var mesh=SharedGeometry.Use(ref geometry,key,Build);
            filter.sharedMesh=mesh;
            if(collider.sharedMesh!=mesh) collider.sharedMesh=mesh;
        }
        Mesh Build()
        {
            var mesh=new Mesh {name="House slab"};
            if(vertices.Length>65535) mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices=vertices;
            mesh.subMeshCount=slots;
            for(int s=0;s<slots;s++)
                mesh.SetTriangles(Enumerable.Range(0,faceSlots.Length).Where(i=>faceSlots[i]==s)
                    .SelectMany(i=>new[]{triangles[i*3],triangles[i*3+1],triangles[i*3+2]}).ToArray(),s);
            mesh.RecalculateNormals();
            var normals=mesh.normals;
            var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++)
            {
                var p=vertices[i]; var n=normals[i];
                uv[i]=Mathf.Abs(n.y)>.5f ? new Vector2(p.x,p.z) : new Vector2(p.x*n.z-p.z*n.x,p.y);
            }
            mesh.uv=uv; mesh.RecalculateTangents();return mesh;
        }
    }
}
