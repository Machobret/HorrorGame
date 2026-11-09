using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Boundary-only edits share the ordinary copied candidate and native Undo publisher.
    internal static class HouseSurfaceCommands
    {
        internal static HouseEditResult AddDivider(HousePlan plan, string boundaryId, Vector2 start, Vector2 end)
            => AddDividerStroke(plan, boundaryId, new[] { start, end });

        internal static HouseEditResult AddDividerStroke(HousePlan plan, string boundaryId, IReadOnlyList<Vector2> points)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.AddDividerStroke(boundaryId, points, true, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishBuildingGeometry(preview, "Draw floor divider", requireEditedFit: false);
        }
        internal static HouseEditResult MoveDividerNodes(HousePlan plan, string boundaryId, IReadOnlyDictionary<string, Vector2> positions)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.MoveDividerNodes(boundaryId, positions, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishBuildingGeometry(preview, "Move floor divider");
        }
        internal static HouseEditResult InsertDividerNode(HousePlan plan, string boundaryId, string dividerId,
            Vector2 position, out string nodeId)
        {
            nodeId = null;
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.InsertDividerNode(boundaryId, dividerId, position, out nodeId, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishBuildingGeometry(preview, "Insert floor divider point");
        }
        internal static HouseEditResult DeleteDividers(HousePlan plan, string boundaryId, IEnumerable<string> dividerIds)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.DeleteDividers(boundaryId, dividerIds, out reason)) return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishBuildingGeometry(preview, "Delete floor divider", requireEditedFit: false);
        }

    }
}
