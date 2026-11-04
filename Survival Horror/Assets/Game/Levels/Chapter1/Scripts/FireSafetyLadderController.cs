using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Enables a prebuilt ClimbableLadder only when the alarm is active and its motor circuit has power.</summary>
    public sealed class FireSafetyLadderController : MonoBehaviour
    {
        public DistrictPowerGrid Grid;
        public string EmergencyExitCircuitId = "apartment_emergency_exit";
        public GameObject Ladder;
        private void OnEnable()
        {
            DistrictFireAlarm.AlarmRaised += Refresh;
            if (Grid != null) Grid.CircuitChanged += OnCircuitChanged;
            Refresh();
        }
        private void OnDisable()
        {
            DistrictFireAlarm.AlarmRaised -= Refresh;
            if (Grid != null) Grid.CircuitChanged -= OnCircuitChanged;
        }
        private void OnCircuitChanged(string id, bool powered) { if (id == EmergencyExitCircuitId) Refresh(); }
        public void Refresh()
        {
            if (Ladder) Ladder.SetActive(DistrictFireAlarm.IsActive && Grid != null && Grid.IsCircuitPowered(EmergencyExitCircuitId));
        }
    }
}
