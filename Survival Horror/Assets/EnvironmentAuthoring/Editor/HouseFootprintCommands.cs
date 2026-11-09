using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // One-time copies only. No saved source reference or propagation dependency exists.
    internal static class HouseFootprintCommands
    {
        internal static IReadOnlyList<HouseStack.Level> Sources(HousePlan target)
        {
            var building = target == null ? null : HouseBuilding.For(target);
            var level = building?.Find(target);
            if (level == null) return Array.Empty<HouseStack.Level>();
            return building.Levels.Where(l => l.Plan != null && building.Data.Elevation(l) < building.Data.Elevation(level))
                .OrderByDescending(l => building.Data.Elevation(l)).ToList().AsReadOnly();
        }

        internal static HouseEditResult Copy(HousePlan target, HousePlan source, Func<string, bool> confirm)
        {
            if (!HouseEditValidation.Source(target, out string reason)) return HouseEditResult.Reject(reason);
            if (!Sources(target).Any(l => l.Plan == source))
                return HouseEditResult.Reject("Choose a lower level in the same House as the footprint source.");
            if (!source.HasSupportedTransform) return HouseEditResult.Reject("Keep the source House upright at unit scale.");
            using var preview = new HouseEditPreview(target);
            if (!preview.ReplaceFootprint(source, out reason)) return HouseEditResult.Reject(reason);
            if (preview.PlanJson() == preview.Original) return new HouseEditResult(HouseEditStatus.Unchanged);

            if (target.Data.Corners.Count > 0 || target.Data.Walls.Count > 0 || target.Data.Cells.Count > 0)
            {
                if (confirm == null) return HouseEditResult.Reject("Confirm replacement of the existing wall layout before copying.");
                var next = preview.Plan;
                int retained = target.Data.Cells.Count(c => next.Cells.Any(n => n.Id == c.Id));
                int removedWalls = target.Data.Walls.Count(w => !next.Walls.Any(n => n.Id == w.Id));
                int removedOpenings = target.Data.Walls.SelectMany(w => w.Openings)
                    .Count(o => !next.Walls.SelectMany(w => w.Openings).Any(n => n.Id == o.Id));
                string message = $"Copy the exterior contour from {source.name} to {target.name}?\n\n"
                    + $"Unchanged rooms retained: {retained}. Rooms removed/replaced: {target.Data.Cells.Count - retained}.\n"
                    + $"Walls removed: {removedWalls}. Door/window openings removed: {removedOpenings}.\n\n"
                    + "Replaced rooms lose their material overrides. Changed generated geometry loses its paint. "
                    + "Slab segment settings and shared cuts are preserved where supported. Manually placed objects stay and may need repositioning.\n\n"
                    + "This is an independent copy and can be undone.";
                if (!confirm(message)) return new HouseEditResult(HouseEditStatus.Unchanged, "The footprint copy was cancelled.");
            }
            // A confirmation dialog may outlive other edits. Recheck the reviewed candidate,
            // including the source level, instead of copying new data after confirmation.
            return HouseEditCommands.CommitPreview(preview, "Copy exterior footprint");
        }
    }
}
