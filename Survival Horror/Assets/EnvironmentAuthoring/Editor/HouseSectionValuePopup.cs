using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // A view of one retained operation. Closing the popup releases its preview;
    // only an explicit Cancel discards entered values.
    internal sealed class HouseSectionValuePopup : PopupWindowContent
    {
        private readonly HousePlan plan;
        private readonly string title, hint;
        private HousePendingEdit Pending => HouseEditorSession.instance.Pending;
        internal HouseSectionValuePopup(HousePlan plan, string title, string hint)
        { this.plan = plan; this.title = title; this.hint = hint; }
        public override void OnOpen()
        {
            // Reopening this exact property is an explicit request to review it;
            // a scheduled repaint must never silently recreate interrupted previews.
            if (Matches && Pending.Suspended && !Pending.Conflicted) Pending.Resume(plan);
        }
        public override Vector2 GetWindowSize() => new Vector2(290, 160);
        private bool Matches => Pending.Plan == plan && Pending.ActiveKind == HousePendingEdit.Kind.SlabThickness;
        private float Value => Matches ? Pending.Total : HouseBuilding.For(plan)?.SlabThickness ?? 0;
        private void SetValue(float value)
        {
            Pending.SetSlab(plan, value, Matches ? Pending.Finish : HouseBuilding.For(plan)?.FloorThickness ?? 0);
            if (Matches && !Pending.IsPending) Pending.Discard();
        }
        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Pending.ActiveKind != HousePendingEdit.Kind.None && !Matches))
            {
                using (new EditorGUI.DisabledScope(Matches && (Pending.Suspended || Pending.Conflicted)))
                {
                    EditorGUI.BeginChangeCheck();
                    float next = EditorGUILayout.FloatField("Metres", Value);
                    if (EditorGUI.EndChangeCheck()) SetValue(next);
                }
                EditorGUILayout.LabelField(hint, EditorStyles.wordWrappedMiniLabel);
                if (Matches && Pending.Error.Length > 0) EditorGUILayout.HelpBox(Pending.Error, MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply"))
                    {
                        var result = ApplyPending();
                        if (result.Status != HouseEditStatus.Rejected) { SceneView.RepaintAll(); editorWindow.Close(); }
                    }
                    if (GUILayout.Button("Cancel")) Cancel();
                }
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { Event.current.Use(); Cancel(); }
        }
        private void Cancel()
        {
            if (Matches) Pending.Discard();
            editorWindow.Close();
        }
        public override void OnClose()
        {
            if (Matches) Pending.Interrupt();
        }
        internal HouseEditResult ApplyPending() => Matches ? HouseEditorSession.instance.ApplyPending()
            : Pending.IsPending ? HouseEditResult.Reject("Finish or discard the current edit first.")
            : new HouseEditResult(HouseEditStatus.Unchanged);
    }
}
