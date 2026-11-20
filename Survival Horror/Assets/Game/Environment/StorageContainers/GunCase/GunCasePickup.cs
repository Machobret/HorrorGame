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
            string summary = firstCollection
                ? (tookAmmo ? $"Collected a loaded 1911 and {spareRounds} spare rounds."
                    : "Collected a loaded 1911. Spare rounds remain in the case.")
                : $"Collected {spareRounds} spare rounds.";

            if (collected == EmptyCase)
                gameObject.SetActive(false);
            GetComponent<SavableObjectState>().SaveState();
            base.Take();

            if (firstRead)
                UIManager.Get<UIDocument>().Show(manual, playShowSound: false, openingCaption: summary);
            else
                ShowSummary(summary);
        }

        private static void ShowSummary(string summary)
        {
            var dialog = new DialogData();
            dialog.SetLines(new[] { new DialogLine { LineText = new LocalizableText { Unlocalized = summary } } });
            UIManager.Get<UIDialog>().Show(dialog);
        }

        public string GetSavableData() => collected.ToString(CultureInfo.InvariantCulture);

        public void SetFromSavedData(string savedData)
        {
            if (int.TryParse(savedData, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                collected = value & EmptyCase;
        }
    }
}
