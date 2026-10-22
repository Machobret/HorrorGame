using System;
using System.Collections.Generic;
using HorrorEngine;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Weapons.Editor
{
    // Copy into Assets/.../Editor in an isolated validation checkout. Call before
    // entering Play Mode; the parent play test covers real UI/state transitions.
    public static class InventoryReloadChecks
    {
        static int checks;

        public static void RunInventoryChecks()
        {
            checks = 0;
            var paths = new[]
            {
                "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset",
                "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset",
                "Assets/Game/Weapons/Data/HuntingRifle.asset",
                "Assets/Game/Weapons/Data/FlareGun.asset",
                "Assets/Game/Weapons/Data/Revolver.asset"
            };
            foreach (var path in paths)
            {
                var data = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(path);
                Check(data && data.AmmoItem && data.MaxAmmo > 0, "Missing reloadable data: " + path);
                CheckWeapon(data);
            }
            Debug.Log("INVENTORY_RELOAD_CHECKS_OK assertions=" + checks);
        }

        static Inventory Setup(ReloadableWeaponData data, float loaded, float reserve,
            out InventoryEntry gun, out InventoryEntry ammo)
        {
            var inventory = new Inventory { Items = new List<InventoryEntry>() };
            gun = new InventoryEntry { Item = data, Count = 1, SecondaryCount = loaded };
            ammo = new InventoryEntry { Item = data.AmmoItem, Count = reserve };
            inventory.Items.Add(gun);
            inventory.Items.Add(ammo);
            // A standalone inventory has no player equipment to instantiate.
            inventory.Equipped[inventory.WeaponSlot] = gun;
            return inventory;
        }

        static void CheckWeapon(ReloadableWeaponData data)
        {
            string name = data.name + ": ";
            float capacity = data.MaxAmmo;
            var inventory = Setup(data, 0, capacity + 3, out var gun, out var ammo);
            inventory.RequestWeaponReload(gun, ammo);
            inventory.RequestWeaponReload(gun, ammo);
            Check(gun.SecondaryCount == 0 && ammo.Count == capacity + 3,
                name + "combine or repeated combine transferred ammunition before animation");
            Check(inventory.HasPendingEquippedWeaponReload(), name + "valid request was not queued");
            var save = inventory.GetSavableData();
            Check(save.Items[0].ItemId == data.UniqueId && save.Items[0].SecondaryCount == 0 &&
                save.Items[1].ItemId == data.AmmoItem.UniqueId && save.Items[1].Count == capacity + 3,
                name + "pending reload changed saved IDs or ammunition counts");

            // Closing/cancelling the menu must not itself transfer anything.
            Check(inventory.HasPendingEquippedWeaponReload() && gun.SecondaryCount == 0 &&
                ammo.Count == capacity + 3, name + "queued reload was consumed by observation");
            inventory.ReloadWeapon(gun);
            Check(gun.SecondaryCount == capacity && ammo.Count == 3,
                name + "completion did not conserve ammunition");
            Check(!inventory.HasPendingEquippedWeaponReload(), name + "completion left another reload queued");
            inventory.ReloadWeapon(gun);
            Check(gun.SecondaryCount == capacity && ammo.Count == 3,
                name + "a duplicate completion consumed more ammunition");

            inventory = Setup(data, 0, 1, out gun, out ammo);
            var nextStack = new InventoryEntry { Item = data.AmmoItem, Count = capacity + 2 };
            inventory.Items.Add(nextStack);
            inventory.RequestWeaponReload(gun, ammo);
            inventory.ReloadWeapon(gun);
            Check(gun.SecondaryCount == capacity && ammo.Count + nextStack.Count == 3,
                name + "multi-stack refill lost ammunition or stopped after the first stack");
            Check(!inventory.HasPendingEquippedWeaponReload(), name + "multi-stack completion left request pending");

            inventory = Setup(data, 0, capacity + 3, out gun, out ammo);
            inventory.RequestWeaponReload(gun, ammo);
            inventory.Equipped.Clear();
            Check(!inventory.HasPendingEquippedWeaponReload(), name + "unequipped gun triggered a reload");
            // The queue follows weapon identity even when equip moves it into a new entry.
            var movedGun = new InventoryEntry();
            movedGun.Copy(gun);
            inventory.Equipped[inventory.WeaponSlot] = movedGun;
            Check(inventory.HasPendingEquippedWeaponReload(), name + "unequipped request was lost");
            Check(movedGun.SecondaryCount == 0 && ammo.Count == capacity + 3,
                name + "equipping a queued gun instantly transferred ammo");
            // An interrupted animation does not call completion. Its request and counts remain.
            Check(inventory.HasPendingEquippedWeaponReload() && movedGun.SecondaryCount == 0,
                name + "interrupted reload lost request or granted ammunition");
            inventory.ReloadWeapon(movedGun);
            Check(movedGun.SecondaryCount == capacity && ammo.Count == 3 &&
                !inventory.HasPendingEquippedWeaponReload(), name + "retried reload did not complete once");

            inventory = Setup(data, capacity, 5, out gun, out ammo);
            inventory.RequestWeaponReload(gun, ammo);
            Check(!inventory.HasPendingEquippedWeaponReload() && ammo.Count == 5,
                name + "full weapon accepted a reload");
            inventory = Setup(data, 0, 0, out gun, out ammo);
            inventory.RequestWeaponReload(gun, ammo);
            Check(!inventory.HasPendingEquippedWeaponReload() && !inventory.CanReloadWeapon(gun),
                name + "zero-count reserve was accepted");

            inventory = Setup(data, 0, capacity + 3, out gun, out ammo);
            var wrongAmmo = new InventoryEntry { Item = data, Count = 9 };
            inventory.RequestWeaponReload(gun, wrongAmmo);
            inventory.ReloadWeapon(gun, wrongAmmo);
            Check(!inventory.HasPendingEquippedWeaponReload() && gun.SecondaryCount == 0 && wrongAmmo.Count == 9,
                name + "mismatched ammunition was accepted");
            inventory.RequestWeaponReload(gun, ammo);
            inventory.Remove(gun);
            var replacement = new InventoryEntry { Item = data, Count = 1 };
            inventory.Items.Add(replacement);
            inventory.Equipped[inventory.WeaponSlot] = replacement;
            Check(!inventory.HasPendingEquippedWeaponReload(), name + "dropped weapon left a stale request");

            inventory.RequestWeaponReload(replacement, ammo);
            Check(inventory.HasPendingEquippedWeaponReload(), name + "replacement queue was not accepted");
            inventory.Clear();
            inventory.Items = new List<InventoryEntry> { replacement, ammo };
            inventory.Equipped[inventory.WeaponSlot] = replacement;
            Check(!inventory.HasPendingEquippedWeaponReload(), name + "inventory reset retained a stale request");
        }

        static void Check(bool condition, string message)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
