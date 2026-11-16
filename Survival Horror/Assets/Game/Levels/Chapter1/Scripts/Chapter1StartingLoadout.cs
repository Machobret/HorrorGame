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
            // This component only grants the Chapter 1 map. It must never
            // alter the player's inventory, equipment, or slot capacity.
            if (StartingMap && !inventory.Maps.Contains(StartingMap))
                inventory.Maps.Add(StartingMap);
        }
    }
}
