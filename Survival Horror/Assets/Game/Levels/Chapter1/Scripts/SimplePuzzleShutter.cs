using System.Collections;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Minimal shutter that can be driven either by an Interactive event or a circuit-map target.</summary>
    public sealed class SimplePuzzleShutter : CircuitDrivenObject
    {
        public Transform MovingPart;
        public Vector3 ClosedLocalPosition;
        public Vector3 OpenLocalPosition = new Vector3(0, 2.6f, 0);
        [Min(.01f)] public float TravelSeconds = .45f;
        public Collider[] BlockingColliders;
        public bool IsOpen { get; private set; }
        private Coroutine move;

        protected override void Start()
        {
            if (!MovingPart && transform.childCount > 0) MovingPart = transform.GetChild(0);
            // The placed shutter's local position is its closed position.  The
            // serialized positions define the travel delta, so moving a prefab
            // in a scene never snaps its panel back to local Y = 0 at runtime.
            if (MovingPart)
            {
                Vector3 openOffset = OpenLocalPosition - ClosedLocalPosition;
                ClosedLocalPosition = MovingPart.localPosition;
                OpenLocalPosition = ClosedLocalPosition + openOffset;
            }
            if ((BlockingColliders == null || BlockingColliders.Length == 0) && MovingPart)
                BlockingColliders = MovingPart.GetComponentsInChildren<Collider>(true);
            base.Start();
        }

        public override void Refresh() => SetOpen(Powered);

        public void SetOpen(bool value)
        {
            IsOpen = value;
            foreach (Collider blocker in BlockingColliders) if (blocker) blocker.enabled = !value;
            if (!MovingPart) return;
            if (move != null) StopCoroutine(move);
            move = StartCoroutine(Move(value ? OpenLocalPosition : ClosedLocalPosition));
        }

        public void Toggle() => SetOpen(!IsOpen);

        private IEnumerator Move(Vector3 destination)
        {
            Vector3 start = MovingPart.localPosition;
            for (float time = 0f; time < 1f; time += Time.deltaTime / TravelSeconds)
            {
                MovingPart.localPosition = Vector3.Lerp(start, destination, time);
                yield return null;
            }
            MovingPart.localPosition = destination;
        }
    }
}
