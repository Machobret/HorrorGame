using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // A sibling of visual output: hiding a renderer must not switch off physics.
    [AddComponentMenu("")]
    public sealed class HouseCollisionOutput : MonoBehaviour
    {
        [Serializable]
        public sealed class Group
        {
            public string Key, Signature;
            public GameObject Root;
            public int ColliderCount;
        }

        [HideInInspector] public HousePlan Owner;
        [HideInInspector] public string InputSignature, Problem;
        [HideInInspector] public List<Group> Groups = new List<Group>();
    }
}
