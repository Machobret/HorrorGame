// Compile only in the isolated full-project snapshot prepared by prefab_collision.py.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Object=UnityEngine.Object;

public static class PrefabCollisionBatch
{
    [Serializable] public class Entry
    {
        public string path, guid, status, reason;
        public int meshes, skinned_meshes, colliders_before, colliders_after, added, boxes, convex_meshes, triangle_meshes, physics_checks, missing_parts;
        public string[] failures;
    }
    [Serializable] public class Receipt { public bool verified; public string completed_utc; public Entry[] assets; public int changed_prefabs, added_colliders; }
    const string Output="../";
    static bool Owned(string p)=>p.StartsWith("Assets/Game/")||p.StartsWith("Assets/Sandbox/");
    static string Skip(string path)
    {
        if(path.Contains("/UI/")||path.Contains("MapUI")||path.EndsWith("DistrictPowerGridMapUI.prefab"))return "UI prefab";
        if(path.Contains("_Examination"))return "Examination presentation";
        if(path.StartsWith("Assets/Game/Characters/")&&path.EndsWith("_Visual.prefab"))return "Character presentation under its gameplay controller";
        if(path.Contains("/Effects/")||path.Contains("/Sparkle/")||path.EndsWith("BruteAcidPool.prefab"))return "Effect or scripted damage volume";
        if(path.StartsWith("Assets/Game/Weapons/")&&!path.Contains("_Pickup"))return "Held weapon or shared examination presentation";
        return null;
    }
    static bool Solid(Collider c)=>c&&c.enabled&&!c.isTrigger && (!(c is MeshCollider mc)||mc.sharedMesh);
    static bool Covered(MeshFilter f)=>f.GetComponentsInParent<Collider>(true).Any(Solid);
    static bool Effect(MeshFilter f)=>f.GetComponentInParent<Canvas>()||f.GetComponent<ParticleSystem>()||
        f.transform.name.Equals("Emitter",StringComparison.OrdinalIgnoreCase)||f.GetComponent<Renderer>().shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    static MeshFilter[] Meshes(GameObject root)
    {
        var lowerLods=new HashSet<Renderer>();
        foreach(var group in root.GetComponentsInChildren<LODGroup>(true))
            foreach(var lod in group.GetLODs().Skip(1))foreach(var renderer in lod.renderers)if(renderer)lowerLods.Add(renderer);
        return root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&f.sharedMesh.vertexCount>0&&f.GetComponent<MeshRenderer>()&&!Effect(f)&&!lowerLods.Contains(f.GetComponent<Renderer>())).ToArray();
    }
    static bool Cuboid(Mesh m)
    {
        if(!m.isReadable)return false;
        var b=m.bounds;if(b.size.x<1e-7f||b.size.y<1e-7f||b.size.z<1e-7f)return false;
        float tolerance=b.size.magnitude*1e-5f;
        bool Edge(float p,float min,float max)=>Mathf.Abs(p-min)<tolerance||Mathf.Abs(p-max)<tolerance;
        return m.triangles.Length==36&&m.vertices.All(v=>Edge(v.x,b.min.x,b.max.x)&&Edge(v.y,b.min.y,b.max.y)&&Edge(v.z,b.min.z,b.max.z));
    }
    static bool RayHit(Collider c)
    {
        var bounds=c.bounds;if(bounds.size.sqrMagnitude<1e-14f)return false;
        float distance=Mathf.Max(.1f,bounds.extents.magnitude*2+.1f);
        foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
            foreach(float u in new[]{-.8f,-.4f,0,.4f,.8f})foreach(float v in new[]{-.8f,-.4f,0,.4f,.8f})
            {
                var offset=axis==Vector3.right?new Vector3(0,u*bounds.extents.y,v*bounds.extents.z):axis==Vector3.up?new Vector3(u*bounds.extents.x,0,v*bounds.extents.z):new Vector3(u*bounds.extents.x,v*bounds.extents.y,0);
                foreach(float sign in new[]{-1f,1f})if(c.Raycast(new Ray(bounds.center+offset+axis*distance*sign,-axis*sign),out _,distance*2))return true;
            }
        // Thin or sparse geometry may miss a regular grid: aim at actual triangle surfaces.
        if(c is MeshCollider mc&&mc.sharedMesh)
        {
            Vector3[] vertices;int[] indices;
            if(mc.sharedMesh.isReadable){vertices=mc.sharedMesh.vertices;indices=mc.sharedMesh.triangles;}
            else
            {
                using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mc.sharedMesh))
                {
                    var data=snapshot[0];
                    using(var points=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                    {data.GetVertices(points);vertices=points.ToArray();}
                    var all=new List<int>();
                    for(int sm=0;sm<data.subMeshCount;sm++)
                        using(var buffer=new NativeArray<int>(data.GetSubMesh(sm).indexCount,Allocator.Temp))
                        {data.GetIndices(buffer,sm,true);all.AddRange(buffer.ToArray());}
                    indices=all.ToArray();
                }
            }
            int stride=Math.Max(1,indices.Length/3/48);
            for(int i=0;i+2<indices.Length;i+=3*stride)
            {
                Vector3 a=c.transform.TransformPoint(vertices[indices[i]]),b=c.transform.TransformPoint(vertices[indices[i+1]]),d=c.transform.TransformPoint(vertices[indices[i+2]]);
                var n=Vector3.Cross(b-a,d-a);if(n.sqrMagnitude<1e-20f)continue;n.Normalize();var point=(a+b+d)/3;
                foreach(float sign in new[]{-1f,1f})if(c.Raycast(new Ray(point+n*distance*sign,-n*sign),out _,distance*2))return true;
            }
        }
        return false;
    }
    static string[] CheckPhysics(GameObject root, out int count)
    {
        var clone=Object.Instantiate(root);var failures=new List<string>();count=0;
        try
        {
            SceneManager.MoveGameObjectToScene(clone,SceneManager.GetActiveScene());
            foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.SetActive(true);
            Physics.SyncTransforms();
            foreach(var c in clone.GetComponentsInChildren<Collider>(true).Where(Solid))
            {count++;if(!RayHit(c))failures.Add(c.transform.name+":"+c.GetType().Name);}
        }
        finally{Object.DestroyImmediate(clone);}
        return failures.ToArray();
    }
    static int Depth(string path,HashSet<string> visiting=null)
    {
        visiting=visiting??new HashSet<string>();if(!visiting.Add(path))return 0;
        return 1+AssetDatabase.GetDependencies(path,false).Where(p=>p!=path&&p.EndsWith(".prefab")&&Owned(p)).Select(p=>Depth(p,visiting)).DefaultIfEmpty(0).Max();
    }
    static void AddCollision(MeshFilter f,Entry row)
    {
        var body=f.GetComponentInParent<Rigidbody>();
        var size=Vector3.Scale(f.sharedMesh.bounds.size,f.transform.lossyScale);float worldMax=Mathf.Max(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
        if(Cuboid(f.sharedMesh)||(f.sharedMesh.bounds.size.magnitude<.01f&&worldMax<2))
        {var c=f.gameObject.AddComponent<BoxCollider>();c.center=f.sharedMesh.bounds.center;c.size=f.sharedMesh.bounds.size;row.boxes++;}
        else
        {var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=body&&!body.isKinematic;if(c.convex)row.convex_meshes++;else row.triangle_meshes++;}
        row.added++;
    }
    public static void CompleteCoverageBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(Output+"BatchReceipt.json"));
            foreach(var row in receipt.assets)
            {
                var skip=Skip(row.path);if(skip!=null){row.status="ExcludedNonPlacement";row.reason=skip;continue;}
                var root=PrefabUtility.LoadPrefabContents(row.path);
                try
                {
                    int previous=row.added;var filters=Meshes(root);
                    foreach(var f in filters)if(!Covered(f))AddCollision(f,row);
                    row.missing_parts=filters.Count(f=>!Covered(f));
                    row.colliders_after=root.GetComponentsInChildren<Collider>(true).Count(Solid);
                    if(row.added>previous)
                    {
                        row.status="AddedCollision";row.reason="";row.failures=CheckPhysics(root,out row.physics_checks);
                        PrefabUtility.SaveAsPrefabAsset(root,row.path);
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            receipt.changed_prefabs=receipt.assets.Count(a=>a.added>0);receipt.added_colliders=receipt.assets.Sum(a=>a.added);
            receipt.verified=receipt.assets.All(a=>a.missing_parts==0);
            AssetDatabase.SaveAssets();File.WriteAllText(Output+"BatchReceipt.json",JsonUtility.ToJson(receipt,true));
            Debug.Log("COLLISION_COVERAGE_COMPLETED "+receipt.changed_prefabs+" prefabs "+receipt.added_colliders+" colliders");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"Failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RunBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game","Assets/Sandbox"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".prefab")).OrderBy(p=>Depth(p)).ThenBy(p=>p).ToArray();
            var results=new List<Entry>();
            foreach(var path in paths)
            {
                var row=new Entry{path=path,guid=AssetDatabase.AssetPathToGUID(path),failures=Array.Empty<string>()};results.Add(row);
                var skip=Skip(path);if(skip!=null){row.status="ExcludedNonPlacement";row.reason=skip;continue;}
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var filters=Meshes(root);row.meshes=filters.Length;
                    var skinned=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh).ToArray();row.skinned_meshes=skinned.Length;
                    row.colliders_before=root.GetComponentsInChildren<Collider>(true).Count(Solid);
                    if(skinned.Length>0&&row.colliders_before==0)
                    {
                        var bounds=skinned[0].bounds;foreach(var renderer in skinned.Skip(1))bounds.Encapsulate(renderer.bounds);
                        Vector3 scale=root.transform.lossyScale;
                        var size=new Vector3(bounds.size.x/Mathf.Max(.0001f,Mathf.Abs(scale.x)),bounds.size.y/Mathf.Max(.0001f,Mathf.Abs(scale.y)),bounds.size.z/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
                        var capsule=root.AddComponent<CapsuleCollider>();capsule.center=root.transform.InverseTransformPoint(bounds.center);
                        capsule.direction=1;capsule.radius=Mathf.Max(.01f,Mathf.Min(size.x,size.z)*.45f);capsule.height=Mathf.Max(size.y,capsule.radius*2);row.added++;
                    }
                    foreach(var f in filters)
                    {
                        if(Covered(f))continue;
                        var body=f.GetComponentInParent<Rigidbody>();
                        var size=Vector3.Scale(f.sharedMesh.bounds.size,f.transform.lossyScale);float worldMax=Mathf.Max(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
                        bool box=Cuboid(f.sharedMesh)||(f.sharedMesh.bounds.size.magnitude<.01f&&worldMax<2);
                        if(box)
                        {
                            var c=f.gameObject.AddComponent<BoxCollider>();c.center=f.sharedMesh.bounds.center;c.size=f.sharedMesh.bounds.size;
                            row.boxes++;
                        }
                        else
                        {
                            var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=body&&!body.isKinematic;
                            if(c.convex)row.convex_meshes++;else row.triangle_meshes++;
                        }
                        row.added++;
                    }
                    row.colliders_after=root.GetComponentsInChildren<Collider>(true).Count(Solid);
                    row.missing_parts=filters.Count(f=>!Covered(f));
                    row.status=row.added>0?"AddedCollision":row.colliders_after>0?"ExistingCollision":filters.Length==0?"NoPhysicalGeometry":"Unresolved";
                    if(filters.Length==0&&row.colliders_after==0)row.reason="No placeable mesh; UI/effect/controller or toolkit authoring preset";
                    if(row.added>0)
                    {
                        row.failures=CheckPhysics(root,out row.physics_checks);
                        if(row.missing_parts>0)throw new InvalidOperationException("Uncovered mesh "+path);
                        PrefabUtility.SaveAsPrefabAsset(root,path);
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                if(results.Count%100==0){File.WriteAllText(Output+"Progress.txt",results.Count+" / "+paths.Length+" prefabs");Debug.Log("COLLISION_PROGRESS "+results.Count+" / "+paths.Length);}
            }
            AssetDatabase.SaveAssets();
            var receipt=new Receipt{verified=results.All(r=>r.missing_parts==0),completed_utc=DateTime.UtcNow.ToString("o"),assets=results.ToArray(),changed_prefabs=results.Count(r=>r.added>0),added_colliders=results.Sum(r=>r.added)};
            File.WriteAllText(Output+"BatchReceipt.json",JsonUtility.ToJson(receipt,true));
            Debug.Log("COLLISION_BATCH_VERIFIED changed="+receipt.changed_prefabs+" colliders="+receipt.added_colliders);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"Failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void VerifyMergedBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(Output+"BatchReceipt.json"));int checkedMeshes=0,physics=0;var failures=new List<string>();
            foreach(var row in receipt.assets)
            {
                var skip=Skip(row.path);
                if(skip!=null){row.status="ExcludedNonPlacement";row.reason=skip;continue;}
                if(AssetDatabase.AssetPathToGUID(row.path)!=row.guid)throw new InvalidOperationException("GUID changed "+row.path);
                var root=PrefabUtility.LoadPrefabContents(row.path);
                try
                {
                    foreach(var f in Meshes(root)){checkedMeshes++;if(!Covered(f))failures.Add("No solid collider: "+row.path+"/"+f.name);}
                    if(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.sharedMesh)&&!root.GetComponentsInChildren<Collider>(true).Any(Solid))
                        failures.Add("No animated-body collider: "+row.path);
                    if(row.added>0)
                    {
                        var failed=CheckPhysics(root,out int count);physics+=count;
                        row.physics_checks=count;row.failures=failed;
                        failures.AddRange(failed.Select(name=>"No physics hit: "+row.path+"/"+name));
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllText(Output+"MergedVerification.json","{\"verified\":"+(failures.Count==0?"true":"false")+",\"checked_meshes\":"+checkedMeshes+",\"physics_checks\":"+physics+",\"failures\":"+JsonUtility.ToJson(new FailureList{items=failures.ToArray()})+"}");
            receipt.verified=failures.Count==0;
            File.WriteAllText(Output+"BatchReceipt.json",JsonUtility.ToJson(receipt,true));
            if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
            Debug.Log("COLLISION_MERGED_VERIFIED meshes="+checkedMeshes+" physics="+physics);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"VerifyFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    [Serializable] class FailureList{public string[] items;}
}
