using HorrorEngine;
using HorrorGame.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Owns the first-tablet map tutorial for Chapter 1.</summary>
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class Chapter1MapTutorial : MonoBehaviour, ISavableObjectStateExtra
    {
        public static Chapter1MapTutorial Instance { get; private set; }

        [Tooltip("Leave off for Chapter 1: M is unavailable until the tablet is used.")]
        public bool StartsUnlocked;
        public bool CollectTabletWithoutOpening;
        public DistrictPowerGrid Grid;
        public MapData MapToUnlock;
        [Tooltip("Drag the circuit-dot GameObject from PowerPanelController here. Its map and ID drive the tutorial automatically.")]
        public PowerCircuitPanelElement TutorialCircuit;
        public string TargetCircuitId = "store_shutters";
        [TextArea] public string QuestText = "Activate circuit";

        public bool MapUnlocked { get; private set; }
        public bool TutorialActive { get; private set; }
        public bool TutorialCircuitUnlocked { get; private set; }
        public string CurrentQuest { get; private set; }

        public static bool CanOpenMap => Instance == null || Instance.MapUnlocked;
        private float nextCircuitScan;
        private readonly HashSet<string> discoveredCircuits = new HashSet<string>();

        private void Update()
        {
            if (!MapUnlocked || !GameManager.Exists || !GameManager.Instance.Player || !UIManager.Exists) return;
            var mapView = UIManager.Get<UIMap>(true);
            if ((mapView && mapView.isActiveAndEnabled) ||
                (PauseController.Exists && PauseController.Instance.IsPaused)) return;

            float now = Time.unscaledTime;
            if (now < nextCircuitScan) return;
            nextCircuitScan = now + .2f;
            var controller = MapController.GetCurrent();
            if (!controller || !controller.Data) return;
            var room = controller.GetCurrentRoom();
            if (!room) return;

            int discovered = 0;
            Vector3 player = GameManager.Instance.Player.transform.position;
            foreach (var dot in FindObjectsByType<PowerCircuitPanelElement>(FindObjectsSortMode.None))
            {
                if (dot.Room != room || dot.GetMapData() != controller.Data || string.IsNullOrWhiteSpace(dot.CircuitId))
                    continue;
                Vector3 offset = room.transform.TransformPoint(new Vector3(dot.RoomOffset.x, 0f, dot.RoomOffset.y)) - player;
                offset.y = 0f;
                if (offset.sqrMagnitude <= dot.ActivationRange * dot.ActivationRange &&
                    discoveredCircuits.Add(controller.Data.UniqueId + ":" + dot.CircuitId))
                    discovered++;
            }
            if (discovered == 0) return;
            GameplayNotifications.CircuitsDiscovered(discovered);
            GetComponent<SavableObjectState>()?.SaveState();
        }
        public static bool CanCloseMap => Instance == null || Instance.CollectTabletWithoutOpening || !Instance.TutorialActive;
        public static bool IsTargetCircuit(MapData map, string circuitId)
        {
            if (!Instance || (!Instance.TutorialActive && !Instance.TutorialCircuitUnlocked)) return false;
            MapData targetMap = Instance.TutorialCircuit ? Instance.TutorialCircuit.GetMapData() : Instance.MapToUnlock;
            string targetId = Instance.TutorialCircuit ? Instance.TutorialCircuit.CircuitId : Instance.TargetCircuitId;
            return targetMap == map && targetId == circuitId;
        }

        /// <summary>Sets the shared Chapter 1 top-right quest text.</summary>
        public static void SetQuest(string quest)
        {
            if (Instance) Instance.CurrentQuest = quest;
        }

        public static void ClearQuest()
        {
            if (Instance) Instance.CurrentQuest = string.Empty;
        }

        private void Awake()
        {
            Instance = this;
            MapUnlocked = StartsUnlocked;
            if (!Grid) Grid = FindFirstObjectByType<DistrictPowerGrid>();
        }

        private IEnumerator Start()
        {
            // Let the inventory and this object's SavableObjectState restore
            // first, then make the tablet's unlocked map resilient even in a
            // prototype scene without a rebuilt MapDatabase.
            yield return null;
            if (MapUnlocked && MapToUnlock && GameManager.Exists && GameManager.Instance.Inventory != null && !GameManager.Instance.Inventory.Maps.Contains(MapToUnlock))
                GameManager.Instance.Inventory.Maps.Add(MapToUnlock);
            if (TutorialActive || TutorialCircuitUnlocked) EnsureTutorialOverlay();
            if (TutorialActive) SubscribeGrid();
        }

        private void OnDestroy()
        {
            if (Grid) Grid.CircuitChanged -= OnCircuitChanged;
            if (Instance == this) Instance = null;
        }

        public void UnlockAndStart(MapData map)
        {
            MapUnlocked = true;
            if (map) MapToUnlock = map;
            if (MapToUnlock && GameManager.Exists && GameManager.Instance.Inventory != null && !GameManager.Instance.Inventory.Maps.Contains(MapToUnlock))
                GameManager.Instance.Inventory.Maps.Add(MapToUnlock);

            TutorialActive = true;
            CurrentQuest = CollectTabletWithoutOpening ? string.Empty : QuestText;
            EnsureTutorialOverlay();
            SubscribeGrid();
            GetComponent<SavableObjectState>()?.SaveState();
        }

        private void EnsureTutorialOverlay()
        {
            if (!TutorialCircuit) return;
            MapData circuitMap = TutorialCircuit.GetMapData();
            if (!circuitMap) return;
            var controller = TutorialCircuit.GetComponentInParent<PowerPanelController>();
            if (!Grid && controller) Grid = controller.ResolveGrid();
            if (!Grid) return;
            if (MapUnlocked && GameManager.Exists && GameManager.Instance.Inventory is { } inventory &&
                !inventory.Maps.Contains(circuitMap))
                inventory.Maps.Add(circuitMap);

            Grid.SyncDefinitionsFromData();
            if (!Grid.Circuits.Exists(c => c.Id == TutorialCircuit.CircuitId) &&
                !string.IsNullOrWhiteSpace(TutorialCircuit.CircuitId))
                Grid.Circuits.Add(new PowerCircuit
                {
                    Id = TutorialCircuit.CircuitId,
                    DisplayName = TutorialCircuit.Label,
                    Consumption = TutorialCircuit.Consumption
                });

            foreach (var panel in FindObjectsByType<PowerMapCircuitPanel>(FindObjectsSortMode.None))
                if (panel.MapData == circuitMap && panel.SourceGrid) return;

            var overlay = new GameObject("Tutorial Circuit Overlay").AddComponent<PowerMapCircuitPanel>();
            overlay.transform.SetParent(transform, false);
            overlay.MapData = circuitMap;
            overlay.Grid = Grid;
            if (controller) overlay.Layout = controller.Data;
        }

        private void SubscribeGrid()
        {
            if (!Grid) Grid = FindFirstObjectByType<DistrictPowerGrid>();
            if (!Grid) return;
            Grid.CircuitChanged -= OnCircuitChanged;
            Grid.CircuitChanged += OnCircuitChanged;
        }

        private void OnCircuitChanged(string circuitId, bool powered)
        {
            // The highlighted gate teaches the intended action, while any
            // successfully powered circuit proves the player understands it.
            if (!TutorialActive || !powered) return;
            if (CollectTabletWithoutOpening && TutorialCircuit && circuitId != TutorialCircuit.CircuitId) return;
            TutorialActive = false;
            TutorialCircuitUnlocked = true;
            CurrentQuest = string.Empty;
            if (Grid) Grid.CircuitChanged -= OnCircuitChanged;
            GetComponent<SavableObjectState>()?.SaveState();
        }

        public string GetSavableData()
        {
            return JsonUtility.ToJson(new SavedState
            {
                MapUnlocked = MapUnlocked,
                TutorialActive = TutorialActive,
                TutorialCircuitUnlocked = TutorialCircuitUnlocked,
                CurrentQuest = CurrentQuest,
                DiscoveredCircuits = new List<string>(discoveredCircuits)
            });
        }

        public void SetFromSavedData(string data)
        {
            SavedState state = JsonUtility.FromJson<SavedState>(data);
            MapUnlocked = state.MapUnlocked;
            TutorialActive = state.TutorialActive;
            TutorialCircuitUnlocked = state.TutorialCircuitUnlocked;
            CurrentQuest = CollectTabletWithoutOpening && state.CurrentQuest == "Find the depot code in the staff room"
                ? Chapter1Opening.DepotObjective
                : state.CurrentQuest;
            discoveredCircuits.Clear();
            if (state.DiscoveredCircuits != null) discoveredCircuits.UnionWith(state.DiscoveredCircuits);
            if (TutorialActive) SubscribeGrid();
        }

        [System.Serializable]
        private struct SavedState
        {
            public bool MapUnlocked;
            public bool TutorialActive;
            public bool TutorialCircuitUnlocked;
            public string CurrentQuest;
            public List<string> DiscoveredCircuits;
        }

    }
}
