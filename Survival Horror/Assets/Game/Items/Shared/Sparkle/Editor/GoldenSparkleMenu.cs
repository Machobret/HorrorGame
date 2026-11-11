using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Items.Editor
{
    public static class GoldenSparkleMenu
    {
        const string EffectPath = "Assets/Game/Items/Shared/Sparkle/Item_GoldenSparkle.prefab";
        const string HierarchyMenu = "GameObject/Horror Game/Add Golden Sparkle";
        const string AssetMenu = "Assets/Horror Game/Add Golden Sparkle";

        [MenuItem(HierarchyMenu, false, 20)]
        static void AddToObjects(MenuCommand command)
        {
            var clicked = command.context as GameObject;
            var targets = clicked && !Selection.gameObjects.Contains(clicked)
                ? new[] { clicked } : Selection.gameObjects;
            var prefab = LoadEffect();
            foreach (var target in targets)
            {
                if (!CanAddToObject(target)) continue;
                var added = AddSetup(target, prefab);
                Undo.RegisterCreatedObjectUndo(added, "Add Golden Sparkle and Trigger");
                EditorGUIUtility.PingObject(added);
            }
        }

        [MenuItem(HierarchyMenu, true)]
        static bool CanAddToObjects() => Selection.gameObjects.Any(CanAddToObject);

        [MenuItem(AssetMenu, false, 20)]
        static void AddToAssets()
        {
            var prefab = LoadEffect();
            foreach (var asset in Selection.gameObjects.Where(CanAddToAsset))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (HasCompleteSetup(root)) continue;
                    AddSetup(root, prefab);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"Added Golden Sparkle to {path}", asset);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        [MenuItem(AssetMenu, true)]
        static bool CanAddToAssets() => Selection.gameObjects.Any(CanAddToAsset);

        static bool CanAddToObject(GameObject target) => target &&
            !EditorUtility.IsPersistent(target) &&
            (target.hideFlags & HideFlags.NotEditable) == 0 &&
            !HasCompleteSetup(target);

        static bool HasCompleteSetup(GameObject target)
        {
            var sparkle = target.GetComponentInChildren<ItemGoldenSparkle>(true);
            return sparkle && target.GetComponentsInChildren<RoomSparkleTrigger>(true)
                .Any(trigger => trigger.Targets != null && trigger.Targets.Contains(sparkle));
        }

        static bool CanAddToAsset(GameObject asset)
        {
            if (!asset || !EditorUtility.IsPersistent(asset)) return false;
            string path = AssetDatabase.GetAssetPath(asset);
            return path.StartsWith("Assets/", StringComparison.Ordinal) &&
                path != EffectPath &&
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.IsOpenForEdit(path) &&
                !PrefabUtility.IsPartOfImmutablePrefab(asset) &&
                !HasCompleteSetup(asset);
        }

        static GameObject LoadEffect()
        {
            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
            if (!effect) throw new InvalidOperationException("Build the Golden Sparkle prefabs from Horror Game > Items first.");
            return effect;
        }

        static GameObject AddSetup(GameObject target, GameObject prefab)
        {
            var sparkle = target.GetComponentInChildren<ItemGoldenSparkle>(true);
            bool createdEffect = !sparkle;
            if (createdEffect) sparkle = AddChild(target, prefab).GetComponent<ItemGoldenSparkle>();
            var area = new GameObject("Sparkle_TriggerArea");
            area.transform.SetParent(sparkle.transform, false);
            // Default to a 1 x 1 x 1 metre area, even on small scaled pickup prefabs.
            Vector3 scale = area.transform.lossyScale;
            var box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.001f, Mathf.Abs(scale.y)),
                1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            var trigger = area.AddComponent<RoomSparkleTrigger>();
            trigger.Targets = new[] { sparkle };
            trigger.EntryDelay = 2f;
            // New effects use this trigger. Existing effects retain their visibility settings.
            return createdEffect ? sparkle.gameObject : area;
        }

        static GameObject AddChild(GameObject target, GameObject prefab)
        {
            // Position above the visible object, rather than inside an opaque mesh.
            var renderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(r => !(r is ParticleSystemRenderer)).ToArray();
            Vector3 position = target.transform.position + Vector3.up * .05f;
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                position = new Vector3(bounds.center.x, bounds.max.y + .035f, bounds.center.z);
            }
            var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.transform);
            child.transform.position = position;
            child.transform.rotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.transform);
            return child;
        }
    }
}
