using UnityEngine;

namespace EnvironmentAuthoring
{
    [AddComponentMenu("")]
    public sealed class TownOutput : MonoBehaviour
    {
        [HideInInspector] public TownAuthoring Owner;
        [HideInInspector] public string Signature, Problem;
    }
}
