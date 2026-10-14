using UnityEngine;

namespace HorrorEngine
{
    // Optional trigger requirement; ordinary weapons keep their existing input.
    public sealed class AttackCharge : MonoBehaviour
    {
        [Min(0)] public float HoldDuration = .5f;
        public bool ReleasedTooEarly(float elapsed, bool released) => released && elapsed < HoldDuration;
    }
}
