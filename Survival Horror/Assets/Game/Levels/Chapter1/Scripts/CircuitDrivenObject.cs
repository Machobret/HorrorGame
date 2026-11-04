using UnityEngine;

namespace HorrorGame.Chapter1
{
    public abstract class CircuitDrivenObject : MonoBehaviour
    {
        public DistrictPowerGrid Grid;
        public string CircuitId;

        protected virtual void Start()
        {
            if (Grid != null) Grid.CircuitChanged += OnCircuitChanged;
            Refresh();
        }

        protected virtual void OnDestroy()
        {
            if (Grid != null) Grid.CircuitChanged -= OnCircuitChanged;
        }

        private void OnCircuitChanged(string id, bool powered)
        {
            if (id == CircuitId) Refresh();
        }

        protected bool Powered => Grid != null && Grid.IsCircuitPowered(CircuitId);
        public abstract void Refresh();
    }
}
