using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Passive plan feedback; input ownership belongs to ScenePointerInput.
    internal static class HousePlanSceneDrawing
    {
        internal static void DrawPlan(HousePlan plan)
        {
            if (Event.current.type != EventType.Repaint) return;
            using (new Handles.DrawingScope(new Color(0.25f, 0.9f, 0.95f)))
            {
                for (int i = 0; i < plan.Data.Walls.Count; i++)
                {
                    var wall = plan.Data.Walls[i];
                    if (!wall.IsPhysical)
                    {
                        using (new Handles.DrawingScope(HouseSceneFeedback.RoomBoundaryColor))
                            foreach (var piece in HouseSceneQueries.RoomBoundaryPieces(plan, plan.Data.GetPosition(wall.StartId), plan.Data.GetPosition(wall.EndId)))
                                Handles.DrawAAPolyLine(HouseSceneFeedback.RoomBoundaryWidth, piece.Start, piece.End);
                        continue;
                    }
                    // Closed physical walls already show their footprint in geometry.
                    // Keep a guide only for unfinished runs, not every floor perimeter.
                    if (!plan.Data.IsIncompleteWall(i)) continue;
                    using (new Handles.DrawingScope(new Color(1f, 0.75f, 0.35f)))
                        Handles.DrawAAPolyLine(3f, HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.StartId)), HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.EndId)));
                }
                // Room-scoped control dots belong to Select feedback, not passive plan drawing.
            }
        }
    }
}
