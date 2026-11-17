using UnityEngine;

namespace HouseToolkit
{
    public abstract class ProceduralPath : ProceduralSurface
    {
        [HideInInspector] public RoadGraph paths = new RoadGraph();
        internal override RoadGraph Paths { get => paths; set => paths = value; }
    }
}
