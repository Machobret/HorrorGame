using System;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // A browsing collection, not a wall-compatibility rule. Attachments retain
    // their chosen mesh, so reorganizing a family never changes placed trims.
    [CreateAssetMenu(menuName = "Environment/Trim Family")]
    public sealed class TrimFamily : ScriptableObject
    {
        public Mesh[] Meshes = Array.Empty<Mesh>();
    }
}
