using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Uses the normal item-pickup presentation, then immediately converts itself into a non-slot document unlock.</summary>
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/Circuit Diagram Unlock")]
    public sealed class CircuitDiagramUnlockItemData : ItemData
    {
        public DocumentData CircuitDiagram;

        public override void OnUse(InventoryEntry entry, Actor user)
        {
            base.OnUse(entry, user);
            if (GameManager.Exists && CircuitDiagram)
            {
                GameManager.Instance.Inventory.Documents.Add(CircuitDiagram);
                GameManager.Instance.Inventory.Remove(this);
            }
        }
    }
}
