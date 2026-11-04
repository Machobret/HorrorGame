using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Authoring asset for a district's available electrical circuits. Runtime grids clone this data before changing state.</summary>
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/District Power Grid Data", fileName = "DistrictPowerGridData")]
    public sealed class DistrictPowerGridData : ScriptableObject
    {
        [Min(1)] public int MaximumCapacity = 100;
        public List<PowerCircuit> Circuits = new List<PowerCircuit>();
    }
}
