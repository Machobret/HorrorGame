using System;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Enemies.Editor
{
    public static class MutantValidation
    {
        [MenuItem("Tools/Horror Game/Validate Mutant")]
        public static void Validate()
        {
            const string path = "Assets/Game/Enemies/Mutant/mutant.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab || prefab.name != "mutant") throw new Exception("Mutant prefab failed to load.");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!root.GetComponent<UnityEngine.AI.NavMeshAgent>() || !root.GetComponent<HorrorEngine.Health>())
                    throw new Exception("Mutant lost template gameplay components.");
                if (!root.GetComponentInChildren<SkinnedMeshRenderer>() || !root.GetComponentInChildren<Animator>())
                    throw new Exception("Mutant is missing its animated model.");
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long id))
                    throw new Exception("Could not resolve prefab reference.");
                Debug.Log("MUTANT_VALIDATION_OK guid=" + guid + " root=" + id);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
