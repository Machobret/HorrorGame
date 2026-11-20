using System.Collections;
using Game.Environment;
using HorrorEngine;
using HorrorGame.Environment;
using HorrorGame.UI;
using UnityEngine;
using Unity.Cinemachine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class Chapter1Opening : MonoBehaviour, ISavableObjectStateExtra
    {
        internal const string DepotObjective = "Power the entrance shutter and use the office button";
        internal const string MainShutterObjective = "Power the main shutter with the tablet";
        internal const string OpenMainShutterObjective = "Open the main shutter with the office button";
        public static Chapter1Opening Instance { get; private set; }

        public DistrictPowerGrid Grid;
        public CircuitShutterTeleport DepotGate;
        public GarageDoorCircuitLink DepotMotor;
        public ElectricalDeviceFeed DepotMapControl;
        public MapRoom DepotOfficeRoom;
        public Interactive DepotOfficeButton;
        [SerializeField] private CameraPOV depotOpeningCamera;
        public ShutterIndicatorLamp DepotEntranceLamp;
        public TwoSidedDoor CourtyardGate;
        [SerializeField] private InteractionData lockedGateInteraction;
        public Transform IntercomDevice;
        public Transform StoreHoursSign;
        public Transform StoreDoorVisual;

        public bool DepotUnlocked { get; private set; }
        private CircuitDrivenObject DepotCircuit => DepotMotor ? DepotMotor : (CircuitDrivenObject)DepotGate;
        public bool DepotHasPower => DepotCircuit && DepotCircuit.Grid && DepotCircuit.Grid.IsCircuitPowered(DepotCircuit.CircuitId);
        public bool DepotControlHasPower => DepotCircuit && DepotCircuit.Grid &&
            DepotCircuit.Grid.HasSupply && !DepotCircuit.Grid.IsShutdown;
        internal string DepotQuest => DepotUnlocked ? string.Empty :
            DepotHasPower ? OpenMainShutterObjective : MainShutterObjective;
        public bool CourtyardHasPower => Grid && Grid.Supply && Grid.HasSupply && !Grid.IsShutdown;
        public bool CourtyardPassageAvailable => CourtyardHasPower && CourtyardGate &&
            CourtyardGate.isActiveAndEnabled && (!intercomStatus || intercomStatus.AccessGranted);
        private InteractionData courtyardOpenInteraction;
        private Interactive courtyardInteraction;
        private IntercomStatus intercomStatus;
        private CinemachineBrain openingBrain;
        private bool openingBrainIgnoredTimeScale;
        private int openingCameraOverride = -1;
        private Coroutine openingShot;
        private PauseController openingPause;
        private UIInputListener openingInput;

        private void Awake()
        {
            Instance = this;
            if (IntercomDevice) IntercomDevice.TryGetComponent(out intercomStatus);
            if (StoreHoursSign && StoreDoorVisual)
                StoreHoursSign.SetParent(StoreDoorVisual, true);
            if (DepotMotor) DepotMotor.SetManualOpen(false);
        }

        private void Start()
        {
            if (CourtyardGate) CourtyardGate.TryGetComponent(out courtyardInteraction);
            if (DepotOfficeButton) DepotOfficeButton.OnInteract.AddListener(UseDepotOfficeButton);
            if (DepotCircuit && DepotCircuit.Grid)
            {
                DepotCircuit.Grid.CircuitChanged += OnDepotCircuitChanged;
                DepotCircuit.Grid.SupplyChanged += RefreshDepot;
            }
            RefreshDepot();
            if (Grid)
            {
                Grid.OnGridChanged.AddListener(Refresh);
                if (Grid.Supply) Grid.Supply.AuthorizationChanged += Refresh;
            }
            if (intercomStatus) intercomStatus.AccessChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (DepotOfficeButton) DepotOfficeButton.OnInteract.RemoveListener(UseDepotOfficeButton);
            if (DepotCircuit && DepotCircuit.Grid)
            {
                DepotCircuit.Grid.CircuitChanged -= OnDepotCircuitChanged;
                DepotCircuit.Grid.SupplyChanged -= RefreshDepot;
            }
            if (Instance == this) Instance = null;
            if (Grid)
            {
                Grid.OnGridChanged.RemoveListener(Refresh);
                if (Grid.Supply) Grid.Supply.AuthorizationChanged -= Refresh;
            }
            if (intercomStatus) intercomStatus.AccessChanged -= Refresh;
        }

        private void OnDisable()
        {
            if (openingShot != null) StopCoroutine(openingShot);
            EndOpeningShot();
        }

        private void Refresh()
        {
            bool passageAvailable = CourtyardPassageAvailable;
            if (intercomStatus)
                intercomStatus.SetPowerState(Grid && Grid.Supply && Grid.Supply.IsAuthorized, CourtyardHasPower);
            if (!CourtyardGate) return;
            CourtyardGate.TraversalEnabled = passageAvailable;
            if (courtyardInteraction)
            {
                if (!courtyardOpenInteraction) courtyardOpenInteraction = courtyardInteraction.Data;
                var data = passageAvailable || !lockedGateInteraction ? courtyardOpenInteraction : lockedGateInteraction;
                if (courtyardInteraction.Data != data) courtyardInteraction.SetInteractionData(data);
                courtyardInteraction.SetPromptIcon(passageAvailable ? null : LockedGateIcon.Sprite);
                courtyardInteraction.enabled = true;
            }
        }

        public bool IsDepotCircuit(DistrictPowerGrid source, string circuitId) =>
            DepotCircuit && source == DepotCircuit.Grid && DepotCircuit.CircuitId == circuitId;

        public bool CanUseDepotCircuitFromOffice(DistrictPowerGrid source, string id, string roomId) =>
            DepotMotor && IsDepotCircuit(source, id) && DepotOfficeRoom &&
            DepotOfficeRoom.TryGetComponent(out ObjectUniqueId office) && office.Id == roomId;

        public bool TryToggleDepotPower()
        {
            if (DepotMotor)
            {
                if (!DepotControlHasPower)
                {
                    PlayerMonologue.Show("No power");
                    return false;
                }

                var grid = DepotMotor.Grid;
                bool wasRequested = grid.IsCircuitRequested(DepotMotor.CircuitId);
                grid.SetCircuit(DepotMotor.CircuitId, !wasRequested);
                if (wasRequested == grid.IsCircuitRequested(DepotMotor.CircuitId)) return false;
                if (!DepotUnlocked) Chapter1MapTutorial.SetQuest(DepotQuest);
                GetComponent<SavableObjectState>().SaveState();
                return true;
            }

            if (DepotUnlocked || !DepotGate || !DepotGate.Grid || !DepotGate.Grid.HasSupply) return false;
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
            if (DepotMotor)
            {
                if (!DepotHasPower)
                {
                    PlayerMonologue.Show("No power");
                    return;
                }
                bool showingCamera = BeginOpeningShot();
                DepotMotor.SetManualOpen(true, animateWhilePaused: showingCamera);
                UnlockDepot();
                if (showingCamera) openingShot = StartCoroutine(ShowOpeningShot());
                return;
            }
            if (!DepotHasPower)
            {
                PlayerMonologue.Show("Use the tablet to power the shutter from the outside");
                return;
            }
            UnlockDepot();
        }

        private bool BeginOpeningShot()
        {
            if (!depotOpeningCamera || !CameraSystem.Exists || !PauseController.Exists) return false;
            var camera = CameraSystem.Instance.MainCamera;
            var view = depotOpeningCamera.GetComponentInChildren<CinemachineVirtualCameraBase>(true);
            if (!view || !camera || !camera.TryGetComponent(out openingBrain)) return false;

            openingPause = PauseController.Instance;
            openingPause.Pause(this);
            openingInput = UIManager.Exists ? UIManager.Get<UIInputListener>(true) : null;
            if (openingInput) openingInput.AddBlockingContext(this);
            openingBrainIgnoredTimeScale = openingBrain.IgnoreTimeScale;
            openingBrain.IgnoreTimeScale = true;
            depotOpeningCamera.Activate();
            openingCameraOverride = openingBrain.SetCameraOverride(-1, 0, null, view, 1f, -1f);
            return true;
        }

        private IEnumerator ShowOpeningShot()
        {
            float timeout = Time.unscaledTime + 5f;
            yield return new WaitUntil(() => !DepotMotor || DepotMotor.IsOpen || !DepotHasPower || Time.unscaledTime >= timeout);
            yield return new WaitForSecondsRealtime(.4f);
            EndOpeningShot();
        }

        private void EndOpeningShot()
        {
            if (openingCameraOverride >= 0)
            {
                if (openingBrain)
                {
                    openingBrain.ReleaseCameraOverride(openingCameraOverride);
                    openingBrain.IgnoreTimeScale = openingBrainIgnoredTimeScale;
                }
                if (depotOpeningCamera) depotOpeningCamera.Deactivate();
                openingCameraOverride = -1;
            }
            if (openingInput) openingInput.RemoveBlockingContext(this);
            if (openingPause) openingPause.Resume(this);
            openingInput = null;
            openingPause = null;
            openingBrain = null;
            openingShot = null;
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
            if (id == DepotCircuit.CircuitId) RefreshDepot();
        }

        private void RefreshDepot()
        {
            if (DepotMapControl) DepotMapControl.SetLocalPower(DepotControlHasPower);
            if (DepotMapControl && DepotMapControl.TryGetComponent(out DeviceIndicator control))
                control.SetDeviceState(DepotHasPower ? IndicatorLamp.Green : IndicatorLamp.InactiveYellow);
            if (DepotOfficeButton)
            {
                var officeIndicator = DepotOfficeButton.GetComponentInParent<DeviceIndicator>();
                if (officeIndicator) officeIndicator.SetDeviceState(DepotUnlocked || DepotHasPower ? IndicatorLamp.Green : IndicatorLamp.Red);
            }
            if (DepotGate) DepotGate.SetPuzzleUnlocked(DepotUnlocked);
            if (DepotEntranceLamp)
                DepotEntranceLamp.SetManualColor(DepotHasPower ? IndicatorLamp.Green : IndicatorLamp.InactiveYellow);
        }

        public string GetSavableData() => JsonUtility.ToJson(new AccessState
        {
            Depot = DepotUnlocked,
            DepotPowered = DepotCircuit && DepotCircuit.Grid && DepotCircuit.Grid.IsCircuitRequested(DepotCircuit.CircuitId),
            ManualOpening = DepotMotor
        });

        public void SetFromSavedData(string data)
        {
            AccessState state = JsonUtility.FromJson<AccessState>(data);
            DepotUnlocked = state.Depot;
            if (DepotMotor)
            {
                // In the previous sequence Depot recorded pressing the button to authorize power.
                bool powerRequested = state.ManualOpening ? state.DepotPowered : state.Depot || state.DepotPowered;
                DepotUnlocked = state.Depot && (state.ManualOpening || state.DepotPowered);
                DepotMotor.RestoreManualOpen(DepotUnlocked);
                if (DepotMotor.Grid) DepotMotor.Grid.SetCircuit(DepotMotor.CircuitId, powerRequested);
            }
            else if (DepotGate && DepotGate.Grid)
                DepotGate.Grid.SetCircuit(DepotGate.CircuitId, DepotUnlocked || state.DepotPowered);
            Refresh();
            RefreshDepot();
        }

        [System.Serializable]
        private struct AccessState
        {
            public bool Depot;
            public bool DepotPowered;
            public bool ManualOpening;
        }
    }
}
