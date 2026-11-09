using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public static partial class HouseWallLayout
    {
        public static WallModuleDefinition DoorwayFor(FloorPlan plan, FloorPlan.Wall wall, WallKit builder, float width = 0)
            => OpeningFor(plan, wall, builder, FloorPlan.OpeningKind.Doorway, width);

        public static WallModuleDefinition OpeningFor(FloorPlan plan, FloorPlan.Wall wall, WallKit kit, FloorPlan.Opening opening)
            => OpeningFor(plan, wall, kit, opening.Kind, opening.Width, opening.Shape);

        public static WallModuleDefinition OpeningFor(FloorPlan plan, FloorPlan.Wall wall, WallKit builder, FloorPlan.OpeningKind kind, float width = 0, string shape = null)
        {
            if (!wall.IsPhysical || builder == null) return null;
            return builder.FamilyFor(plan, wall)?.OpeningFor(kind, width == 0 ? 2 : width, shape);
        }

        public static bool CanPlaceDoorway(FloorPlan plan, WallKit builder, string wallId,
            float distance, float width, string ignoreId, out string reason)
        {
            return CanPlaceOpening(plan, builder, wallId, distance, width, FloorPlan.OpeningKind.Doorway, ignoreId, out reason);
        }

        public static bool CanPlaceOpening(FloorPlan plan, WallKit builder, string wallId,
            float distance, float width, FloorPlan.OpeningKind kind, string ignoreId, out string reason, Result layout = null, string shape = null)
        {
            reason = "Enable 3D walls first.";
            if (builder == null) return false;
            if (!plan.CanPlaceOpening(wallId, distance, width, ignoreId, out reason)) return false;
            // Ignore existing openings here so an invalid opening can still be dragged back to safety.
            layout ??= Calculate(plan, builder, false);
            reason = layout.Problem;
            if (!layout.IsValid) return false;
            var wall = FindWall(plan, wallId);
            FloorPlan.Opening selection = null;
            foreach (var opening in wall.Openings) if (opening.Id == ignoreId) selection = opening;
            if (selection == null && shape != null)
            {
                selection = new FloorPlan.Opening(distance, width, kind);
                selection.SetAssets(shape, null);
            }
            return FitsOpening(plan, wall, builder, layout.Spans.Find(s => s.WallId == wallId), distance, width, kind, out reason, layout, selection);
        }

        private static void AssignInsert(FloorPlan.Wall wall, Span span)
        {
            foreach (var opening in wall.Openings)
                if (opening.Id == span.DoorwayId) { span.Insert = opening.Insert; return; }
        }

        private static FloorPlan.Wall FindWall(FloorPlan plan, string id)
        {
            foreach (var wall in plan.Walls) if (wall.Id == id) return wall;
            return null;
        }

        public static string InsertProblem(FloorPlan plan, FloorPlan.Wall wall, WallKit kit, FloorPlan.Opening opening)
        {
            if (opening.Insert == null) return "";
            var module = OpeningFor(plan, wall, kit, opening);
            return opening.Insert.Fits(kit?.FamilyFor(plan, wall), opening.Kind, module) ? ""
                : "Choose a door/window model matching the aperture, mounting datum and wall thickness. Clear the model before an incompatible opening change.";
        }

        private static Vector2 DoorwayCenter(FloorPlan plan, FloorPlan.Wall wall, float distance, Result layout = null)
        {
            Vector2 start = plan.GetPosition(wall.StartId);
            return start + (plan.GetPosition(wall.EndId) - start).normalized * distance + (layout?.PlacementOffset(wall.Id) ?? Vector2.zero);
        }

        private static bool FitsOpening(FloorPlan plan, FloorPlan.Wall wall, WallKit builder,
            Span span, float distance, float width, FloorPlan.OpeningKind kind, out string reason, Result layout = null, FloorPlan.Opening selection = null)
        {
            var door = OpeningFor(plan, wall, builder, kind, width, selection?.Shape);
            reason = $"Assign a {width:0.####} m {kind.ToString().ToLowerInvariant()} segment for this wall family in House Wall Builder.";
            if (door == null) return false;
            if (!door.IsValid(out reason)) return false;
            reason = "The opening overlaps a corner or junction. Move it farther along the wall.";
            if (span == null) return false;
            reason = "Opening modules must keep their authored width and match the wall height and thickness.";
            if (door.AllowLengthStretch || Mathf.Abs(door.Width - width) > Tolerance
                || Mathf.Abs(door.Height - span.Definition.Height) > Tolerance
                || Mathf.Abs(door.Thickness - span.Definition.Thickness) > Tolerance) return false;
            float along = Vector2.Dot(DoorwayCenter(plan, wall, distance, layout) - span.Start, (span.End - span.Start).normalized);
            reason = "The opening overlaps a corner or junction. Move it farther along the wall.";
            // The solid surround can participate in the normal wall overlap. Reserve
            // clearance for the aperture itself, not the entire rigid wall segment.
            var aperture = door.OpeningOutline;
            float left = aperture.width > 0 ? aperture.xMin : 0;
            float right = aperture.width > 0 ? aperture.xMax : width;
            float origin = along - width / 2;
            if (origin + left < span.StartClearance - Tolerance
                || origin + right > Vector2.Distance(span.Start, span.End) - span.EndClearance + Tolerance) return false;
            reason = "";
            return true;
        }

        private static void FitDoorways(FloorPlan plan, WallKit builder, Result result)
        {
            foreach (var wall in plan.Walls)
                if (wall.IsPhysical && UsableBlank(DefinitionFor(plan, wall, builder))
                    && wall.Openings.Count > 0 && !result.Spans.Exists(s => s.WallId == wall.Id))
                    result.Reject("An opening lies inside a reserved junction. Move or remove it before changing this corner.", plan.GetPosition(wall.StartId));
            if (!result.IsValid) return;
            var fitted = new List<Span>();
            foreach (var span in result.Spans)
            {
                var wall = FindWall(plan, span.WallId);
                if (wall.Openings.Count == 0) { fitted.Add(span); continue; }
                Vector2 along = (span.End - span.Start).normalized;
                var doors = new List<FloorPlan.Opening>(wall.Openings);
                // Boundary-facing orientation can reverse the output relative to the stored wall.
                doors.Sort((a, b) => Vector2.Dot(DoorwayCenter(plan, wall, a.Distance, result) - span.Start, along)
                    .CompareTo(Vector2.Dot(DoorwayCenter(plan, wall, b.Distance, result) - span.Start, along)));
                Vector2 cursor = span.Start;
                foreach (var door in doors)
                {
                    var center = DoorwayCenter(plan, wall, door.Distance, result);
                    if (!plan.CanPlaceOpening(wall.Id, door.Distance, door.Width, door.Id, out string reason)
                        || !FitsOpening(plan, wall, builder, span, door.Distance, door.Width, door.Kind, out reason, layout: result, selection: door))
                    {
                        result.Reject(door.Kind + ": " + reason + " Its placement is retained; move or delete it in Edit/Delete.", center);
                        continue;
                    }
                    Vector2 start = center - along * door.Width / 2;
                    Vector2 end = center + along * door.Width / 2;
                    bool hasBlank = Vector2.Distance(cursor, start) > Tolerance;
                    if (hasBlank)
                        fitted.Add(new Span { WallId = wall.Id, Start = cursor, End = start,
                            Definition = span.Definition });
                    fitted.Add(new Span { WallId = wall.Id, DoorwayId = door.Id, Start = start, End = end,
                        Definition = OpeningFor(plan, wall, builder, door.Kind, door.Width, door.Shape), OpeningKind = door.Kind });
                    cursor = end;
                }
                if (Vector2.Distance(cursor, span.End) > Tolerance)
                    fitted.Add(new Span { WallId = wall.Id, Start = cursor, End = span.End,
                        Definition = span.Definition });
            }
            if (!result.IsValid) return;
            foreach (var child in fitted)
            {
                var original = result.Spans.Find(s => s.WallId == child.WallId);
                child.Depths = original.Depths;
                AssignInsert(FindWall(plan, child.WallId), child);
            }
            result.Spans.Clear();
            result.Spans.AddRange(fitted);
        }
    }
}
