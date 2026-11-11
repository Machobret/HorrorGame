using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Items
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoomSparkleTrigger : MonoBehaviour
    {
        [Min(0)] public float EntryDelay = 2f;
        [Tooltip("Assign the sparkle children of the items in this room.")]
        public ItemGoldenSparkle[] Targets = new ItemGoldenSparkle[0];
        [Tooltip("Keep hints unlocked after the first completed visit, for this play session.")]
        public bool KeepVisibleAfterLeaving;
        readonly HashSet<Collider> occupants = new HashSet<Collider>();
        float enteredAt;
        bool revealed;

        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponentInParent<PlayerActor>()) return;
            if (occupants.Add(other) && occupants.Count == 1) enteredAt = Time.time;
        }
        void OnTriggerExit(Collider other)
        {
            if (!occupants.Remove(other) || occupants.Count != 0) return;
            if (!KeepVisibleAfterLeaving) SetVisible(false);
        }
        void Update()
        {
            // Disabled/destroyed player colliders may not produce OnTriggerExit.
            occupants.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
            if (occupants.Count == 0)
            {
                if (!KeepVisibleAfterLeaving && revealed) SetVisible(false);
                return;
            }
            if (!revealed && Time.time - enteredAt >= EntryDelay) SetVisible(true);
        }
        void SetVisible(bool value)
        {
            revealed = value;
            foreach (var target in Targets) if (target) target.SetRoomVisible(value);
        }
        void OnDisable() { occupants.Clear(); SetVisible(false); }
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, .75f, .2f, .65f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
