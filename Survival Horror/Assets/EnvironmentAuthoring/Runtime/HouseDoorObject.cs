using UnityEngine;

namespace EnvironmentAuthoring
{
    // Scene clicks select the playable prefab, rather than an ancestor House or one visual child.
    [SelectionBase, DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class HouseDoorObject : MonoBehaviour { }
}
