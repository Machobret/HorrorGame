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
        [Tooltip("Assign the breaker room when the tutorial ends by restoring the grid supply.")]
        public MapRoom BreakerRoom;
        [Tooltip("Drag the circuit-dot GameObject from PowerPanelController here. Its map and ID drive the tutorial automatically.")]
        public PowerCircuitPanelElement TutorialCircuit;
        public string TargetCircuitId = "store_shutters";
        [TextArea] public string QuestText = "Activate circuit";
        [TextArea] public string FollowUpQuestText;

        public bool MapUnlocked { get; private set; }
        public bool TutorialActive { get; private set; }
        public bool ControlsTutorialSeen { get; private set; }
        public bool TutorialCircuitUnlocked { get; private set; }
        public bool UsesBreakerObjective => BreakerRoom && Grid && Grid.Supply;
        public bool WaitingForCircuitPower => TutorialActive && Grid && (!Grid.HasSupply || Grid.IsShutdown);
        public bool WaitingForBreakerAuthorization => WaitingForCircuitPower && Grid.Supply && !Grid.Supply.IsAuthorized;
        public string CurrentQuest
        {
            get
            {
                if (WaitingForBreakerAuthorization) return "Enable the electricity box on the tablet";
                if (WaitingForCircuitPower) return "Turn on the alley breaker";
                var opening = Chapter1Opening.Instance;
                if (opening && opening.DepotMotor &&
                    (currentQuest == Chapter1Opening.MainShutterObjective || currentQuest == Chapter1Opening.OpenMainShutterObjective))
                    return opening.DepotQuest;
                return currentQuest;
            }
            private set => currentQuest = value;
        }
        private string currentQuest;

        public static bool CanOpenMap => Instance == null || Instance.MapUnlocked;
        private float nextCircuitScan;
        private readonly HashSet<string> discoveredCircuits = new HashSet<string>();

        private void Update()
        {
            if (!GameManager.Exists || !GameManager.Instance.Player || !UIManager.Exists) return;
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
            foreach (var dot in FindObjectsByType<PowerCircuitPanelElement>(FindObjectsSortMode.None))
            {
                if (dot.Room != room || dot.GetMapData() != controller.Data || string.IsNullOrWhiteSpace(dot.CircuitId))
                    continue;
                if (discoveredCircuits.Add(controller.Data.UniqueId + ":" + dot.CircuitId))
                    discovered++;
            }
            if (discovered == 0) return;
            // Remember discoveries made before pickup so they never replay as notifications.
            GameplayNotifications.CircuitsDiscovered(discovered);
            GetComponent<SavableObjectState>()?.SaveState();
        }
        public static bool IsTargetCircuit(MapData map, string circuitId)
        {
            if (!Instance || (!Instance.TutorialActive && !Instance.TutorialCircuitUnlocked)) return false;
            if (Instance.UsesBreakerObjective)
                return Instance.MapToUnlock == map && circuitId == PowerMapCircuitPanel.BreakerCircuitId;
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
            UnsubscribeGrid();
            if (Instance == this) Instance = null;
        }

        public void UnlockAndStart(MapData map)
        {
            MapUnlocked = true;
            if (map) MapToUnlock = map;
            if (MapToUnlock && GameManager.Exists && GameManager.Instance.Inventory != null && !GameManager.Instance.Inventory.Maps.Contains(MapToUnlock))
                GameManager.Instance.Inventory.Maps.Add(MapToUnlock);

            TutorialActive = true;
            CurrentQuest = QuestText;
            EnsureTutorialOverlay();
            SubscribeGrid();
            GetComponent<SavableObjectState>()?.SaveState();
        }

        private void EnsureTutorialOverlay()
        {
            MapData circuitMap = UsesBreakerObjective ? MapToUnlock : TutorialCircuit ? TutorialCircuit.GetMapData() : null;
            if (!circuitMap) return;
            var controller = TutorialCircuit ? TutorialCircuit.GetComponentInParent<PowerPanelController>() : null;
            if (!Grid && controller) Grid = controller.ResolveGrid();
            if (!Grid) return;

            Grid.SyncDefinitionsFromData();
            if (!UsesBreakerObjective && TutorialCircuit && !Grid.Circuits.Exists(c => c.Id == TutorialCircuit.CircuitId) &&
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
            UnsubscribeGrid();
            if (UsesBreakerObjective)
            {
                Grid.SupplyChanged += OnSupplyChanged;
                OnSupplyChanged();
            }
            else Grid.CircuitChanged += OnCircuitChanged;
        }

        private void UnsubscribeGrid()
        {
            if (!Grid) return;
            Grid.CircuitChanged -= OnCircuitChanged;
            Grid.SupplyChanged -= OnSupplyChanged;
        }

        private void OnSupplyChanged()
        {
            if (TutorialActive && Grid.HasSupply && !Grid.IsShutdown) FinishTutorial();
        }

        private void OnCircuitChanged(string circuitId, bool powered)
        {
            if (!TutorialActive || !powered) return;
            string targetId = TutorialCircuit ? TutorialCircuit.CircuitId : TargetCircuitId;
            if (!string.IsNullOrEmpty(targetId) && circuitId != targetId) return;
            FinishTutorial();
        }

        private void FinishTutorial()
        {
            TutorialActive = false;
            TutorialCircuitUnlocked = true;
            CurrentQuest = FollowUpQuestText;
            UnsubscribeGrid();
            GetComponent<SavableObjectState>()?.SaveState();
        }

        public void CompleteControlsTutorial()
        {
            ControlsTutorialSeen = true;
            GetComponent<SavableObjectState>().SaveState();
        }

        public string GetSavableData()
        {
            return JsonUtility.ToJson(new SavedState
            {
                MapUnlocked = MapUnlocked,
                TutorialActive = TutorialActive,
                ControlsTutorialSeen = ControlsTutorialSeen,
                TutorialCircuitUnlocked = TutorialCircuitUnlocked,
                CurrentQuest = currentQuest,
                DiscoveredCircuits = new List<string>(discoveredCircuits)
            });
        }

        public void SetFromSavedData(string data)
        {
            SavedState state = JsonUtility.FromJson<SavedState>(data);
            MapUnlocked = state.MapUnlocked;
            TutorialActive = state.TutorialActive;
            ControlsTutorialSeen = state.ControlsTutorialSeen;
            TutorialCircuitUnlocked = state.TutorialCircuitUnlocked;
            CurrentQuest = state.CurrentQuest;
            if (Chapter1Opening.Instance && Chapter1Opening.Instance.DepotMotor &&
                (currentQuest == "Find a way around the storage unit" ||
                 currentQuest == "Use the office button to power the main shutter" ||
                 currentQuest == "Open the main shutter with the tablet"))
                CurrentQuest = Chapter1Opening.MainShutterObjective;
            if (CollectTabletWithoutOpening && (currentQuest == "Find the depot code in the staff room" ||
                currentQuest == Chapter1Opening.DepotObjective))
            {
                var opening = Chapter1Opening.Instance;
                CurrentQuest = opening && opening.DepotMotor ? Chapter1Opening.MainShutterObjective : Chapter1Opening.DepotObjective;
            }
            if (TutorialActive && (UsesBreakerObjective || string.IsNullOrWhiteSpace(currentQuest)))
                CurrentQuest = QuestText;
            discoveredCircuits.Clear();
            if (state.DiscoveredCircuits != null) discoveredCircuits.UnionWith(state.DiscoveredCircuits);
            if (TutorialActive) SubscribeGrid();
        }

        [System.Serializable]
        private struct SavedState
        {
            public bool MapUnlocked;
            public bool TutorialActive;
            public bool ControlsTutorialSeen;
            public bool TutorialCircuitUnlocked;
            public string CurrentQuest;
            public List<string> DiscoveredCircuits;
        }

    }
}
