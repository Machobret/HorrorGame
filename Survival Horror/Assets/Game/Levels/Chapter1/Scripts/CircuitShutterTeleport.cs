using HorrorEngine;
using HorrorGame.UI;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Powered garage-door passage with optional feedback while locked.</summary>
    public class CircuitShutterTeleport : CircuitDrivenObject
    {
        [Min(.5f)] public float ExitClearance = 1.25f;
        [Tooltip("Optional arrival transform for side A. Its forward arrow is the arrival direction.")]
        public Transform ExitPoint;
        [Tooltip("Optional arrival transform for side B. Its forward arrow is the arrival direction.")]
        public Transform OtherExitPoint;
        public DoorAnimation Animation;
        public InteractionData DoorInteraction;
        public bool RequiresPuzzleUnlock;
        [SerializeField] private InteractionData lockedInteraction;
        [SerializeField] private string lockedHint;
        private TwoSidedDoor passage;
        private Interactive interaction;
        private bool puzzleUnlocked;
        protected virtual bool PassageAvailable => puzzleUnlocked || (!RequiresPuzzleUnlock && Powered);

        public void SetPuzzleUnlocked(bool unlocked)
        {
            puzzleUnlocked = unlocked;
            Refresh();
        }
        protected virtual float PromptDepth => .8f;
        protected virtual Transform PassageLeaf => FindLeaf(transform);
        protected virtual bool KeepPromptAtThreshold => false;

        protected override void Start()
        {
            passage = gameObject.AddComponent<TwoSidedDoor>();
            passage.ExitPoint = ExitPoint ? ExitPoint : CreateExit("Exit Point A", 1f);
            passage.OtherExitPoint = OtherExitPoint ? OtherExitPoint : CreateExit("Exit Point B", -1f);
            passage.Animation = Animation;
            Transform leaf = PassageLeaf;
            int layer = LayerMask.NameToLayer("InteractionSystem");
            if (leaf && layer >= 0 && leaf.TryGetComponent(out BoxCollider blocker))
            {
                var prompt = new GameObject("Shutter Door Interaction");
                prompt.SetActive(false);
                prompt.layer = layer;
                prompt.transform.SetParent(KeepPromptAtThreshold ? leaf.parent : leaf, false);
                if (KeepPromptAtThreshold)
                {
                    prompt.transform.SetLocalPositionAndRotation(leaf.localPosition, leaf.localRotation);
                    prompt.transform.localScale = leaf.localScale;
                }
                var promptCollider = prompt.AddComponent<BoxCollider>();
                promptCollider.isTrigger = true;
                promptCollider.center = blocker.center;
                Vector3 size = blocker.size;
                size.z += PromptDepth / Mathf.Max(.01f, Mathf.Abs(leaf.lossyScale.z));
                promptCollider.size = size;
                interaction = prompt.AddComponent<Interactive>();
                interaction.Data = DoorInteraction;
                interaction.OnInteract = new OnInteractionEvent();
                interaction.OnDataChanged = new UnityEngine.Events.UnityEvent();
                interaction.OnInteract.AddListener(Traverse);
            }
            base.Start();
        }

        public override void Refresh()
        {
            if (!interaction) return;

            bool available = PassageAvailable;
            var data = available ? DoorInteraction : lockedInteraction;
            if (interaction.Data != data) interaction.SetInteractionData(data);
            interaction.gameObject.SetActive(available || lockedInteraction);
        }

        private void Traverse(IInteractor interactor)
        {
            if (PassageAvailable)
            {
                if (passage) UsePassage(interactor, passage);
            }
            else if (lockedInteraction)
            {
                GameplayNotifications.ShowControls(lockedHint);
            }
        }

        protected virtual void UsePassage(IInteractor interactor, TwoSidedDoor door)
        {
            door.Use(interactor);
        }

        private Transform CreateExit(string exitName, float side)
        {
            var exit = new GameObject(exitName).transform;
            exit.SetParent(transform, false);
            exit.SetPositionAndRotation(DefaultExitPosition(side), Quaternion.LookRotation(transform.forward * side));
            return exit;
        }

        private Vector3 DefaultExitPosition(float side) => transform.position + transform.forward * (ExitClearance * side);

        private void OnDrawGizmosSelected()
        {
            DrawExit(ExitPoint, 1f);
            DrawExit(OtherExitPoint, -1f);
        }

        private void DrawExit(Transform exit, float side)
        {
            Vector3 position = exit ? exit.position : DefaultExitPosition(side);
            Gizmos.DrawWireCube(position, new Vector3(.5f, 0f, .5f));
            GizmoUtils.DrawArrow(position, exit ? exit.forward : transform.forward * side, 1f);
        }

        private static Transform FindLeaf(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Leaf") return child;
            return null;
        }
    }
}
