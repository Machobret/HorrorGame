using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public abstract class Pickup : MonoBehaviour
    {
        public UnityEvent OnPickup;

        public virtual void Take() 
        { 
            OnPickup?.Invoke();

            var game = GameManager.Instance;
            bool isDocument = this is PickupDocument ||
                (this is PickupItem item && (item.Entry.Item is DocumentItemData ||
                    item.Entry.Item is HorrorGame.Chapter1.CircuitDiagramUnlockItemData));
            AudioClip clip = isDocument ? game.DocumentPickupSound : game.ItemPickupSound;

            if (clip && game.Player && game.Player.TryGetComponent(out AudioSource playerSource))
                playerSource.PlayOneShot(clip);
        }
    }
}