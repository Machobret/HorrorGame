using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    [CustomEditor(typeof(MeshDecal))]
    internal sealed class MeshDecalInspector : UnityEditor.Editor
    {
        int family;

        void OnEnable()
        {
            family = WallDecalTool.AtlasIndex(((MeshDecal)target).atlas);
        }

        public override void OnInspectorGUI()
        {
            var decal = (MeshDecal)target;
            var previous = decal.atlas ? decal.atlas.Find(decal.poster) : null;
            WallDecalTool.DrawAtlas(ref family);
            var choices = WallDecalTool.ChoicesForAtlas(family);
            int current = choices.FindIndex(c => c.atlas == decal.atlas && c.poster.id == decal.poster);
            int next = WallDecalTool.DrawSwatches(choices, current);
            if (next < 0 || next == current) return;
            var choice = choices[next];
            Undo.RecordObjects(new Object[] { decal, decal.transform, decal.GetComponent<MeshRenderer>(), decal.gameObject }, "Replace decal");
            var scale = decal.transform.localScale;
            if (previous != null && previous.size.x > 0 && previous.size.y > 0)
            {
                scale.x *= choice.poster.size.x / previous.size.x;
                scale.y *= choice.poster.size.y / previous.size.y;
            }
            decal.transform.localScale = scale;
            if (decal.name == previous?.label) decal.name = choice.poster.label;
            decal.atlas = choice.atlas;
            decal.poster = choice.poster.id;
            decal.Refresh();
            foreach (var item in new Object[] { decal, decal.transform, decal.GetComponent<MeshRenderer>(), decal.gameObject })
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoad]
    internal static class LegacyDecalMigration
    {
        static LegacyDecalMigration()
        {
            EditorSceneManager.sceneOpened += (scene, _) => Convert(scene);
            EditorApplication.delayCall += () =>
            {
                for (int i = 0; i < SceneManager.sceneCount; i++) Convert(SceneManager.GetSceneAt(i));
            };
        }

        internal static void Convert(Scene scene)
        {
            if (!scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorSceneManager.IsPreviewScene(scene) || PrefabStageUtility.GetCurrentPrefabStage()?.scene == scene) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var old in root.GetComponentsInChildren<WallDecals>(true))
                {
                    int count = old.placements.Count;
                    if (old.Detach()) Object.DestroyImmediate(old);
                    else if (old.placements.Count == count) continue;
                    else PrefabUtility.RecordPrefabInstancePropertyModifications(old);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
        }
    }
}
