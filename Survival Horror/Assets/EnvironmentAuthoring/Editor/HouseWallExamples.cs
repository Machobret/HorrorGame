using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEngine;
using System.Linq;

namespace EnvironmentAuthoring.Editor
{
    public static class HouseWallExamples
    {
        public const string ModuleReferenceScenePath = "Assets/EnvironmentAuthoring/Reference/WallModules/WallModuleDemo.unity";

        internal static Scene OpenSampleCopy(string templatePath)
        {
            var template = ScriptableObject.CreateInstance<SceneTemplateAsset>();
            try
            {
                template.templateScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(templatePath);
                // Share kit assets while copying the complete scene, including lighting.
                // Unity owns the untitled copy and its eventual Save As destination.
                template.dependencies = AssetDatabase.GetDependencies(templatePath)
                    // Scene contents are not importable subassets; load only their SceneAsset.
                    .SelectMany(path => path.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase)
                        ? new Object[] { AssetDatabase.LoadAssetAtPath<SceneAsset>(path) }
                        : AssetDatabase.LoadAllAssetsAtPath(path)).Where(asset => asset != null)
                    .Select(asset => new DependencyInfo { dependency = asset,
                        instantiationMode = TemplateInstantiationMode.Reference }).ToArray();
                var result = SceneTemplateService.Instantiate(template, false);
                if (result == null) return default;
                EditorSceneManager.MarkSceneDirty(result.scene);
                return result.scene;
            }
            finally { Object.DestroyImmediate(template); }
        }

        [MenuItem("Tools/Environment/Open Modular Reference Scene")]
        public static void OpenModuleReference()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ModuleReferenceScenePath);
        }
    }
}
