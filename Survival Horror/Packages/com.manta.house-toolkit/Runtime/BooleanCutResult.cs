using UnityEngine;
namespace HouseToolkit
{
    // The parent holds the original geometry. This child holds the serialized, playable cut.
    [AddComponentMenu("")]
    public sealed class BooleanCutResult : MonoBehaviour
    {
        [HideInInspector] public Collider[] originalColliders;
        [System.NonSerialized] public Mesh lastSource;
        [System.NonSerialized] public Hash128 signature;
    }
}
