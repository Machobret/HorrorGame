using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Only permits combining its configured items while the player is in the signal zone.</summary>
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/Cigarette Ladder Combination")]
    public sealed class CigaretteLadderInventoryCombination : InventoryItemCombination
    {
        public override bool CanCombine(InventoryEntry entry1, InventoryEntry entry2)
        {
            return base.CanCombine(entry1, entry2) && CigaretteLadderSignalArea.ActiveZone != null && CigaretteLadderSignalArea.ActiveZone.CanCombineNow();
        }

        public override InventoryEntry OnCombine(InventoryEntry entry1, InventoryEntry entry2)
        {
            GameManager.Instance.Inventory.Remove(entry1);
            GameManager.Instance.Inventory.Remove(entry2);
            CigaretteLadderSignalArea.ActiveZone?.TriggerFromInventoryCombination();
            return null;
        }
    }
}
