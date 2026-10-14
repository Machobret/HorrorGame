using System;
using HorrorEngine;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Weapons.Editor
{
    public static class WeaponPresentationBuilder
    {
        const string Root = "Assets/Game/Weapons/";
        [MenuItem("Horror Game/Weapons/Apply Custom Audio and Icons")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "Revolver", "HuntingRifle", "FlareGun" })
            {
                string shotPath = Root + "Audio/" + name + "_Shot.wav";
                ConfigureAudio(shotPath);
                var data = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(Root + "Data/" + name + ".asset");
                data.ShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>(shotPath);
                string reloadPath = Root + "Audio/" + name + "_Reload.wav";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(reloadPath))
                {
                    ConfigureAudio(reloadPath);
                    data.ReloadSound = AssetDatabase.LoadAssetAtPath<AudioClip>(reloadPath);
                }
                string iconPath = Root + "Models/" + name + "/" + name + "_Icon.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.maxTextureSize = 1024;
                importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                data.Image = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (!data.ShotSound || !data.Image) throw new Exception("Missing presentation asset " + name);
                if (data.ShotSound.length > 3 || data.ShotSound.channels != 1) throw new Exception("Incorrect shot import " + name);
                if (data.ReloadSound && data.ReloadSound.length > data.ReloadDuration) throw new Exception("Reload clip exceeds animation duration " + name);
                var montage = data.EquipPrefab.GetComponent<AttackMontage>();
                if (!montage.Attack || data.EquipPrefab.GetComponent<Weapon>().WeaponData != data) throw new Exception("Incorrect weapon/audio wiring " + name);
                string prefabPath = AssetDatabase.GetAssetPath(data.EquipPrefab);
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var shotAudio = contents.GetComponent<WeaponShotAudio>();
                    if (!shotAudio) shotAudio = contents.AddComponent<WeaponShotAudio>();
                    shotAudio.Volume = name == "FlareGun" ? 1.25f : 1;
                    contents.GetComponent<AudioSource>().spatialBlend = 0;
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                EditorUtility.SetDirty(data);
                Debug.Log("WEAPON_PRESENTATION_OK " + name + " shot=" + data.ShotSound.length + " icon=" + data.Image.texture.width);
            }
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        static void ConfigureAudio(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        }
    }
}
