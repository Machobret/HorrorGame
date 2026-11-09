using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseOpeningTool
    {
        private readonly HouseEditorSession session;
        private readonly HouseSceneFeedback feedback;
        internal HouseOpeningTool(HouseEditorSession session, HouseSceneFeedback feedback)
        { this.session = session; this.feedback = feedback; }
        internal void Handle(HousePlan plan, SceneView view, ScenePointerInput input)
        {
            Event current = input.Event;
            if (input.Navigating) return;
            int control = input.ControlId;
            if (HouseSceneQueries.RaycastFloor(plan, HandleUtility.GUIPointToWorldRay(current.mousePosition), out var cursor))
                feedback.DrawPlacementGuides(plan, cursor);
            var kind = session.ActiveMode == HouseEditorSession.Mode.Window ? FloorPlan.OpeningKind.Window : FloorPlan.OpeningKind.Doorway;
            var hit = HouseSceneQueries.PickWallItem(plan, current.mousePosition);
            int wallIndex = hit.HasValue ? plan.Data.Walls.ToList().FindIndex(w => w.Id == hit.Value.Id || w.Openings.Any(o => o.Id == hit.Value.Id)) : -1;
            float distance = hit?.Along ?? 0;
            session.Message = "Hover over a wall to preview a " + kind.ToString().ToLowerInvariant() + ". Escape clears selection, then exits House editing.";
            if (wallIndex >= 0)
            {
                var host = plan.Data.Walls[wallIndex];
                distance = HouseSceneQueries.SnapOpeningDistance(plan, session, host, distance);
                var builder = plan.GetComponent<HouseWallBuilder>();
                var family = builder?.Data.FamilyFor(plan.Data, host);
                if (session.OpeningHostFamily != family) { session.OpeningHostFamily = family; session.PendingOpeningModel = null; }
                float width = session.OpeningWidth == 0 ? 2 : session.OpeningWidth;
                var supported = HouseOpeningProperties.Families(new[] { (family, width) }, kind);
                if (!session.OpeningOnly && !supported.Contains(session.OpeningFamily))
                { session.OpeningFamily = supported.FirstOrDefault(); session.PendingOpeningModel = null; }
                var models = session.OpeningFamily != null && !session.OpeningOnly
                    ? session.OpeningFamily.Compatible(family, width, session.OpeningCondition).ToArray() : Array.Empty<WallOpeningInsert>();
                if (models.Length == 0 && session.OpeningFamily != null && !session.OpeningOnly)
                {
                    session.OpeningCondition = Enum.GetValues(typeof(OpeningFamily.Condition)).Cast<OpeningFamily.Condition>()
                        .FirstOrDefault(state => session.OpeningFamily.Compatible(family, width, state).Any());
                    models = session.OpeningFamily.Compatible(family, width, session.OpeningCondition).ToArray();
                }
                if (session.OpeningModel != null && models.Contains(session.OpeningModel)) session.PendingOpeningModel = session.OpeningModel;
                else if (!models.Contains(session.PendingOpeningModel))
                    session.PendingOpeningModel = models.Length == 0 ? null : models[new System.Random().Next(models.Length)];
                bool valid = OpeningPlacement.CanPlace(plan, wallIndex, distance, kind, out var definition, out var reason,
                    session.OpeningWidth, session.PendingOpeningModel?.Shape);
                session.Message = valid ? "Click to place " + kind.ToString().ToLowerInvariant() + ". Keep clicking to place more; Escape clears selection, then exits House editing." : reason;
                var wall = plan.Data.Walls[wallIndex];
                Color color = valid ? new Color(0.2f, 1f, 0.35f) : new Color(1f, 0.2f, 0.15f);
                using (new Handles.DrawingScope(color))
                    Handles.DrawAAPolyLine(4, HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.StartId)), HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.EndId)));
                if (definition != null)
                    feedback.DrawOpeningPreview(definition, OpeningPlacement.PreviewMatrix(plan, wallIndex, distance, definition.Width), color);
                if (current.type == EventType.MouseDown && current.button == 0 && HandleUtility.nearestControl == control)
                {
                    if (valid)
                    {
                        session.Message = HouseEditCommands.PlaceOpening(plan, wallIndex, distance, kind, session.OpeningWidth,
                            session.OpeningOnly ? null : session.OpeningFamily, session.OpeningCondition, session.PendingOpeningModel).Message;
                        session.PendingOpeningModel = null;
                    }
                    current.Use(); view.Repaint();
                }
            }
            // Keep a failed/no-target click from selecting another object underneath the tool.
            else if (current.type == EventType.MouseDown && current.button == 0 && HandleUtility.nearestControl == control)
                current.Use();
        }

    }
}
