using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("Procedural Environment/Ground Area")]
    public sealed class GroundArea : ProceduralSurface
    {
        [HideInInspector] public GroundPlan ground = new GroundPlan();
        public Material material;
        internal override GroundPlan Areas { get => ground; set => ground=value; }
        public override Material Pavement => material;
    }
}
