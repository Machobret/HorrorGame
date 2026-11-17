using UnityEngine;

namespace HouseToolkit
{
    public abstract class ProceduralPath : ProceduralSurface
    {
        public virtual bool Elevated => false;
        [HideInInspector] public RoadGraph paths = new RoadGraph();
        internal override RoadGraph Paths { get => paths; set => paths = value; }
    }
}
