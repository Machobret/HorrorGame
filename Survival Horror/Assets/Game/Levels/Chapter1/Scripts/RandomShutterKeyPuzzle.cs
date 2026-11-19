using System;
using HorrorEngine;
using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class RandomShutterKeyPuzzle : MonoBehaviour, ISavableObjectStateExtra
    {
        [Tooltip("One standard key pickup per shutter, ordered left then right.")]
        public PickupItem[] PickupSlots;
        [Tooltip("The possible keys. The selected key also configures the linked door.")]
        public ItemData[] PossibleKeys;
        [Tooltip("Preferred: the DoorLockKeyItem on the existing door prefab.")]
        public DoorLockKeyItem LockedDoor;
        [Tooltip("One document pickup behind each shutter, ordered left then right.")]
        public PickupDocument[] NoteSlots;
        private readonly GarageDoor[] shutters = new GarageDoor[2];
        private readonly float[] previousHeights = new float[2];
        private int firstOpened = -1;
        private bool keyTaken, noteTaken;
        private bool UsesOpeningOrder => shutters[0] && shutters[1];

        [SerializeField] private int selectedSlot = -1;
        [SerializeField] private int selectedKey = -1;

        private void Awake()
        {
            if (TryGetComponent(out MutuallyExclusiveShutterPair pair))
            {
                shutters[0] = pair.LeftShutter ? pair.LeftShutter.GetComponent<GarageDoor>() : null;
                shutters[1] = pair.RightShutter ? pair.RightShutter.GetComponent<GarageDoor>() : null;
            }

            if (selectedKey < 0 || !UsesOpeningOrder && selectedSlot < 0) Generate();
            if (UsesOpeningOrder)
            {
                foreach (var slot in PickupSlots)
                    if (slot) (slot.OnPickup ??= new UnityEngine.Events.UnityEvent()).AddListener(() => RecordPickup(false));
                foreach (var slot in NoteSlots)
                    if (slot) (slot.OnPickup ??= new UnityEngine.Events.UnityEvent()).AddListener(() => RecordPickup(true));
            }
            Apply();
        }

        private void LateUpdate()
        {
            if (!UsesOpeningOrder || firstOpened >= 0 || Time.deltaTime <= 0f) return;
            for (int i = 0; i < shutters.Length; i++)
            {
                var shutter = shutters[i];
                if (!shutter || !shutter.leaf) continue;
                float height = shutter.leaf.transform.localPosition.y;
                if (height > previousHeights[i] + .0001f)
                {
                    firstOpened = i;
                    Apply();
                    GetComponent<SavableObjectState>().SaveState();
                    return;
                }
                previousHeights[i] = height;
            }
        }

        private void RecordPickup(bool document)
        {
            if (document) noteTaken = true;
            else keyTaken = true;
            GetComponent<SavableObjectState>().SaveState();
        }

        private void Generate()
        {
            selectedSlot = !UsesOpeningOrder && PickupSlots != null && PickupSlots.Length > 0 ? UnityEngine.Random.Range(0, PickupSlots.Length) : -1;
            selectedKey = PossibleKeys != null && PossibleKeys.Length > 0 ? UnityEngine.Random.Range(0, PossibleKeys.Length) : -1;
        }

        private void Apply()
        {
            ItemData key = selectedKey >= 0 && PossibleKeys != null && selectedKey < PossibleKeys.Length ? PossibleKeys[selectedKey] : null;
            if (LockedDoor) LockedDoor.ConfigureKey(key);
            for (int i = 0; PickupSlots != null && i < PickupSlots.Length; ++i)
            {
                PickupItem pickup = PickupSlots[i];
                if (!pickup) continue;
                pickup.Entry.Item = key;
                bool showKey = UsesOpeningOrder ? firstOpened >= 0 && i != firstOpened && !keyTaken : i == selectedSlot;
                pickup.gameObject.SetActive(showKey);
            }
            for (int i = 0; NoteSlots != null && i < NoteSlots.Length; i++)
            {
                if (NoteSlots[i]) NoteSlots[i].gameObject.SetActive(i == firstOpened && !noteTaken);
            }
        }

        public string GetSavableData() => JsonUtility.ToJson(new State { Slot = selectedSlot, Key = selectedKey, FirstOpened = firstOpened + 1, KeyTaken = keyTaken, NoteTaken = noteTaken });
        public void SetFromSavedData(string data)
        {
            State state = JsonUtility.FromJson<State>(data);
            selectedSlot = state.Slot; selectedKey = state.Key;
            firstOpened = state.FirstOpened - 1;
            keyTaken = state.KeyTaken;
            noteTaken = state.NoteTaken;
            Apply();
        }
        [Serializable] private struct State { public int Slot; public int Key; public int FirstOpened; public bool KeyTaken; public bool NoteTaken; }
    }
}
