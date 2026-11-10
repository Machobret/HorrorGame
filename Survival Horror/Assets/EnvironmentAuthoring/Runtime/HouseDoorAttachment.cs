using UnityEngine;

namespace EnvironmentAuthoring
{
    // Persistent opening anchor. Rebuilding walls moves this parent, never the prefab's overrides.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class HouseDoorAttachment : MonoBehaviour
    {
        [HideInInspector] public HousePlan Owner;
        [HideInInspector] public string OpeningId;
        [HideInInspector] public WallOpeningInsert Preset;
        [HideInInspector] public GameObject SourcePrefab;
        [HideInInspector] public GameObject Instance;
        // Last inherited finishes distinguish host updates from ordinary prefab overrides.
        [HideInInspector] public Material[] InheritedMaterials = System.Array.Empty<Material>();
    }
}
