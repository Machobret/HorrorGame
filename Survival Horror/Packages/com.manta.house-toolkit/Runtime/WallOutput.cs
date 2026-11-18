using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("")]
    public sealed class WallOutput : MonoBehaviour
    {
        [HideInInspector] public PanelLayout builtPanelLayout;
        [HideInInspector] public bool recordsTrims;
        [HideInInspector] public List<MeshRenderer> trims = new List<MeshRenderer>();
        public List<MeshRenderer> authored = new List<MeshRenderer>();
        public List<MeshRenderer> surfaces = new List<MeshRenderer>();
        public List<MeshRenderer> panes = new List<MeshRenderer>();
    }
}
