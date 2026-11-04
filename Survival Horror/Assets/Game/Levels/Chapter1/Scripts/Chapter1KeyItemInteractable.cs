using HorrorEngine;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame.Chapter1
{
    /// <summary>Configurable powered interaction: can require/consume an inventory item, grant an item, and raise the fire alarm.</summary>
    public sealed class Chapter1KeyItemInteractable : MonoBehaviour
    {
        [Header("Identity")]
        public string Prompt = "Use";
        public bool CanOnlyActivateOnce = true;
        [Header("Power gate")]
        public DistrictPowerGrid Grid;
        public string RequiredCircuitId;
        [Header("Key item exchange")]
        public ItemData NeedsKeyItem;
        public bool ConsumeRequiredItem = true;
        public ItemData GivesKeyItem;
        public int GivenAmount = 1;
        [Header("Consequences")]
        public bool StartsFireAlarm;
        public UnityEvent OnActivated;
        public UnityEvent OnPowerMissing;
        public UnityEvent OnItemMissing;
        public bool Activated { get; private set; }

        private void Awake()
        {
            Interactive interactive = GetComponent<Interactive>();
            if (interactive != null) interactive.OnInteract.AddListener(_ => Activate());
        }
        private void OnMouseDown() => Activate();

        public void Activate()
        {
            if (Activated && CanOnlyActivateOnce) return;
            if (Grid != null && !string.IsNullOrWhiteSpace(RequiredCircuitId) && !Grid.IsCircuitPowered(RequiredCircuitId)) { OnPowerMissing?.Invoke(); return; }
            if (NeedsKeyItem && (GameManager.Instance == null || !GameManager.Instance.Inventory.Contains(NeedsKeyItem))) { OnItemMissing?.Invoke(); return; }
            if (NeedsKeyItem && ConsumeRequiredItem) GameManager.Instance.Inventory.Remove(NeedsKeyItem);
            if (GivesKeyItem && GameManager.Instance != null) GameManager.Instance.Inventory.Add(GivesKeyItem, Mathf.Max(1, GivenAmount));
            Activated = true;
            OnActivated?.Invoke();
            if (StartsFireAlarm) DistrictFireAlarm.Raise(this);
        }
    }

    public sealed class DistrictFireAlarm : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        public static event System.Action AlarmRaised;
        public static void Raise(Object source)
        {
            if (IsActive) return;
            IsActive = true;
            AlarmRaised?.Invoke();
            Debug.Log("District fire alarm confirmed by " + source.name, source);
        }
    }
}
