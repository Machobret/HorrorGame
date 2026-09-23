using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HorrorGame.Enemies.Editor
{
    public static class BruteNavigationTools
    {
        private const string Menu = "Tools/Horror Game/Bake Navigation for Open Scene";

        [MenuItem(Menu, true)]
        private static bool CanBake() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            PrefabStageUtility.GetCurrentPrefabStage() == null;

        [MenuItem(Menu)]
        public static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            var surfaces = new List<NavMeshSurface>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var surface in root.GetComponentsInChildren<NavMeshSurface>())
                    if (surface.isActiveAndEnabled && surface.agentTypeID == 0)
                        surfaces.Add(surface);

            if (surfaces.Count == 0)
            {
                var navigation = new GameObject("Navigation");
                SceneManager.MoveGameObjectToScene(navigation, scene);
                Undo.RegisterCreatedObjectUndo(navigation, "Create scene navigation");
                surfaces.Add(Undo.AddComponent<NavMeshSurface>(navigation));
            }

            foreach (var surface in surfaces)
            {
                if (NavMeshAssetManager.instance.IsSurfaceBaking(surface)) continue;
                Undo.RecordObject(surface, "Configure scene navigation");
                // Include unmarked floors too. Agents are excluded by NavMeshSurface.
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = LayerMask.GetMask("Default");
                EditorUtility.SetDirty(surface);
                NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = surfaces[0].gameObject;
            Debug.Log("Navigation bake started using Default-layer floor and wall colliders. " +
                "When it finishes, save the scene and place the Brute and player on the blue NavMesh.", surfaces[0]);
        }
    }
}
