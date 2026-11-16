using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Applies Chapter 1's fresh-test loadout after saved player state has loaded.</summary>
    public sealed class Chapter1StartingLoadout : MonoBehaviour
    {
        public MapData StartingMap;

        private IEnumerator Start()
        {
            // The GameManager restores its persistent character state during
            // startup. Wait until that work and the equipment visuals finish.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!GameManager.Exists || GameManager.Instance.Inventory == null) yield break;
            Inventory inventory = GameManager.Instance.Inventory;
            inventory.Unequip(EquipmentSlot.Primary);
            inventory.Unequip(EquipmentSlot.Secondary);
            inventory.Clear();
            // Saved games can restore the old eight-slot capacity.
            inventory.ResetCapacity(6);
            if (StartingMap) inventory.Maps.Add(StartingMap);
        }
    }
}
