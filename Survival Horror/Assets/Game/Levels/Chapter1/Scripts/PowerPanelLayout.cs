using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/Power Panel Layout", fileName = "PowerPanelLayout")]
    public sealed class PowerPanelLayout : ScriptableObject
    {
        [Tooltip("Design-space size used by the panel. Circuit positions and polygons are editable in this space.")]
        public Vector2 CanvasSize = new Vector2(800, 500);
        public List<PowerPanelCircuitShape> Circuits = new List<PowerPanelCircuitShape>();
    }

    [Serializable]
    public sealed class PowerPanelCircuitShape
    {
        [Tooltip("Matches DistrictPowerGrid.PowerCircuit.Id")]
        public string CircuitId;
        public string Label;
        [Tooltip("Selectable panel bounds in the layout's design space.")]
        public Rect Bounds = new Rect(40, 40, 180, 64);
        [Tooltip("Optional map-style border points, relative to the layout canvas. They are drawn over Bounds.")]
        public List<Vector2> BorderPoints = new List<Vector2>();
        [HideInInspector] public Vector2 MapPosition;
    }
}
