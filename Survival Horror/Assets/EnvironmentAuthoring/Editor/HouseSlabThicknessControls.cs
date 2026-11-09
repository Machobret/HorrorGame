using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    // All views edit the session's one candidate. Controls own only their pointer capture.
    internal sealed class HouseSlabThicknessControls
    {
        internal readonly VisualElement Root = new VisualElement { name = "house-slab-thickness" };
        private readonly FloatField total = new FloatField("Slab thickness (m)") { name = "house-slab-total" };
        private readonly FloatField finish = new FloatField("Floor finish (m)") { name = "house-slab-finish" };
        private readonly Label error = new Label();
        private readonly VisualElement actions = new VisualElement();
        private readonly Button review;
        private readonly Func<HouseBuilding> target;
        private readonly Action draftChanged;
        private HouseBuilding building;
        private bool dragging, releasePending;
        private FloatField dragField;
        private float startValue, startX;
        private int pointerId;
        private HousePendingEdit Pending => HouseEditorSession.instance.Pending;
        private HousePlan TargetPlan => HouseBuilding.For(HouseEditingContext.Active?.Session?.SessionPlan) == building
            ? HouseEditingContext.Active.Session.SessionPlan : building?.Levels.Find(l => l.Plan != null)?.Plan;
        private bool OwnsOperation => Pending.ActiveKind == HousePendingEdit.Kind.SlabThickness
            && HouseBuilding.For(Pending.Plan) == building;
        internal bool HasDraft => Pending.IsPending && OwnsOperation;

        internal HouseSlabThicknessControls(Func<HouseBuilding> target, Action draftChanged = null)
        {
            this.target = target;
            this.draftChanged = draftChanged;
            Root.Add(new Label("House-wide: affects every level and room.") { style = { whiteSpace = WhiteSpace.Normal, fontSize = 11 } });
            Root.Add(total); Root.Add(finish);
            total.tooltip = "Centred total slab, including the floor finish. Drag the label to preview; release commits once.";
            finish.tooltip = "Upper finish inside the same slab. Keep at least 0.01 m each for finish and supporting structure.";
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.color = new Color(1, .65f, .3f);
            Root.Add(error);
            actions.style.flexDirection = FlexDirection.Row;
            review = new Button(() => { if (OwnsOperation && !Pending.Conflicted) Pending.Resume(Pending.Plan); Refresh(); })
                { text = "Review", name = "house-slab-review", tooltip = "Resume the retained values against their original unchanged House." };
            actions.Add(review);
            actions.Add(new Button(() => Apply()) { text = "Apply", name = "house-slab-apply" });
            actions.Add(new Button(Cancel) { text = "Cancel", name = "house-slab-cancel" });
            Root.Add(actions);
            Wire(total); Wire(finish);
            Root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                Cancel(); e.StopPropagation();
            }, TrickleDown.TrickleDown);
            Root.RegisterCallback<AttachToPanelEvent>(_ => Pending.Changed += PendingChanged);
            Root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Pending.Changed -= PendingChanged;
                Suspend();
            });
            HouseSceneUI.Watch(Root, Refresh);
            Refresh();
        }

        private void Wire(FloatField field)
        {
            HousePropertyFields.Align(field);
            field.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || building == null || e.target != field.labelElement) return;
                if (!CanEdit()) { e.StopImmediatePropagation(); return; }
                // Capturing baseline values creates no authored history. The retained owner
                // acquires the operation before Unity's built-in label dragger can run.
                Pending.SetSlab(TargetPlan, total.value, finish.value);
                if (!OwnsOperation) { Refresh(); e.StopImmediatePropagation(); return; }
                dragging = true; releasePending = false; pointerId = e.pointerId;
                dragField = field; startValue = field.value; startX = e.position.x;
                field.Focus(); field.CapturePointer(pointerId);
                draftChanged?.Invoke();
                e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging || dragField != field || e.pointerId != pointerId) return;
                float speed = e.shiftKey ? .02f : e.altKey ? .001f : .005f;
                float next = Mathf.Round((startValue + (e.position.x - startX) * speed) * 1000) / 1000;
                field.SetValueWithoutNotify(next);
                Pending.SetSlab(TargetPlan, total.value, finish.value);
                HousePropertyFields.Message(error, Pending.Error);
                draftChanged?.Invoke();
                e.StopImmediatePropagation();
            });
            field.RegisterValueChangedCallback(_ =>
            {
                if (dragging) return;
                Pending.SetSlab(TargetPlan, total.value, finish.value);
                if (OwnsOperation && !Pending.IsPending) Pending.Discard();
                Refresh();
            });
            field.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging || e.pointerId != pointerId || e.button != 0) return;
                releasePending = true;
                field.ReleasePointer(pointerId);
                dragging = false; releasePending = false;
                // Clicking an existing typed draft must not quietly Apply it. Only
                // a changed drag publishes on release; a clean reservation is released.
                if (!field.value.Equals(startValue)) Apply();
                else
                {
                    if (OwnsOperation && !Pending.IsPending) Pending.Discard();
                    Refresh();
                }
                e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (dragging && !releasePending) Suspend();
            });
        }

        private void PendingChanged()
        {
            // An interruption can originate in another view or the context. Never
            // leave this adapter producing pointer values after its candidate is released.
            if (dragging && (!OwnsOperation || Pending.Suspended || Pending.Conflicted)) ReleaseCapture();
            Refresh();
        }

        private bool CanEdit() => building != null && !EditorUtility.IsPersistent(building)
            && !EditorApplication.isPlayingOrWillChangePlaymode
            && (Pending.ActiveKind == HousePendingEdit.Kind.None || OwnsOperation && !Pending.Suspended && !Pending.Conflicted);

        internal void Refresh()
        {
            var next = target();
            if (next != building)
            {
                if (dragging) Suspend();
                building = next;
            }
            Root.style.display = building == null ? DisplayStyle.None : DisplayStyle.Flex;
            total.SetEnabled(CanEdit()); finish.SetEnabled(CanEdit());
            actions.style.display = HasDraft && !dragging ? DisplayStyle.Flex : DisplayStyle.None;
            review.style.display = HasDraft && Pending.Suspended ? DisplayStyle.Flex : DisplayStyle.None;
            review.SetEnabled(OwnsOperation && !Pending.Conflicted);
            if (!dragging) ResetFields();
            HousePropertyFields.Message(error, HasDraft ? Pending.Error : "");
            draftChanged?.Invoke();
        }

        private void ResetFields()
        {
            if (building == null) return;
            total.SetValueWithoutNotify(HasDraft ? Pending.Total : building.SlabThickness);
            finish.SetValueWithoutNotify(HasDraft ? Pending.Finish : building.FloorThickness);
        }

        internal bool Apply()
        {
            if (!HasDraft) { if (OwnsOperation) Pending.Discard(); Refresh(); return true; }
            var result = HouseEditorSession.instance.ApplyPending();
            HousePropertyFields.Message(error, result.Message);
            Refresh();
            return result.Status != HouseEditStatus.Rejected;
        }

        internal void Cancel()
        {
            ReleaseCapture();
            if (OwnsOperation) Pending.Discard();
            Refresh();
        }

        internal void Suspend()
        {
            bool captured = dragging;
            ReleaseCapture();
            if (captured && OwnsOperation) Pending.Interrupt();
            Refresh();
        }

        private void ReleaseCapture()
        {
            dragging = false; releasePending = false;
            if (dragField != null && dragField.HasPointerCapture(pointerId)) dragField.ReleasePointer(pointerId);
            dragField = null;
        }
    }
}
