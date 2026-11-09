using UnityEngine;

namespace EnvironmentAuthoring
{
    // Settings only. The editor creates saved mesh placements; players do no rebuilding.
    [SelectionBase, DisallowMultipleComponent, RequireComponent(typeof(HousePlan))]
    public sealed class HouseWallBuilder : MonoBehaviour
    {
        [SerializeField] private WallKit data = new WallKit();
        public WallKit Data => data;

        public HouseWallOutput Output
        {
            get
            {
                foreach (var output in GetComponentsInChildren<HouseWallOutput>(true))
                    if (output.Owner == this) return output;
                return null;
            }
        }

    }
}
