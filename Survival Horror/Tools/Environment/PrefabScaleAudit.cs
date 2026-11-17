// Run in the isolated cached project; reports resolved prefab sizes without modifying assets.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PrefabScaleAudit
{
    [Serializable] public class Entry
    {
        public string path,guid,family;
        public float[] size,center,root_scale;
        public int meshes,solid_colliders;
        public string[] mesh_names;
    }
    [Serializable] public class Result { public Entry[] assets; }
    [Serializable] public class Surface { public string path; public float[] levels,areas; }
    [Serializable] public class SurfaceResult { public Surface[] assets; }
    public static void CaptureCalibrationBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var plan=JsonUtility.FromJson<PrefabScaleCalibration.Plan>(File.ReadAllText("PrefabScalePlan.json"));
            var chosen=new HashSet<string>(new[]{"P0181","P0376","P0164","P0507","P0396","P0253","P0515","P0455"});
            var selected=plan.assets.Where(a=>chosen.Contains(a.id)||a.path.Contains("Classic64_RollingToolChest_")||a.path.Contains("Classic64_GarbageBag_")||a.path.Contains("Classic64_WoodPallet_")||a.path.EndsWith("Office_armchair_blue.prefab")).ToArray();
            string output="../ScaleAudit/CalibratedCaptures/";Directory.CreateDirectory(output);
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();var pipeline=UniversalRenderPipelineAsset.Create(renderer);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(40,-35,0);
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=200;camera.backgroundColor=new Color(.1f,.13f,.17f);camera.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(320,320,24);camera.targetTexture=rt;var tile=new Texture2D(320,320,TextureFormat.RGB24,false);
            var grey=new Material(Shader.Find("Universal Render Pipeline/Lit"));grey.color=new Color(.5f,.6f,.65f);
            foreach(var a in selected)
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(a.path));
                var reference=GameObject.CreatePrimitive(PrimitiveType.Capsule);reference.name="1.8 metre human reference";reference.transform.localScale=new Vector3(.45f,.9f,.45f);
                reference.transform.position=new Vector3(a.target_size_m[0]*.5f+.55f,.9f,0);reference.GetComponent<Renderer>().sharedMaterial=grey;
                var bounds=reference.GetComponent<Renderer>().bounds;
                foreach(var r in root.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
                float radius=bounds.extents.magnitude;camera.orthographicSize=radius*1.05f;camera.transform.position=bounds.center+new Vector3(1,.6f,1).normalized*radius*4;camera.transform.LookAt(bounds.center);
                camera.Render();camera.Render();RenderTexture.active=rt;tile.ReadPixels(new Rect(0,0,320,320),0,0);tile.Apply();
                File.WriteAllBytes(output+Path.GetFileNameWithoutExtension(a.path)+".png",tile.EncodeToPNG());UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(reference);
            }
            File.WriteAllText("../ScaleAudit/CapturesVerified.txt",selected.Length+" calibrated prefabs captured beside a 1.8 m reference.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("../ScaleAuditFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void FurnitureBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string output="../ScaleAudit/";Directory.CreateDirectory(output+"FurnitureCaptures");
            var data=JsonUtility.FromJson<Result>(File.ReadAllText(output+"AllPrefabs.json"));var surfaces=new List<Surface>();
            var rendererData=ScriptableObject.CreateInstance<UniversalRendererData>();var pipeline=UniversalRenderPipelineAsset.Create(rendererData);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.13f,.17f);
            var target=new RenderTexture(256,256,24);camera.targetTexture=target;var tile=new Texture2D(256,256,TextureFormat.RGB24,false);
            foreach(var entry in data.assets.Where(a=>a.meshes>0&&a.family!="ModelsPackPSX"&&a.path.StartsWith("Assets/Game/Environment/")&&
                new[]{"chair","couch","table","bed","sofa","armchair","RollingToolChest","GarbageBag","WoodPallet"}.Any(t=>a.path.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0)))
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entry.path));
                try
                {
                    var buckets=new Dictionary<int,float>();var bounds=new Bounds(new Vector3(entry.center[0],entry.center[1],entry.center[2]),new Vector3(entry.size[0],entry.size[1],entry.size[2]));
                    foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh))
                        using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(f.sharedMesh))
                        {
                            var mesh=snapshot[0];
                            using(var vertices=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
                            {
                                mesh.GetVertices(vertices);
                                for(int sm=0;sm<mesh.subMeshCount;sm++)
                                    using(var indices=new NativeArray<int>(mesh.GetSubMesh(sm).indexCount,Allocator.Temp))
                                    {
                                        mesh.GetIndices(indices,sm,true);
                                        for(int i=0;i+2<indices.Length;i+=3)
                                        {
                                            var a=f.transform.TransformPoint(vertices[indices[i]]);var b=f.transform.TransformPoint(vertices[indices[i+1]]);var c=f.transform.TransformPoint(vertices[indices[i+2]]);
                                            var n=Vector3.Cross(b-a,c-a);float area=n.magnitude*.5f;
                                            if(area<.00001f||Mathf.Abs(n.normalized.y)<.95f)continue;
                                            float y=(a.y+b.y+c.y)/3-bounds.min.y;if(y<.1f||y>bounds.size.y*.95f)continue;
                                            int key=Mathf.RoundToInt(y*50);buckets[key]=buckets.TryGetValue(key,out float prior)?prior+area:area;
                                        }
                                    }
                            }
                        }
                    var top=buckets.OrderByDescending(p=>p.Value).Take(6).ToArray();surfaces.Add(new Surface{path=entry.path,levels=top.Select(p=>p.Key/50f).ToArray(),areas=top.Select(p=>p.Value).ToArray()});
                    if(entry.family=="Furniture"&&entry.path.IndexOf("bed",StringComparison.OrdinalIgnoreCase)>=0 || entry.path.Contains("Classic64_GarbageBag")||entry.path.Contains("RollingToolChest")||entry.path.Contains("WoodPallet"))
                    {
                        float radius=bounds.extents.magnitude;camera.orthographicSize=radius*1.05f;camera.transform.position=bounds.center+new Vector3(1,.7f,1).normalized*radius*4;camera.transform.LookAt(bounds.center);
                        camera.Render();camera.Render();RenderTexture.active=target;tile.ReadPixels(new Rect(0,0,256,256),0,0);tile.Apply();
                        File.WriteAllBytes(output+"FurnitureCaptures/"+Path.GetFileNameWithoutExtension(entry.path)+".png",tile.EncodeToPNG());
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            File.WriteAllText(output+"FurnitureSurfaces.json",JsonUtility.ToJson(new SurfaceResult{assets=surfaces.ToArray()},true));
            Debug.Log("FURNITURE_SCALE_MEASURED "+surfaces.Count);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("../ScaleAuditFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RunBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var rows=new List<Entry>();
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game","Assets/Sandbox"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".prefab")).OrderBy(p=>p))
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh&&r.name!="Emitter"&&!r.GetComponentInParent<Canvas>()).ToArray();
                    var b=renderers.Length>0?renderers[0].bounds:new Bounds();foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);
                    var scale=root.transform.localScale;
                    rows.Add(new Entry{path=path,guid=AssetDatabase.AssetPathToGUID(path),family=path.Split('/')[3],size=new[]{b.size.x,b.size.y,b.size.z},center=new[]{b.center.x,b.center.y,b.center.z},root_scale=new[]{scale.x,scale.y,scale.z},meshes=renderers.Length,
                        solid_colliders=root.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled&&!c.isTrigger),mesh_names=renderers.Select(r=>r.name).ToArray()});
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            Directory.CreateDirectory("../ScaleAudit");File.WriteAllText("../ScaleAudit/AllPrefabs.json",JsonUtility.ToJson(new Result{assets=rows.ToArray()},true));
            Debug.Log("SCALE_AUDIT_COMPLETE "+rows.Count);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("../ScaleAuditFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
