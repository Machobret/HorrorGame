using HorrorEngine;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>
    /// An invisible trigger zone.  It has no world interaction or prompt: it only allows
    /// the cigarette/lighter inventory combination while the player is inside it.
    /// </summary>
    public sealed class CigaretteLadderSignalArea : MonoBehaviour
    {
        [Header("Inventory combination")]
        public CigaretteLadderInventoryCombination Combination;
        [Header("Result")]
        public Chapter1LadderSignalCutscene Sequence;
        public bool CanOnlyUseOnce = true;

        public bool HasBeenUsed { get; private set; }
        public static CigaretteLadderSignalArea ActiveZone { get; private set; }

        private void Start()
        {
            if (!GameManager.Exists || GameManager.Instance.InventorySetup == null || Combination == null) return;
            if (GameManager.Instance.InventorySetup.Combinations == null)
                GameManager.Instance.InventorySetup.Combinations = new List<InventoryItemCombination>();
            if (!GameManager.Instance.InventorySetup.Combinations.Contains(Combination))
                GameManager.Instance.InventorySetup.Combinations.Add(Combination);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerActor>()) ActiveZone = this;
        }

        private void OnTriggerExit(Collider other)
        {
            if (ActiveZone == this && other.GetComponentInParent<PlayerActor>()) ActiveZone = null;
        }

        private void OnDisable()
        {
            if (ActiveZone == this) ActiveZone = null;
        }

        public bool CanCombineNow()
        {
            return ActiveZone == this && (!CanOnlyUseOnce || !HasBeenUsed);
        }

        public void TriggerFromInventoryCombination()
        {
            if (!CanCombineNow()) return;
            HasBeenUsed = true;
            UIManager.Get<UIInventory>()?.Hide();
            if (Sequence) Sequence.Play();
        }
    }
}
