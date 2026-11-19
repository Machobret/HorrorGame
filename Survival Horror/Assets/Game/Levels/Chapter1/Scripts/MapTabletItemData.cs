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
            var tutorial = Chapter1MapTutorial.Instance;
            MapData map = tutorial && tutorial.CollectTabletWithoutOpening ? tutorial.MapToUnlock : null;
            if (!map && tutorial && tutorial.TutorialCircuit) map = tutorial.TutorialCircuit.GetMapData();
            if (!map && tutorial) map = tutorial.MapToUnlock;
            if (!map && MapController.Exists && GameManager.Exists && GameManager.Instance.Player)
            {
                var current = MapController.GetCurrent();
                if (current) map = current.Data;
            }
            if (!map) map = MapToUnlock;
            if (tutorial) tutorial.UnlockAndStart(map);
            var mapScreen = UIManager.Get<UIMap>(true);
            if (mapScreen && !mapScreen.isActiveAndEnabled) mapScreen.Show();
        }
    }
}
