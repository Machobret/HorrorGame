using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    public static class EnvironmentHierarchy
    {
        // Projects can classify their own authored scenery by prefab asset path.
        public static System.Func<string, string> PrefabCategory { private get; set; }
        static EnvironmentHierarchy() => EditorSceneManager.sceneSaving += (scene, _) => Organize(scene);

        internal static void Organize(Scene scene)
        {
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
                PrefabStageUtility.GetCurrentPrefabStage()?.scene == scene) return;
            var roots = scene.GetRootGameObjects();
            var groups = new Dictionary<string, GameObject>();
            foreach (var root in roots)
                if (IsEmptyParent(root)) groups.TryAdd(root.name, root);
            bool environmentScene = roots.Any(root => root.GetComponentInChildren<House>(true) ||
                root.GetComponentInChildren<ProceduralSurface>(true));
            foreach (var root in roots)
                Group(root, groups, environmentScene);
        }

        internal static void Group(GameObject item) => Group(item, null, false);

        static string Category(GameObject item, bool includePrefabs)
        {
            return item.TryGetComponent<House>(out var house) ? (house.wallsOnly ? "Standalone Walls" : "Buildings")
                : item.GetComponent<FillerBuilding>() ? "Buildings"
                : item.GetComponent<RoadNetwork>() ? "Roads"
                : item.GetComponent<ProceduralMarking>() ? "Markings"
                : item.GetComponent<ProceduralShelf>() ? "Shelves"
                : item.GetComponent<GroundArea>() ? "Ground Areas"
                : item.GetComponent<ProceduralSidewalk>() ? "Sidewalks"
                : includePrefabs && PrefabUtility.IsAnyPrefabInstanceRoot(item)
                    ? PrefabCategory?.Invoke(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item)) : null;
        }

        static bool IsEmptyParent(GameObject go) => go.activeSelf && go.hideFlags == HideFlags.None &&
            go.transform.localScale == Vector3.one && !PrefabUtility.IsPartOfAnyPrefab(go) && go.GetComponents<Component>().Length == 1;

        // An organizational root is transparent to room assignment. A box in Props
        // can belong to one room even when a different box belongs to another.
        internal static bool IsCategoryGroup(Transform node) =>
            !node.parent && IsEmptyParent(node.gameObject) && node.Cast<Transform>().Any(t => Category(t.gameObject, true) == node.name);

        static void Group(GameObject item, Dictionary<string, GameObject> groups, bool includePrefabs)
        {
            if (item.transform.parent || item.hideFlags != HideFlags.None) return;
            string category = Category(item, includePrefabs);
            if (category == null) return;
            GameObject group;
            if (groups == null) group = item.scene.GetRootGameObjects().FirstOrDefault(go => go.name == category && IsEmptyParent(go));
            else groups.TryGetValue(category, out group);
            if (!group)
            {
                group = new GameObject(category);
                SceneManager.MoveGameObjectToScene(group, item.scene);
                Undo.RegisterCreatedObjectUndo(group, "Group " + category);
                if (groups != null) groups[category] = group;
            }
            Undo.SetTransformParent(item.transform, group.transform, "Group " + category);
        }
    }
}
