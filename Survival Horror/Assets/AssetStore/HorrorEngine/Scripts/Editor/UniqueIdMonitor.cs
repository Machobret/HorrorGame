using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    [InitializeOnLoad]
    public static class HierarchyMonitor
    {
        private static Dictionary<string, ObjectUniqueId> knownOwners = new Dictionary<string, ObjectUniqueId>();
        static HierarchyMonitor()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.delayCall += CheckUniqueIdDuplication;
        }

        // --------------------------------------------------------------------

        static void OnHierarchyChanged()
        {
            CheckUniqueIdDuplication();
        }

        // --------------------------------------------------------------------

        static void OnSceneSaving(Scene scene, string path)
        {
            CheckUniqueIdDuplication();
        }

        // --------------------------------------------------------------------

        static void CheckUniqueIdDuplication()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Dictionary<string, ObjectUniqueId> hashSet = new Dictionary<string, ObjectUniqueId>();
            // Keep established objects first so duplicating a rack changes the
            // new copies' IDs rather than the originals' saved identities.
            var all = GameObject.FindObjectsByType<ObjectUniqueId>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(obj => knownOwners.TryGetValue(obj.Id ?? "", out var owner) && owner == obj ? 0 : 1);
            foreach (var obj in all)
            {
                ObjectUniqueId uniqueId = obj as ObjectUniqueId;
                // Prefab assets and Prefab Mode retain their template IDs. Only
                // placed scene instances need independent save identities.
                if (EditorUtility.IsPersistent(uniqueId) ||
                    StageUtility.GetStageHandle(uniqueId.gameObject) != StageUtility.GetMainStageHandle())
                    continue;

                bool canAssignSceneId = !uniqueId.IsUniqueInstance && !uniqueId.IntentionallyDuplicated;
                bool reusablePrefab = canAssignSceneId &&
                    PrefabUtility.IsPartOfPrefabInstance(uniqueId);
                ObjectUniqueId source = reusablePrefab ? PrefabUtility.GetCorrespondingObjectFromSource(uniqueId) : null;
                ObjectUniqueId originalSource = reusablePrefab ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(uniqueId) : null;
                bool inheritedId = reusablePrefab &&
                    ((source && uniqueId.Id == source.Id) || (originalSource && uniqueId.Id == originalSource.Id));
                bool duplicatedId = hashSet.TryGetValue(uniqueId.Id ?? "", out var existing) &&
                    !uniqueId.IntentionallyDuplicated && !existing.IntentionallyDuplicated;

                if (canAssignSceneId && (inheritedId || duplicatedId || string.IsNullOrEmpty(uniqueId.Id)))
                {
                    Undo.RecordObject(uniqueId, "Assign unique scene object ID");
                    do { uniqueId.RegenerateId(); } while (hashSet.ContainsKey(uniqueId.Id));
                    if (PrefabUtility.IsPartOfPrefabInstance(uniqueId))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(uniqueId);
                    EditorUtility.SetDirty(uniqueId);
                    EditorSceneManager.MarkSceneDirty(uniqueId.gameObject.scene);
                }

                if (!hashSet.ContainsKey(uniqueId.Id ?? ""))
                {
                    hashSet.Add(uniqueId.Id ?? "", uniqueId);
                }
                else
                {
                    var otherObject = hashSet[uniqueId.Id ?? ""];
                    if (!otherObject.IntentionallyDuplicated && !uniqueId.IntentionallyDuplicated)
                        Debug.LogError($"The Id on the ObjectUniqueId component is already in use by {hashSet[uniqueId.Id]}, you need to resolve this conflict or save/load won't work correctly", uniqueId);
                }
            }
            knownOwners = hashSet;
        }
    }
}
