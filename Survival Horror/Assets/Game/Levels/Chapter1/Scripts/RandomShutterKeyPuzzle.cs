using System;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class RandomShutterKeyPuzzle : MonoBehaviour, ISavableObjectStateExtra
    {
        [Tooltip("One standard PickupItem behind each shutter. Only one is enabled each run.")]
        public PickupItem[] PickupSlots;
        [Tooltip("The possible keys. The selected key also configures the linked door.")]
        public ItemData[] PossibleKeys;
        [Tooltip("Preferred: the DoorLockKeyItem on the existing door prefab.")]
        public DoorLockKeyItem LockedDoor;
        [SerializeField] private int selectedSlot = -1;
        [SerializeField] private int selectedKey = -1;

        private void Awake()
        {
            if (selectedSlot < 0 || selectedKey < 0) Generate();
            Apply();
        }

        private void Generate()
        {
            selectedSlot = PickupSlots != null && PickupSlots.Length > 0 ? UnityEngine.Random.Range(0, PickupSlots.Length) : -1;
            selectedKey = PossibleKeys != null && PossibleKeys.Length > 0 ? UnityEngine.Random.Range(0, PossibleKeys.Length) : -1;
        }

        private void Apply()
        {
            ItemData key = selectedKey >= 0 && selectedKey < PossibleKeys.Length ? PossibleKeys[selectedKey] : null;
            if (LockedDoor) LockedDoor.ConfigureKey(key);
            for (int i = 0; PickupSlots != null && i < PickupSlots.Length; ++i)
            {
                PickupItem pickup = PickupSlots[i];
                if (!pickup) continue;
                pickup.Entry.Item = key;
                pickup.gameObject.SetActive(i == selectedSlot);
            }
        }

        public string GetSavableData() => JsonUtility.ToJson(new State { Slot = selectedSlot, Key = selectedKey });
        public void SetFromSavedData(string data)
        {
            State state = JsonUtility.FromJson<State>(data);
            selectedSlot = state.Slot; selectedKey = state.Key;
            Apply();
        }
        [Serializable] private struct State { public int Slot; public int Key; }
    }
}
