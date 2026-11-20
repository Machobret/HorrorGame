using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    [InitializeOnLoad]
    internal static class NeonSignPlacement
    {
        const string SignFolder = "Assets/Game/Environment/NeonSigns/";

        static NeonSignPlacement()
        {
            DragAndDrop.AddDropHandlerV2(DropOnWall);
        }

        static DragAndDropVisualMode DropOnWall(Object target, Vector3 position, Vector2 viewport,
            Transform parent, bool perform)
        {
            var objects = DragAndDrop.objectReferences;
            if (objects.Length != 1 || objects[0] is not GameObject prefab ||
                !PrefabUtility.IsPartOfPrefabAsset(prefab) ||
                !AssetDatabase.GetAssetPath(prefab).StartsWith(SignFolder, StringComparison.Ordinal))
                return DragAndDropVisualMode.None;

            if (Event.current == null || !HandleUtility.PlaceObject(Event.current.mousePosition, out var hit, out var normal) ||
                normal.sqrMagnitude < 0.9f || Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.35f)
                return DragAndDropVisualMode.None;

            if (perform)
            {
                var scene = target is GameObject surface && surface.scene.IsValid() ? surface.scene : SceneManager.GetActiveScene();
                Selection.activeGameObject = Place(prefab, hit, normal, scene, parent);
            }
            return DragAndDropVisualMode.Copy;
        }

        internal static GameObject Place(GameObject prefab, Vector3 surface, Vector3 normal, Scene scene, Transform parent = null)
        {
            var sign = (GameObject)(parent ? PrefabUtility.InstantiatePrefab(prefab, parent) :
                PrefabUtility.InstantiatePrefab(prefab, scene));
            sign.transform.SetPositionAndRotation(surface + normal * 0.006f, Quaternion.LookRotation(-normal, Vector3.up));
            PrefabUtility.RecordPrefabInstancePropertyModifications(sign.transform);
            Undo.RegisterCreatedObjectUndo(sign, "Place neon sign");
            return sign;
        }
    }
}
