using UnityEngine;

namespace HorrorGame
{
    public sealed class SlidingGate : MonoBehaviour
    {
        [SerializeField] Transform leaf;
        [SerializeField] float travel = 5.4f;
        [SerializeField, Min(.01f)] float speed = 2.5f;
        Vector3 closedPosition;
        bool open;

        void Awake() => closedPosition = leaf.localPosition;

        public void Toggle() => SetOpen(!open);

        public void SetOpen(bool value)
        {
            open = value;
            enabled = true;
        }

        void Update()
        {
            var target = closedPosition + (open ? Vector3.right * travel : Vector3.zero);
            leaf.localPosition = Vector3.MoveTowards(leaf.localPosition, target, speed * Time.deltaTime);
            if (leaf.localPosition == target) enabled = false;
        }
    }
}
