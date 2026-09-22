using System;
using System.IO;
using HorrorGame.General;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.General.Editor
{
    /// <summary>Keeps the main-menu carousel in sync with MenuBg*.png assets in General/UI/Menu.</summary>
    public sealed class MenuBackgroundCarouselSynchronizer : AssetPostprocessor
    {
        private const string MenuAssetFolder = "Assets/General/UI/Menu";
        private const string LogoSequenceFolder = "Assets/General/UI/LogoSheet";
        private const string MainMenuPrefab = "Assets/AssetStore/HorrorEngine/Prefabs/UI/UIMainMenu.prefab";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (ContainsMenuBackground(imported) || ContainsMenuBackground(deleted) || ContainsMenuBackground(moved) || ContainsMenuBackground(movedFrom) ||
                ContainsLogoFrame(imported) || ContainsLogoFrame(deleted) || ContainsLogoFrame(moved) || ContainsLogoFrame(movedFrom))
                EditorApplication.delayCall += Synchronize;
        }

        [MenuItem("Horror Game/Refresh Main Menu Backgrounds")]
        private static void Synchronize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefab);
            var carousel = prefab ? prefab.GetComponent<MainMenuBackgroundCarousel>() : null;
            if (!carousel) return;

            string[] assetGuids = AssetDatabase.FindAssets("t:Sprite", new[] { MenuAssetFolder });
            Array.Sort(assetGuids, (left, right) => string.Compare(
                Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(left)),
                Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(right)),
                StringComparison.OrdinalIgnoreCase));

            var serialized = new SerializedObject(carousel);
            var backgrounds = serialized.FindProperty("m_Backgrounds");
            backgrounds.arraySize = 0;
            foreach (string guid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(assetPath).StartsWith("MenuBg", StringComparison.OrdinalIgnoreCase))
                    continue;

                int index = backgrounds.arraySize++;
                backgrounds.GetArrayElementAtIndex(index).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }

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

        private static bool ContainsMenuBackground(string[] paths)
        {
            foreach (string path in paths)
                if (path.StartsWith(MenuAssetFolder, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileNameWithoutExtension(path).StartsWith("MenuBg", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
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
