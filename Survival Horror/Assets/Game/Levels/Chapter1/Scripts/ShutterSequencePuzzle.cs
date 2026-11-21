using System;
using System.Collections;
using System.Collections.Generic;
using Game.Environment;
using HouseToolkit;
using HorrorGame.Environment;
using HorrorEngine;
using HorrorGame.Lighting;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(SavableObjectState))]
    public sealed class ShutterSequencePuzzle : MonoBehaviour, ISavableObjectStateExtra
    {
        [Tooltip("Exactly six non-electrical shutters, arranged in your desired 2x3 order.")]
        public SimplePuzzleShutter[] Shutters = new SimplePuzzleShutter[6];
        public SimplePuzzleShutter MainShutter;
        public DistrictPowerGrid Grid;
        [FormerlySerializedAs("CheckDoor")] public CircuitShutterTeleport PuzzleDoor;
        public CircuitDrivenObject OutputShutter;
        public Interactive[] OutputButtons = Array.Empty<Interactive>();
        public PowerCircuitPanelElement[] SegmentDots = Array.Empty<PowerCircuitPanelElement>();
        public DocumentData Note;
        [SerializeField] private Texture2D unitCluePaper;
        [SerializeField] private Texture2D tenantCluePaper;
        [SerializeField] private Texture2D clueGlyphs;
        [SerializeField] private Renderer clueWorldPaper;
        [SerializeField] private int[] bayNumbers = { 1, 2, 3, 4, 5, 6 };
        private List<DocumentPage> authoredPages;
        private LocalizableSprite authoredCover;
        private bool useTenantClue;
        public CircuitDiagramData ControlDiagram;
        private Sprite noteSprite;
        private Texture2D noteTexture;
        public GameObject WrongAnswerCamera;
        public AudioSource WrongAnswerBuzzer;
        [Min(.1f)] public float WrongCameraSeconds = 2f;
        [SerializeField] private bool[] solution = new bool[6];
        [SerializeField] private bool solved;
        public CameraPOV CheckCamera;
        public Transform CheckButton;
        public Transform[] LeverPositions = Array.Empty<Transform>();
        public Interactive[] PatternButtons = Array.Empty<Interactive>();
        [SerializeField] private ElectricalFixtureFeed bayFixtureFeed;
        [SerializeField] private Light[] bayLights = Array.Empty<Light>();
        public CircuitDrivenObject[] SegmentShutters = Array.Empty<CircuitDrivenObject>();
        public ShutterIndicatorLamp[] SegmentLamps = Array.Empty<ShutterIndicatorLamp>();
        public ShutterIndicatorLamp CheckLamp;
        public InteractionData LeverInteraction;
        public InteractionData CheckInteraction;

        private const int SegmentCount = 6;
        private static readonly Color Red = new Color(1f, .12f, .06f);
        private static readonly Color Green = new Color(.18f, 1f, .25f);
        [SerializeField] private Transform[] leverHandles = new Transform[SegmentCount];
        // In the powered bays a set bit means the switch is off.
        private int switchedMask;
        private Coroutine feedback;
        private Interactive checkInteraction;
        private Action<IInteractor>[] patternActions;
        private GameObject letterbox;
        private UIInputListener input;
        private PauseController pause;
        private CinemachineBrain checkBrain;
        private bool checkBrainIgnoredTimeScale;
        private int checkCameraOverride = -1;
        private bool UsesBayButtons => PatternButtons.Length == SegmentCount;
        private bool UsesPrintedClue => unitCluePaper && tenantCluePaper && clueGlyphs;
        private bool ManualPuzzle => CheckButton || UsesBayButtons;
        public bool ShowingWrongAnswer { get; private set; }

        public bool[] Solution => solution;
        public bool IsSolved => solved;
        private void Awake()
        {
            useTenantClue = CorridorPuzzleSetting.UseTenantClue;
            if (solution == null || solution.Length != 6 || CountSolution() == 0) GenerateSolution();
            Apply();
            RefreshNote();
        }

        private void Start()
        {
            ConfigureBayNumberMaterials();
            if (ManualPuzzle)
            {
                if (UsesBayButtons)
                {
                    patternActions = new Action<IInteractor>[SegmentCount];
                    for (int i = 0; i < SegmentCount; i++)
                    {
                        int index = i;
                        if (!PatternButtons[i]) continue;
                        patternActions[i] = _ => ToggleLever(index);
                        PatternButtons[i].OnInteract.AddListener(patternActions[i].Invoke);
                    }
                }
                else
                    for (int i = 0; i < LeverPositions.Length && i < SegmentCount; i++)
                    {
                        int index = i;
                        if (LeverPositions[i])
                            InstallControl(LeverPositions[i], LeverInteraction, _ => ToggleLever(index));
                    }
                if (CheckButton) checkInteraction = InstallControl(CheckButton, CheckInteraction, UseCheckButton);
                if (PuzzleDoor) PuzzleDoor.OnLockedInteraction += CheckAnswer;
                UpdateCheckPrompt();
                RefreshIndicators();
            }
            foreach (var button in OutputButtons)
                if (button) button.OnInteract.AddListener(UseOutputButton);
            if (!Grid || (!PuzzleDoor && !OutputShutter)) return;
            if (SegmentDots == null) return;
            var anchor = Array.Find(SegmentDots, dot => dot && dot.Room && dot.GetMapData());
            if (!anchor) return;
            var controller = anchor.GetComponentInParent<PowerPanelController>();
            var sequenceMap = anchor.GetMapData();
            PowerMapCircuitPanel overlay = null;
            foreach (var candidate in FindObjectsByType<PowerMapCircuitPanel>(FindObjectsSortMode.None))
                if (candidate.MapData == sequenceMap && candidate.SourceGrid == Grid) { overlay = candidate; break; }
            if (!overlay)
            {
                overlay = gameObject.AddComponent<PowerMapCircuitPanel>();
                overlay.MapData = sequenceMap;
                overlay.Grid = Grid;
                if (controller) overlay.Layout = controller.Data;
            }
            overlay.SequencePuzzle = this;
            foreach (var dot in SegmentDots) RegisterDot(dot);
        }

        private void RegisterDot(PowerCircuitPanelElement dot)
        {
            if (!dot || Grid.Circuits.Exists(c => c.Id == dot.CircuitId)) return;
            Grid.Circuits.Add(new PowerCircuit
            {
                Id = dot.CircuitId, DisplayName = dot.Label, Consumption = dot.Consumption
            });
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= UpdateBayNumberAtlas;
            if (bayNumberMaterial) Destroy(bayNumberMaterial);
            if (PuzzleDoor) PuzzleDoor.OnLockedInteraction -= CheckAnswer;
            foreach (var button in OutputButtons)
                if (button) button.OnInteract.RemoveListener(UseOutputButton);
            if (patternActions != null)
                for (int i = 0; i < patternActions.Length; i++)
                    if (PatternButtons[i] && patternActions[i] != null) PatternButtons[i].OnInteract.RemoveListener(patternActions[i].Invoke);
            if (noteSprite)
            {
                if (Note && Note.Pages != null && Note.Pages.Count >= 2 && Note.Pages[1].Image?.Unlocalized == noteSprite)
                    Note.Pages[1].Image.Unlocalized = null;
                Destroy(noteSprite);
            }
            if (noteTexture) Destroy(noteTexture);
            if (Note)
            {
                if (authoredPages != null) Note.Pages = authoredPages;
                if (authoredCover != null) Note.Image = authoredCover;
            }
        }

        public bool OwnsControlDiagram => ControlDiagram && GameManager.Exists &&
            GameManager.Instance.Inventory.Documents.Contains(ControlDiagram);

        public bool CanAccessDot(string id)
        {
            if (!OwnsControlDiagram) return false;
            foreach (var dot in SegmentDots)
                if (dot && dot.CircuitId == id) return true;
            return false;
        }

        private void RefreshNote()
        {
            if (!Note || Note.Pages == null) return;
            authoredPages ??= Note.Pages;
            if (UsesPrintedClue && authoredPages.Count >= 4)
            {
                if (noteSprite) Destroy(noteSprite);
                if (noteTexture) Destroy(noteTexture);
                Note.Pages = CorridorClueArtwork.Create(solution, bayNumbers, useTenantClue,
                    useTenantClue ? tenantCluePaper : unitCluePaper, clueGlyphs, authoredPages[useTenantClue ? 3 : 2],
                    out noteTexture, out noteSprite);
                authoredCover ??= Note.Image;
                Note.Image = new LocalizableSprite { Unlocalized = noteSprite };
                if (clueWorldPaper)
                {
                    var material = new MaterialPropertyBlock();
                    clueWorldPaper.GetPropertyBlock(material, 0);
                    material.SetTexture("_BaseMap", noteTexture);
                    material.SetTexture("_MainTex", noteTexture);
                    clueWorldPaper.SetPropertyBlock(material, 0);
                }
                return;
            }
            if (authoredPages.Count < 2) return;
            if (Note.Pages == authoredPages && authoredPages.Count > 2)
                Note.Pages = authoredPages.GetRange(0, 2);
            if (noteSprite) Destroy(noteSprite);
            if (noteTexture) Destroy(noteTexture);
            const int cell = 54, pitch = 68, width = pitch * 2, height = pitch * 3;
            var ink = new Color32(44, 53, 111, 255);
            var paper = new Color32(245, 235, 214, 255);
            var crossInk = new Color32(139, 34, 34, 255);
            noteTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[width * height];

            void Fill(int x, int y, int w, int h, Color32 color)
            {
                for (int row = y; row < y + h; row++)
                    Array.Fill(pixels, color, row * width + x, w);
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                bool left = i % 2 == 0;
                int x = i % 2 * pitch + 7;
                int y = (2 - i / 2) * pitch + 7;
                if (!UsesBayButtons)
                {
                    Fill(x, y, cell, cell, solution[i] ? new Color32(52, 160, 89, 255) : new Color32(185, 63, 57, 255));
                    continue;
                }

                Fill(x, y, cell, cell, ink);
                Fill(x + 2, y + 2, cell - 4, cell - 4, paper);
                int shutterX = left ? x + cell - 12 : x - 2;
                int shutterY = y + 15;
                Fill(shutterX, shutterY, 14, 24, ink);
                Fill(shutterX + 2, shutterY + 2, 10, 20, paper);
                for (int slat = 4; slat < 22; slat += 4)
                    Fill(shutterX + 2, shutterY + slat, 10, 1, ink);
                if (!solution[i]) continue;
                for (int step = 0; step < 26; step++)
                {
                    int dx = step * 17 / 25;
                    Fill(shutterX - 3 + dx, shutterY - 1 + step, 3, 2, crossInk);
                    Fill(shutterX + 14 - dx, shutterY - 1 + step, 3, 2, crossInk);
                }
            }
            noteTexture.SetPixels32(pixels);
            noteTexture.Apply();
            noteSprite = Sprite.Create(noteTexture, new Rect(0, 0, width, height), Vector2.one * .5f);
            Note.Pages[1].Image.Unlocalized = noteSprite;
        }

        [SerializeField] private TextMesh[] bayNumberLabels = Array.Empty<TextMesh>();
        private Font bayNumberFont;
        private Material bayNumberMaterial;

        private void ConfigureBayNumberMaterials()
        {
            if (bayNumberLabels.Length == 0 || !bayNumberLabels[0]) return;
            var font = bayNumberLabels[0].font;
            var template = Resources.Load<Material>("MapTransparentIcon");
            if (!font || !template) return;
            bayNumberFont = font;
            font.RequestCharactersInTexture("123456", 64, FontStyle.Bold);
            // The built-in TextMesh material always draws on top. This shader tests world depth.
            bayNumberMaterial = new Material(template) { name = "Storage bay number text" };
            bayNumberMaterial.SetColor("_BaseColor", new Color(.96f, .9f, .73f));
            UpdateBayNumberAtlas(font);
            Font.textureRebuilt += UpdateBayNumberAtlas;
            foreach (var label in bayNumberLabels)
                if (label) label.GetComponent<MeshRenderer>().sharedMaterial = bayNumberMaterial;
        }

        private void UpdateBayNumberAtlas(Font font)
        {
            if (font == bayNumberFont && bayNumberMaterial)
                bayNumberMaterial.SetTexture("_BaseMap", font.material.mainTexture);
        }

        private static Interactive InstallControl(Transform anchor, InteractionData data, Action<IInteractor> action)
        {
            var hitbox = anchor.gameObject.AddComponent<BoxCollider>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector3(.65f, .65f, .65f);
            anchor.gameObject.layer = LayerMask.NameToLayer("InteractionSystem");
            var interactive = anchor.gameObject.AddComponent<Interactive>();
            interactive.Data = data;
            interactive.OnInteract = new OnInteractionEvent();
            interactive.OnDataChanged = new UnityEngine.Events.UnityEvent();
            interactive.OnInteract.AddListener(interactor => action(interactor));
            return interactive;
        }

        private void ToggleLever(int index)
        {
            if (Grid && (!Grid.HasSupply || Grid.IsShutdown))
            {
                HorrorGame.UI.PlayerMonologue.Show("No power");
                return;
            }
            if (feedback != null || index >= SegmentShutters.Length || !SegmentShutters[index] ||
                !SegmentShutters[index].TryGetComponent(out GarageDoor door) || !door.IsOpen) return;
            switchedMask ^= 1 << index;
            if (UsesBayButtons)
            {
                bool matched = MatchesSolution();
                solved = matched;
                RefreshOutputButtons();
            }
            RefreshIndicators();
            GetComponent<SavableObjectState>().SaveState();
        }

        private void RefreshIndicators()
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                bool switched = (switchedMask & (1 << i)) != 0;
                if (i < SegmentLamps.Length && SegmentLamps[i]) SegmentLamps[i].SetManualColor(switched ? Green : Red);
                if (i < leverHandles.Length && leverHandles[i]) leverHandles[i].localRotation = Quaternion.Euler(switched ? -45f : 45f, 0f, 0f);
                if (i < PatternButtons.Length && PatternButtons[i])
                    SetButtonIndicator(PatternButtons[i], !switched, IndicatorLamp.Red);
                if (bayFixtureFeed && i < bayLights.Length)
                    bayFixtureFeed.SetLocalPower(bayLights[i], !switched);
            }
        }

        private void UseOutputButton(IInteractor interactor)
        {
            if (feedback != null) return;
            if (!OutputShutter || !Grid || !Grid.HasSupply || Grid.IsShutdown || !solved)
            {
                feedback = StartCoroutine(ShowCheck(false));
                return;
            }

            bool open = !Grid.IsCircuitRequested(OutputShutter.CircuitId);
            if (OutputShutter is GarageDoorCircuitLink motor && BeginCheckShot())
            {
                motor.SetCircuitRequested(open, true);
                feedback = StartCoroutine(ShowOutputTravel(motor, open));
            }
            else
            {
                Grid.SetCircuit(OutputShutter.CircuitId, open);
                ShowPowerResult(Grid.IsCircuitRequested(OutputShutter.CircuitId) == open);
            }
            RefreshOutputButtons();
            GetComponent<SavableObjectState>().SaveState();
        }

        private void RefreshOutputButtons()
        {
            bool requested = OutputShutter && Grid && Grid.IsCircuitRequested(OutputShutter.CircuitId);
            foreach (var button in OutputButtons)
            {
                if (!button) continue;
                if (button.GetComponentInParent<ElectricalDeviceFeed>() is ElectricalDeviceFeed feed) feed.SetLocalPower(solved);
                SetButtonIndicator(button, requested, IndicatorLamp.InactiveYellow);
            }
        }

        private static void SetButtonIndicator(Interactive button, bool on, Color inactive)
        {
            var device = button.GetComponentInParent<DeviceIndicator>();
            if (device) device.SetDeviceState(on ? IndicatorLamp.Green : inactive);
        }

        private void UseCheckButton(IInteractor interactor)
        {
            if (feedback != null) return;
            if (solved && PuzzleDoor) PuzzleDoor.TryTraverse(interactor);
            else CheckAnswer();
        }

        private void UpdateCheckPrompt()
        {
            if (!checkInteraction) return;
            var data = solved && PuzzleDoor && PuzzleDoor.DoorInteraction ? PuzzleDoor.DoorInteraction : CheckInteraction;
            if (checkInteraction.Data != data) checkInteraction.SetInteractionData(data);
        }

        public void CheckAnswer()
        {
            if (feedback != null) return;
            if (Grid && !Grid.HasSupply)
            {
                feedback = StartCoroutine(ShowCheck(false));
                return;
            }
            if (solved)
            {
                if (ManualPuzzle) feedback = StartCoroutine(ShowCheck(true));
                return;
            }
            bool matches = MatchesSolution();
            if (matches)
            {
                solved = true;
                Apply();
                GetComponent<SavableObjectState>().SaveState();
            }
            if (ManualPuzzle || !matches) feedback = StartCoroutine(ShowCheck(matches));
        }

        private bool MatchesSolution()
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                bool isSelected = ManualPuzzle ? (switchedMask & (1 << i)) != 0 :
                    Shutters != null && i < Shutters.Length && Shutters[i] && Shutters[i].IsOpen;
                if (isSelected != solution[i]) return false;
            }
            return true;
        }

        private IEnumerator ShowCheck(bool passed)
        {
            ShowingWrongAnswer = !passed;
            if (!passed && WrongAnswerBuzzer) WrongAnswerBuzzer.Play();
            if (ManualPuzzle) BeginCheckShot();
            else if (WrongAnswerCamera) WrongAnswerCamera.SetActive(true);

            yield return new WaitForSecondsRealtime(WrongCameraSeconds);
            EndCheck();
            ShowPowerResult(passed);
        }

        private bool BeginCheckShot()
        {
            var outputCamera = CameraSystem.Exists ? CameraSystem.Instance.MainCamera : Camera.main;
            var view = CheckCamera ? CheckCamera.GetComponentInChildren<CinemachineVirtualCameraBase>(true) : null;
            if (!view || !outputCamera || !outputCamera.TryGetComponent(out checkBrain)) return false;

            pause = PauseController.Instance;
            pause.Pause(this);
            input = UIManager.Get<UIInputListener>(true);
            if (input) input.AddBlockingContext(this);
            checkBrainIgnoredTimeScale = checkBrain.IgnoreTimeScale;
            checkBrain.IgnoreTimeScale = true;
            CheckCamera.Activate();
            checkCameraOverride = checkBrain.SetCameraOverride(-1, 0, null, view, 1f, -1f);
            letterbox = CameraPreview.CreateLetterbox(CheckCamera);
            return true;
        }

        private IEnumerator ShowOutputTravel(GarageDoorCircuitLink motor, bool open)
        {
            float timeout = Time.unscaledTime + 5f;
            yield return new WaitUntil(() => !motor || (open ? motor.IsOpen : motor.IsClosed) ||
                !Grid || !Grid.HasSupply || Time.unscaledTime >= timeout);
            yield return new WaitForSecondsRealtime(.4f);
            bool hasPower = Grid && Grid.HasSupply && !Grid.IsShutdown &&
                Grid.IsCircuitRequested(motor.CircuitId) == open;
            EndCheck();
            ShowPowerResult(hasPower);
        }

        private static void ShowPowerResult(bool hasPower) =>
            HorrorGame.UI.PlayerMonologue.Show(hasPower ? "The power is restored" : "There is no power");

        private void OnDisable()
        {
            if (feedback == null) return;
            StopCoroutine(feedback);
            EndCheck();
        }

        private void EndCheck()
        {
            if (letterbox) Destroy(letterbox);
            if (checkCameraOverride >= 0)
            {
                if (checkBrain)
                {
                    checkBrain.ReleaseCameraOverride(checkCameraOverride);
                    checkBrain.IgnoreTimeScale = checkBrainIgnoredTimeScale;
                }
                if (CheckCamera) CheckCamera.Deactivate();
                checkCameraOverride = -1;
            }
            if (WrongAnswerCamera) WrongAnswerCamera.SetActive(false);
            if (input) input.RemoveBlockingContext(this);
            if (pause) pause.Resume(this);
            checkBrain = null;
            input = null;
            pause = null;
            feedback = null;
            ShowingWrongAnswer = false;
            RefreshIndicators();
        }

        private void Apply()
        {
            if (MainShutter) MainShutter.SetOpen(solved);
            if (PuzzleDoor) PuzzleDoor.SetPuzzleUnlocked(solved);
            RefreshOutputButtons();
            UpdateCheckPrompt();
        }
        private void GenerateSolution()
        {
            solution = new bool[6];
            int count = UnityEngine.Random.Range(2, 5);
            while (count > 0) { int index = UnityEngine.Random.Range(0, 6); if (solution[index]) continue; solution[index] = true; --count; }
        }
        private int CountSolution() { int count = 0; foreach (bool value in solution) if (value) ++count; return count; }
        public string GetSavableData() => JsonUtility.ToJson(new State
        {
            Solution = solution, Solved = solved, LeverMask = switchedMask,
            OutputRequested = OutputShutter && Grid && Grid.IsCircuitRequested(OutputShutter.CircuitId),
            UseTenantClue = useTenantClue
        });
        public void SetFromSavedData(string data)
        {
            State state = JsonUtility.FromJson<State>(data);
            useTenantClue = state.UseTenantClue;
            if (state.Solution != null && state.Solution.Length == 6) solution = state.Solution;
            switchedMask = state.LeverMask;
            solved = UsesBayButtons ? MatchesSolution() : state.Solved;
            if (OutputShutter && Grid) Grid.SetCircuit(OutputShutter.CircuitId, state.OutputRequested);
            RefreshIndicators();
            Apply();
            RefreshNote();
        }
        [Serializable] private struct State
        {
            public bool[] Solution;
            public bool Solved;
            public int LeverMask;
            public bool OutputRequested;
            public bool UseTenantClue;
        }
    }
}
