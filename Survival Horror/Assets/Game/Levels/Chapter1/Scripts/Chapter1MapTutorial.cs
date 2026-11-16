using HorrorEngine;
using System.Collections;
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
        public DistrictPowerGrid Grid;
        public MapData MapToUnlock;
        [Tooltip("Drag the circuit-dot GameObject from PowerPanelController here. Its map and ID drive the tutorial automatically.")]
        public PowerCircuitPanelElement TutorialCircuit;
        public string TargetCircuitId = "store_shutters";
        [TextArea] public string QuestText = "ACTIVATE THE GATE CIRCUIT";

        public bool MapUnlocked { get; private set; }
        public bool TutorialActive { get; private set; }
        public bool TutorialCircuitUnlocked { get; private set; }
        public string CurrentQuest { get; private set; }

        public static bool CanOpenMap => Instance == null || Instance.MapUnlocked;
        public static bool CanCloseMap => Instance == null || !Instance.TutorialActive;
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
            CurrentQuest = QuestText;
            SubscribeGrid();
            GetComponent<SavableObjectState>()?.SaveState();
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
                CurrentQuest = CurrentQuest
            });
        }

        public void SetFromSavedData(string data)
        {
            SavedState state = JsonUtility.FromJson<SavedState>(data);
            MapUnlocked = state.MapUnlocked;
            TutorialActive = state.TutorialActive;
            TutorialCircuitUnlocked = state.TutorialCircuitUnlocked;
            CurrentQuest = state.CurrentQuest;
            if (TutorialActive) SubscribeGrid();
        }

        [System.Serializable]
        private struct SavedState
        {
            public bool MapUnlocked;
            public bool TutorialActive;
            public bool TutorialCircuitUnlocked;
            public string CurrentQuest;
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(CurrentQuest)) return;
            const float width = 330f;
            Rect panel = new Rect(Screen.width - width - 24f, 24f, width, 54f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 18f), "CURRENT OBJECTIVE");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 27f, panel.width - 28f, 22f), CurrentQuest);
        }
    }
}
