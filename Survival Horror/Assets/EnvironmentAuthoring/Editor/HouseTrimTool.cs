using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Click-only attachment selection. The session still owns selection and Undo.
    internal static class HouseTrimTool
    {
        internal static void Handle(HouseEditorSession session, ScenePointerInput input, HouseSceneHit? hover)
        {
            if (!input.LeftMouseDown || input.Navigating) return;
            if (hover?.Kind == HouseEditSelection.Kind.Trim)
            {
                session.Pick(HouseEditSelection.Kind.Trim, hover.Value.Id, input.Event.shift, input.Event.control || input.Event.command);
                HousePropertiesWindow.ShowSelection();
            }
            else if (!input.Event.shift && !input.Event.control && !input.Event.command) session.ClearSelection();
            input.Event.Use();
        }

        internal static void Draw(HouseEditorSession session, HouseSceneFeedback feedback, HouseSceneHit? hover)
        {
            if (Event.current.type != EventType.Repaint) return;
            var plan = session.SessionPlan;
            foreach (var item in session.Selection.Items.Where(i => i.Type == HouseEditSelection.Kind.Trim))
            {
                var wall = plan.Data.Walls.FirstOrDefault(w => w.Id == item.Id);
                if (wall != null) feedback.DrawTrimHighlight(plan, wall, HouseSceneFeedback.SelectionColor);
            }
            if (hover?.Kind == HouseEditSelection.Kind.Trim && !session.Selection.Contains(HouseEditSelection.Kind.Trim, hover.Value.Id))
                feedback.DrawTrimHighlight(plan, plan.Data.Walls.First(w => w.Id == hover.Value.Id), HouseSceneFeedback.HoverColor);
        }
    }
}
