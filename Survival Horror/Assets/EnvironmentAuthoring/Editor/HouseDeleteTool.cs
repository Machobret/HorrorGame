using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseDeleteTool
    {
        private readonly HouseEditorSession session;
        private readonly HouseSceneFeedback feedback;
        internal HouseDeleteTool(HouseEditorSession session, HouseSceneFeedback feedback)
        { this.session = session; this.feedback = feedback; }
        internal void Handle(HousePlan plan, SceneView view, ScenePointerInput input, HouseSceneTarget hover)
        {
            Event current = input.Event;
            if (input.Navigating) return;
            bool click = current.type == EventType.MouseDown && current.button == 0
                && HandleUtility.nearestControl == input.ControlId;
            if (!click && current.type != EventType.MouseMove) return;
            var target = Target(plan, hover);
            bool valid = HouseEditCommands.CanDeleteSelection(plan, target, out _, out string message);
            session.Message = message;
            if (!click) return;
            if (session.Pending.IsPending)
                session.Message = "Finish or cancel the current edit before deleting an element.";
            else if (valid)
            {
                session.Message = HouseEditCommands.DeleteSelection(plan, target).Message;
                session.Selection.Prune(plan);
            }
            current.Use();
            view.Repaint();
        }

        // Consume the controller's one hover winner, including slab cuts. Falling
        // back to another ray pick here could delete the wall behind a cut.
        private static HouseEditSelection Target(HousePlan plan, HouseSceneTarget hover)
        {
            var selection = new HouseEditSelection();
            if (hover.Element.HasValue)
            {
                var hit = hover.Element.Value;
                selection.Items.Add(new HouseEditSelection.Item(hit.Kind, hit.Id, hit.BoundaryId));
            }
            else if (hover.Cut.HasValue)
            {
                var building = HouseBuilding.For(plan);
                var level = building?.Find(plan);
                var boundary = level == null ? null : hover.Ceiling ? building.Above(level) : building.Below(level);
                if (boundary != null) selection.Items.Add(new HouseEditSelection.Item(HouseEditSelection.Kind.SlabCut,
                    hover.Cut.Value.OpeningId, boundary.Id, hover.Ceiling));
            }
            return selection;
        }

        internal void DrawFeedback(HousePlan plan, HouseSceneTarget hover)
        {
            // Repaint must not replace the detailed rejection from the last click.
            // Pointer movement refreshes hover guidance in Handle instead.
            if (!HouseEditCommands.CanDeleteSelection(plan, Target(plan, hover), out var affected, out _)) return;
            var color = new Color(1f, .65f, .15f);
            foreach (var item in affected)
            {
                var wall = plan.Data.Walls.FirstOrDefault(w => w.Id == item.Id);
                if (item.Type == HouseEditSelection.Kind.Wall && wall.IsPhysical) feedback.DrawWallHighlight(plan, wall, color);
                else if (item.Type == HouseEditSelection.Kind.Wall || item.Type == HouseEditSelection.Kind.Divider)
                {
                    using (new Handles.DrawingScope(color))
                        foreach (var band in HouseSceneQueries.FloorBands(plan).Where(b => b.Kind == item.Type
                            && b.Id == item.Id && b.BoundaryId == item.BoundaryId))
                            Handles.DrawAAPolyLine(4, band.Start, band.End);
                }
                else if (item.Type == HouseEditSelection.Kind.Opening)
                {
                    wall = plan.Data.Walls.First(w => w.Openings.Any(o => o.Id == item.Id));
                    feedback.DrawOpeningHighlight(plan, wall, wall.Openings.First(o => o.Id == item.Id), color);
                }
                else if (item.Type == HouseEditSelection.Kind.SlabCut)
                {
                    var boundary = HouseBuilding.For(plan).Boundaries.First(b => b.Id == item.BoundaryId);
                    feedback.DrawSlabLine(plan, item.Ceiling, boundary.Openings.First(o => o.Id == item.Id).Points, color, true);
                }
                else if (item.Type == HouseEditSelection.Kind.Trim) feedback.DrawTrimHighlight(plan, wall, color);
            }
        }
    }
}
