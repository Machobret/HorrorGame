using System.Collections;
using System.Globalization;
using HorrorEngine;
using HorrorGame.Environment;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame.Chapter1
{
    [DefaultExecutionOrder(30)]
    public sealed class BrokenCircuitShutter : CircuitShutterTeleport, ISavableObjectStateExtra
    {
        public Pushable RequiredCrate;
        [Min(.05f)] public float RaiseSeconds = 1.15f;
        [Min(.05f)] public float HoldRaisedSeconds = .35f;
        [Tooltip("Minimum and maximum lift as fractions of full travel.")]
        [SerializeField] private Vector2 raisedHeightRange = new Vector2(.75f, .75f);
        public AudioSource SlamSound;
        [Tooltip("Show the demo ending when the jammed final gate is used.")]
        public bool EndsDemo;

        public bool Jammed => jammedLift >= 0f;
        protected override bool PassageAvailable => Jammed;
        protected override float PromptDepth => 2.6f;
        protected override bool KeepPromptAtThreshold => true;

        protected override void UsePassage(IInteractor interactor, TwoSidedDoor door)
        {
            if (!EndsDemo)
            {
                base.UsePassage(interactor, door);
                return;
            }

            var choices = UIManager.Get<UIChoices>();
            if (choices.isActiveAndEnabled) return;

            var note = new DialogData { PauseGame = false };
            note.SetLines(new[]
            {
                new DialogLine
                {
                    LineText = new LocalizableText { Unlocalized = "Thank you for playing the Exaltfall demo!\n\nReturn to the main menu?" }
                }
            });
            var returnToMenu = new UnityEvent();
            returnToMenu.AddListener(() => GameManager.Instance.QuitGame());
            choices.Show(new ChoiceData
            {
                ChoiceDialog = note,
                Choices = new[]
                {
                    new ChoiceEntry { ChoiceText = new LocalizableText { Unlocalized = "Keep Exploring" } },
                    new ChoiceEntry
                    {
                        ChoiceText = new LocalizableText { Unlocalized = "Return to Main Menu" },
                        OnSelected = returnToMenu
                    }
                }
            });
        }

        private BoxCollider leafCollider;
        private BoxCollider passageWall;
        private Collider[] crateColliders;
        private Vector3 closedPosition;
        private Vector3 closedScale;
        private float fullLift;
        private float lift;
        private float caughtCrateLift;
        private float jammedLift = -1f;
        private Vector3? jammedCratePosition;
        private Coroutine cycle;

        // Tablet input controls the shutter even while the map freezes physics and actors.
        private static float CycleDeltaTime => PauseController.Exists && PauseController.Instance.Context is UIMap
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        protected override void Start()
        {
            Transform leaf = PassageLeaf;
            if (!leaf || !leaf.TryGetComponent(out leafCollider))
            {
                Debug.LogError("Broken shutter needs a Leaf with a BoxCollider.", this);
                return;
            }

            closedPosition = leaf.localPosition;
            closedScale = leaf.localScale;
            fullLift = closedScale.y * leafCollider.size.y;

            var wall = new GameObject("Shutter passage blocker", typeof(BoxCollider));
            wall.layer = leaf.gameObject.layer;
            wall.transform.SetParent(leaf.parent, false);
            wall.transform.SetLocalPositionAndRotation(closedPosition, leaf.localRotation);
            wall.transform.localScale = closedScale;
            passageWall = wall.GetComponent<BoxCollider>();
            passageWall.center = leafCollider.center;
            passageWall.size = leafCollider.size;

            crateColliders = RequiredCrate ? RequiredCrate.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
            // The threshold always blocks walking. Only the crate can enter as the leaf clears it.
            foreach (var collider in crateColliders)
                if (!collider.isTrigger) Physics.IgnoreCollision(passageWall, collider);

            base.Start();
        }

        public override void Refresh()
        {
            base.Refresh();
            if (!leafCollider) return;
            if (cycle != null) StopCoroutine(cycle);
            cycle = null;

            if (Jammed)
            {
                PinCrate(jammedCratePosition);
                SetLift(jammedLift, false);
            }
            else if (Powered && isActiveAndEnabled)
                cycle = StartCoroutine(SlamLoop());
            else
                CloseWithoutJamming();
        }

        private void Update()
        {
            if (!leafCollider || Jammed) return;
            if (cycle != null && Powered)
                CatchCrateInTrack();
            else if (!Powered && lift > 0f)
                CloseWithoutJamming();
        }

        private void CatchCrateInTrack()
        {
            if (caughtCrateLift > 0f || !RequiredCrate) return;
            Vector3 offset = RequiredCrate.transform.position - passageWall.bounds.center;
            if (Mathf.Abs(Vector3.Dot(offset, passageWall.transform.forward)) > .12f) return;

            // Hold a crate centered beneath the leaf so a long push cannot strand it past the barrier.
            caughtCrateLift = GetCrateLift();
            if (caughtCrateLift > 0f) PinCrate(RequiredCrate.transform.localPosition);
        }

        private void OnDisable()
        {
            if (cycle != null) StopCoroutine(cycle);
            cycle = null;
        }

        private void OnEnable()
        {
            if (leafCollider) Refresh();
        }

        private IEnumerator SlamLoop()
        {
            float minHeight = fullLift * Mathf.Clamp01(Mathf.Min(raisedHeightRange.x, raisedHeightRange.y));
            float maxHeight = fullLift * Mathf.Clamp01(Mathf.Max(raisedHeightRange.x, raisedHeightRange.y));
            while (true)
            {
                float peak = maxHeight > minHeight ? Random.Range(minHeight, maxHeight) : minHeight;
                float start = lift;
                for (float time = 0f; time < RaiseSeconds; time += CycleDeltaTime)
                {
                    SetLift(Mathf.SmoothStep(start, peak, time / RaiseSeconds), true);
                    yield return null;
                }
                for (float time = 0f; time < HoldRaisedSeconds; time += CycleDeltaTime)
                {
                    float progress = time / HoldRaisedSeconds;
                    float twitch = Mathf.Sin(progress * 4f * Mathf.PI) * (1f - progress);
                    float heldHeight = peak + fullLift * .035f * twitch;
                    SetLift(maxHeight > minHeight ? Mathf.Clamp(heldHeight, minHeight, maxHeight) : heldHeight, true);
                    yield return null;
                }

                float releasedHeight = lift;
                float metersPerUnit = leafCollider.transform.parent.TransformVector(Vector3.up).magnitude;
                float fallAcceleration = Physics.gravity.magnitude / Mathf.Max(.001f, metersPerUnit);
                for (float time = 0f; ; time += CycleDeltaTime)
                {
                    float next = Mathf.Max(0f, releasedHeight - .5f * fallAcceleration * time * time);
                    float contact = caughtCrateLift > 0f ? caughtCrateLift : GetCrateLift();
                    if (contact > 0f && next <= contact)
                    {
                        FinishJam(contact);
                        yield break;
                    }
                    SetLift(next, next <= 0f);
                    if (next <= 0f) break;
                    yield return null;
                }

                if (SlamSound) SlamSound.Play();
                yield return Hold(.2f);
            }
        }

        private static IEnumerator Hold(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += CycleDeltaTime)
                yield return null;
        }

        private void CloseWithoutJamming()
        {
            if (caughtCrateLift > 0f)
            {
                FinishJam(caughtCrateLift);
                return;
            }
            float clearance = GetCrateLift();
            SetLift(clearance, clearance <= 0f);
        }

        private void FinishJam(float contact)
        {
            jammedLift = contact;
            jammedCratePosition = RequiredCrate.transform.localPosition;
            caughtCrateLift = 0f;
            cycle = null;
            Refresh();
            if (SlamSound) SlamSound.Play();
            GetComponent<SavableObjectState>()?.SaveState();
            RequiredCrate.GetComponent<SavableObjectState>()?.SaveState();
        }

        private float GetCrateLift()
        {
            float clearance = 0f;
            Transform wall = passageWall.transform;
            foreach (var collider in crateColliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger ||
                    !Physics.ComputePenetration(passageWall, wall.position, wall.rotation,
                        collider, collider.transform.position, collider.transform.rotation, out _, out _)) continue;

                Vector3 top = collider.bounds.center;
                top.y = collider.bounds.max.y;
                float bottom = passageWall.center.y - passageWall.size.y * .5f;
                clearance = Mathf.Max(clearance, (wall.InverseTransformPoint(top).y - bottom) * closedScale.y);
            }
            return Mathf.Clamp(clearance, 0f, fullLift);
        }

        private void SetLift(float value, bool solid)
        {
            lift = Mathf.Clamp(value, 0f, fullLift);
            leafCollider.transform.localPosition = closedPosition + Vector3.up * lift;
            leafCollider.enabled = solid;
        }

        private void PinCrate(Vector3? localPosition)
        {
            if (!RequiredCrate) return;
            if (RequiredCrate.TryGetComponent(out ResetTransformOnDoorTransition doorReset))
                doorReset.KeepPinnedTransform();
            if (localPosition.HasValue && RequiredCrate.transform.localPosition != localPosition.Value)
            {
                RequiredCrate.transform.localPosition = localPosition.Value;
                if (RequiredCrate.TryGetComponent(out Rigidbody crateBody))
                    crateBody.position = RequiredCrate.transform.position;
            }
            RequiredCrate.enabled = false;
            var rig = RequiredCrate.GetComponentInChildren<CrateClimbRig>();
            if (rig) rig.LockInPlace();
            else if (RequiredCrate.TryGetComponent(out Rigidbody body))
            {
                body.constraints = RigidbodyConstraints.FreezeAll;
                body.isKinematic = true;
            }
        }

        [System.Serializable]
        private struct JamState
        {
            public float Lift;
            public Vector3 CratePosition;
        }

        public string GetSavableData()
        {
            if (jammedCratePosition.HasValue)
                return JsonUtility.ToJson(new JamState { Lift = jammedLift, CratePosition = jammedCratePosition.Value });
            if (caughtCrateLift > 0f && RequiredCrate)
                return JsonUtility.ToJson(new JamState { Lift = caughtCrateLift, CratePosition = RequiredCrate.transform.localPosition });
            return jammedLift.ToString(CultureInfo.InvariantCulture);
        }

        public void SetFromSavedData(string savedData)
        {
            caughtCrateLift = 0f;
            jammedCratePosition = null;
            if (!string.IsNullOrEmpty(savedData) && savedData.StartsWith("{"))
            {
                var state = JsonUtility.FromJson<JamState>(savedData);
                jammedLift = state.Lift;
                jammedCratePosition = state.CratePosition;
            }
            else if (!float.TryParse(savedData, NumberStyles.Float, CultureInfo.InvariantCulture, out jammedLift))
                jammedLift = -1f;
            if (leafCollider) Refresh();
        }
    }
}
