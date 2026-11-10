using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    /// <summary>Applies the approved geometry scope using Unity asset operations.</summary>
    public static class Classic64GeometryScopeBuilder
    {
        const string PlanPath = "ArtSource/Environment/Classic64/GeometryScope.json";
        const string ResultPath = "Exports/Reviews/Classic64/Library/GeometryScopeResult.json";
        const string QualityPlanPath = "ArtSource/Environment/Classic64/FinishedAssetsScope.json";
        const string QualityResultPath = "Exports/Reviews/Classic64/Library/FinishedAssetsScopeResult.json";
        [Serializable] public class Plan { public string project_root; public bool preserve_scene_files; public string[] remove_assets, review_scenes, excluded_subjects; }
        [Serializable] public class Result { public bool success, preserved_scene_files; public string plan_sha256, error; public string[] removed_assets, updated_scenes; }

        static string Hash(string path)
        {
            using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        [MenuItem("Tools/Horror Game/Classic64/Apply Approved Geometry Exclusion")]
        public static void Apply() { ApplyPlan(PlanPath, ResultPath); }

        [MenuItem("Tools/Horror Game/Classic64/Apply Finished Assets Scope")]
        public static void ApplyQuality() { ApplyPlan(QualityPlanPath, QualityResultPath); }

        // Pending quality cleanup is invoked explicitly after scene-save scope is resolved.
        static void ApplyPendingQualityScope()
        {
            if (!File.Exists(QualityPlanPath)) return;
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(QualityPlanPath));
            if (!string.Equals(Path.GetFullPath(plan.project_root).TrimEnd('\\','/'),Path.GetFullPath(Path.Combine(Application.dataPath,"..")).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(QualityResultPath))
            {
                var previous=JsonUtility.FromJson<Result>(File.ReadAllText(QualityResultPath));
                if (previous.success&&previous.plan_sha256==Hash(QualityPlanPath)) return;
            }
            EditorApplication.delayCall+=QualityWhenReady;
        }

        static void QualityWhenReady()
        {
            if (EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=QualityWhenReady;return; }
            ApplyQuality();
        }

        static void ApplyPlan(string planPath,string resultPath)
        {
            var result = new Result { plan_sha256=Hash(planPath), removed_assets=Array.Empty<string>(),updated_scenes=Array.Empty<string>() };
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying the geometry scope");
                var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));
                if (!string.Equals(Path.GetFullPath(plan.project_root).TrimEnd('\\','/'),Path.GetFullPath(Path.Combine(Application.dataPath,"..")).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Cleanup project does not match approved plan");
                var removed=new HashSet<string>(plan.remove_assets);
                if (removed.Any(p=>!p.StartsWith("Assets/Game/Environment/",StringComparison.Ordinal)||!Path.GetFileName(p).StartsWith("Classic64_",StringComparison.Ordinal)))
                    throw new InvalidOperationException("Cleanup contains an unowned path");
                var scenes=new HashSet<string>(plan.review_scenes);
                for (int index=0;index<SceneManager.sceneCount;index++)
                {
                    var loaded=SceneManager.GetSceneAt(index);
                    if (scenes.Contains(loaded.path)) continue;
                    foreach (var root in loaded.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        string prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject);
                        var mesh=transform.GetComponent<MeshFilter>();
                        if (removed.Contains(prefab)||(mesh&&mesh.sharedMesh&&removed.Contains(AssetDatabase.GetAssetPath(mesh.sharedMesh))))
                            throw new InvalidOperationException("Excluded geometry is used in loaded scene "+loaded.path+": "+transform.name);
                    }
                }
                foreach (string consumer in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&!removed.Contains(p)&&!scenes.Contains(p)))
                {
                    string ext=Path.GetExtension(consumer).ToLowerInvariant();
                    if (ext!=".unity"&&ext!=".prefab"&&ext!=".asset"&&ext!=".mat"&&ext!=".controller"&&ext!=".overridecontroller"&&ext!=".fbx") continue;
                    if (AssetDatabase.GetDependencies(consumer,false).Any(removed.Contains)) throw new InvalidOperationException("Excluded asset still used by "+consumer);
                }
                var active=SceneManager.GetActiveScene();
                foreach (string path in plan.preserve_scene_files ? Array.Empty<string>() : plan.review_scenes)
                {
                    var scene=SceneManager.GetSceneByPath(path); bool wasLoaded=scene.IsValid()&&scene.isLoaded;
                    if (!wasLoaded) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    var destroy=new HashSet<GameObject>();
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        var go=transform.gameObject;
                        string prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                        if (removed.Contains(prefab)) destroy.Add(PrefabUtility.GetNearestPrefabInstanceRoot(go));
                        if (go.name=="MasonrySurfaces / Display copies"||go.name=="Windows / Display copies") destroy.Add(go);
                        if (plan.excluded_subjects.Any(n=>go.name==n+"_DisplayBase"||go.name==n.Replace("Classic64_","")||go.name.StartsWith(n.Replace("Classic64_","")+" [",StringComparison.Ordinal))) destroy.Add(go);
                        if (plan.excluded_subjects.Any(n=>go.name==n+"_DisplayBase")&&transform.parent)
                        {
                            Vector3 dimensionPosition=transform.localPosition+new Vector3(0,-.42f,-1.25f);
                            foreach (var text in transform.parent.GetComponentsInChildren<TextMesh>(true))
                                if (text.transform.parent==transform.parent&&(text.transform.localPosition-dimensionPosition).sqrMagnitude<.00001f) destroy.Add(text.gameObject);
                        }
                    }
                    // Remove highest selected roots only, preserving all other user scene data.
                    foreach (var go in destroy.Where(g=>g&&!g.transform.GetComponentsInParent<Transform>(true).Skip(1).Any(t=>destroy.Contains(t.gameObject))).ToArray()) Object.DestroyImmediate(go);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var text in root.GetComponentsInChildren<TextMesh>(true)) text.text=text.text.Replace("12 PROPS", "11 PROPS");
                    if (!EditorSceneManager.SaveScene(scene,path)) throw new InvalidOperationException("Cannot update review scene "+path);
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene,true);
                    if (active.IsValid()&&active.isLoaded) SceneManager.SetActiveScene(active);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                }
                foreach (string path in removed.OrderBy(p=>p.EndsWith(".prefab",StringComparison.Ordinal)?0:1))
                    if (File.Exists(path)&&!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("AssetDatabase could not remove "+path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                result.success=true;result.removed_assets=plan.remove_assets;result.updated_scenes=plan.preserve_scene_files ? Array.Empty<string>() : plan.review_scenes;result.preserved_scene_files=plan.preserve_scene_files;
                Debug.Log("CLASSIC64_SCOPE_APPLIED plan="+planPath+" removed_assets="+removed.Count);
            }
            catch (Exception e) { result.error=e.ToString(); Debug.LogException(e); }
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath,JsonUtility.ToJson(result,true));
        }
    }
}
