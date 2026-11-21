using System;
using System.Globalization;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class GunCasePickup : Pickup, ISavableObjectStateExtra
    {
        [SerializeField] private ReloadableWeaponData pistol;
        [SerializeField] private DocumentData manual;
        [SerializeField, Min(1)] private int spareRounds = 12;

        private const int PistolTaken = 1;
        private const int AmmoTaken = 2;
        private const int EmptyCase = PistolTaken | AmmoTaken;
        private int collected;

        public override void Take()
        {
            if (!isActiveAndEnabled || collected == EmptyCase || !pistol || !pistol.AmmoItem || !manual)
                return;

            var inventory = GameManager.Instance.Inventory;
            bool firstCollection = (collected & PistolTaken) == 0;
            if (firstCollection)
            {
                var entry = new InventoryEntry { Item = pistol, Count = 1, SecondaryCount = pistol.MaxAmmo };
                if (inventory.TryAddAndShowFullMessage(entry) == null)
                    return;
                collected |= PistolTaken;
            }

            var ammo = new InventoryEntry { Item = pistol.AmmoItem, Count = spareRounds };
            bool tookAmmo = (collected & AmmoTaken) == 0 &&
                (firstCollection ? inventory.Add(ammo) : inventory.TryAddAndShowFullMessage(ammo)) != null;
            if (tookAmmo)
                collected |= AmmoTaken;
            if (!firstCollection && !tookAmmo)
                return;

            bool firstRead = inventory.Documents.Add(manual);
            if (collected == EmptyCase)
                gameObject.SetActive(false);
            GetComponent<SavableObjectState>().SaveState();
            base.Take();

            var splash = UIManager.Get<UIItem>(true);
            Action next = null;
            if (firstRead)
            {
                DocumentData pickedUpManual = manual;
                next = () => splash.ShowDocumentPickup(pickedUpManual,
                    () => UIManager.Get<UIDocument>().Show(pickedUpManual, playShowSound: false));
            }
            if (tookAmmo)
            {
                Action afterAmmo = next;
                ItemData pickedUpAmmo = ammo.Item;
                next = () => splash.Show(pickedUpAmmo, afterAmmo);
            }

            if (firstCollection) splash.Show(pistol, next);
            else next?.Invoke();
        }

        public string GetSavableData() => collected.ToString(CultureInfo.InvariantCulture);

        public void SetFromSavedData(string savedData)
        {
            if (int.TryParse(savedData, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                collected = value & EmptyCase;
        }
    }
}
