using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Derived output metadata only. No authoring state or runtime generation behavior.
    [AddComponentMenu("")]
    public sealed class GeneratedMeshOwnership : MonoBehaviour
    {
        [HideInInspector] public List<Mesh> Meshes = new List<Mesh>();
    }
}
