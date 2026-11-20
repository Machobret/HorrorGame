using System;
using System.IO;
using HorrorGame.General;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.General.Editor
{
    /// <summary>Keeps the main menu logo animation in sync with the branding assets.</summary>
    public sealed class MenuBackgroundCarouselSynchronizer : AssetPostprocessor
    {
        private const string LogoSequenceFolder = "Assets/Game/UI/Branding/LogoSequence";
        private const string MainMenuPrefab = "Assets/AssetStore/HorrorEngine/Prefabs/UI/UIMainMenu.prefab";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (ContainsLogoFrame(imported) || ContainsLogoFrame(deleted) || ContainsLogoFrame(moved) || ContainsLogoFrame(movedFrom))
                EditorApplication.delayCall += Synchronize;
        }

        [MenuItem("Horror Game/Refresh Main Menu Logo Sequence")]
        private static void Synchronize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefab);
            var carousel = prefab ? prefab.GetComponent<MainMenuBackgroundCarousel>() : null;
            if (!carousel) return;

            var serialized = new SerializedObject(carousel);
            string[] logoGuids = AssetDatabase.FindAssets("t:Sprite", new[] { LogoSequenceFolder });
            Array.Sort(logoGuids, (left, right) => string.Compare(
                Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(left)),
                Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(right)),
                StringComparison.OrdinalIgnoreCase));

            var logoSequence = serialized.FindProperty("m_LogoSequence");
            logoSequence.arraySize = 0;
            foreach (string guid in logoGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(assetPath).Equals("LogoSheet", StringComparison.OrdinalIgnoreCase))
                    continue;

                int index = logoSequence.arraySize++;
                logoSequence.GetArrayElementAtIndex(index).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
        }

        [InitializeOnLoadMethod]
        private static void ScheduleInitialSynchronize()
        {
            EditorApplication.delayCall += Synchronize;
        }

        private static bool ContainsLogoFrame(string[] paths)
        {
            foreach (string path in paths)
                if (path.StartsWith(LogoSequenceFolder, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
