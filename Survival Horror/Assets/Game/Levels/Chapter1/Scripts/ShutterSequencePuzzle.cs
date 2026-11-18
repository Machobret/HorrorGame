using System;
using HorrorEngine;
using UnityEngine;
using UnityEngine.Serialization;

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
        private float wrongCameraUntil;
        private float wrongFeedbackUntil;
        private UIMap tablet;
        private MapRoom sequenceRoom;
        private bool changedOnTablet;
        public bool ShowingWrongAnswer => Time.unscaledTime < wrongFeedbackUntil;

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
            if (!Grid || !PuzzleDoor) return;
            if (SegmentDots == null) return;
            var anchor = Array.Find(SegmentDots, dot => dot && dot.Room && dot.GetMapData());
            if (!anchor) return;
            var controller = anchor.GetComponentInParent<PowerPanelController>();
            sequenceRoom = anchor.Room;
            var sequenceMap = anchor.GetMapData();
            tablet = UIManager.Get<UIMap>(true);
            if (tablet) tablet.Closed += CheckAfterTabletClosed;
            Grid.CircuitChanged += TrackSegmentChange;
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

        private void TrackSegmentChange(string id, bool powered)
        {
            if (!solved && tablet && tablet.isActiveAndEnabled &&
                Array.Exists(SegmentDots, dot => dot && dot.CircuitId == id))
                changedOnTablet = true;
        }

        private void CheckAfterTabletClosed()
        {
            bool showWrongAnswer = changedOnTablet;
            changedOnTablet = false;
            if (solved || !sequenceRoom || !GameManager.Exists) return;
            var player = GameManager.Instance.Player;
            if (player && sequenceRoom.ContainsWorldPosition(player.transform.position))
                TryConfirmPattern(showWrongAnswer);
        }

        private void TryConfirmPattern(bool showWrongAnswer)
        {
            if (solved || Grid.IsShutdown || SegmentDots.Length != 6) return;
            for (int i = 0; i < 6; i++)
            {
                var dot = SegmentDots[i];
                if (dot && Grid.IsCircuitPowered(dot.CircuitId) == solution[i]) continue;
                if (showWrongAnswer) Wrong();
                return;
            }
            solved = true;
            Apply();
            GetComponent<SavableObjectState>().SaveState();
        }

        private void OnDestroy()
        {
            if (tablet) tablet.Closed -= CheckAfterTabletClosed;
            if (Grid) Grid.CircuitChanged -= TrackSegmentChange;
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
        private void Update() { if (WrongAnswerCamera && WrongAnswerCamera.activeSelf && Time.unscaledTime >= wrongCameraUntil) WrongAnswerCamera.SetActive(false); }

        /// <summary>Assign this to a normal Interactive's UnityEvent to submit the current shutter pattern.</summary>
        public void CheckAnswer()
        {
            if (Grid && PuzzleDoor) return;
            if (solved) return;
            for (int i = 0; i < 6; ++i)
            {
                bool open = Shutters != null && i < Shutters.Length && Shutters[i] && Shutters[i].IsOpen;
                if (open != solution[i]) { Wrong(); return; }
            }
            solved = true; Apply(); GetComponent<SavableObjectState>().SaveState();
        }
        private void Wrong()
        {
            wrongFeedbackUntil = Time.unscaledTime + WrongCameraSeconds;
            if (Grid && GameManager.Exists)
                GameManager.Instance.Inventory.AddTabletLog("Shutter pattern rejected.");
            if (WrongAnswerCamera) { WrongAnswerCamera.SetActive(true); wrongCameraUntil = Time.unscaledTime + WrongCameraSeconds; }
            if (WrongAnswerBuzzer) WrongAnswerBuzzer.Play();
        }
        private void Apply()
        {
            if (MainShutter) MainShutter.SetOpen(solved);
            if (PuzzleDoor) PuzzleDoor.SetPuzzleUnlocked(solved);
        }
        private void GenerateSolution()
        {
            solution = new bool[6];
            int count = UnityEngine.Random.Range(2, 5);
            while (count > 0) { int index = UnityEngine.Random.Range(0, 6); if (solution[index]) continue; solution[index] = true; --count; }
        }
        private int CountSolution() { int count = 0; foreach (bool value in solution) if (value) ++count; return count; }
        public string GetSavableData() => JsonUtility.ToJson(new State { Solution = solution, Solved = solved });
        public void SetFromSavedData(string data)
        {
            State state = JsonUtility.FromJson<State>(data);
            if (state.Solution != null && state.Solution.Length == 6) solution = state.Solution;
            solved = state.Solved;
            Apply();
            RefreshNote();
        }
        [Serializable] private struct State { public bool[] Solution; public bool Solved; }
    }
}
