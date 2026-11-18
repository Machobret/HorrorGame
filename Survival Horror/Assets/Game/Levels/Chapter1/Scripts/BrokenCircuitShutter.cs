using System.Collections;
using System.Globalization;
using HorrorEngine;
using HorrorGame.Environment;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [DefaultExecutionOrder(30)]
    public sealed class BrokenCircuitShutter : CircuitShutterTeleport, ISavableObjectStateExtra
    {
        public Pushable RequiredCrate;
        [Min(.05f)] public float RaiseSeconds = 1.15f;
        [Min(.05f)] public float HoldRaisedSeconds = .35f;
        public AudioSource SlamSound;

        public bool Jammed => jammedLift >= 0f;
        protected override bool PassageAvailable => Jammed;
        protected override float PromptDepth => 2.6f;
        protected override bool KeepPromptAtThreshold => true;

        private BoxCollider leafCollider;
        private BoxCollider passageWall;
        private Collider[] crateColliders;
        private Vector3 closedPosition;
        private Vector3 closedScale;
        private float fullLift;
        private float lift;
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
                PinCrate();
                SetLift(jammedLift, false);
            }
            else if (Powered && isActiveAndEnabled)
                cycle = StartCoroutine(SlamLoop());
            else
                CloseWithoutJamming();
        }

        private void Update()
        {
            if (leafCollider && !Jammed && !Powered && lift > 0f)
                CloseWithoutJamming();
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
            float peak = fullLift * .75f;
            while (true)
            {
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
                    SetLift(peak + fullLift * .035f * twitch, true);
                    yield return null;
                }

                float releasedHeight = lift;
                float metersPerUnit = leafCollider.transform.parent.TransformVector(Vector3.up).magnitude;
                float fallAcceleration = Physics.gravity.magnitude / Mathf.Max(.001f, metersPerUnit);
                for (float time = 0f; ; time += CycleDeltaTime)
                {
                    float next = Mathf.Max(0f, releasedHeight - .5f * fallAcceleration * time * time);
                    float contact = GetCrateLift();
                    if (contact > 0f && next <= contact)
                    {
                        jammedLift = contact;
                        jammedCratePosition = RequiredCrate.transform.localPosition;
                        cycle = null;
                        Refresh();
                        if (SlamSound) SlamSound.Play();
                        GetComponent<SavableObjectState>()?.SaveState();
                        RequiredCrate.GetComponent<SavableObjectState>()?.SaveState();
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
            float clearance = GetCrateLift();
            SetLift(clearance, clearance <= 0f);
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

        private void PinCrate()
        {
            if (!RequiredCrate) return;
            if (RequiredCrate.TryGetComponent(out ResetTransformOnDoorTransition doorReset))
                doorReset.KeepPinnedTransform();
            if (jammedCratePosition.HasValue && RequiredCrate.transform.localPosition != jammedCratePosition.Value)
            {
                RequiredCrate.transform.localPosition = jammedCratePosition.Value;
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

        public string GetSavableData() => jammedCratePosition.HasValue
            ? JsonUtility.ToJson(new JamState { Lift = jammedLift, CratePosition = jammedCratePosition.Value })
            : jammedLift.ToString(CultureInfo.InvariantCulture);

        public void SetFromSavedData(string savedData)
        {
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
