using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>
    /// Circuit-controlled motorized ladder. The puzzle sequence must complete first;
    /// afterwards the assigned circuit lowers it when powered and raises it when off.
    /// </summary>
    public sealed class CircuitDrivenLadder : CircuitDrivenObject
    {
        [Tooltip("The existing ClimbableLadder transform to move and enable.")]
        public Transform ClimbableLadder;
        [Tooltip("Optional exact raised pose. When omitted, the ladder's initial pose is used.")]
        public Transform RaisedPosition;
        [Tooltip("Exact lowered pose for the climbable ladder.")]
        public Transform LoweredPosition;
        [Min(.05f)] public float MoveSeconds = 1.2f;
        [Tooltip("Usually leave this off: a raised ladder remains visible and its height prevents use. Enable only if a specific ladder must vanish when retracted.")]
        public bool DisableWhenRaised;
        [Tooltip("The climb interactions to gate. Left empty, all Climbable components under the ladder are found automatically.")]
        public Climbable[] Climbables;
        [Tooltip("The matching E-interactions. Left empty, they are found from the Climbable components automatically.")]
        public Interactive[] ClimbInteractions;
        [SerializeField] private bool m_PuzzleComplete;

        private Vector3 m_InitialPosition;
        private Quaternion m_InitialRotation;
        private Coroutine m_MoveRoutine;

        protected override void Start()
        {
            if (ClimbableLadder)
            {
                m_InitialPosition = ClimbableLadder.position;
                m_InitialRotation = ClimbableLadder.rotation;
                if (Climbables == null || Climbables.Length == 0)
                    Climbables = ClimbableLadder.GetComponentsInChildren<Climbable>(true);
                if (ClimbInteractions == null || ClimbInteractions.Length == 0)
                {
                    var interactions = new System.Collections.Generic.List<Interactive>();
                    foreach (Climbable climbable in Climbables)
                    {
                        if (!climbable) continue;
                        Interactive interaction = climbable.GetComponent<Interactive>();
                        if (interaction) interactions.Add(interaction);
                    }
                    ClimbInteractions = interactions.ToArray();
                }
            }
            SetClimbableEnabled(false);
            base.Start();
        }

        public void MarkPuzzleComplete()
        {
            m_PuzzleComplete = true;
            Refresh();
        }

        public void ResetPuzzleCompletion()
        {
            m_PuzzleComplete = false;
            Refresh();
        }

        public override void Refresh()
        {
            SetLowered(m_PuzzleComplete && Powered);
        }

        private void SetLowered(bool lowered)
        {
            if (!ClimbableLadder) return;
            if (m_MoveRoutine != null) StopCoroutine(m_MoveRoutine);
            // Never allow the player to enter the climbing state while the motor
            // is moving or while the ladder is raised.
            SetClimbableEnabled(false);
            // The normal raised state is visible. Reactivate an old ladder that
            // was disabled by the previous alarm-only implementation.
            if (lowered || !DisableWhenRaised) ClimbableLadder.gameObject.SetActive(true);
            m_MoveRoutine = StartCoroutine(MoveRoutine(lowered));
        }

        private IEnumerator MoveRoutine(bool lowered)
        {
            Vector3 fromPosition = ClimbableLadder.position;
            Quaternion fromRotation = ClimbableLadder.rotation;
            Vector3 targetPosition = lowered && LoweredPosition ? LoweredPosition.position : RaisedPosition ? RaisedPosition.position : m_InitialPosition;
            Quaternion targetRotation = lowered && LoweredPosition ? LoweredPosition.rotation : RaisedPosition ? RaisedPosition.rotation : m_InitialRotation;
            float elapsed = 0f;
            while (elapsed < MoveSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / MoveSeconds);
                ClimbableLadder.SetPositionAndRotation(Vector3.Lerp(fromPosition, targetPosition, t), Quaternion.Slerp(fromRotation, targetRotation, t));
                yield return null;
            }
            ClimbableLadder.SetPositionAndRotation(targetPosition, targetRotation);
            // The ladder becomes usable only after it has fully landed.
            SetClimbableEnabled(lowered);
            if (!lowered && DisableWhenRaised) ClimbableLadder.gameObject.SetActive(false);
            m_MoveRoutine = null;
        }

        private void SetClimbableEnabled(bool enabled)
        {
            if (Climbables == null) return;
            foreach (Climbable climbable in Climbables)
                if (climbable) climbable.enabled = enabled;
            if (ClimbInteractions == null) return;
            foreach (Interactive interaction in ClimbInteractions)
                if (interaction) interaction.enabled = enabled;
        }
    }
}
