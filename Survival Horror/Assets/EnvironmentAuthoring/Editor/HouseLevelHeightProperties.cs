using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    // Field events edit retained session values. Only Apply publishes authored data.
    internal sealed class HouseLevelHeightProperties
    {
        internal readonly VisualElement Root = new VisualElement { name = "house-level-height" };
        private readonly FloatField height = new FloatField("Wall height (m)") { name = "house-level-height-value" };
        private readonly Label summary = new Label { name = "house-level-height-summary" };
        private readonly Label error = new Label { name = "house-level-height-error" };
        private readonly VisualElement actions = new VisualElement();
        private readonly Button apply;
        private readonly System.Action draftChanged;
        private HousePendingEdit Pending => HouseEditorSession.instance.Pending;
        internal bool HasDraft => Pending.ActiveKind == HousePendingEdit.Kind.LevelHeight && Pending.IsPending;

        internal HouseLevelHeightProperties(System.Action draftChanged = null)
        {
            this.draftChanged = draftChanged;
            Root.Add(height);
            HousePropertyFields.Align(height);
            height.tooltip = "Default floor surface to ceiling underside. Higher levels move together; basements expand downward with Level 1 fixed. Apply commits one Undo action.";
            summary.style.whiteSpace = error.style.whiteSpace = WhiteSpace.Normal;
            summary.style.fontSize = 11;
            error.style.color = new Color(1, .65f, .3f);
            Root.Add(summary); Root.Add(error);
            actions.style.flexDirection = FlexDirection.Row;
            apply = new Button(Apply) { text = "Apply", name = "house-level-height-apply" };
            var cancel = new Button(Discard) { text = "Cancel", name = "house-level-height-cancel" };
            apply.style.flexGrow = cancel.style.flexGrow = 1;
            actions.Add(apply); actions.Add(cancel); Root.Add(actions);
            height.RegisterValueChangedCallback(e =>
            {
                if (!Pending.SetHeight(HouseEditingContext.Active?.Session?.SessionPlan, e.newValue))
                { Refresh(); return; }
                if (!Pending.IsPending && Pending.ActiveKind == HousePendingEdit.Kind.LevelHeight) Pending.Discard();
                HousePropertyFields.Message(error, Pending.Error);
                Refresh();
            });
            HouseSceneUI.Watch(Root, Refresh);
        }

        internal void Discard()
        {
            if (HasDraft) Pending.Discard();
            HousePropertyFields.Message(error, "");
            Refresh();
        }

        internal void Refresh()
        {
            var draft = Pending;
            var plan = HouseEditingContext.Active?.Session?.SessionPlan;
            bool pending = HasDraft;
            bool editable = plan != null && plan.HasSupportedTransform && !HouseRebuildCoordinator.NeedsSceneInstance(plan)
                && (draft.ActiveKind == HousePendingEdit.Kind.None
                    || draft.ActiveKind == HousePendingEdit.Kind.LevelHeight && draft.Plan == plan && !draft.Suspended);
            height.SetValueWithoutNotify(pending ? draft.HeightValue : HousePendingEdit.CurrentHeight(plan));
            height.SetEnabled(editable);
            actions.style.display = pending ? DisplayStyle.Flex : DisplayStyle.None;
            apply.SetEnabled(pending && !draft.Conflicted);
            HousePropertyFields.Message(error, pending ? draft.Error : "");
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            height.label = level != null && building.Levels.IndexOf(level) < building.Levels.IndexOf(building.Data.Anchor) ? "Depth (m)" : "Wall height (m)";
            float delta = pending ? draft.HeightValue - draft.BaselineHeight : 0;
            HousePropertyFields.Message(summary, !pending ? "" : level != null && building.Levels.IndexOf(level) < building.Levels.IndexOf(building.Data.Anchor)
                ? "Moves this floor and deeper levels " + (-delta).ToString("+0.##;-0.##;0") + " m; ceiling stays fixed."
                : "Moves this ceiling and higher levels " + delta.ToString("+0.##;-0.##;0") + " m.");
            draftChanged?.Invoke();
        }

        internal void Apply()
        {
            if (!HasDraft) return;
            var result = HouseEditorSession.instance.ApplyPending();
            HousePropertyFields.Message(error, result.Message);
            Refresh();
        }
    }
}
