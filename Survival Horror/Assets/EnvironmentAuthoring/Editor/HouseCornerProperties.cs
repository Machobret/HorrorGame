using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseCornerProperties
    {
        internal static void Draw(HouseEditorSession session)
        {
            var plan = session.SessionPlan;
            if (plan == null || session.ActiveMode != HouseEditorSession.Mode.Edit) return;
            var items = session.Selection.Items;
            if (items.Count != 1 || items[0].Type != HouseEditSelection.Kind.Node) return;
            var corner = plan.Data.Corners.FirstOrDefault(c => c.Id == items[0].Id);
            if (corner == null) return;
            int connections = plan.Data.ConnectedWallCount(corner.Id);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected node", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{connections} connected walls");

        }

    }
}
