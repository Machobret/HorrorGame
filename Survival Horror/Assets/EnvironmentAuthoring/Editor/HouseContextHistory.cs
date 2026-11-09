using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Only the active authoring target belongs in context Undo. Scene data, pending
    // gestures and temporary renderer flags keep their existing owners and lifetimes.
    [InitializeOnLoad]
    internal sealed class HouseContextHistory : ScriptableSingleton<HouseContextHistory>
    {
        [SerializeField] private HousePlan anchor, selectedPlan;
        [SerializeField] private bool initialized;
        private static bool applying;

        static HouseContextHistory() { Undo.undoRedoPerformed += Restore; }

        internal static void Change(HousePlan target)
        {
            if (applying) return;
            var state = instance;
            state.initialized = true;
            var active = HouseEditingContext.Active;
            string action = target == null ? "Leave House editor" : active == null ? "Enter House editor" : "Change House editing target";
            int group = -1;
            // Storey navigation stays within one House, without an extra context
            // Undo step. Explicit same-target entry is handled before this method.
            if (target != null && active?.Root == Root(target))
                state.selectedPlan = target; // Changing storeys is not leaving/entering a House.
            else group = Record(target, action);
            applying = true;
            try
            {
                if (target != null) HouseEditingContext.EnterWithoutHistory(target);
                else active?.ExitWithoutHistory();
            }
            finally { applying = false; }
            if (group >= 0)
            {
                // Unity records object selection separately. Include entry's selection in
                // this transition instead of leaving a surprise Select step above it.
                Undo.SetCurrentGroupName(action);
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
            }
        }

        private static int Record(HousePlan target, string action)
        {
            var state = instance;
            if (state.anchor == target) return -1;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(state, action);
            state.anchor = EntryAnchor(target);
            state.selectedPlan = target;
            EditorUtility.SetDirty(state);
            Undo.FlushUndoRecordObjects();
            return group;
        }

        internal static void ExternalExit()
        {
            if (applying) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) instance.anchor = instance.selectedPlan = null;
            else
            {
                int group = Record(null, "Leave House editor");
                if (group >= 0)
                {
                    Undo.CollapseUndoOperations(group);
                    Undo.IncrementCurrentGroup();
                }
            }
        }

        internal static bool OwnsSelection(GameObject root) => applying || Root(instance.anchor) == root;

        internal static void ResumeTarget(HousePlan target)
        {
            // A context retained across script reload can predate this history object.
            // Synchronize its baseline without pretending reload was a user transition.
            if (applying) return;
            if (!instance.initialized)
            {
                instance.initialized = true;
                instance.anchor = EntryAnchor(target);
                instance.selectedPlan = target;
            }
            else if (instance.anchor != null && Root(instance.anchor) == Root(target))
                instance.selectedPlan = target;
        }

        private static GameObject Root(HousePlan target) => target == null ? null
            : HouseBuilding.For(target)?.gameObject ?? target.gameObject;

        private static HousePlan EntryAnchor(HousePlan target) => target == null ? null
            : HouseBuilding.For(target)?.Data.Anchor?.Plan ?? target;

        private static void Restore()
        {
            if (applying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Keep a stable entry anchor when a newly selected level is undone/destroyed.
            // Leaving still remembers the selected level when that level remains available.
            var target = instance.selectedPlan != null && Root(instance.selectedPlan) == Root(instance.anchor)
                ? instance.selectedPlan : instance.anchor;
            var active = HouseEditingContext.Active;
            if (target != null && active?.Root == Root(target) || target == null && active == null) return;
            applying = true;
            try
            {
                if (target != null) HouseEditingContext.EnterWithoutHistory(target, false);
                else active?.ExitWithoutHistory();
            }
            finally { applying = false; }
        }
    }

}
