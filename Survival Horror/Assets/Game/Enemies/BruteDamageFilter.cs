using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Enemies
{
    [CreateAssetMenu(menuName = "Horror Game/Brute Damage Filter")]
    public sealed class BruteDamageFilter : AttackFilter
    {
        public override bool Passes(AttackInfo info)
        {
            if (info.Attack is BruteAcidPool acid) return acid.DamageEnabled;
            if (!info.Attack || !info.Attack.Owner) return false;
            var settings = info.Attack.Owner.GetComponent<BruteCombatController>();
            return settings && settings.EnableDamage;
        }
    }
}
