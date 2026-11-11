using HorrorEngine;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame.Chapter1
{
    /// <summary>Scene trigger that reveals one or more blocked-route markers on the map.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MapBlockadeTriggerZone : MonoBehaviour
    {
        public MapNavigationIndicator[] BlockadesToShow;
        public MapNavigationIndicator[] IndicatorsToHide;
        public UnityEvent OnBlockadesShown;
        public bool OneShot = true;

        private bool triggered;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !IsPlayerCollider(other)) return;
            foreach (MapNavigationIndicator blockade in BlockadesToShow) if (blockade) blockade.SetEnabled(true);
            foreach (MapNavigationIndicator indicator in IndicatorsToHide) if (indicator) indicator.SetEnabled(false);
            OnBlockadesShown?.Invoke();
            triggered = OneShot;
        }

        private static bool IsPlayerCollider(Collider collider)
        {
            if (!GameManager.Exists || !GameManager.Instance.Player) return false;
            Transform player = GameManager.Instance.Player.transform;
            return collider.transform == player || collider.transform.IsChildOf(player) || player.IsChildOf(collider.transform);
        }
    }
}
