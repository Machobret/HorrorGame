using UnityEditor;
using UnityEngine;

namespace HorrorGame.Items.Editor
{
    public static class GoldenSparkleBuilder
    {
        const string Folder = "Assets/Game/Items/Shared/Sparkle/";
        [MenuItem("Horror Game/Items/Build Golden Sparkle Prefabs")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/ExampleContent/Effects/SparkleFX.prefab");
            if (!source) throw new System.InvalidOperationException("Template SparkleFX was not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Item_GoldenSparkle.mat");
            if (!material)
            {
                material = new Material(source.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                material.name = "Item_GoldenSparkle";
                material.DisableKeyword("_COLORCOLOR_ON");
                material.SetFloat("_ColorMode", 0);
                AssetDatabase.CreateAsset(material, Folder + "Item_GoldenSparkle.mat");
            }
            var effect = new GameObject("Item_GoldenSparkle");
            try
            {
                effect.name = "Item_GoldenSparkle";
                effect.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                effect.transform.localScale = Vector3.one;
                var ps = effect.AddComponent<ParticleSystem>();
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var main = ps.main;
                main.playOnAwake = false; main.loop = true; main.startSpeed = 0;
                main.startSize = .12f; main.startColor = new Color(1, .73f, .25f, .5f);
                main.maxParticles = 4;
                var emission = ps.emission; emission.enabled = false;
                var shape = ps.shape; shape.enabled = false;
                effect.AddComponent<ItemGoldenSparkle>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                PrefabUtility.SaveAsPrefabAsset(effect, Folder + "Item_GoldenSparkle.prefab");
            }
            finally { Object.DestroyImmediate(effect); }
            var room = new GameObject("Room_SparkleTrigger");
            try
            {
                room.AddComponent<BoxCollider>().size = new Vector3(8, 3, 8);
                room.GetComponent<BoxCollider>().center = new Vector3(0, 1.5f, 0);
                room.GetComponent<BoxCollider>().isTrigger = true;
                room.AddComponent<RoomSparkleTrigger>();
                PrefabUtility.SaveAsPrefabAsset(room, Folder + "Room_SparkleTrigger.prefab");
            }
            finally { Object.DestroyImmediate(room); }
            AssetDatabase.SaveAssets();
            Debug.Log("GOLDEN_SPARKLE_BUILD_OK: item effect and room trigger prefabs created.");
        }
    }
}
