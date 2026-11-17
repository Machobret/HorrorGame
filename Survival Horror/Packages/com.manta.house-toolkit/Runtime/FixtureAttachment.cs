using UnityEngine;

namespace HouseToolkit
{
    [SelectionBase, DisallowMultipleComponent]
    public sealed class FixtureAttachment : MonoBehaviour
    {
        [HideInInspector] public FixtureMount mount;
        [HideInInspector] public int wallSide = 1;
        [HideInInspector] public float surfaceOffset;

        // The local transform stores the editable coordinates along the surface.
        internal Vector3 MountedPosition(Vector3 local)
        {
            if (mount == FixtureMount.Wall && transform.parent.TryGetComponent<HouseWall>(out var wall))
            {
                local.x = Mathf.Clamp(local.x, 0, wall.length);
                local.y = Mathf.Clamp(local.y, 0, wall.Floor.height);
                local.z = wallSide * wall.Thickness * .5f + surfaceOffset;
            }
            else if (transform.parent.TryGetComponent<HouseFloor>(out var floor))
                local.y = mount == FixtureMount.Ceiling ? floor.height : 0;
            return local;
        }
    }
}
