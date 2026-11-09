using System.Collections.Generic;
using System.Linq;

namespace EnvironmentAuthoring.Editor
{
    public static partial class HouseEditCommands
    {
        public static HouseEditResult DissolveNode(HousePlan plan, string nodeId)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.Plan.TryDissolveCorner(nodeId, out reason) || !preview.PrepareSurfaces(out reason))
                return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishPlanGeometry(preview, "Dissolve wall junction", allowExistingFitProblems: true);
        }

        public static HouseEditResult DeleteNode(HousePlan plan, string nodeId)
        {
            if (!HouseEditValidation.Source(plan, out string reason)) return HouseEditResult.Reject(reason);
            using var preview = new HouseEditPreview(plan);
            if (!preview.Plan.TryDeleteCorner(nodeId, out reason) || !preview.PrepareSurfaces(out reason))
                return HouseEditResult.Reject(reason);
            return HouseEditCommit.PublishPlanGeometry(preview, "Delete wall junction", allowExistingFitProblems: true);
        }

        // Keyboard and click deletion share target policy. Domain commands below
        // still own candidate reconciliation, validation and one authored Undo.
        internal static HouseEditResult DeleteSelection(HousePlan plan, HouseEditSelection selection)
        {
            if (!CanDeleteSelection(plan, selection, out _, out string reason)) return HouseEditResult.Reject(reason);
            var item = selection.Items[0];
            switch (item.Type)
            {
                case HouseEditSelection.Kind.Wall: return DeleteWall(plan, item.Id);
                case HouseEditSelection.Kind.Node: return DeleteNode(plan, item.Id);
                case HouseEditSelection.Kind.Opening: return RemoveOpening(plan, item.Id);
                case HouseEditSelection.Kind.SlabCut:
                    return HouseSlabCommands.DeleteSlabOpening(HouseBuilding.For(plan), item.BoundaryId, item.Id);
                case HouseEditSelection.Kind.Divider:
                    return HouseSurfaceCommands.DeleteDividers(plan, item.BoundaryId, selection.Items.Select(i => i.Id));
                case HouseEditSelection.Kind.Trim: return HouseTrimCommands.Set(plan, new[] { item.Id }, null);
                case HouseEditSelection.Kind.BooleanShape: return HouseBooleanCommands.Delete(plan, item.Id);
                default: return HouseEditResult.Reject(reason);
            }
        }

        internal static bool CanDeleteSelection(HousePlan plan, HouseEditSelection selection,
            out List<HouseEditSelection.Item> affected, out string message)
        {
            affected = new List<HouseEditSelection.Item>();
            message = "Select a wall, room boundary, floor divider, opening, slab cut or attached trim to delete.";
            var items = selection.Items;
            if (plan == null || items.Count == 0) return false;
            var item = items[0];
            // Only divider removal already has an atomic batch operation. Never
            // partly delete a mixed selection or guess a topology edit order.
            if (items.Count != 1 && (item.Type != HouseEditSelection.Kind.Divider
                || items.Any(i => i.Type != item.Type || i.BoundaryId != item.BoundaryId)))
            { message = "Delete one element at a time, or select floor dividers on the same slab."; return false; }
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = building?.Boundaries.FirstOrDefault(b => b.Id == item.BoundaryId
                && level != null && (b.LowerId == level.Id || b.UpperId == level.Id));
            message = "The selected element no longer exists on this level.";
            switch (item.Type)
            {
                case HouseEditSelection.Kind.Wall:
                    int index = plan.Data.Walls.ToList().FindIndex(w => w.Id == item.Id);
                    if (!plan.Data.CanDeleteWall(index, out var ids, out message)) return false;
                    affected.AddRange(ids.Select(id => new HouseEditSelection.Item(item.Type, id)));
                    int owners = plan.Data.Cells.Count(c => c.WallIds.Contains(item.Id));
                    string subject = plan.Data.Walls[index].IsPhysical ? "wall" : "room boundary";
                    message = owners == 2 ? $"Delete {ids.Count} shared {subject} segment(s) and merge the rooms."
                        : owners == 1 ? $"Delete this {subject} and open the room. Other walls stay."
                        : $"Delete this {subject} segment.";
                    return true;
                case HouseEditSelection.Kind.Opening:
                    if (!plan.Data.Walls.Any(w => w.Openings.Any(o => o.Id == item.Id))) return false;
                    message = "Remove this opening and restore solid wall."; break;
                case HouseEditSelection.Kind.SlabCut:
                    if (boundary == null || !boundary.Openings.Any(o => o.Id == item.Id)) return false;
                    message = "Remove this slab cut and restore its floor/ceiling surface."; break;
                case HouseEditSelection.Kind.Divider:
                    if (boundary == null || items.Any(i => !boundary.Dividers.Any(d => d.Id == i.Id))) return false;
                    message = "Remove the selected floor divider(s). Conflicting heights or finishes must be resolved first."; break;
                case HouseEditSelection.Kind.Trim:
                    if (!plan.Data.Walls.Any(w => w.Id == item.Id && HouseTrimLayout.Resolve(plan, w)?.Mesh != null))
                    { message = "There is no trim attached to this wall."; return false; }
                    message = "Remove this trim attachment. Keep its wall."; break;
                case HouseEditSelection.Kind.BooleanShape:
                    if (building == null || !building.Data.BooleanVolumes.Any(volume => volume.Id == item.Id)) return false;
                    message = "Remove this Boolean shape and restore the generated surfaces."; break;
                case HouseEditSelection.Kind.Room:
                    message = "Room deletion is unavailable. Select its wall or room boundary instead."; return false;
                case HouseEditSelection.Kind.Node:
                    var incident = plan.Data.Walls.Where(w => w.StartId == item.Id || w.EndId == item.Id).ToArray();
                    if (incident.Length == 0) return false;
                    if (!plan.Data.Copy().TryDeleteCorner(item.Id, out message)) return false;
                    affected.AddRange(incident.Select(w => new HouseEditSelection.Item(HouseEditSelection.Kind.Wall, w.Id)));
                    message = $"Delete this junction and its {incident.Length} incident wall(s).";
                    return true;
                case HouseEditSelection.Kind.DividerNode:
                    message = "Point deletion is unavailable. Select a wall, room boundary or floor divider instead."; return false;
                default:
                    message = "Whole floor/ceiling deletion is unavailable. Use Slab cut to remove a floor area."; return false;
            }
            affected.AddRange(items);
            return true;
        }
    }
}
