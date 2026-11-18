using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>
    /// Keeps two shutters mutually exclusive, including when their circuits are
    /// toggled from the map. Wire its public methods to existing Interactives.
    /// </summary>
    public sealed class MutuallyExclusiveShutterPair : MonoBehaviour
    {
        public CircuitDrivenObject LeftShutter;
        public CircuitDrivenObject RightShutter;
        public bool LeftStartsOpen = true;
        private DistrictPowerGrid grid;

        private void Start()
        {
            grid = LeftShutter ? LeftShutter.Grid : RightShutter ? RightShutter.Grid : null;
            if (grid != null)
            {
                grid.CircuitChanged += OnCircuitChanged;
                if (LeftShutter && RightShutter &&
                    grid.IsCircuitPowered(LeftShutter.CircuitId) && grid.IsCircuitPowered(RightShutter.CircuitId))
                {
                    if (LeftStartsOpen) OpenLeft();
                    else OpenRight();
                }
            }
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
            if (LeftShutter is SimplePuzzleShutter left) left.SetOpen(true);
            if (RightShutter is SimplePuzzleShutter right) right.SetOpen(false);
        }

        public void OpenRight()
        {
            if (grid != null)
            {
                if (LeftShutter) grid.SetCircuit(LeftShutter.CircuitId, false);
                if (RightShutter) grid.SetCircuit(RightShutter.CircuitId, true);
                return;
            }
            if (LeftShutter is SimplePuzzleShutter left) left.SetOpen(false);
            if (RightShutter is SimplePuzzleShutter right) right.SetOpen(true);
        }

        public void ToggleLeft()
        {
            if (grid != null && LeftShutter != null) { if (grid.IsCircuitPowered(LeftShutter.CircuitId)) grid.SetCircuit(LeftShutter.CircuitId, false); else OpenLeft(); return; }
            if (LeftShutter is SimplePuzzleShutter left && left.IsOpen) OpenRight();
            else OpenLeft();
        }

        public void ToggleRight()
        {
            if (grid != null && RightShutter != null) { if (grid.IsCircuitPowered(RightShutter.CircuitId)) grid.SetCircuit(RightShutter.CircuitId, false); else OpenRight(); return; }
            if (RightShutter is SimplePuzzleShutter right && right.IsOpen) OpenLeft();
            else OpenRight();
        }
    }
}
