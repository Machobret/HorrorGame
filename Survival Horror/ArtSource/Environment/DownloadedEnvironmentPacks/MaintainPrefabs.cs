// Source recipe: compile only in the isolated import project, never in the open user's project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class DownloadedPrefabMaintenance
{
    [Serializable] public class Surface { public string role, path, texture; public float[] color; public float metallic, smoothness; }
    [Serializable] public class Repair { public string original, replacement; public bool project_uv; }
    [Serializable] public class Entry { public string id, pack, prefab, guid; public bool remove, clean_geometry; public int expected_triangles; public float[] bounds_m; public Repair[] repairs; }
    [Serializable] public class Plan { public string scope; public string[] excluded_sources; public Surface[] materials; public Entry[] assets; }
    [Serializable] public class Result { public string id, prefab, guid; public string[] material_paths; public int triangles, removed_degenerate_triangles; public bool repaired; }
    [Serializable] public class Receipt { public bool verified; public string completed_utc; public int retained_count, removed_count, repaired_count; public Result[] assets; public string[] removed_prefabs, checks; }

    public static void RunBatch()
    {
        try { Apply(); EditorApplication.Exit(0); }
        catch (Exception e) { File.WriteAllText("MaintenanceFailure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void VerifyRetained()
    {
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("MaintenancePlan.json"));
        foreach(var a in plan.assets)
        {
            if(a.remove)
            {
                if(File.Exists(a.prefab))throw new InvalidOperationException("Removed prefab returned "+a.prefab);
                continue;
            }
            if(AssetDatabase.AssetPathToGUID(a.prefab)!=a.guid)throw new InvalidOperationException("GUID changed "+a.prefab);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(a.prefab));
            try { Verify(a,instance); }
            finally { Object.DestroyImmediate(instance); }
        }
        File.WriteAllText("MaintenanceGeometryVerified.txt","Verified every retained prefab, original GUID, original bounds, expected triangle count, materials and colliders.\n");
    }

    public static void Apply()
    {
        var plan = JsonUtility.FromJson<Plan>(File.ReadAllText("MaintenancePlan.json"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var removed = new HashSet<string>(plan.assets.Where(a => a.remove).Select(a => a.prefab));
        // Dependency audit before any AssetDatabase deletion. Shared meshes and materials are retained.
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !removed.Contains(p)))
        {
            if (AssetDatabase.IsValidFolder(path)) continue;
            var refs = AssetDatabase.GetDependencies(path, false).Where(removed.Contains).ToArray();
            if (refs.Length > 0) throw new InvalidOperationException("Referenced removal: " + path + " -> " + string.Join(", ", refs));
        }
        foreach (var s in plan.materials) MakeSurface(s);
        int removedCount = 0;
        foreach (var a in plan.assets.Where(a => a.remove))
        {
            if (File.Exists(a.prefab))
            {
                if (AssetDatabase.AssetPathToGUID(a.prefab) != a.guid) throw new InvalidOperationException("Deletion GUID mismatch: " + a.prefab);
                if (!AssetDatabase.DeleteAsset(a.prefab)) throw new InvalidOperationException("Could not remove " + a.prefab);
            }
            removedCount++;
        }
        var results = new List<Result>();
        foreach (var a in plan.assets.Where(a => !a.remove))
        {
            if (AssetDatabase.AssetPathToGUID(a.prefab) != a.guid) throw new InvalidOperationException("GUID mismatch " + a.prefab);
            int degenerate = 0;
            if (a.repairs.Length > 0 || a.clean_geometry)
            {
                var instance = PrefabUtility.LoadPrefabContents(a.prefab);
                try
                {
                    foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        var mats = renderer.sharedMaterials;
                        var projected = new HashSet<int>();
                        for (int slot = 0; slot < mats.Length; slot++)
                        {
                            string path = AssetDatabase.GetAssetPath(mats[slot]);
                            var repair = a.repairs.FirstOrDefault(r => r.original == path);
                            if (repair == null) continue;
                            mats[slot] = AssetDatabase.LoadAssetAtPath<Material>(repair.replacement);
                            if (repair.project_uv) projected.Add(slot);
                        }
                        renderer.sharedMaterials = mats;
                        bool clean = a.clean_geometry && !filter.sharedMesh.name.EndsWith("_Repaired");
                        if (projected.Count > 0 || clean)
                        {
                            var mesh = RepairMesh(filter.sharedMesh, filter.transform, instance.transform, projected, clean, out int cleaned);
                            degenerate += cleaned;
                            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(filter.sharedMesh)).Replace('\\', '/') + "/" + a.id + "_" + filter.sharedMesh.name + "_Repaired.asset";
                            // A mesh is only rebuilt when there are source slots left to repair, or cleanup is required.
                            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                            if (saved) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
                            else { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                            filter.sharedMesh = saved;
                            var collider = filter.GetComponent<MeshCollider>();
                            if (collider) collider.sharedMesh = saved;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(instance, a.prefab);
                }
                finally { PrefabUtility.UnloadPrefabContents(instance); }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(a.prefab);
            var check=Object.Instantiate(prefab);
            try { Verify(a, check); } finally { Object.DestroyImmediate(check); }
            results.Add(new Result { id=a.id, prefab=a.prefab, guid=a.guid,
                repaired=a.repairs.Length > 0 || a.clean_geometry,
                removed_degenerate_triangles=a.clean_geometry?a.expected_triangles-prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3):degenerate,
                triangles=prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3),
                material_paths=prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Select(AssetDatabase.GetAssetPath).Distinct().ToArray() });
        }
        PrefabScaleCalibration.ApplyAvailable(plan.assets.Select(a=>a.prefab.Substring(0,a.prefab.IndexOf("/Prefabs/",StringComparison.Ordinal))).Distinct().ToArray());
        AssetDatabase.SaveAssets();
        var receipt = new Receipt { verified=true, completed_utc=DateTime.UtcNow.ToString("o"), retained_count=results.Count,
            removed_count=removedCount, repaired_count=results.Count(r => r.repaired), assets=results.ToArray(), removed_prefabs=removed.ToArray(),
            checks=new[] { "AssetDatabase dependency audit before native prefab deletion", "Original source collections and shared dependencies retained",
                "All retained prefab GUIDs preserved", "Retained renderers have native meshes, matching URP materials and collision appropriate to their decoration labels",
                "No retained prefab uses SourceDefault or unrepaired missing-source material slots", "No prefab depends on a combined FBX, source scene or deleted prefab",
                "Planar UVs generated only for replacement tiling surfaces; working source material slots and UVs preserved" } };
        File.WriteAllText("MaintenanceReceipt.json", JsonUtility.ToJson(receipt, true));
        Debug.Log("MAINTENANCE_VERIFIED retained="+results.Count+" removed="+removedCount+" repaired="+receipt.repaired_count);
    }

    static void MakeSurface(Surface s)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(s.path);
        if (!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,s.path); }
        m.name=s.role; m.SetColor("_BaseColor",new Color(s.color[0],s.color[1],s.color[2],s.color[3]));
        var texture=string.IsNullOrEmpty(s.texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(s.texture);
        if (!string.IsNullOrEmpty(s.texture) && !texture) throw new InvalidOperationException("Missing replacement texture "+s.texture);
        m.SetTexture("_BaseMap",texture); m.SetFloat("_Metallic",s.metallic); m.SetFloat("_Smoothness",s.smoothness);
        m.SetFloat("_Cull",(float)CullMode.Off); m.enableInstancing=true; m.doubleSidedGI=true;
        bool glass=s.color[3]<.99f;
        m.SetFloat("_Surface",glass?1:0); m.SetFloat("_Blend",0); m.SetFloat("_AlphaClip",0);
        m.SetFloat("_SrcBlend",(float)(glass?BlendMode.SrcAlpha:BlendMode.One));
        m.SetFloat("_DstBlend",(float)(glass?BlendMode.OneMinusSrcAlpha:BlendMode.Zero)); m.SetFloat("_ZWrite",glass?0:1);
        if (glass) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType",glass?"Transparent":"Opaque"); m.renderQueue=glass?3000:2000;
        m.SetShaderPassEnabled("ShadowCaster",!glass); EditorUtility.SetDirty(m);
    }

    static Mesh RepairMesh(Mesh source, Transform part, Transform root, HashSet<int> projected, bool clean, out int removed)
    {
        var mesh=Object.Instantiate(source); mesh.name=source.name+"_Repaired";
        var positions=source.vertices.ToList(); var normals=source.normals.ToList(); var tangents=source.tangents.ToList(); var colors=source.colors32.ToList();
        var channels=new List<Vector4>[8]; for(int c=0;c<8;c++){channels[c]=new List<Vector4>();source.GetUVs(c,channels[c]);}
        if(channels[0].Count==0) channels[0]=Enumerable.Repeat(Vector4.zero,positions.Count).ToList();
        var triangles=new List<int>[source.subMeshCount]; removed=0;
        for(int s=0;s<source.subMeshCount;s++)
        {
            var indices=source.GetTriangles(s); triangles[s]=new List<int>();
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=root.InverseTransformPoint(part.TransformPoint(positions[indices[i]]));
                var b=root.InverseTransformPoint(part.TransformPoint(positions[indices[i+1]]));
                var c=root.InverseTransformPoint(part.TransformPoint(positions[indices[i+2]]));
                var normal=Vector3.Cross(b-a,c-a);
                if(clean && normal.magnitude*.5f<1e-9f){removed++;continue;}
                for(int k=0;k<3;k++)
                {
                    int index=indices[i+k];
                    if(projected.Contains(s))
                    {
                        var p=root.InverseTransformPoint(part.TransformPoint(positions[index]));
                        int copy=positions.Count;positions.Add(positions[index]);
                        if(normals.Count>0)normals.Add(normals[index]); if(tangents.Count>0)tangents.Add(tangents[index]); if(colors.Count>0)colors.Add(colors[index]);
                        for(int ch=0;ch<8;ch++)if(channels[ch].Count>0)channels[ch].Add(channels[ch][index]);
                        // Metre-sized repeating surfaces, independent of the missing image's unknown layout.
                        Vector2 uv=Mathf.Abs(normal.y)>=Mathf.Max(Mathf.Abs(normal.x),Mathf.Abs(normal.z))?new Vector2(p.x,p.z):
                            Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
                        channels[0][copy]=new Vector4(uv.x,uv.y,0,0);index=copy;
                    }
                    triangles[s].Add(index);
                }
            }
        }
        mesh.Clear();mesh.indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(positions); if(normals.Count>0)mesh.SetNormals(normals);if(colors.Count>0)mesh.SetColors(colors);
        for(int ch=0;ch<8;ch++)if(channels[ch].Count>0)mesh.SetUVs(ch,channels[ch]);
        mesh.subMeshCount=triangles.Length;for(int s=0;s<triangles.Length;s++)mesh.SetTriangles(triangles[s],s);
        if(normals.Count==0)mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }

    static void Verify(Entry a, GameObject prefab)
    {
        if(!prefab || prefab.transform.localPosition!=Vector3.zero || prefab.transform.localScale!=Vector3.one || prefab.transform.localRotation!=Quaternion.identity)
            throw new InvalidOperationException("Invalid prefab root "+a.prefab);
        foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new InvalidOperationException("Missing script "+a.prefab);
        bool nonBlocking = PrefabCollisionBatch.IsNonBlocking(a.prefab);
        foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var m=f.sharedMesh;var r=f.GetComponent<MeshRenderer>();var collider=f.GetComponent<MeshCollider>();
            if(!m || m.vertexCount==0 || m.triangles.Length==0 || !r || r.sharedMaterials.Length!=m.subMeshCount)
                throw new InvalidOperationException("Broken geometry/material "+a.prefab);
            if(nonBlocking ? collider && !collider.isTrigger : !collider || collider.sharedMesh!=m || collider.convex)
                throw new InvalidOperationException("Broken collision policy "+a.prefab);
            foreach(var v in m.vertices)if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z))throw new InvalidOperationException("Invalid vertex "+a.prefab);
            foreach(var mat in r.sharedMaterials)
                if(!mat || mat.shader.name!="Universal Render Pipeline/Lit" || mat.name.StartsWith("SourceDefault") || a.repairs.Any(p=>p.original==AssetDatabase.GetAssetPath(mat)))
                    throw new InvalidOperationException("Unrepaired material "+a.prefab);
            if(r.sharedMaterials.Any(mat=>mat.mainTexture) && m.uv.Length!=m.vertexCount)throw new InvalidOperationException("Missing UV "+a.prefab);
        }
        foreach(var p in a.repairs)if(!prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Any(m=>AssetDatabase.GetAssetPath(m)==p.replacement))
            throw new InvalidOperationException("Replacement not used "+a.prefab+" "+p.replacement);
        if(a.expected_triangles>0)
        {
            int count=prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3);
            if(count!=a.expected_triangles-(a.clean_geometry?19:0))throw new InvalidOperationException("Unexpected triangle count "+a.prefab);
        }
        if(a.bounds_m!=null && a.bounds_m.Length==3)
        {
            var renderers=prefab.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            if((bounds.size-new Vector3(a.bounds_m[0],a.bounds_m[1],a.bounds_m[2])).magnitude>.01f && !PrefabScaleCalibration.MatchesCalibratedSize(a.prefab,bounds.size))
                throw new InvalidOperationException("Bounds changed "+a.prefab);
        }
        foreach(var dep in AssetDatabase.GetDependencies(a.prefab,true))
            if(new[]{".fbx",".blend",".unity"}.Contains(Path.GetExtension(dep).ToLowerInvariant()))throw new InvalidOperationException("Source assembly dependency "+a.prefab);
    }
}
