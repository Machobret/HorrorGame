using System;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Items.Editor
{
    public static class CigaretteCartonBuilder
    {
        const string Folder = "Assets/Game/Items/CigarettePack/";
        const string Output = Folder + "CigaretteCarton_Pickup.prefab";

        [MenuItem("Horror Game/Items/Build Cigarette Carton")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CigarettePack_Pickup.prefab");
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(Folder + "CigarettePack.asset");
            var originalMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "CigarettePack_Paper.mat");
            var database = AssetDatabase.LoadAssetAtPath<SpawnableSavableDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/SpawnableSavableDB.asset");
            if (!source || !item || !originalMaterial || !database)
                throw new InvalidOperationException("Cigarette pack sources or spawn database missing.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "CigaretteCarton_Paper.mat");
            if (!material)
            {
                material = new Material(originalMaterial);
                AssetDatabase.CreateAsset(material, Folder + "CigaretteCarton_Paper.mat");
            }
            // Retain the same point-filtered face atlas, with warmer red paper and burgundy printing.
            material.SetTexture("_BaseMap", originalMaterial.GetTexture("_BaseMap"));
            material.SetColor("_BaseColor", new Color(1f, .65f, .60f, 1));
            EditorUtility.SetDirty(material);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "CigaretteCarton_Pickup";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var identity = root.GetComponent<ObjectUniqueId>();
                var previous = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
                if (previous) identity.SetId(previous.GetComponent<ObjectUniqueId>().Id);
                else identity.RegenerateId();
                identity.IsUniqueInstance = false;
                identity.IntentionallyDuplicated = false;

                var visual = root.transform.Find("CigarettePack_Visual");
                if (!visual) throw new InvalidOperationException("Source pack visual was not found.");
                var holder = new GameObject("CigaretteCarton_Visual").transform;
                holder.SetParent(root.transform, false);
                visual.SetParent(holder, true);
                var renderers = holder.GetComponentsInChildren<MeshRenderer>(true);
                Bounds bounds = BoundsOf(renderers);
                Vector3 size = bounds.size;
                if (Mathf.Min(size.x, size.y, size.z) <= .000001f)
                    throw new InvalidOperationException("Source cigarette mesh has invalid bounds.");
                Vector3 cartonSize = CartonSizeForPack(size);
                holder.localScale = new Vector3(cartonSize.x / size.x, cartonSize.y / size.y, cartonSize.z / size.z);
                bounds = BoundsOf(renderers);
                holder.position -= new Vector3(bounds.center.x, bounds.min.y - .002f, bounds.center.z);
                foreach (var renderer in renderers) renderer.sharedMaterial = material;

                // Native Take handles inventory-full rejection, the single-pack splash, and deactivation.
                var pickup = root.GetComponent<PickupItem>();
                pickup.Entry = new InventoryEntry { Item = item, Count = 1 };
                pickup.ShowItemSplash = true;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Output);
                var spawnable = prefab.GetComponent<SpawnableSavable>();
                if (!database.Entries.Contains(spawnable)) database.Entries.Add(spawnable);
                EditorUtility.SetDirty(database);
                // Keep item.DropPrefab pointing to the regular pack, not the carton.
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Validate();
            var built = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
            Vector3 measured = BoundsOf(built.GetComponentsInChildren<MeshRenderer>(true)).size;
            Debug.Log($"CIGARETTE_CARTON_BUILD_OK: measured {measured.x * 100:F1} x {measured.z * 100:F1} x {measured.y * 100:F1} cm. Five packs across, two deep, plus 1 cm total packaging allowance per axis. Gives one regular pack.");
        }

        static Vector3 CartonSizeForPack(Vector3 packSize)
        {
            // A real retail carton contains ten packs, in two rows of five.
            // Sort the measured pack axes so its resting orientation does not affect the calculation.
            float[] sides = { packSize.x, packSize.y, packSize.z };
            Array.Sort(sides);
            const float packagingAllowance = .01f;
            return new Vector3(5 * sides[1] + packagingAllowance,
                2 * sides[0] + packagingAllowance, sides[2] + packagingAllowance);
        }

        static Bounds BoundsOf(MeshRenderer[] renderers)
        {
            if (renderers.Length == 0) throw new InvalidOperationException("Cigarette mesh has no renderer.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Horror Game/Items/Validate Cigarette Carton")]
        public static void Validate()
        {
            var carton = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CigarettePack_Pickup.prefab");
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(Folder + "CigarettePack.asset");
            if (!carton || !pack || !item) throw new InvalidOperationException("Cigarette carton content is missing.");
            var pickup = carton.GetComponent<PickupItem>();
            var interaction = carton.GetComponentInChildren<Interactive>(true);
            if (pickup.Entry.Item != item || pickup.Entry.Count != 1 || !pickup.ShowItemSplash ||
                interaction.OnInteract.GetPersistentTarget(0) != pickup ||
                interaction.OnInteract.GetPersistentMethodName(0) != "Take")
                throw new InvalidOperationException("Carton must award exactly one pack through native Take.");
            if (carton.GetComponent<ObjectUniqueId>().Id == pack.GetComponent<ObjectUniqueId>().Id ||
                item.DropPrefab != pack.GetComponent<SpawnableSavable>())
                throw new InvalidOperationException("Carton ID or regular pack drop reference is incorrect.");
            var database = AssetDatabase.LoadAssetAtPath<SpawnableSavableDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/SpawnableSavableDB.asset");
            database.HashRegisters();
            if (database.GetPrefab(carton.GetComponent<ObjectUniqueId>().Id) != carton)
                throw new InvalidOperationException("Carton spawn registration did not resolve.");
            var size = BoundsOf(carton.GetComponentsInChildren<MeshRenderer>(true)).size;
            Vector3 expected = CartonSizeForPack(BoundsOf(pack.GetComponentsInChildren<MeshRenderer>(true)).size);
            if ((size - expected).sqrMagnitude > .00001f)
                throw new InvalidOperationException("Carton dimensions are incorrect.");
        }
    }
}
