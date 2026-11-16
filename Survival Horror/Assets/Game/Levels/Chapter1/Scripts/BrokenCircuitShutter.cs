using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Standalone broken shutter that repeatedly slams until a pushed crate jams it.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BrokenCircuitShutter : MonoBehaviour
    {
        public Transform MovingPart;
        public Vector3 RaisedLocalPosition = new Vector3(0, 2.5f, 0);
        public Vector3 SlammedLocalPosition;
        public Collider[] HazardWallColliders;
        [Tooltip("Only this crate can jam the shutter. Leave empty to accept any Pushable.")]
        public Pushable RequiredCrate;
        [Min(.05f)] public float RaiseSeconds = .45f;
        [Min(.05f)] public float SlamSeconds = .12f;
        [Min(.05f)] public float HoldRaisedSeconds = .6f;
        public AudioSource SlamSound;
        public bool StartsActive = true;
        public bool Jammed { get; private set; }
        private Coroutine routine;
        private Pushable crateInGap;

        private void Start()
        {
            GetComponent<Collider>().isTrigger = true;
            if (StartsActive) Activate();
        }
        public void Activate()
        {
            if (routine != null) StopCoroutine(routine);
            if (Jammed)
            {
                SetWall(false);
                if (MovingPart) MovingPart.localPosition = SlammedLocalPosition;
                return;
            }
            routine = StartCoroutine(SlamLoop());
        }
        public void Deactivate()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            SetWall(true);
            if (MovingPart) MovingPart.localPosition = SlammedLocalPosition;
        }
        private IEnumerator SlamLoop()
        {
            while (!Jammed)
            {
                yield return Move(RaisedLocalPosition, RaiseSeconds);
                yield return new WaitForSeconds(HoldRaisedSeconds);
                bool jamNow = crateInGap && (!RequiredCrate || crateInGap == RequiredCrate);
                yield return Move(SlammedLocalPosition, SlamSeconds);
                if (SlamSound) SlamSound.Play();
                if (jamNow) { Jammed = true; SetWall(false); yield break; }
                SetWall(true);
                yield return new WaitForSeconds(.2f);
            }
        }
        private IEnumerator Move(Vector3 target, float seconds)
        {
            if (!MovingPart) yield break;
            Vector3 start = MovingPart.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds) { MovingPart.localPosition = Vector3.Lerp(start, target, t); yield return null; }
            MovingPart.localPosition = target;
        }
        private void SetWall(bool value) { foreach (Collider wall in HazardWallColliders) if (wall) wall.enabled = value; }
        private void OnTriggerEnter(Collider other) { Pushable pushable = other.GetComponentInParent<Pushable>(); if (pushable && (!RequiredCrate || pushable == RequiredCrate)) crateInGap = pushable; }
        private void OnTriggerExit(Collider other) { if (other.GetComponentInParent<Pushable>() == crateInGap) crateInGap = null; }
    }
}
