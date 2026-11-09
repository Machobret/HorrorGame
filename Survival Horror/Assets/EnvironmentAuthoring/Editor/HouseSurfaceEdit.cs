using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Commands reconcile a boundary before publication. Output never repairs its records.
    internal static partial class HouseSurfaceEdit
    {
        // A planar edit affects both adjoining slabs. Stage both before changing
        // either candidate boundary, so failure above cannot leave a partial edit below.
        internal static bool TryReconcileLevel(HouseStack before, HouseStack after, string levelId,
            Func<HouseStack.Level, FloorPlan> oldPlanFor, Func<HouseStack.Level, FloorPlan> newPlanFor, out string reason)
        {
            reason = "Choose an existing level with the same adjoining slab boundaries.";
            var level = before?.Find(levelId);
            if (level == null || after?.Find(levelId) == null) return false;
            var staged = new List<(HouseStack.Boundary target, HouseStack.Boundary value)>();
            foreach (var original in new[] { before.Below(level), before.Above(level) })
            {
                reason = "Choose an existing level with the same adjoining slab boundaries.";
                if (original == null) return false;
                var target = after.Boundaries.FirstOrDefault(boundary => boundary.Id == original.Id);
                if (target == null || target.LowerId != original.LowerId || target.UpperId != original.UpperId) return false;
                var candidate = target.Copy();
                if (!TryReconcile(original, candidate,
                    Plan(before, original.LowerId, oldPlanFor), Plan(before, original.UpperId, oldPlanFor),
                    Plan(after, target.LowerId, newPlanFor), Plan(after, target.UpperId, newPlanFor), out reason,
                    movingDividers: candidate.DividerNodes.Any(node => original.DividerNodes.Any(old => old.Id == node.Id && old.Position != node.Position)))) return false;
                staged.Add((target, candidate));
            }
            foreach (var item in staged)
            {
                item.target.ReplaceDividerGraph(item.value);
                item.target.ReplaceRegions(item.value.Regions);
            }
            reason = "";
            return true;

            static FloorPlan Plan(HouseStack stack, string id, Func<HouseStack.Level, FloorPlan> read)
            {
                var owner = stack.Find(id);
                return owner == null ? null : read(owner);
            }
        }

        internal static bool TryReconcile(HouseStack.Boundary before, HouseStack.Boundary after,
            FloorPlan oldLower, FloorPlan oldUpper, FloorPlan newLower, FloorPlan newUpper, out string reason, bool movingDividers = false,
            bool retainConflictingDividers = true)
        {
            if (!HouseSurfaceLayout.TryRead(before, oldLower, oldUpper, out var oldFaces, out reason)) return false;
            return Reconcile(before, after, oldFaces, oldLower, oldUpper, newLower, newUpper,
                out reason, movingDividers, retainConflictingDividers, followSources: true);
        }

        internal static bool TryReconcileCuts(HouseStack.Boundary before, HouseStack.Boundary after,
            FloorPlan lower, FloorPlan upper, out string reason)
        {
            if (!HouseSurfaceLayout.TryRead(before, lower, upper, out var oldFaces, out reason)) return false;
            // A hole moves independently of the material beneath it. Source-lineage
            // matching would incorrectly carry the hidden face's values with the hole.
            bool movingDividers = after.DividerNodes.Any(node => before.DividerNodes.Any(old => old.Id == node.Id && old.Position != node.Position));
            return Reconcile(before, after, oldFaces, lower, upper, lower, upper,
                out reason, movingDividers, true, followSources: false);
        }

        private static bool Reconcile(HouseStack.Boundary before, HouseStack.Boundary after,
            List<HouseSurfaceLayout.Face> oldFaces, FloorPlan oldLower, FloorPlan oldUpper, FloorPlan newLower, FloorPlan newUpper,
            out string reason, bool movingDividers, bool retainConflictingDividers, bool followSources)
        {
            var candidate = after.Copy();
            if (!candidate.TryValidateSurfaceData(out reason)) return false;
            var faces = HouseSurfaceLayout.Derive(candidate, newLower, newUpper);
            var coverage = SlabGeometry.Union(new PathsD(faces.SelectMany(face => face.Shape)));
            if (!ClipDividers(candidate, coverage, newLower, newUpper, out bool clipped, out reason)) return false;
            if (clipped) faces = HouseSurfaceLayout.Derive(candidate, newLower, newUpper);
            var values = Assign(before, candidate, oldFaces, faces, oldLower, oldUpper, newLower, newUpper,
                out var conflicts, out var represented, out var followingSource, movingDividers, followSources);
            if (conflicts.Count > 0)
            {
                // A delete must remove the requested edge. Recreating it would report
                // success while silently undoing the user's intent.
                if (!retainConflictingDividers)
                {
                    reason = "Choose compatible segment finishes before deleting this divider.";
                    return false;
                }
                bool added = false;
                foreach (var conflict in conflicts)
                    for (int i = 0; i < conflict.parents.Count; i++)
                        for (int j = i + 1; j < conflict.parents.Count; j++)
                        {
                            var a = conflict.parents[i]; var b = conflict.parents[j];
                            if (a.Record.SamePayload(b.Record)) continue;
                            foreach (var edge in SurfacePartition.SharedEdges(a.Shape, b.Shape))
                                foreach (var portion in SurfacePartition.ClipLine(edge, conflict.face.Shape))
                                {
                                    if (!TryAddDivider(candidate, portion.a, portion.b, out bool inserted, out reason)) return false;
                                    added |= inserted;
                                }
                        }
                // One deterministic separator pass is enough. Do not feed derived
                // geometry back into a solver until it happens to reach a fixed point.
                if (added)
                {
                    faces = HouseSurfaceLayout.Derive(candidate, newLower, newUpper);
                    values = Assign(before, candidate, oldFaces, faces, oldLower, oldUpper, newLower, newUpper,
                        out conflicts, out represented, out followingSource, movingDividers, followSources);
                }
                if (conflicts.Count > 0)
                {
                    reason = "This edit has ambiguous slab correspondence. Keep the dividing boundary or choose compatible segment values first.";
                    return false;
                }
            }
            // An omitted floor follows its owner. Finishes
            // still require preservation checks before losing coverage.
            if (oldFaces.Any(face => face.Record.IsCustomized && !represented.Contains(face.Record.Id)))
            {
                reason = "A customized slab segment has no surviving correspondence. Resolve its values before replacing the contour.";
                return false;
            }
            foreach (var old in oldFaces.Where(face => face.Record.IsCustomized && !followingSource.Contains(face.Record.Id)))
            {
                // Ignore only the existing solver's boundary precision. A genuine loss
                // of customized coverage must be resolved explicitly, never reset.
                var interior = Clipper.InflatePaths(old.Shape, -.0001, JoinType.Miter, EndType.Polygon, precision: 4);
                if (SurfacePartition.Area(SlabGeometry.Intersect(old.Shape, coverage)) > 1e-8
                    && SurfacePartition.Area(SlabGeometry.Subtract(interior, coverage)) <= 1e-8) continue;
                reason = "This edit removes customized slab coverage. Resolve the affected segments before replacing the contour.";
                return false;
            }
            candidate.ReplaceRegions(values);
            after.ReplaceDividerGraph(candidate);
            after.ReplaceRegions(candidate.Regions);
            reason = "";
            return true;
        }

        private static List<HouseStack.SurfaceRegion> Assign(HouseStack.Boundary before, HouseStack.Boundary after,
            List<HouseSurfaceLayout.Face> oldFaces, List<HouseSurfaceLayout.Face> faces,
            FloorPlan oldLower, FloorPlan oldUpper, FloorPlan newLower, FloorPlan newUpper,
            out List<(HouseSurfaceLayout.Face face, List<HouseSurfaceLayout.Face> parents)> conflicts, out HashSet<string> represented,
            out HashSet<string> followingSource, bool movingDividers, bool followSources)
        {
            var matched = new Dictionary<HouseSurfaceLayout.Face, HouseSurfaceLayout.Face>();
            var used = new HashSet<string>();
            var sourceMatches = new HashSet<string>();
            // Authored divider perimeters lead spatial matching. Otherwise retained
            // source rooms/edges lead matching so moving rooms carries their values.
            if (followSources)
            {
                MatchUnique(face => DividerLineage(face, before), face => DividerLineage(face, after), movesPerimeter: movingDividers);
                MatchUnique(face => Lineage(face, before, oldLower, oldUpper), face => Lineage(face, after, newLower, newUpper), followsSource: true);
            }
            foreach (var face in faces)
            {
                if (matched.ContainsKey(face)) continue;
                var old = oldFaces.FirstOrDefault(item => !used.Contains(item.Record.Id) && item.Key == face.Key);
                if (old == null) continue;
                matched.Add(face, old); used.Add(old.Record.Id);
            }
            void MatchUnique(Func<HouseSurfaceLayout.Face, string> oldKey, Func<HouseSurfaceLayout.Face, string> newKey, bool followsSource = false, bool movesPerimeter = false)
            {
                var originals = oldFaces.Where(face => !used.Contains(face.Record.Id)).GroupBy(oldKey)
                    .Where(group => group.Key != null).ToDictionary(group => group.Key, group => group.ToArray());
                foreach (var group in faces.Where(face => !matched.ContainsKey(face)).GroupBy(newKey))
                {
                    // A signature shared by several disconnected faces is not correspondence.
                    if (group.Key == null || group.Count() != 1 || !originals.TryGetValue(group.Key, out var olds) || olds.Length != 1) continue;
                    matched.Add(group.Single(), olds[0]); used.Add(olds[0].Record.Id);
                    // Source-owned surfaces move with their defining walls. A fully
                    // divider-owned perimeter stays spatially protected during layout edits.
                    if (movesPerimeter || followsSource && DividerLineage(olds[0], before) == null) sourceMatches.Add(olds[0].Record.Id);
                }
            }
            conflicts = new List<(HouseSurfaceLayout.Face, List<HouseSurfaceLayout.Face>)>();
            represented = new HashSet<string>();
            var result = new List<HouseStack.SurfaceRegion>();
            var cuts = followSources ? null : SlabGeometry.Cuts(after);
            // When introducing void faces, retain an old output identity on its
            // visible child before assigning identities to the hidden recovery faces.
            foreach (var face in faces.OrderByDescending(item => SurfacePartition.Area(cuts == null ? item.Shape : SlabGeometry.Subtract(item.Shape, cuts)))
                .ThenByDescending(item => SurfacePartition.Area(item.Shape)).ThenBy(item => item.Key, StringComparer.Ordinal))
            {
                if (matched.TryGetValue(face, out var retained))
                {
                    result.Add(retained.Record.Copy(face.Key));
                    represented.Add(retained.Record.Id);
                    continue;
                }
                // Values already carried by their room/divider owner must not
                // also be donated spatially to the area that owner left behind.
                var parents = oldFaces.Where(old => old.Ceiling == face.Ceiling && !sourceMatches.Contains(old.Record.Id)
                    && SurfacePartition.Area(SlabGeometry.Intersect(face.Shape, old.Shape)) > 1e-8).ToList();
                if (parents.Count == 0) { result.Add(new HouseStack.SurfaceRegion(face.Key, face.Ceiling)); continue; }
                if (parents.Any(parent => !parent.Record.SamePayload(parents[0].Record)))
                { conflicts.Add((face, parents)); continue; }
                var parent = parents.OrderByDescending(old => SurfacePartition.Area(SlabGeometry.Intersect(face.Shape, old.Shape)))
                    .ThenBy(old => used.Contains(old.Record.Id))
                    .ThenBy(old => old.Record.Id, StringComparer.Ordinal).First();
                result.Add(parent.Record.Copy(face.Key, used.Add(parent.Record.Id)));
                foreach (var contributor in parents)
                {
                    represented.Add(contributor.Record.Id);
                }
            }
            followingSource = sourceMatches;
            // Region order has no geometric meaning. Keep accepted records in place
            // so a no-op edit cannot publish a changed House or add an Undo entry.
            var order = before.Regions.Select((region, index) => (region.Id, index)).ToDictionary(item => item.Id, item => item.index);
            return result.OrderBy(region => order.TryGetValue(region.Id, out int index) ? index : int.MaxValue)
                .ThenBy(region => region.FaceKey, StringComparer.Ordinal).ToList();
        }

        private static string Lineage(HouseSurfaceLayout.Face face, HouseStack.Boundary boundary, FloorPlan lower, FloorPlan upper)
        {
            // An undivided room has one source-owned face. Its room identity is
            // sufficient; scanning every wall cannot distinguish it any further.
            if (face.RoomId != null && boundary.Dividers.Count == 0 && boundary.DividerNodes.Count == 0)
                return face.Ceiling + ":" + face.RoomId;
            var owners = new List<string>();
            // Owned faces follow their own room boundary. Unowned roof/soffit
            // outlines can depend on both footprints, but never on interior opposite seams.
            if (face.RoomId == null) { AddWalls(lower, "lower:"); AddWalls(upper, "upper:"); }
            else if (face.Ceiling) AddWalls(lower, "lower:");
            else AddWalls(upper, "upper:");
            foreach (var divider in boundary.Dividers)
                if (Touches(face.Shape, boundary.DividerPosition(divider.StartId), boundary.DividerPosition(divider.EndId))) owners.Add("divider:" + divider.Id);
            return face.Ceiling + ":" + face.RoomId + ":" + string.Join(";", owners.OrderBy(value => value, StringComparer.Ordinal));
            void AddWalls(FloorPlan plan, string side)
            {
                if (plan == null) return;
                foreach (var wall in plan.Walls)
                    if (Touches(face.Shape, plan.GetPosition(wall.StartId), plan.GetPosition(wall.EndId))) owners.Add(side + wall.Id);
            }
        }

        private static string DividerLineage(HouseSurfaceLayout.Face face, HouseStack.Boundary boundary)
        {
            if (boundary.Dividers.Count == 0) return null;
            var lines = new PathsD(boundary.Dividers.Select(edge =>
            {
                var a = boundary.DividerPosition(edge.StartId); var b = boundary.DividerPosition(edge.EndId);
                return new PathD { new PointD(a.x, a.y), new PointD(b.x, b.y) };
            }));
            foreach (var path in face.Shape)
                for (int i = 0; i < path.Count; i++)
                {
                    var a = new Vector2((float)path[i].x, (float)path[i].y);
                    var b = new Vector2((float)path[(i + 1) % path.Count].x, (float)path[(i + 1) % path.Count].y);
                    var direction = (b - a).normalized;
                    var edge = new PathsD { new PathD { path[i], path[(i + 1) % path.Count] } };
                    var ranges = SurfacePartition.SharedEdges(edge, lines).Select(portion =>
                    {
                        float first = Vector2.Dot(portion.a - a, direction), last = Vector2.Dot(portion.b - a, direction);
                        return (min: Mathf.Min(first, last), max: Mathf.Max(first, last));
                    }).OrderBy(range => range.min);
                    float covered = 0;
                    foreach (var range in ranges)
                    {
                        if (range.min > covered + .0001f) break;
                        covered = Mathf.Max(covered, range.max);
                    }
                    if (covered < Vector2.Distance(a, b) - .0001f) return null;
                }
            return face.Ceiling + ":" + face.RoomId + ":dividers:" + string.Join(";", boundary.Dividers.Where(edge =>
                Touches(face.Shape, boundary.DividerPosition(edge.StartId), boundary.DividerPosition(edge.EndId)))
                .Select(edge => edge.Id).OrderBy(id => id, StringComparer.Ordinal));
        }

        private static bool Touches(PathsD shape, Vector2 start, Vector2 end)
            => SurfacePartition.SharedEdges(shape, new PathsD { new PathD { new PointD(start.x, start.y), new PointD(end.x, end.y) } }).Any();

        private static bool TryAddDivider(HouseStack.Boundary boundary, Vector2 a, Vector2 b, out bool added, out string reason)
        {
            added = false; reason = "";
            if (boundary.Dividers.Any(edge => (boundary.DividerPosition(edge.StartId) == a && boundary.DividerPosition(edge.EndId) == b)
                || (boundary.DividerPosition(edge.StartId) == b && boundary.DividerPosition(edge.EndId) == a))) return true;
            var oldIds = new HashSet<string>(boundary.Dividers.Select(edge => edge.Id));
            if (!boundary.TryAddDivider(a, b, out reason)) return false;
            added = boundary.Dividers.Any(edge => !oldIds.Contains(edge.Id));
            return true;
        }

    }
}
