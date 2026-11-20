using System.Collections;
using HorrorEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HorrorGame.Chapter1
{
    public sealed class CircuitDrivenShutter : CircuitDrivenObject
    {
        public Transform MovingPart;
        public Vector3 ClosedLocalPosition;
        public Vector3 OpenLocalPosition = new Vector3(0, 2.5f, 0);
        public float TravelSeconds = .45f;
        [Tooltip("Colliders that physically block the player while the shutter is lowered. Defaults to colliders on Moving Part.")]
        public Collider[] BlockingColliders;
        [Tooltip("The shutter-side door interaction(s). These are disabled while the shutter is lowered, so there is no E prompt on this side.")]
        [FormerlySerializedAs("BlockedDoorInteractions")]
        public Interactive[] LocalDoorInteractionsToDisable;
        [Tooltip("The linked door on the other side. It stays interactable, but its teleport is blocked while this shutter is lowered.")]
        [FormerlySerializedAs("BlockedDoors")]
        public DoorBase[] OtherSideDoorsToBlock;
        [Tooltip("Optional dialogue displayed when the player tries an Other Side Door while this shutter is lowered.")]
        public DialogData BlockedDialog;
        [Tooltip("Fires only when the player tries an Other Side Door while this shutter is lowered.")]
        public UnityEvent OnBlocked;
        private Coroutine move;
        private bool forceOpen;
        private bool lastOpen;

        public bool IsOpen => Grid && !Grid.HasSupply ? lastOpen : Powered || forceOpen;

        /// <summary>Used by solved puzzle controllers to keep a main shutter open.</summary>
        public void SetForcedOpen(bool value) { forceOpen = value; Refresh(); }

        protected override void Start()
        {
            if (!MovingPart && transform.childCount > 0) MovingPart = transform.GetChild(0);
            // Preserve the authored placement of each shutter instance.  The
            // two serialized positions are used as a travel delta rather than
            // absolute local coordinates, avoiding a snap to ground level.
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

        public override void Refresh()
        {
            if (move != null) StopCoroutine(move);
            move = null;
            if (Grid && !Grid.HasSupply) return;
            bool open = IsOpen;
            lastOpen = open;
            if (BlockingColliders != null)
                foreach (Collider collider in BlockingColliders) if (collider) collider.enabled = !open;
            if (LocalDoorInteractionsToDisable != null)
                foreach (Interactive interactive in LocalDoorInteractionsToDisable) if (interactive) interactive.enabled = open;

            // A door can be toggled while its interaction collider is already
            // inside the player's detector. Enabling/disabling the component
            // does not cause Unity to send another trigger event, so force the
            // prompt to clear or return immediately.
            RefreshInteractionPrompts();

            if (!MovingPart) return;
            move = StartCoroutine(Move(open ? OpenLocalPosition : ClosedLocalPosition));
        }

        private static void RefreshInteractionPrompts()
        {
            InteractionDetector[] detectors = FindObjectsByType<InteractionDetector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (InteractionDetector detector in detectors)
                detector.RefreshFocusedInteractive();
        }

        private IEnumerator Move(Vector3 destination)
        {
            Vector3 start = MovingPart.localPosition;
            for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(.01f, TravelSeconds))
            {
                MovingPart.localPosition = Vector3.Lerp(start, destination, t);
                yield return null;
            }
            MovingPart.localPosition = destination;
        }

        /// <summary>Called by Horror Engine door classes before beginning a teleport transition.</summary>
        public static bool TryBlockDoorUse(DoorBase door)
        {
            if (!door) return false;
            CircuitDrivenShutter[] shutters = FindObjectsByType<CircuitDrivenShutter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (CircuitDrivenShutter shutter in shutters)
            {
                if (shutter.IsOpen || shutter.OtherSideDoorsToBlock == null) continue;
                foreach (DoorBase blockedDoor in shutter.OtherSideDoorsToBlock)
                {
                    if (blockedDoor != door) continue;
                    shutter.OnBlocked?.Invoke();
                    if (shutter.BlockedDialog != null && shutter.BlockedDialog.IsValid()) UIManager.Get<UIDialog>().Show(shutter.BlockedDialog);
                    else door.OnLocked?.Invoke();
                    return true;
                }
            }
            return false;
        }
    }
}
