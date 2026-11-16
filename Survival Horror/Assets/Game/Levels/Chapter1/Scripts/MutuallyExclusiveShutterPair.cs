using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>
    /// Keeps two shutters mutually exclusive, including when their circuits are
    /// toggled from the map. Wire its public methods to existing Interactives.
    /// </summary>
    public sealed class MutuallyExclusiveShutterPair : MonoBehaviour
    {
        public SimplePuzzleShutter LeftShutter;
        public SimplePuzzleShutter RightShutter;
        public bool LeftStartsOpen = true;
        private DistrictPowerGrid grid;

        private void Start()
        {
            grid = LeftShutter ? LeftShutter.Grid : RightShutter ? RightShutter.Grid : null;
            if (grid != null)
                grid.CircuitChanged += OnCircuitChanged;
            else if (LeftStartsOpen) OpenLeft();
            else OpenRight();
        }

        private void OnDestroy()
        {
            if (grid != null) grid.CircuitChanged -= OnCircuitChanged;
        }

        private void OnCircuitChanged(string circuitId, bool powered)
        {
            if (!powered || grid == null) return;
            if (LeftShutter && circuitId == LeftShutter.CircuitId && RightShutter)
                grid.SetCircuit(RightShutter.CircuitId, false);
            else if (RightShutter && circuitId == RightShutter.CircuitId && LeftShutter)
                grid.SetCircuit(LeftShutter.CircuitId, false);
        }

        public void OpenLeft()
        {
            if (grid != null)
            {
                if (RightShutter) grid.SetCircuit(RightShutter.CircuitId, false);
                if (LeftShutter) grid.SetCircuit(LeftShutter.CircuitId, true);
                return;
            }
            if (LeftShutter) LeftShutter.SetOpen(true);
            if (RightShutter) RightShutter.SetOpen(false);
        }

        public void OpenRight()
        {
            if (grid != null)
            {
                if (LeftShutter) grid.SetCircuit(LeftShutter.CircuitId, false);
                if (RightShutter) grid.SetCircuit(RightShutter.CircuitId, true);
                return;
            }
            if (LeftShutter) LeftShutter.SetOpen(false);
            if (RightShutter) RightShutter.SetOpen(true);
        }

        public void ToggleLeft()
        {
            if (grid != null && LeftShutter != null) { if (grid.IsCircuitPowered(LeftShutter.CircuitId)) grid.SetCircuit(LeftShutter.CircuitId, false); else OpenLeft(); return; }
            if (LeftShutter != null && LeftShutter.IsOpen) OpenRight();
            else OpenLeft();
        }

        public void ToggleRight()
        {
            if (grid != null && RightShutter != null) { if (grid.IsCircuitPowered(RightShutter.CircuitId)) grid.SetCircuit(RightShutter.CircuitId, false); else OpenRight(); return; }
            if (RightShutter != null && RightShutter.IsOpen) OpenLeft();
            else OpenRight();
        }
    }
}
