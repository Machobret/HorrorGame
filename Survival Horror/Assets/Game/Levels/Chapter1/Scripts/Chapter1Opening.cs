using HorrorEngine;
using HorrorGame.UI;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class Chapter1Opening : CircuitDrivenObject, ISavableObjectStateExtra
    {
        internal const string DepotObjective = "Power the entrance shutter and use the office button";
        public static Chapter1Opening Instance { get; private set; }

        public Chapter1MapTutorial Tablet;
        public CircuitShutterTeleport DepotGate;
        public Interactive DepotOfficeButton;
        public ShutterIndicatorLamp DepotEntranceLamp;
        public TwoSidedDoor CourtyardGate;
        [SerializeField] private InteractionData lockedGateInteraction;
        public Interactive Intercom;
        public Interactive CourtyardButton;
        [Tooltip("World position used for the alley intercom circuit marker.")]
        public Transform AlleyPowerBox;
        public Transform StoreHoursSign;
        public Transform StoreDoorVisual;

        public bool DepotUnlocked { get; private set; }
        public bool DepotHasPower => DepotGate && DepotGate.Grid && DepotGate.Grid.IsCircuitPowered(DepotGate.CircuitId);
        public bool CourtyardHasPower => Powered;
        public bool CourtyardPassageAvailable => courtyardUnlocked && CourtyardGate && CourtyardGate.isActiveAndEnabled;
        private bool courtyardUnlocked;
        private bool interactionHintShown;
        private InteractionData courtyardOpenInteraction;
        private Interactive courtyardInteraction;
        private InteractionData courtyardButtonInteraction;

        private void Awake()
        {
            Instance = this;
            if (StoreHoursSign && StoreDoorVisual)
                StoreHoursSign.SetParent(StoreDoorVisual, true);
        }

        protected override void Start()
        {
            if (Intercom) Intercom.OnInteract.AddListener(UseIntercom);
            if (CourtyardButton)
            {
                courtyardButtonInteraction = CourtyardButton.Data;
                CourtyardButton.OnInteract.AddListener(UseCourtyardButton);
            }
            if (CourtyardGate && CourtyardGate.TryGetComponent(out courtyardInteraction))
                courtyardInteraction.OnInteract.AddListener(UseLockedGate);
            if (DepotOfficeButton) DepotOfficeButton.OnInteract.AddListener(UseDepotOfficeButton);
            if (DepotGate && DepotGate.Grid) DepotGate.Grid.CircuitChanged += OnDepotCircuitChanged;
            RefreshDepot();
            base.Start();
        }

        protected override void OnDestroy()
        {
            if (Intercom) Intercom.OnInteract.RemoveListener(UseIntercom);
            if (CourtyardButton) CourtyardButton.OnInteract.RemoveListener(UseCourtyardButton);
            if (courtyardInteraction) courtyardInteraction.OnInteract.RemoveListener(UseLockedGate);
            if (DepotOfficeButton) DepotOfficeButton.OnInteract.RemoveListener(UseDepotOfficeButton);
            if (DepotGate && DepotGate.Grid) DepotGate.Grid.CircuitChanged -= OnDepotCircuitChanged;
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void Refresh()
        {
            if (!CourtyardGate) return;
            CourtyardGate.TraversalEnabled = courtyardUnlocked;
            if (CourtyardGate.TryGetComponent(out Interactive gateInteraction))
            {
                if (!courtyardOpenInteraction) courtyardOpenInteraction = gateInteraction.Data;
                var data = courtyardUnlocked || !lockedGateInteraction ? courtyardOpenInteraction : lockedGateInteraction;
                if (gateInteraction.Data != data) gateInteraction.SetInteractionData(data);
                gateInteraction.SetPromptIcon(courtyardUnlocked ? null : LockedGateIcon.Sprite);
                gateInteraction.enabled = true;
            }
            if (CourtyardButton && courtyardButtonInteraction)
            {
                var data = courtyardUnlocked && courtyardOpenInteraction ? courtyardOpenInteraction : courtyardButtonInteraction;
                if (CourtyardButton.Data != data) CourtyardButton.SetInteractionData(data);
            }
        }

        private void Update()
        {
            if (!GameManager.Exists || !GameManager.Instance.Player || !UIManager.Exists ||
                (PauseController.Exists && PauseController.Instance.IsPaused)) return;

            if (interactionHintShown || courtyardUnlocked || !Intercom) return;
            Vector3 offset = GameManager.Instance.Player.transform.position - Intercom.transform.position;
            if (new Vector2(offset.x, offset.z).sqrMagnitude > 6.25f) return;
            interactionHintShown = true;
            PlayerMonologue.Show("\"E\" to interact");
        }

        private void UseLockedGate(IInteractor interactor)
        {
            if (!courtyardUnlocked) ConfirmCourtyardGate();
        }

        private void UseIntercom(IInteractor interactor)
        {
            if (!CourtyardButton || courtyardUnlocked || !Powered)
            {
                UseCourtyardButton(interactor);
                return;
            }

            PlayerMonologue.Show("Use the button beside the maintenance schedule");
        }

        private void UseCourtyardButton(IInteractor interactor)
        {
            if (!CourtyardGate) return;
            if (courtyardUnlocked) CourtyardGate.Use(interactor);
            else ConfirmCourtyardGate();
        }

        private void ConfirmCourtyardGate()
        {
            if (!Powered)
            {
                PlayerMonologue.Show(Tablet && Tablet.MapUnlocked
                    ? "Use the tablet to power the alley intercom"
                    : "There has to be a way to activate this");
                return;
            }

            courtyardUnlocked = true;
            Refresh();
            GetComponent<SavableObjectState>().SaveState();
        }

        public bool IsDepotCircuit(DistrictPowerGrid source, string circuitId) =>
            DepotGate && source == Grid && DepotGate.CircuitId == circuitId;

        public bool TryToggleDepotPower()
        {
            if (DepotUnlocked || !DepotGate || !DepotGate.Grid) return false;
            bool wasPowered = DepotHasPower;
            DepotGate.Grid.SetCircuit(DepotGate.CircuitId, !wasPowered);
            if (DepotHasPower == wasPowered) return false;

            // Scenes with no office button retain the original single-step unlock.
            if (!DepotOfficeButton && DepotHasPower) UnlockDepot();
            else GetComponent<SavableObjectState>().SaveState();
            return true;
        }

        private void UseDepotOfficeButton(IInteractor interactor)
        {
            if (DepotUnlocked) return;
            if (!DepotHasPower)
            {
                PlayerMonologue.Show("Use the tablet to power the shutter from the outside");
                return;
            }
            UnlockDepot();
        }

        private void UnlockDepot()
        {
            DepotUnlocked = true;
            RefreshDepot();
            Chapter1MapTutorial.ClearQuest();
            GetComponent<SavableObjectState>().SaveState();
        }

        private void OnDepotCircuitChanged(string id, bool powered)
        {
            if (id == DepotGate.CircuitId) RefreshDepot();
        }

        private void RefreshDepot()
        {
            if (DepotGate) DepotGate.SetPuzzleUnlocked(DepotUnlocked);
            if (DepotEntranceLamp)
                DepotEntranceLamp.SetManualColor(DepotUnlocked ? new Color(.18f, 1f, .25f) :
                    DepotHasPower ? new Color(1f, .67f, .12f) : new Color(1f, .12f, .06f));
        }

        public string GetSavableData() => JsonUtility.ToJson(new AccessState
        {
            Courtyard = courtyardUnlocked,
            CourtyardPowered = CourtyardHasPower,
            Depot = DepotUnlocked,
            DepotPowered = DepotHasPower
        });

        public void SetFromSavedData(string data)
        {
            AccessState state = JsonUtility.FromJson<AccessState>(data);
            courtyardUnlocked = state.Courtyard;
            DepotUnlocked = state.Depot;
            if (Grid) Grid.SetCircuit(CircuitId, courtyardUnlocked || state.CourtyardPowered);
            Refresh();
            if (DepotGate && DepotGate.Grid)
                DepotGate.Grid.SetCircuit(DepotGate.CircuitId, DepotUnlocked || state.DepotPowered);
            RefreshDepot();
        }

        [System.Serializable]
        private struct AccessState
        {
            public bool Courtyard;
            public bool CourtyardPowered;
            public bool Depot;
            public bool DepotPowered;
        }
    }
}
