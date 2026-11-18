using HorrorEngine;
using HorrorGame.Environment;
using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Configures the named Chapter 1 garage doors without moving their guides/header.</summary>
    public sealed class NamedCircuitShutterBinder : MonoBehaviour
    {
        public DistrictPowerGrid Grid;
        public string RaisedShutterName = "Circuit Shutters";
        public string TeleportShutterName = "Circuit Shutters Button";
        [Min(0)] public int Consumption = 10;

        private void Start()
        {
            if (!Grid) Grid = GetComponent<DistrictPowerGrid>();
            if (!Grid) return;
            int raised = 0, button = 0;
            foreach (GarageDoor door in FindObjectsByType<GarageDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!door) continue;
                if (door.name == RaisedShutterName)
                {
                    string id = $"circuit_shutter_{++raised:00}";
                    ConfigureCircuit(id, door.gameObject, false);
                }
                else if (door.name == TeleportShutterName)
                {
                    if (door.TryGetComponent(out CircuitShutterTeleport authored))
                    {
                        if (!string.IsNullOrEmpty(authored.CircuitId) && !Grid.Circuits.Exists(c => c.Id == authored.CircuitId))
                            Grid.Circuits.Add(new PowerCircuit { Id = authored.CircuitId, DisplayName = authored.CircuitId.Replace('_', ' '), Consumption = Consumption });
                        continue;
                    }
                    string id = $"circuit_shutter_button_{++button:00}";
                    ConfigureCircuit(id, door.gameObject, true);
                }
            }
        }

        private void ConfigureCircuit(string id, GameObject target, bool isTeleport)
        {
            if (!Grid.Circuits.Exists(c => c.Id == id))
                Grid.Circuits.Add(new PowerCircuit { Id = id, DisplayName = id.Replace('_', ' '), Consumption = Consumption });

            if (isTeleport)
            {
                CircuitShutterTeleport link = target.GetComponent<CircuitShutterTeleport>() ?? target.AddComponent<CircuitShutterTeleport>();
                link.Grid = Grid; link.CircuitId = id;
            }
            else
            {
                GarageDoorCircuitLink link = target.GetComponent<GarageDoorCircuitLink>() ?? target.AddComponent<GarageDoorCircuitLink>();
                link.Grid = Grid; link.CircuitId = id;
            }
        }
    }
}
