using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/Circuit Diagram", fileName = "CircuitDiagram")]
    public sealed class CircuitDiagramData : DocumentData
    {
        public MapData Map;
        [HideInInspector] public string RoomUniqueId;
    }
}
