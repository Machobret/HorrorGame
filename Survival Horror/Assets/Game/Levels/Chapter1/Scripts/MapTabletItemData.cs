using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [CreateAssetMenu(menuName = "Horror Game/Chapter 1/Map Tablet", fileName = "MapTablet")]
    public sealed class MapTabletItemData : ItemData
    {
        public MapData MapToUnlock;

        public bool TryCollectWithoutOpening()
        {
            var tutorial = Chapter1MapTutorial.Instance;
            if (!tutorial || !tutorial.CollectTabletWithoutOpening) return false;
            tutorial.UnlockAndStart(tutorial.MapToUnlock ? tutorial.MapToUnlock : MapToUnlock);
            Chapter1Opening.Instance?.OnTabletCollected();
            return true;
        }

        public override void OnUse(InventoryEntry entry, Actor user)
        {
            base.OnUse(entry, user);
            var tutorial = Chapter1MapTutorial.Instance;
            MapData map = tutorial && tutorial.TutorialCircuit ? tutorial.TutorialCircuit.GetMapData() : null;
            if (!map && tutorial) map = tutorial.MapToUnlock;
            if (!map && MapController.Exists && GameManager.Exists && GameManager.Instance.Player)
            {
                var current = MapController.GetCurrent();
                if (current) map = current.Data;
            }
            if (!map) map = MapToUnlock;
            if (tutorial) tutorial.UnlockAndStart(map);
            UIManager.Get<UIMap>(true)?.Show(map);
        }
    }
}
