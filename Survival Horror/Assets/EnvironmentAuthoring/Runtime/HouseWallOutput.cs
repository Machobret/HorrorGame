using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Ownership prevents regeneration from touching the user's other child objects.
    [AddComponentMenu("")]
    public sealed class HouseWallOutput : MonoBehaviour
    {
        [Serializable]
        public sealed class Group
        {
            public string Key;
            public string Signature;
            public string BooleanSignature;
            public GameObject Root;
            public int MeshPartCount, ModuleCount;
        }

        [HideInInspector] public HouseWallBuilder Owner;
        // Saved ownership and inputs let unrelated walls survive edits and scene reloads.
        [HideInInspector] public List<Group> Groups = new List<Group>();
        [HideInInspector] public string InputSignature;
        [HideInInspector] public string Problem;
        [HideInInspector] public Vector2[] ProblemPoints = new Vector2[0];

        // Derived picking data for the currently applied output, never saved or Undo state.
        [NonSerialized] internal HouseWallLayout.Result PlacementLayout;
        [NonSerialized] internal string PlacementInputs;

    }
}
