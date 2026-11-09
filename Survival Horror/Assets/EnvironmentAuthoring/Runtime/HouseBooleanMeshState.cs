using UnityEngine;

namespace EnvironmentAuthoring
{
    // One uncut source and current derived result, never a mesh history.
    // Keeping the source also preserves existing paint when a shape moves or is removed.
    [AddComponentMenu("")]
    public sealed class HouseBooleanMeshState : MonoBehaviour
    {
        [HideInInspector] public Mesh Source, Result, CollisionSource, CollisionResult;
        [HideInInspector] public Material[] SourceMaterials;
        [HideInInspector] public string Problem;
    }
}
