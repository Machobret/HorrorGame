using System;
using System.Collections;
using System.Collections.Generic;
using HorrorEngine;
using HouseToolkit;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class RandomShutterKeyPuzzle : MonoBehaviour, ISavableObjectStateExtra
    {
        internal static RandomShutterKeyPuzzle TabletBayPuzzle { get; private set; }
        [Tooltip("One standard key pickup per shutter, ordered left then right.")]
        public PickupItem[] PickupSlots;
        [Tooltip("The possible keys. The selected key also configures the linked door.")]
        public ItemData[] PossibleKeys;
        [Tooltip("Preferred: the DoorLockKeyItem on the existing door prefab.")]
        public DoorLockKeyItem LockedDoor;
        [Tooltip("One document pickup behind each shutter, ordered left then right.")]
        public PickupDocument[] NoteSlots;
        public GameObject MonsterPrefab;
        [Tooltip("One monster spawn point per shutter, in the same order as the key pickups.")]
        public Transform[] MonsterSpawns = Array.Empty<Transform>();
        private readonly GarageDoor[] shutters = new GarageDoor[2];
        private readonly NavMeshLinkInstance[] shutterLinks = new NavMeshLinkInstance[2];
        private readonly NavMeshObstacle[] shutterObstacles = new NavMeshObstacle[2];
        private readonly float[] previousHeights = new float[2];
        private readonly List<(string Id, bool Requested)> pendingCircuits = new();
        private MutuallyExclusiveShutterPair pair;
        private UIMap tablet;
        private int firstOpened = -1;
        private bool keyTaken, noteTaken, monsterRevealed, monsterDefeated;
        private Health monster;
        private bool HasMonsterEncounter => MonsterPrefab && MonsterSpawns.Length == 2 && MonsterSpawns[0] && MonsterSpawns[1];
        private bool UsesOpeningOrder => shutters[0] && shutters[1];

        [SerializeField] private int selectedSlot = -1;
        [SerializeField] private int selectedKey = -1;

        private void Awake()
        {
            if (TryGetComponent(out pair))
            {
                shutters[0] = pair.LeftShutter ? pair.LeftShutter.GetComponent<GarageDoor>() : null;
                shutters[1] = pair.RightShutter ? pair.RightShutter.GetComponent<GarageDoor>() : null;
            }
            if (HasMonsterEncounter)
            {
                TabletBayPuzzle = this;
                for (int i = 0; i < shutters.Length; i++)
                    if (shutters[i] && shutters[i].leaf)
                        shutterObstacles[i] = shutters[i].leaf.GetComponent<NavMeshObstacle>();
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

        private void OnDestroy()
        {
            if (tablet) tablet.Closed -= ApplyTabletChanges;
            if (TabletBayPuzzle == this) TabletBayPuzzle = null;
        }

        private void OnDisable()
        {
            for (int i = 0; i < shutterLinks.Length; i++)
            {
                if (shutterLinks[i].valid) shutterLinks[i].Remove();
                if (shutterObstacles[i]) shutterObstacles[i].enabled = true;
            }
        }

        internal bool TryQueueCircuit(DistrictPowerGrid grid, string id, out bool changed)
        {
            changed = false;
            int index = GetBayIndex(grid, id);
            if (index < 0) return false;
            if (!tablet)
            {
                tablet = UIManager.Get<UIMap>(true);
                if (!tablet) return true;
                tablet.Closed += ApplyTabletChanges;
            }
            bool request = !IsBayRequested(grid, id);
            if (request && !pair.Independent)
            {
                string other = GetOtherBayId(index);
                bool wasRequested = IsBayRequested(grid, other);
                pendingCircuits.RemoveAll(change => change.Id == other && change.Requested);
                if (wasRequested) grid.ReportCircuitDisplacement(other);
            }
            pendingCircuits.RemoveAll(change => change.Id == id);
            if (request != grid.IsCircuitRequested(id)) pendingCircuits.Add((id, request));
            changed = true;
            return true;
        }

        internal bool TryGetQueuedCircuit(DistrictPowerGrid grid, string id, out bool requested)
        {
            requested = false;
            int bay = GetBayIndex(grid, id);
            if (bay < 0) return false;
            int queued = pendingCircuits.FindIndex(value => value.Id == id);
            if (queued >= 0)
            {
                requested = pendingCircuits[queued].Requested;
                return true;
            }
            return !pair.Independent && grid.IsCircuitRequested(id) &&
                pendingCircuits.Exists(change => change.Id == GetOtherBayId(bay) && change.Requested);
        }

        private bool IsBayRequested(DistrictPowerGrid grid, string id) =>
            TryGetQueuedCircuit(grid, id, out bool requested) ? requested : grid.IsCircuitRequested(id);

        private string GetOtherBayId(int index) => (index == 0 ? pair.RightShutter : pair.LeftShutter).CircuitId;

        private int GetBayIndex(DistrictPowerGrid grid, string id)
        {
            if (!pair || !grid || !pair.LeftShutter || !pair.RightShutter) return -1;
            if (pair.LeftShutter.Grid == grid && pair.LeftShutter.CircuitId == id) return 0;
            return pair.RightShutter.Grid == grid && pair.RightShutter.CircuitId == id ? 1 : -1;
        }

        private void ApplyTabletChanges()
        {
            if (pendingCircuits.Count == 0) return;
            var changes = pendingCircuits.ToArray();
            pendingCircuits.Clear();
            StartCoroutine(ApplyTabletChanges(changes));
        }

        private IEnumerator ApplyTabletChanges((string Id, bool Requested)[] changes)
        {
            var grid = pair.LeftShutter.Grid;
            foreach (var change in changes)
            {
                if (!grid || !grid.HasSupply || grid.IsShutdown) yield break;
                int index = GetBayIndex(grid, change.Id);
                if (index < 0) continue;
                if (change.Requested && firstOpened < 0) RecordFirstOpened(index);
                if (change.Requested && index == firstOpened && !monster && !monsterDefeated)
                {
                    RevealMonster();
                    // Let the spawned actor finish its first frame before energizing the motor.
                    yield return null;
                    if (!grid.HasSupply || grid.IsShutdown) yield break;
                }
                grid.SetCircuit(change.Id, change.Requested);
            }
        }

        private void LateUpdate()
        {
            if (HasMonsterEncounter) UpdateShutterLinks();
            if (!UsesOpeningOrder || Time.deltaTime <= 0f || (tablet && tablet.isActiveAndEnabled)) return;
            if (firstOpened < 0)
            {
                for (int i = 0; i < shutters.Length; i++)
                {
                    var shutter = shutters[i];
                    if (!shutter || !shutter.leaf) continue;
                    float height = shutter.leaf.transform.localPosition.y;
                    if (height > previousHeights[i] + .0001f)
                    {
                        RecordFirstOpened(i);
                        break;
                    }
                    previousHeights[i] = height;
                }
            }
            if (!HasMonsterEncounter || monster || monsterDefeated || firstOpened < 0) return;
            if (!monsterRevealed && !shutters[firstOpened].IsOpen) return;
            RevealMonster();
        }

        private void UpdateShutterLinks()
        {
            for (int i = 0; i < shutters.Length; i++)
            {
                var shutter = shutters[i];
                if (!shutter) continue;
                bool open = shutter.IsOpen;
                if (shutterObstacles[i]) shutterObstacles[i].enabled = !open;
                if (!open)
                {
                    if (shutterLinks[i].valid) shutterLinks[i].Remove();
                    continue;
                }
                if (shutterLinks[i].valid) continue;

                Vector3 inside = MonsterSpawns[i].position;
                Vector3 offset = Vector3.ProjectOnPlane(inside - shutter.transform.position, Vector3.up).normalized;
                Vector3 outside = shutter.transform.position - offset * 1.45f;
                if (!NavMesh.SamplePosition(inside, out var from, .7f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(outside, out var to, .55f, NavMesh.AllAreas)) continue;
                shutterLinks[i] = NavMesh.AddLink(new NavMeshLinkData
                {
                    startPosition = from.position, endPosition = to.position,
                    width = .65f, costModifier = -1f, bidirectional = true
                });
            }
        }

        private void RecordFirstOpened(int index)
        {
            firstOpened = index;
            Apply();
            GetComponent<SavableObjectState>().SaveState();
        }

        private void RevealMonster()
        {
            Transform spawn = MonsterSpawns[firstOpened];
            var encounter = Instantiate(MonsterPrefab, spawn.position, spawn.rotation, spawn);
            if (TryGetComponent(out StorageCombatTutorial tutorial))
                tutorial.ConfigureRunner(encounter);
            if (encounter.TryGetComponent(out monster))
                monster.OnDeath.AddListener(OnMonsterDefeated);
            monsterRevealed = true;
            GetComponent<SavableObjectState>().SaveState();
        }

        private void OnMonsterDefeated(Health health)
        {
            monsterDefeated = true;
            GetComponent<SavableObjectState>().SaveState();
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
                bool showKey = !keyTaken && (UsesOpeningOrder ? firstOpened >= 0 && i != firstOpened : i == selectedSlot);
                pickup.gameObject.SetActive(showKey);
            }
            for (int i = 0; NoteSlots != null && i < NoteSlots.Length; i++)
            {
                if (NoteSlots[i]) NoteSlots[i].gameObject.SetActive(i == firstOpened && !noteTaken);
            }
        }

        public string GetSavableData() => JsonUtility.ToJson(new State
        {
            Slot = selectedSlot, Key = selectedKey, FirstOpened = firstOpened + 1, KeyTaken = keyTaken, NoteTaken = noteTaken,
            MonsterRevealed = monsterRevealed, MonsterDefeated = monsterDefeated
        });
        public void SetFromSavedData(string data)
        {
            State state = JsonUtility.FromJson<State>(data);
            selectedSlot = state.Slot; selectedKey = state.Key;
            firstOpened = state.FirstOpened - 1;
            if (firstOpened < 0 || firstOpened >= shutters.Length) firstOpened = -1;
            keyTaken = state.KeyTaken;
            noteTaken = state.NoteTaken;
            monsterRevealed = state.MonsterRevealed;
            monsterDefeated = state.MonsterDefeated;
            Apply();
        }
        [Serializable] private struct State
        {
            public int Slot, Key, FirstOpened;
            public bool KeyTaken, NoteTaken, MonsterRevealed, MonsterDefeated;
        }
    }
}
