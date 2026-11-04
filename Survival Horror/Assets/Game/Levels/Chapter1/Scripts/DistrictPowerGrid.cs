using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame.Chapter1
{
    [Serializable]
    public class PowerCircuit
    {
        public string Id;
        public string DisplayName;
        [Min(0)] public int Consumption = 20;
        public bool StartsEnabled;
        [HideInInspector] public bool Enabled;
    }

    /// <summary>Single source of truth for the district's deliberately limited electrical supply.</summary>
    public sealed class DistrictPowerGrid : MonoBehaviour
    {
        [Tooltip("Shared authored circuit list. It is cloned on play so overloads never modify the asset.")]
        public DistrictPowerGridData Data;
        [Min(1)] public int MaximumCapacity = 100;
        public List<PowerCircuit> Circuits = new List<PowerCircuit>();
        public UnityEvent OnGridChanged;
        public UnityEvent OnGridOverloaded;

        public bool IsShutdown { get; private set; }
        public int CurrentConsumption { get; private set; }
        public int RemainingCapacity => Mathf.Max(0, MaximumCapacity - CurrentConsumption);

        public event Action<string, bool> CircuitChanged;

        private void Awake()
        {
            if (Data != null)
            {
                MaximumCapacity = Data.MaximumCapacity;
                Circuits = new List<PowerCircuit>();
                foreach (PowerCircuit source in Data.Circuits)
                {
                    Circuits.Add(new PowerCircuit { Id = source.Id, DisplayName = source.DisplayName, Consumption = source.Consumption, StartsEnabled = source.StartsEnabled });
                }
            }
            foreach (PowerCircuit circuit in Circuits) circuit.Enabled = circuit.StartsEnabled;
            Recalculate();
        }

        /// <summary>Imports circuit definitions added by the map authoring tools without resetting live circuit state.</summary>
        public void SyncDefinitionsFromData()
        {
            if (Data == null) return;
            MaximumCapacity = Data.MaximumCapacity;
            foreach (PowerCircuit source in Data.Circuits)
            {
                PowerCircuit existing = Circuits.Find(value => value.Id == source.Id);
                if (existing == null)
                {
                    Circuits.Add(new PowerCircuit { Id = source.Id, DisplayName = source.DisplayName, Consumption = source.Consumption, StartsEnabled = source.StartsEnabled });
                }
                else
                {
                    existing.DisplayName = source.DisplayName;
                    existing.Consumption = source.Consumption;
                    existing.StartsEnabled = source.StartsEnabled;
                }
            }
            Recalculate();
        }

        public bool IsCircuitPowered(string id)
        {
            PowerCircuit circuit = Circuits.Find(value => value.Id == id);
            return !IsShutdown && circuit != null && circuit.Enabled;
        }

        public void SetCircuit(string id, bool enabled)
        {
            if (IsShutdown) return;
            PowerCircuit circuit = Circuits.Find(value => value.Id == id);
            if (circuit == null || circuit.Enabled == enabled) return;
            circuit.Enabled = enabled;
            CircuitChanged?.Invoke(id, enabled);
            Recalculate();
        }

        public void ResetGrid()
        {
            IsShutdown = false;
            foreach (PowerCircuit circuit in Circuits)
            {
                if (circuit.Enabled) CircuitChanged?.Invoke(circuit.Id, false);
                circuit.Enabled = false;
            }
            Recalculate();
        }

        private void Recalculate()
        {
            CurrentConsumption = 0;
            if (!IsShutdown)
                foreach (PowerCircuit circuit in Circuits)
                    if (circuit.Enabled) CurrentConsumption += circuit.Consumption;

            if (CurrentConsumption > MaximumCapacity)
            {
                IsShutdown = true;
                foreach (PowerCircuit circuit in Circuits)
                {
                    if (circuit.Enabled) CircuitChanged?.Invoke(circuit.Id, false);
                    circuit.Enabled = false;
                }
                CurrentConsumption = 0;
                OnGridOverloaded?.Invoke();
            }
            OnGridChanged?.Invoke();
        }
    }
}
