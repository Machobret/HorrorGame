using Game.Environment;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [RequireComponent(typeof(ElectricalBoxIndicator))]
    public sealed class CircuitDrivenElectricalBox : CircuitDrivenObject
    {
        protected override void Start()
        {
            if (Grid) Grid.OnGridChanged.AddListener(Refresh);
            Refresh();
        }

        protected override void OnDestroy()
        {
            if (Grid) Grid.OnGridChanged.RemoveListener(Refresh);
        }

        public override void Refresh()
        {
            GetComponent<ElectricalBoxIndicator>().SetPowered(Powered);
        }
    }
}
