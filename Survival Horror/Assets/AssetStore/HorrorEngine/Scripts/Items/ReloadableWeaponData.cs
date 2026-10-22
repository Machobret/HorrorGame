using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Items/Reloadable Weapon")]
    public class ReloadableWeaponData : WeaponData
    {
        public ItemData AmmoItem;
        public int MaxAmmo;
        public AudioClip ShotSound;
        public AudioClip ReloadSound;
        public float ReloadDuration;
        [Tooltip("Allow walking and turning, but not running, while this weapon reloads.")]
        public bool AllowMovementDuringReload = true;
    }
}
