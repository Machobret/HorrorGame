using UnityEngine;

namespace EnvironmentAuthoring
{
    [AddComponentMenu("")]
    public sealed class TownOutputPart : MonoBehaviour
    {
        [HideInInspector] public string Key;
        [HideInInspector] public Mesh Source;
    }
}
