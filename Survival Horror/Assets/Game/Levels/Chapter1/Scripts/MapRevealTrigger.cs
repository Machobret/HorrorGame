using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Trigger volume that reveals authored rooms and refreshes the displayed map when reached.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MapRevealTrigger : MonoBehaviour
    {
        public MapController Map;
        [Tooltip("Rooms to reveal on reaching this point.")]
        public MapRoom[] RoomsToReveal;
        [Tooltip("Indicators enabled when this point is reached, such as the next objective arrow.")]
        public MapNavigationIndicator[] EnableIndicators;
        [Tooltip("Indicators disabled when this point is reached, such as a completed objective arrow.")]
        public MapNavigationIndicator[] DisableIndicators;
        public bool OneShot = true;

        private bool triggered;

        private void Reset() => GetComponent<Collider>().isTrigger = true;
        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !IsPlayerCollider(other)) return;
            triggered = true;
            foreach (MapRoom room in RoomsToReveal) if (room) room.TryMarkAs(MapRoomState.NotVisited);
            foreach (MapNavigationIndicator indicator in EnableIndicators) if (indicator) indicator.SetEnabled(true);
            foreach (MapNavigationIndicator indicator in DisableIndicators) if (indicator) indicator.SetEnabled(false);

            MapController controller = Map ? Map : GetComponentInParent<MapController>();
            UIMap uiMap = UIManager.Get<UIMap>(true);
            if (controller && controller.Data && MapRenderer.Exists && uiMap && uiMap.gameObject.activeInHierarchy && uiMap.CurrentMap == controller.Data)
                MapRenderer.Instance.GenerateAll(controller.Data);

            if (OneShot) enabled = false;
        }

        private static bool IsPlayerCollider(Collider collider)
        {
            if (!GameManager.Exists || !GameManager.Instance.Player) return false;
            Transform player = GameManager.Instance.Player.transform;
            return collider.transform == player || collider.transform.IsChildOf(player) || player.IsChildOf(collider.transform);
        }
    }
}
