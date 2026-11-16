using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/Map Tablet", fileName = "MapTablet")]
    public sealed class MapTabletItemData : ItemData
    {
        public MapData MapToUnlock;

        public override void OnUse(InventoryEntry entry, Actor user)
        {
            base.OnUse(entry, user);
            if (Chapter1MapTutorial.Instance)
                Chapter1MapTutorial.Instance.UnlockAndStart(MapToUnlock);

            // Open the overlay belonging to the tablet's selected map.  The
            // fuse-box tablet can point at a different map (District 7), so it
            // must not decide which map a picked-up inventory tablet opens.
            DistrictPowerTablet tablet = FindFirstObjectByType<DistrictPowerTablet>();
            PowerMapCircuitPanel panel = null;
            foreach (PowerMapCircuitPanel candidate in FindObjectsByType<PowerMapCircuitPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate && candidate.MapData == MapToUnlock)
                {
                    panel = candidate;
                    break;
                }
            }

            if (panel && tablet && tablet.Grid)
                panel.Open(tablet.Grid);
            else
                UIManager.Get<UIMap>(true)?.Show(MapToUnlock);
        }
    }
}
