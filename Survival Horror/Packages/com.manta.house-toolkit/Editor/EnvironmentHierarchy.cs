using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static class EnvironmentHierarchy
    {
        static EnvironmentHierarchy() => EditorSceneManager.sceneSaving += (scene, _) => Organize(scene);

        internal static void Organize(Scene scene)
        {
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
                PrefabStageUtility.GetCurrentPrefabStage()?.scene == scene) return;
            foreach (var root in scene.GetRootGameObjects()) Group(root);
        }

        internal static void Group(GameObject item)
        {
            if (item.transform.parent || item.hideFlags != HideFlags.None) return;
            string category = item.TryGetComponent<House>(out var house) ? (house.wallsOnly ? "Standalone Walls" : "Buildings")
                : item.GetComponent<FillerBuilding>() ? "Buildings"
                : item.GetComponent<RoadNetwork>() ? "Roads"
                : item.GetComponent<ProceduralShelf>() ? "Shelves"
                : item.GetComponent<GroundArea>() ? "Ground Areas"
                : item.GetComponent<ProceduralSidewalk>() ? "Sidewalks" : null;
            if (category == null) return;
            var group = item.scene.GetRootGameObjects().FirstOrDefault(go => go.name == category &&
                go.GetComponents<Component>().Length == 1 && !PrefabUtility.IsPartOfAnyPrefab(go) &&
                go.activeSelf && go.transform.localScale == Vector3.one && go.hideFlags == HideFlags.None);
            if (!group)
            {
                group = new GameObject(category);
                SceneManager.MoveGameObjectToScene(group, item.scene);
                Undo.RegisterCreatedObjectUndo(group, "Group " + category);
            }
            Undo.SetTransformParent(item.transform, group.transform, "Group " + category);
        }
    }
}
