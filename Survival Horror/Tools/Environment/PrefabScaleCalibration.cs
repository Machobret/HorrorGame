// Shared editor recipe; compile only in an isolated validation/import project.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class PrefabScaleCalibration
{
    [Serializable] public class Entry { public string id,path,guid,role,reference; public float factor; public float[] source_size_m,target_size_m; }
    [Serializable] public class Plan { public string units,method; public Entry[] assets; }
    [Serializable] public class Result { public string id,path,guid,role,reference; public float factor; public float[] source_size_m,size_m; public bool resized; public int mesh_parts,colliders,physics_checks; }
    [Serializable] public class Receipt { public bool verified; public string completed_utc; public int reviewed,resized,physics_checks; public Result[] assets; }
    const string PlanPath="PrefabScalePlan.json";
    public static Vector3 Size(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh&&r.name!="Emitter").ToArray();
        if(renderers.Length==0)throw new InvalidOperationException("No mesh geometry "+root.name);
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds.size;
    }
    static Vector3 V(float[] v)=>new Vector3(v[0],v[1],v[2]);
    static bool Close(Vector3 a,Vector3 b)=>(a-b).magnitude<Mathf.Max(.0002f,b.magnitude*.0002f);
    public static bool MatchesCalibratedSize(string path,Vector3 size)
    {
        if(!File.Exists(PlanPath))return false;
        var a=JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath)).assets.FirstOrDefault(a=>a.path==path);
        return a!=null&&Close(size,V(a.target_size_m));
    }
    static void ScaleRootContents(GameObject root,float factor,string path)
    {
        if(root.GetComponent<MeshFilter>()||root.GetComponent<SkinnedMeshRenderer>())
            throw new InvalidOperationException("Direct-root geometry needs a baked correction: "+path);
        foreach(Transform child in root.transform){child.localPosition*=factor;child.localScale*=factor;}
        foreach(var c in root.GetComponents<Collider>())
        {
            if(c is BoxCollider box){box.center*=factor;box.size*=factor;}
            else if(c is SphereCollider sphere){sphere.center*=factor;sphere.radius*=factor;}
            else if(c is CapsuleCollider capsule){capsule.center*=factor;capsule.radius*=factor;capsule.height*=factor;}
            else if(c is MeshCollider)throw new InvalidOperationException("Root proxy mesh requires explicit geometry correction: "+path);
        }
    }
    public static void ApplyAvailable(string[] roots=null)
    {
        if(!File.Exists(PlanPath))return;
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));var results=new List<Result>();
        foreach(var a in plan.assets)
        {
            if(roots!=null&&!roots.Any(r=>a.path.StartsWith(r+"/")))continue;
            if(!File.Exists(a.path)){if(roots==null)throw new InvalidOperationException("Missing planned prefab "+a.path);continue;}
            if(AssetDatabase.AssetPathToGUID(a.path)!=a.guid)throw new InvalidOperationException("GUID mismatch "+a.path);
            var root=PrefabUtility.LoadPrefabContents(a.path);
            try
            {
                Vector3 current=Size(root),target=V(a.target_size_m);bool changed=!Close(current,target);
                if(changed)
                {
                    float ratio=target.magnitude/current.magnitude;
                    if(!Close(current*ratio,target))throw new InvalidOperationException("Nonuniform geometry changed since audit: "+a.path);
                    ScaleRootContents(root,ratio,a.path);
                    if(!Close(Size(root),target))throw new InvalidOperationException("Target size mismatch "+a.path);
                    PrefabUtility.SaveAsPrefabAsset(root,a.path);
                }
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(a.path);
                var labels=new HashSet<string>(AssetDatabase.GetLabels(saved));labels.Add("ScaleCalibrated");
                if(a.path.StartsWith("Assets/Game/Environment/ModelsPackPSX/"))
                {
                    labels.Remove("DownloadedScaleReview");labels.Add("DownloadedScaleCalibrated");
                    labels.Remove("DownloadedKeep");labels.Remove("DownloadedReview");labels.Remove("DownloadedBackgroundOnly");
                    labels.Add(labels.Contains("DownloadedProxyGeometry")?"DownloadedBackgroundOnly":labels.Contains("DownloadedVisualQualityReview")?"DownloadedReview":"DownloadedKeep");
                }
                if(!labels.SetEquals(AssetDatabase.GetLabels(saved)))AssetDatabase.SetLabels(saved,labels.ToArray());
                var clone=Object.Instantiate(saved);
                try
                {
                    if(!Close(Size(clone),target)||clone.transform.localScale!=Vector3.one)throw new InvalidOperationException("Size/root verification failed "+a.path);
                    foreach(var f in clone.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(!f.sharedMesh)throw new InvalidOperationException("Missing mesh "+a.path);
                        var c=f.GetComponent<MeshCollider>();if(c&&c.sharedMesh!=f.sharedMesh)throw new InvalidOperationException("Collider/mesh mismatch "+a.path);
                    }
                    var failures=PrefabCollisionBatch.VerifyPrefabPhysics(clone,out int checks);
                    if(failures.Length>0)throw new InvalidOperationException("Physics failure "+a.path+": "+string.Join(", ",failures));
                    results.Add(new Result{id=a.id,path=a.path,guid=a.guid,role=a.role,reference=a.reference,factor=a.factor,source_size_m=a.source_size_m,size_m=new[]{target.x,target.y,target.z},resized=Mathf.Abs(a.factor-1)>.00001f,
                        mesh_parts=clone.GetComponentsInChildren<MeshFilter>(true).Length,colliders=clone.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled&&!c.isTrigger),physics_checks=checks});
                }
                finally{Object.DestroyImmediate(clone);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();var receipt=new Receipt{verified=true,completed_utc=DateTime.UtcNow.ToString("o"),reviewed=results.Count,resized=results.Count(a=>a.resized),physics_checks=results.Sum(a=>a.physics_checks),assets=results.ToArray()};
        File.WriteAllText("PrefabScaleReceipt.json",JsonUtility.ToJson(receipt,true));Debug.Log("PREFAB_SCALE_VERIFIED "+receipt.reviewed+" reviewed, "+receipt.resized+" resized");
    }
    public static void RunBatch()
    {
        try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ApplyAvailable();EditorApplication.Exit(0);}
        catch(Exception e){File.WriteAllText("PrefabScaleFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void VerifyIdempotenceBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
            var paths=plan.assets.SelectMany(a=>new[]{a.path,a.path+".meta"}).ToArray();var before=paths.ToDictionary(p=>p,Hash);
            ApplyAvailable();
            foreach(var p in paths)if(Hash(p)!=before[p])throw new InvalidOperationException("Calibration was not idempotent: "+p);
            var maintenance=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("DownloadedPrefabMaintenance")).FirstOrDefault(t=>t!=null);
            if(maintenance!=null)maintenance.GetMethod("VerifyRetained").Invoke(null,null);
            File.WriteAllText("ScaleIdempotenceVerified.txt","All calibrated prefab and metadata bytes unchanged on repeated calibration; available downloaded-pack verification passed.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("PrefabScaleFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
