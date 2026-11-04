using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Attach to the fuse-box tablet. It can be opened through Horror Engine's Interactive event or by clicking it in the prototype scene.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DistrictPowerTablet : MonoBehaviour
    {
        public DistrictPowerGrid Grid;
        [Tooltip("Optional panel prefab instance. When omitted, a map-style runtime panel is created automatically.")]
        public ElectricalPanelDisplay Panel;
        [Tooltip("Chapter-owned UIMap variant instantiated by this tablet. The original Horror Engine UIMap is never modified.")]
        public ElectricalPanelDisplay PanelPrefab;
        [Tooltip("The real Horror Engine map data shown behind the selectable circuit points.")]
        public MapData DistrictMapData;
        [Tooltip("The editable map-style circuit layout used when the panel is created at runtime.")]
        public PowerPanelLayout PanelLayout;
        public KeyCode CloseKey = KeyCode.Escape;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Interactive interactive = GetComponent<Interactive>();
            if (interactive != null) interactive.OnInteract.AddListener(_ => Open());
        }

        private void OnMouseDown() => Open();
        public void Open()
        {
            if (PanelPrefab != null && PanelPrefab.TryGetComponent(out PowerMapUI isolatedMap))
            {
                PowerMapUI instance = Instantiate(isolatedMap);
                instance.Open(Grid, DistrictMapData, PanelLayout);
                return;
            }
            if (DistrictMapData != null)
            {
                PowerMapCircuitPanel mapPanel = FindFirstObjectByType<PowerMapCircuitPanel>();
                if (!mapPanel)
                {
                    mapPanel = new GameObject("District Power Map Circuit Panel (Runtime)").AddComponent<PowerMapCircuitPanel>();
                    mapPanel.MapData = DistrictMapData;
                    mapPanel.Layout = PanelLayout;
                }
                mapPanel.Open(Grid);
                return;
            }
            if (Panel == null) Panel = ElectricalPanelDisplay.Instance;
            if (Panel == null)
            {
                if (PanelPrefab != null)
                    Panel = Instantiate(PanelPrefab);
                else
                {
                    GameObject panelObject = new GameObject("Electrical Panel Display (Runtime)");
                    Panel = panelObject.AddComponent<ElectricalPanelDisplay>();
                }
                Panel.Layout = PanelLayout;
            }
            Panel.Open(Grid);
            IsOpen = false;
        }
        public void Close() { if (Panel != null) Panel.Close(); IsOpen = false; }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(CloseKey)) Close();
        }

        private void OnGUI()
        {
            if (!IsOpen || Grid == null || Panel != null) return;
            GUILayout.BeginArea(new Rect(Screen.width * .5f - 220, Screen.height * .5f - 250, 440, 500), GUI.skin.window);
            GUILayout.Label("ELECTRICIAN TABLET — DISTRICT GRID");
            GUILayout.Label(Grid.IsShutdown ? "GRID SHUT DOWN — return/reset required" : $"LOAD {Grid.CurrentConsumption} / {Grid.MaximumCapacity}    AVAILABLE {Grid.RemainingCapacity}");
            GUILayout.Space(10);
            if (Grid.IsShutdown)
            {
                if (GUILayout.Button("RESET FUSE BOX")) Grid.ResetGrid();
            }
            else
            {
                foreach (PowerCircuit circuit in Grid.Circuits)
                {
                    bool requested = GUILayout.Toggle(circuit.Enabled, $"{circuit.DisplayName}  —  {circuit.Consumption} power");
                    if (requested != circuit.Enabled) Grid.SetCircuit(circuit.Id, requested);
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE")) Close();
            GUILayout.EndArea();
        }
    }
}
