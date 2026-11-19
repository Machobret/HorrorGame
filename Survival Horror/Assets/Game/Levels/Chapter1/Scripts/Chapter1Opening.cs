using HorrorEngine;
using HorrorGame.UI;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class Chapter1Opening : CircuitDrivenObject, ISavableObjectStateExtra
    {
        internal const string DepotObjective = "Unlock the depot shutter with the tablet";
        public static Chapter1Opening Instance { get; private set; }

        public Chapter1MapTutorial Tablet;
        public CircuitShutterTeleport DepotGate;
        public TwoSidedDoor CourtyardGate;
        public Interactive Intercom;
        [Tooltip("World position used for the alley intercom circuit marker.")]
        public Transform AlleyPowerBox;
        public Transform StoreHoursSign;
        public Transform StoreDoorVisual;

        public bool DepotUnlocked { get; private set; }
        private bool courtyardUnlocked;
        private bool movementHintShown;
        private bool interactionHintShown;

        private void Awake()
        {
            Instance = this;
            if (StoreHoursSign && StoreDoorVisual)
                StoreHoursSign.SetParent(StoreDoorVisual, true);
        }

        protected override void Start()
        {
            if (Intercom) Intercom.OnInteract.AddListener(UseIntercom);
            if (DepotGate) DepotGate.SetPuzzleUnlocked(DepotUnlocked);
            base.Start();
        }

        protected override void OnDestroy()
        {
            if (Intercom) Intercom.OnInteract.RemoveListener(UseIntercom);
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void Refresh()
        {
            if (CourtyardGate && CourtyardGate.TryGetComponent(out Interactive gateInteraction))
                gateInteraction.enabled = courtyardUnlocked;
        }

        private void Update()
        {
            if (!GameManager.Exists || !GameManager.Instance.Player || !UIManager.Exists ||
                (PauseController.Exists && PauseController.Instance.IsPaused)) return;

            if (!movementHintShown)
            {
                movementHintShown = true;
                if (!Tablet.MapUnlocked) GameplayNotifications.ShowControls("WASD  Move and turn");
            }

            if (interactionHintShown || courtyardUnlocked || !Intercom) return;
            Vector3 offset = GameManager.Instance.Player.transform.position - Intercom.transform.position;
            if (new Vector2(offset.x, offset.z).sqrMagnitude > 6.25f) return;
            interactionHintShown = true;
            GameplayNotifications.ShowControls("E  Interact with the intercom");
        }

        public void OnTabletCollected() => GameplayNotifications.ShowControls("I  Inventory   /   M  Tablet");

        private void UseIntercom(IInteractor interactor)
        {
            if (!courtyardUnlocked && !Powered)
            {
                GameplayNotifications.ShowControls(Tablet.MapUnlocked
                    ? "Use the tablet to power the alley intercom"
                    : "No power. Look for the tablet in the alley.");
                Intercom.Finish();
                return;
            }

            if (!courtyardUnlocked)
            {
                courtyardUnlocked = true;
                Refresh();
                Chapter1MapTutorial.SetQuest(DepotObjective);
                GetComponent<SavableObjectState>().SaveState();
            }
            CourtyardGate.Use(interactor);
        }

        public bool IsDepotCircuit(DistrictPowerGrid source, string circuitId) =>
            DepotGate && source == Grid && DepotGate.CircuitId == circuitId;

        public bool TryUnlockDepot()
        {
            if (DepotUnlocked) return false;
            Grid.SetCircuit(DepotGate.CircuitId, true);
            if (!Grid.IsCircuitPowered(DepotGate.CircuitId)) return false;
            DepotUnlocked = true;
            DepotGate.SetPuzzleUnlocked(true);
            Chapter1MapTutorial.ClearQuest();
            GetComponent<SavableObjectState>().SaveState();
            return true;
        }

        public string GetSavableData() => JsonUtility.ToJson(new AccessState
        {
            Courtyard = courtyardUnlocked,
            Depot = DepotUnlocked
        });

        public void SetFromSavedData(string data)
        {
            AccessState state = JsonUtility.FromJson<AccessState>(data);
            courtyardUnlocked = state.Courtyard;
            DepotUnlocked = state.Depot;
            Refresh();
            if (DepotGate) DepotGate.SetPuzzleUnlocked(DepotUnlocked);
        }

        [System.Serializable]
        private struct AccessState
        {
            public bool Courtyard;
            public bool Depot;
        }
    }
}
