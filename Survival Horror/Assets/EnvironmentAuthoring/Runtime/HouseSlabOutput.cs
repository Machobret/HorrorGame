using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    [AddComponentMenu("")]
    public sealed class HouseSlabOutput : MonoBehaviour
    {
        [Serializable]
        public sealed class Panel
        {
            public string Key, Signature;
            public string BooleanSignature;
            public HousePlan Plan;
            public bool Ceiling;
            public GameObject Root;
            public int MeshPartCount, ModuleCount;
        }

        [HideInInspector]
        public HouseBuilding Owner;
        [HideInInspector]
        public string Signature, Problem;
        [HideInInspector]
        public List<Panel> Panels = new List<Panel>();
    }
}
