using System;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace HorrorGame.Items.Editor
{
    /// <summary>Rebuilds the prop without changing scenes or existing item registrations.</summary>
    public static class LighterBuilder
    {
        const string Folder = "Assets/Game/Items/Lighter/";
        const string ItemPath = Folder + "Lighter.asset";
        const string PrefabPath = Folder + "Lighter_Pickup.prefab";
        const string ExamPath = Folder + "Lighter_Examination.prefab";

        [MenuItem("Horror Game/Items/Build Lighter")]
        public static void Build()
        {
            ConfigureTexture("Lighter_Albedo.png", false);
            ConfigureTexture("Lighter_PickupIcon.png", true);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Lighter.fbx");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Weapons/Ammunition/HuntingRifleAmmo/Prefabs/HuntingRifleAmmo_Pickup.prefab");
            if (!model || !shader || !source) throw new Exception("Lighter source model/shader/pickup template missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Lighter_Enamel.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "Lighter_Enamel.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Lighter_Albedo.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .08f); material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);

            var exam = MakeVisual(model, material);
            GameObject examination;
            try { examination = PrefabUtility.SaveAsPrefabAsset(exam, ExamPath); }
            finally { Object.DestroyImmediate(exam); }
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
            if (!item) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, ItemPath); }
            if (string.IsNullOrEmpty(item.UniqueId)) item.UniqueId = Guid.NewGuid().ToString();
            item.Name = Text("Lighter");
            item.Description = Text("A worn metal flip-top lighter. Its lid is open.");
            item.Image = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Lighter_PickupIcon.png");
            item.ExamineModel = examination;
            item.InventoryAction = InventoryMainAction.None;
            item.Flags = ItemFlags.Examinable | ItemFlags.Droppable | ItemFlags.CreatePickupOnDrop;
            item.Tags = new[] { "Lighter", "PuzzleItem" };
            item.MaxStackSize = 1;

            var interaction = AssetDatabase.LoadAssetAtPath<InteractionData>(Folder + "Lighter_Interaction.asset");
            if (!interaction)
            {
                interaction = Object.Instantiate(source.GetComponentInChildren<Interactive>(true).Data);
                interaction.name = "Lighter_Interaction";
                AssetDatabase.CreateAsset(interaction, Folder + "Lighter_Interaction.asset");
            }
            interaction.Prompt = Text("Take lighter");
            EditorUtility.SetDirty(interaction);
            var root = new GameObject("Lighter_Pickup");
            GameObject prefab;
            try
            {
                var identity = root.AddComponent<ObjectUniqueId>();
                var previous = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (previous) identity.SetId(previous.GetComponent<ObjectUniqueId>().Id);
                else identity.RegenerateId();
                identity.IsUniqueInstance = false; identity.IntentionallyDuplicated = false;
                var spawnable = root.AddComponent<SpawnableSavable>();
                root.AddComponent<PooledGameObject>();
                var pickup = root.AddComponent<PickupItem>();
                pickup.Entry = new InventoryEntry { Item = item, Count = 1 };
                pickup.ShowItemSplash = true;
                pickup.PickupSound = source.GetComponent<PickupItem>().PickupSound;
                pickup.OnPickup = new UnityEvent();
                var visual = MakeVisual(model, material);
                visual.name = "Lighter_Visual";
                visual.transform.SetParent(root.transform, false);
                // Rest the lighter flat with its front facing up.
                visual.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                visual.transform.localPosition = new Vector3(0, .008f, 0);
                var trigger = new GameObject("Interaction");
                trigger.layer = LayerMask.NameToLayer("InteractionSystem");
                trigger.transform.SetParent(root.transform, false);
                var box = trigger.AddComponent<BoxCollider>();
                box.isTrigger = true; box.size = new Vector3(.5f, 1, .5f); box.center = new Vector3(0, .5f, 0);
                var interactive = trigger.AddComponent<Interactive>();
                interactive.Data = interaction; interactive.OnInteract = new OnInteractionEvent();
                interactive.OnFinishInteract = new UnityEvent(); interactive.OnDataChanged = new UnityEvent();
                UnityEventTools.AddVoidPersistentListener(interactive.OnInteract, pickup.Take);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            item.DropPrefab = prefab.GetComponent<SpawnableSavable>();
            EditorUtility.SetDirty(item);
            var itemDB = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/ItemDB.asset");
            var spawnDB = AssetDatabase.LoadAssetAtPath<SpawnableSavableDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/SpawnableSavableDB.asset");
            if (!itemDB || !spawnDB) throw new Exception("Active item databases missing.");
            if (!itemDB.Registers.Contains(item)) itemDB.Registers.Add(item);
            if (!spawnDB.Entries.Contains(item.DropPrefab)) spawnDB.Entries.Add(item.DropPrefab);
            EditorUtility.SetDirty(itemDB); EditorUtility.SetDirty(spawnDB);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("LIGHTER_OK: textured Survival Zippo prop, inventory/pickup sprite, examination and registered pickup/drop prefab. Pickup prop only; ignition behavior is not added.");
        }

        static LocalizableText Text(string value) => new LocalizableText { IsLocalized = false, Unlocalized = value };
        static void ConfigureTexture(string name, bool sprite)
        {
            string path = Folder + name;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = sprite;
            importer.mipmapEnabled = !sprite;
            importer.filterMode = sprite ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }
        static GameObject MakeVisual(GameObject model, Material material)
        {
            var visual = Object.Instantiate(model); visual.name = "Lighter_Examination";
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = material;
            return visual;
        }
        [MenuItem("Horror Game/Items/Validate Lighter")]
        public static void Validate()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!item || !item.Image || !item.ExamineModel || !prefab || prefab.GetComponent<PickupItem>().Entry.Item != item)
                throw new Exception("Missing lighter pickup, icon, item, or examination reference.");
            var interaction = prefab.GetComponentInChildren<Interactive>(true);
            if (!interaction || interaction.OnInteract.GetPersistentMethodName(0) != "Take" || interaction.OnInteract.GetPersistentTarget(0) != prefab.GetComponent<PickupItem>())
                throw new Exception("Lighter pickup interaction is not connected.");
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (!filter.sharedMesh || filter.sharedMesh.uv.Length != filter.sharedMesh.vertexCount)
                    throw new Exception("Missing lighter mesh or UVs.");
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                if (!renderer.sharedMaterial || !renderer.sharedMaterial.GetTexture("_BaseMap")) throw new Exception("Missing carton texture.");
            var itemDB = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/ItemDB.asset");
            var spawnDB = AssetDatabase.LoadAssetAtPath<SpawnableSavableDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/SpawnableSavableDB.asset");
            itemDB.HashRegisters(); spawnDB.HashRegisters();
            if (itemDB.GetRegister(item.UniqueId) != item || spawnDB.GetPrefab(prefab.GetComponent<ObjectUniqueId>().Id) != prefab)
                throw new Exception("Lighter item registration does not resolve.");
            if (spawnDB.Entries.Count(e => e && e.GetComponent<ObjectUniqueId>().Id == prefab.GetComponent<ObjectUniqueId>().Id) != 1)
                throw new Exception("Lighter prefab ID collides with another drop.");
            var probe = Object.Instantiate(prefab);
            try
            {
                probe.GetComponent<ObjectUniqueId>().RegenerateId();
                probe.GetComponent<SpawnableSavable>().SetPrefab(prefab);
                if (probe.GetComponent<ObjectUniqueId>().Id == prefab.GetComponent<ObjectUniqueId>().Id)
                    throw new Exception("Lighter instance duplicated its prefab ID.");
            }
            finally { Object.DestroyImmediate(probe); }
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Exports/Reviews/Lighter/Validation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, "PASS: item and spawn registrations, unique prefab ID, native Take callback, textured mesh UVs, pickup/inventory sprite and examination model.\n");
        }
    }
}
