using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    public class FlareGunAttack : WeaponAttack, IVerticalAttack
    {
        public Transform Muzzle;
        public SignalFlare Projectile;
        public float LaunchSpeed = 11;
        public float AimRange = 40;
        private float verticality;

        public void SetVerticality(float value) => verticality = value;

        public override void StartAttack()
        {
            base.StartAttack();
            if (Muzzle && Projectile && Owner)
            {
                var aiming = GetComponentInParent<IShotAttackAiming>();
                Vector3 direction = aiming != null ? aiming.GetDirection(Muzzle.position, AimRange) :
                    Vector3.Slerp(Vector3.ProjectOnPlane(Muzzle.forward, Vector3.up).normalized, Muzzle.forward, Mathf.Abs(verticality));
                Vector3 origin = Muzzle.position;
                // Stop barrels protruding through a wall from spawning a flare behind it.
                Vector3 shoulder = Owner.transform.position + Vector3.up * 1.2f;
                Vector3 path = origin - shoulder;
                var blockers = Physics.RaycastAll(shoulder, path.normalized, path.magnitude, Projectile.CollisionMask, QueryTriggerInteraction.Ignore);
                float distance = path.magnitude;
                foreach (var hit in blockers)
                    if (!hit.transform.IsChildOf(Owner.transform) && hit.distance < distance)
                    { distance = hit.distance; origin = hit.point - path.normalized * .06f; }
                var flare = Instantiate(Projectile, origin, Quaternion.LookRotation(direction));
                flare.Launch(direction * LaunchSpeed, Owner);
                var data = m_WeaponData as ReloadableWeaponData;
                var audio = GetComponent<AudioSource>();
                if (data && data.ShotSound)
                {
                    var shotAudio = GetComponent<WeaponShotAudio>();
                    if (shotAudio) shotAudio.Play(data.ShotSound);
                    else if (audio) audio.PlayOneShot(data.ShotSound);
                }
            }
            // Inventory consumption is performed by PlayerStateAttack.
            StopAttack();
        }
    }
}
