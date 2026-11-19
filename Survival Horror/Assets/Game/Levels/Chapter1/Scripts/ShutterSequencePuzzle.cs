using System;
using System.Collections;
using HouseToolkit;
using HorrorEngine;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
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
        public PowerCircuitPanelElement[] SegmentDots = Array.Empty<PowerCircuitPanelElement>();
        public DocumentData Note;
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
        public CircuitDrivenObject[] SegmentShutters = Array.Empty<CircuitDrivenObject>();
        public ShutterIndicatorLamp[] SegmentLamps = Array.Empty<ShutterIndicatorLamp>();
        public ShutterIndicatorLamp CheckLamp;
        public InteractionData LeverInteraction;
        public InteractionData CheckInteraction;

        private const int SegmentCount = 6;
        private static readonly Color Red = new Color(1f, .12f, .06f);
        private static readonly Color Green = new Color(.18f, 1f, .25f);
        private static readonly Color Yellow = new Color(1f, .67f, .12f);
        [SerializeField] private Transform[] leverHandles = new Transform[SegmentCount];
        private int leverMask;
        private Coroutine feedback;
        private Interactive checkInteraction;
        private GameObject letterbox;
        private UIInputListener input;
        private PauseController pause;
        private CinemachineBrain checkBrain;
        private int checkCameraOverride = -1;
        private bool ManualPuzzle => CheckButton;
        public bool ShowingWrongAnswer { get; private set; }

        public bool[] Solution => solution;
        public bool IsSolved => solved;
        private void Awake()
        {
            if (solution == null || solution.Length != 6 || CountSolution() == 0) GenerateSolution();
            Apply();
            RefreshNote();
        }

        private void Start()
        {
            if (ManualPuzzle)
            {
                for (int i = 0; i < LeverPositions.Length && i < SegmentCount; i++)
                {
                    int index = i;
                    if (LeverPositions[i])
                        InstallControl(LeverPositions[i], LeverInteraction, _ => ToggleLever(index));
                }
                checkInteraction = InstallControl(CheckButton, CheckInteraction, UseCheckButton);
                if (PuzzleDoor) PuzzleDoor.OnLockedInteraction += CheckAnswer;
                UpdateCheckPrompt();
                RefreshIndicators();
            }
            if (!Grid || !PuzzleDoor) return;
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
            if (PuzzleDoor) PuzzleDoor.OnLockedInteraction -= CheckAnswer;
            if (noteSprite)
            {
                if (Note && Note.Pages != null && Note.Pages.Count >= 2 && Note.Pages[1].Image.Unlocalized == noteSprite)
                    Note.Pages[1].Image.Unlocalized = null;
                Destroy(noteSprite);
            }
            if (noteTexture) Destroy(noteTexture);
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
            if (!Note || Note.Pages == null || Note.Pages.Count < 2) return;
            if (noteSprite) Destroy(noteSprite);
            if (noteTexture) Destroy(noteTexture);
            const int cell = 54, pitch = 68, width = pitch * 2, height = pitch * 3;
            noteTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[width * height];
            for (int i = 0; i < 6; i++)
            {
                Color32 color = solution[i] ? new Color32(52, 160, 89, 255) : new Color32(185, 63, 57, 255);
                int x0 = i % 2 * pitch + 7;
                int y0 = (2 - i / 2) * pitch + 7;
                for (int y = y0; y < y0 + cell; y++)
                    for (int x = x0; x < x0 + cell; x++)
                        pixels[y * width + x] = color;
            }
            noteTexture.SetPixels32(pixels);
            noteTexture.Apply();
            noteSprite = Sprite.Create(noteTexture, new Rect(0, 0, width, height), Vector2.one * .5f);
            Note.Pages[1].Image.Unlocalized = noteSprite;
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
            if (feedback != null || index >= SegmentShutters.Length || !SegmentShutters[index] ||
                !SegmentShutters[index].TryGetComponent(out GarageDoor door) || !door.IsOpen) return;
            leverMask ^= 1 << index;
            RefreshIndicators();
            GetComponent<SavableObjectState>().SaveState();
        }

        private void RefreshIndicators()
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                bool on = (leverMask & (1 << i)) != 0;
                if (i < SegmentLamps.Length && SegmentLamps[i]) SegmentLamps[i].SetManualColor(on ? Green : Red);
                if (i < leverHandles.Length && leverHandles[i]) leverHandles[i].localRotation = Quaternion.Euler(on ? -45f : 45f, 0f, 0f);
            }
            if (CheckLamp) CheckLamp.SetManualColor(solved ? Green : Yellow);
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
            if (solved)
            {
                if (ManualPuzzle) feedback = StartCoroutine(ShowCheck(true));
                return;
            }
            bool matches = true;
            for (int i = 0; i < SegmentCount; i++)
            {
                bool on = ManualPuzzle ? (leverMask & (1 << i)) != 0 :
                    Shutters != null && i < Shutters.Length && Shutters[i] && Shutters[i].IsOpen;
                if (on != solution[i]) matches = false;
            }

            if (matches)
            {
                solved = true;
                Apply();
                GetComponent<SavableObjectState>().SaveState();
            }
            if (ManualPuzzle || !matches) feedback = StartCoroutine(ShowCheck(matches));
        }

        private IEnumerator ShowCheck(bool passed)
        {
            ShowingWrongAnswer = !passed;
            if (CheckLamp) CheckLamp.SetManualColor(passed ? Green : Red);
            if (!passed && WrongAnswerBuzzer) WrongAnswerBuzzer.Play();
            if (ManualPuzzle)
            {
                pause = PauseController.Instance;
                if (pause) pause.Pause(this);
                input = UIManager.Get<UIInputListener>(true);
                if (input) input.AddBlockingContext(this);
                var outputCamera = CameraSystem.Exists ? CameraSystem.Instance.MainCamera : Camera.main;
                if (CheckCamera && outputCamera && outputCamera.TryGetComponent(out checkBrain))
                {
                    var view = CheckCamera.GetComponentInChildren<CinemachineVirtualCameraBase>(true);
                    if (view)
                    {
                        CheckCamera.Activate();
                        checkCameraOverride = checkBrain.SetCameraOverride(-1, 0, null, view, 1f, -1f);
                    }
                }
                MakeLetterbox();
            }
            else if (WrongAnswerCamera) WrongAnswerCamera.SetActive(true);

            yield return new WaitForSecondsRealtime(WrongCameraSeconds);
            EndCheck();
        }

        private void MakeLetterbox()
        {
            letterbox = new GameObject("Shutter sequence letterbox", typeof(Canvas));
            var canvas = letterbox.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var preview = CheckCamera ? CheckCamera.GetComponentInChildren<CameraPreview>(true) : null;
            float height = preview ? preview.LetterboxHeight : .2f;
            AddBar("Top", 1f - height, 1f);
            AddBar("Bottom", 0f, height);
        }

        private void AddBar(string barName, float bottom, float top)
        {
            var bar = new GameObject(barName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bar.transform.SetParent(letterbox.transform, false);
            bar.rectTransform.anchorMin = new Vector2(0f, bottom);
            bar.rectTransform.anchorMax = new Vector2(1f, top);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            bar.color = Color.black;
            bar.raycastTarget = false;
        }

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
                if (checkBrain) checkBrain.ReleaseCameraOverride(checkCameraOverride);
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
            UpdateCheckPrompt();
        }
        private void GenerateSolution()
        {
            solution = new bool[6];
            int count = UnityEngine.Random.Range(2, 5);
            while (count > 0) { int index = UnityEngine.Random.Range(0, 6); if (solution[index]) continue; solution[index] = true; --count; }
        }
        private int CountSolution() { int count = 0; foreach (bool value in solution) if (value) ++count; return count; }
        public string GetSavableData() => JsonUtility.ToJson(new State { Solution = solution, Solved = solved, LeverMask = leverMask });
        public void SetFromSavedData(string data)
        {
            State state = JsonUtility.FromJson<State>(data);
            if (state.Solution != null && state.Solution.Length == 6) solution = state.Solution;
            solved = state.Solved;
            leverMask = state.LeverMask;
            RefreshIndicators();
            Apply();
            RefreshNote();
        }
        [Serializable] private struct State { public bool[] Solution; public bool Solved; public int LeverMask; }
    }
}
