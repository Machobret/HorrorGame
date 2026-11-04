using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HorrorGame.Chapter1
{
    /// <summary>
    /// Map-style electrical panel. Circuit positions come from PowerPanelLayout and use the same UI primary axis as the map (WASD/stick).
    /// Confirm toggles the selected circuit; Cancel closes the panel.
    /// </summary>
    public sealed class ElectricalPanelDisplay : MonoBehaviour
    {
        public static ElectricalPanelDisplay Instance { get; private set; }
        public DistrictPowerGrid Grid;
        public PowerPanelLayout Layout;
        [Min(.1f)] public float NavigationRepeatSeconds = .2f;
        public bool IsOpen { get; private set; }

        private IUIInput input;
        private float nextMoveTime;
        private int selectedIndex;
        private RectTransform panelRoot;
        private TextMeshProUGUI loadText;
        private readonly List<PanelNode> nodes = new List<PanelNode>();

        private sealed class PanelNode
        {
            public Image Background;
            public TextMeshProUGUI Text;
        }

        private void Awake()
        {
            Instance = this;
        }
        private void Start()
        {
            if (UIManager.Instance) input = UIManager.Instance.GetComponent<IUIInput>();
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(DistrictPowerGrid requestedGrid)
        {
            Grid = requestedGrid ? requestedGrid : Grid;
            if (Grid == null) return;
            gameObject.SetActive(true);
            IsOpen = true;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, Shapes.Count - 1));
            BuildMapUi();
            RefreshMapUi();
            if (PauseController.Instance) PauseController.Instance.Pause(this);
            input?.Flush();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (PauseController.Instance) PauseController.Instance.Resume(this);
            gameObject.SetActive(false);
        }

        private List<PowerPanelCircuitShape> Shapes
        {
            get
            {
                if (Layout && Layout.Circuits.Count > 0) return Layout.Circuits;
                return CreateFallbackShapes();
            }
        }
        private List<PowerPanelCircuitShape> CreateFallbackShapes()
        {
            List<PowerPanelCircuitShape> shapes = new List<PowerPanelCircuitShape>();
            if (Grid == null) return shapes;
            for (int i = 0; i < Grid.Circuits.Count; i++)
            {
                PowerCircuit circuit = Grid.Circuits[i];
                shapes.Add(new PowerPanelCircuitShape { CircuitId = circuit.Id, Label = circuit.DisplayName, Bounds = new Rect(30 + (i % 3) * 245, 80 + (i / 3) * 78, 220, 58) });
            }
            return shapes;
        }
        private void Update()
        {
            if (!IsOpen) return;
            if (input == null && UIManager.Instance) input = UIManager.Instance.GetComponent<IUIInput>();
            if (input != null)
            {
                Vector2 axis = input.GetPrimaryAxis();
                if (axis.sqrMagnitude > .25f && Time.unscaledTime >= nextMoveTime)
                {
                    MoveSelection(axis.normalized);
                    nextMoveTime = Time.unscaledTime + NavigationRepeatSeconds;
                }
                if (input.IsConfirmDown()) ToggleSelected();
                if (input.IsCancelDown()) Close();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(Vector2.up);
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(Vector2.down);
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveSelection(Vector2.left);
                if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MoveSelection(Vector2.right);
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) ToggleSelected();
                if (Input.GetKeyDown(KeyCode.Escape)) Close();
            }
        }
        private void MoveSelection(Vector2 direction)
        {
            List<PowerPanelCircuitShape> shapes = Shapes;
            if (shapes.Count == 0) return;
            Vector2 origin = shapes[selectedIndex].Bounds.center;
            float score = float.MaxValue; int best = selectedIndex;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (i == selectedIndex) continue;
                Vector2 delta = shapes[i].Bounds.center - origin;
                if (Vector2.Dot(direction, delta.normalized) <= .25f) continue;
                float candidate = delta.magnitude + Mathf.Abs(Vector2.Dot(new Vector2(-direction.y, direction.x), delta)) * 2f;
                if (candidate < score) { score = candidate; best = i; }
            }
            selectedIndex = best;
            RefreshMapUi();
        }
        private void ToggleSelected()
        {
            List<PowerPanelCircuitShape> shapes = Shapes;
            if (Grid == null || Grid.IsShutdown || selectedIndex >= shapes.Count) return;
            PowerPanelCircuitShape shape = shapes[selectedIndex];
            Grid.SetCircuit(shape.CircuitId, !Grid.IsCircuitPowered(shape.CircuitId));
            RefreshMapUi();
        }
        private void BuildMapUi()
        {
            if (panelRoot != null) return;
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("District Power Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
            GameObject root = new GameObject("District Power Grid Data Map", typeof(RectTransform), typeof(Image));
            panelRoot = root.GetComponent<RectTransform>(); panelRoot.SetParent(canvas.transform, false);
            panelRoot.anchorMin = new Vector2(.5f, .5f); panelRoot.anchorMax = new Vector2(.5f, .5f); panelRoot.sizeDelta = new Vector2(800, 500);
            root.GetComponent<Image>().color = new Color(.015f, .08f, .11f, .94f);
            loadText = CreateText("GridDataHeader", panelRoot, new Vector2(18, -12), new Vector2(764, 45), 18, TextAlignmentOptions.Left);
            CreateText("Controls", panelRoot, new Vector2(18, -470), new Vector2(760, 24), 13, TextAlignmentOptions.Center).text = "WASD: SELECT     CONFIRM: TOGGLE CIRCUIT     CANCEL: CLOSE";
            Button close = CreateButton("Close", panelRoot, new Vector2(690, -12), new Vector2(92, 30), "CLOSE"); close.onClick.AddListener(Close);
            Button reset = CreateButton("ResetGrid", panelRoot, new Vector2(580, -12), new Vector2(100, 30), "RESET"); reset.onClick.AddListener(() => { if (Grid != null) { Grid.ResetGrid(); RefreshMapUi(); } });
            List<PowerPanelCircuitShape> shapes = Shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                int index = i;
                Rect bounds = shapes[i].Bounds;
                Button button = CreateButton(shapes[i].CircuitId, panelRoot, new Vector2(bounds.x, -bounds.y), new Vector2(bounds.width, bounds.height), "");
                Image image = button.GetComponent<Image>(); TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
                button.onClick.AddListener(() => { selectedIndex = index; ToggleSelected(); });
                nodes.Add(new PanelNode { Background = image, Text = text });
            }
        }
        private void RefreshMapUi()
        {
            if (Grid == null || loadText == null) return;
            loadText.text = Grid.IsShutdown ? "DISTRICT POWER GRID DATA    —    GRID SHUT DOWN" : $"DISTRICT POWER GRID DATA    —    LOAD {Grid.CurrentConsumption} / {Grid.MaximumCapacity}    AVAILABLE {Grid.RemainingCapacity}";
            List<PowerPanelCircuitShape> shapes = Shapes;
            for (int i = 0; i < Mathf.Min(nodes.Count, shapes.Count); i++)
            {
                PowerCircuit circuit = Grid.Circuits.Find(value => value.Id == shapes[i].CircuitId);
                bool powered = Grid.IsCircuitPowered(shapes[i].CircuitId);
                nodes[i].Background.color = i == selectedIndex ? new Color(.1f, .72f, .85f, .95f) : powered ? new Color(.12f, .48f, .22f, .95f) : new Color(.14f, .2f, .24f, .95f);
                nodes[i].Text.text = $"{(string.IsNullOrEmpty(shapes[i].Label) ? shapes[i].CircuitId : shapes[i].Label)}\n{(circuit != null ? circuit.Consumption : 0)} POWER   {(powered ? "ON" : "OFF")}";
            }
        }
        private static TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.fontSize = fontSize; text.alignment = alignment; text.color = Color.white; return text;
        }
        private static Button CreateButton(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
            Image image = go.GetComponent<Image>(); image.color = new Color(.14f, .2f, .24f, .95f); Button button = go.GetComponent<Button>(); button.targetGraphic = image;
            TextMeshProUGUI text = CreateText("Label", rect, new Vector2(7, -5), size - new Vector2(14, 10), 14, TextAlignmentOptions.Center); text.text = label; return button;
        }
    }
}
