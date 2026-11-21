using System;
using System.Collections.Generic;
using Game.Environment;
using HorrorEngine;
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
        [Serializable]
        private sealed class TabletCircuitAccess
        {
            public string CircuitId;
            public MapRoom[] Rooms = Array.Empty<MapRoom>();
        }

        [Tooltip("Shared authored circuit list. It is cloned on play so overloads never modify the asset.")]
        public DistrictPowerGridData Data;
        public ElectricalBoxPower Supply;
        [SerializeField, Tooltip("Additional rooms where each circuit can be controlled from the tablet.")]
        private TabletCircuitAccess[] additionalTabletAccess = Array.Empty<TabletCircuitAccess>();
        public bool HasSupply => !Supply || Supply.IsOn;
        public bool EnforceCapacityLimit = true;
        [Min(1)] public int MaximumCapacity = 100;
        public List<PowerCircuit> Circuits = new List<PowerCircuit>();
        public UnityEvent OnGridChanged=new UnityEvent();
        public UnityEvent OnGridOverloaded=new UnityEvent();

        public bool IsShutdown { get; private set; }
        public int CurrentConsumption { get; private set; }
        public int RemainingCapacity => Mathf.Max(0, MaximumCapacity - CurrentConsumption);

        public event Action<string, bool> CircuitChanged;
        internal event Action<string> CircuitDisplaced;
        public event Action SupplyChanged;
        public event Func<string, bool> CanEnableCircuit;

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

        private void OnEnable()
        {
            if (Supply) Supply.PowerChanged += OnSupplyChanged;
        }

        private void OnDisable()
        {
            if (Supply) Supply.PowerChanged -= OnSupplyChanged;
        }

        private void OnSupplyChanged(bool powered)
        {
            Recalculate();
            SupplyChanged?.Invoke();
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
            return HasSupply && !IsShutdown && !IsCircuitOutOfService(id) && IsCircuitRequested(id);
        }

        private readonly HashSet<string> outOfService = new HashSet<string>();
        public bool IsCircuitOutOfService(string id) => outOfService.Contains(id);

        internal void SetCircuitOutOfService(string id, bool unavailable)
        {
            if (unavailable)
            {
                if (outOfService.Add(id)) SetCircuit(id, false);
            }
            else outOfService.Remove(id);
        }

        public bool IsCircuitRequested(string id) => Circuits.Exists(value => value.Id == id && value.Enabled);

        internal bool HasAdditionalTabletAccess(string circuitId, string roomId)
        {
            foreach (var access in additionalTabletAccess)
            {
                if (access.CircuitId != circuitId) continue;
                foreach (var room in access.Rooms)
                    if (room && room.TryGetComponent(out ObjectUniqueId id) && id.Id == roomId)
                        return true;
            }
            return false;
        }

        internal void ReportCircuitDisplacement(string id) => CircuitDisplaced?.Invoke(id);

        public void SetCircuit(string id, bool enabled)
        {
            if (IsShutdown || (enabled && IsCircuitOutOfService(id))) return;
            PowerCircuit circuit = Circuits.Find(value => value.Id == id);
            if (circuit == null || circuit.Enabled == enabled) return;
            if (enabled && CanEnableCircuit != null)
                foreach (Func<string, bool> check in CanEnableCircuit.GetInvocationList())
                    if (!check(id)) return;
            circuit.Enabled = enabled;
            CircuitChanged?.Invoke(id, enabled);
            Recalculate();
        }

        public void ResetGrid()
        {
            IsShutdown = false;
            foreach (PowerCircuit circuit in Circuits)
            {
                if (!circuit.Enabled) continue;
                circuit.Enabled = false;
                CircuitChanged?.Invoke(circuit.Id, false);
            }
            Recalculate();
        }

        private void Recalculate()
        {
            CurrentConsumption = 0;
            if (HasSupply && !IsShutdown)
                foreach (PowerCircuit circuit in Circuits)
                    if (circuit.Enabled && !IsCircuitOutOfService(circuit.Id)) CurrentConsumption += circuit.Consumption;

            if (EnforceCapacityLimit && CurrentConsumption > MaximumCapacity)
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
