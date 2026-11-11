using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Keeps a map renderer's center crosshair aligned with the player's authored map position.</summary>
    public static class MapPlayerFocus
    {
        public static bool FocusOnPlayer(MapRenderer renderer, MapData map)
        {
            if (!renderer || !map || !GameManager.Instance || !GameManager.Instance.Player) return false;

            MapController controller = MapController.GetCurrent();
            if (!controller || controller.Data != map)
            {
                MapController[] controllers = Object.FindObjectsByType<MapController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                controller = null;
                foreach (MapController candidate in controllers)
                {
                    if (candidate && candidate.Data == map) { controller = candidate; break; }
                }
            }
            if (!controller) return false;

            if (!controller.TryGetTransformInRoom(GameManager.Instance.Player.transform, out Vector3 playerPosition, out Quaternion playerRotation))
                return false;
            renderer.SetPlayerPositionAndRotation(playerPosition, playerRotation);
            renderer.SetTargetPosition(playerPosition, true);
            return true;
        }
    }
}
