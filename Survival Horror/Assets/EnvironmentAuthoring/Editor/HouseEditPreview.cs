using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // An isolated authored candidate. Native references identify owners; they are never edited here.
    public sealed class HouseEditPreview : IDisposable
    {
        private readonly HousePlan source;
        private readonly HouseBuilding sourceBuilding;
        private readonly string original, originalInputs;
        private readonly HouseStack originalBuilding;
        private readonly Dictionary<HousePlan, FloorPlan> originals = new Dictionary<HousePlan, FloorPlan>();
        private FloorPlan candidatePlan;
        private readonly Dictionary<HousePlan, WallKit> originalKits = new Dictionary<HousePlan, WallKit>();
        private readonly Dictionary<HousePlan, string> originalWallFitInputs = new Dictionary<HousePlan, string>();
        private readonly Dictionary<(HousePlan, HouseEditValidation.WallFitCheck), Dictionary<string, string>> originalIssues
            = new Dictionary<(HousePlan, HouseEditValidation.WallFitCheck), Dictionary<string, string>>();
        private readonly bool supportedPose;
        private bool disposed;
        private string capturedSourceProblem;
        private bool requireEditedFit = true;
        private List<HouseSlabLayout.Patch> candidateSurfaces;
        internal HousePlan Source => source;
        internal HouseBuilding SourceBuilding => sourceBuilding;
        internal IEnumerable<HousePlan> CapturedPlans => originals.Keys;
        internal HouseStack OriginalBuilding => sourceBuilding == null ? null : originalBuilding;
        internal FloorPlan OriginalPlan(HousePlan plan) => originals[plan];
        internal WallKit OriginalKit(HousePlan plan) => originalKits[plan];
        internal bool HasOriginalWallFitInputs(HousePlan plan)
            => originalWallFitInputs[plan] == HouseEditInputs.WallFitInputs(PlanFor(plan), originalKits[plan]);
        internal IReadOnlyDictionary<string, string> OriginalIssues(HousePlan plan, HouseEditValidation.WallFitCheck fit)
        {
            // The captured baseline does not change when a proposal resets. Keep each
            // validation policy separate; candidate checks still run on every update.
            var key = (plan, fit);
            if (!originalIssues.TryGetValue(key, out var issues))
                originalIssues.Add(key, issues = HouseEditValidation.OriginalIssues(this, plan, fit));
            return issues;
        }
        internal FloorPlan OriginalPlanFor(HouseStack.Level level) => level == null ? null : originals[level.Plan];
        private WallKit builder => originalKits[source];
        internal HouseStack Building { get; private set; }
        internal HouseStack.Level Level => Building?.Find(source);
        internal bool HasSupportedTransform => supportedPose;
        internal bool HasConflict => disposed || source == null || HouseEditInputs.SourceInputs(source) != originalInputs;
        internal string Original => original;
        internal string OriginalInputs => originalInputs;
        public FloorPlan Plan => candidatePlan;
        public bool Valid { get; private set; }
        internal bool DividerPartitionsFloor { get; private set; }
        internal HashSet<string> RemovedOpenings { get; } = new HashSet<string>();

        public HouseEditPreview(HousePlan source)
        {
            this.source = source;
            original = JsonUtility.ToJson(source);
            originalInputs = HouseEditInputs.SourceInputs(source);
            sourceBuilding = HouseBuilding.For(source);
            originalBuilding = sourceBuilding?.Data.Copy();
            var plans = originalBuilding == null ? new[] { source } : originalBuilding.Levels.Select(level => level.Plan);
            supportedPose = true;
            foreach (var plan in plans)
            {
                originals.Add(plan, plan.Data.Copy());
                originalKits.Add(plan, plan.GetComponent<HouseWallBuilder>()?.Data.Copy());
                originalWallFitInputs.Add(plan, HouseEditInputs.WallFitInputs(originals[plan], originalKits[plan]));
                supportedPose &= plan.HasSupportedTransform;
            }
            ResetGeometry();
        }

        internal void ResetGeometry()
        {
            Building = originalBuilding?.Copy();
            candidateSurfaces = null;
            // Only this plan can be published. Neighbours remain captured read-only
            // inputs; resetting a drag must not copy every other level again.
            candidatePlan = originals[source].Copy();
            Valid = false;
            DividerPartitionsFloor = false;
            RemovedOpenings.Clear();
            requireEditedFit = true;
        }
        // Stack-only gestures never mutate the isolated graphs or kit assignments.
        internal void ResetBuildingSettings()
        {
            Building = originalBuilding?.Copy();
            candidateSurfaces = null;
            Valid = false;
        }
        internal FloorPlan PlanFor(HouseStack.Level level) => level == null ? null : PlanFor(level.Plan);
        internal FloorPlan PlanFor(HousePlan plan) => plan == source ? candidatePlan : originals[plan];
        internal void UseSurfaceLayout(List<HouseSlabLayout.Patch> patches) => candidateSurfaces = patches;
        internal IReadOnlyList<HouseSlabLayout.Patch> SurfacePatches
            => Building == null ? null : candidateSurfaces ??= HouseSlabLayout.Patches(Building, PlanFor);
        internal bool ValidateCapturedSource(out string reason)
        {
            // These are origin diagnostics. Publication checks the complete live
            // source signature before using them; a changed source must be rejected.
            if (capturedSourceProblem == null) HouseEditValidation.Source(source, out capturedSourceProblem);
            reason = capturedSourceProblem;
            return reason.Length == 0;
        }

        internal bool CaptureMoveBaseline(out string reason)
        {
            // This depends only on the captured origin, not the release position.
            if (!ValidateCapturedSource(out reason)) return false;
            foreach (var plan in CapturedPlans) OriginalIssues(plan, HouseEditValidation.WallFitCheck.Complete);
            return true;
        }

        internal bool Validate(out string reason) => Validate(out reason, out _);
        internal bool Validate(out string reason, out Dictionary<HousePlan, HouseWallLayout.Result> layouts)
        {
            Valid = HouseEditValidation.Geometry(this, requireEditedFit, out reason, out layouts);
            return Valid;
        }
        public bool MoveNodes(IReadOnlyDictionary<string, Vector2> positions, out string reason)
            => PrepareMoveNodes(positions, out reason) && Validate(out reason);

        // Commands and release prepare once, then the publication boundary validates
        // the exact candidate. Public previews can still request full validation above.
        internal bool PrepareMoveNodes(IReadOnlyDictionary<string, Vector2> positions, out string reason)
        {
            ResetGeometry();
            if (!Plan.TryMoveCorners(positions, out reason)) return false;
            // Moving a storey's walls only reshapes its authored rooms. Exposed
            // coverage is a derived slab face, not an automatically created room.
            // Automatic connection selection reads heights; reconcile candidate slabs first.
            if (!PrepareSurfaces(out reason)) return false;
            RemoveDisplacedOpenings();
            return true;
        }
        internal bool AddWall(Vector2 start, Vector2 end, out bool endsOnWall, out string reason)
            => AddEdge(start, end, FloorPlan.PhysicalPresence.Wall, out endsOnWall, out reason);

        internal bool AddEdge(Vector2 start, Vector2 end, FloorPlan.PhysicalPresence presence, out bool endsOnWall, out string reason)
        {
            ResetGeometry();
            requireEditedFit = false; // Keep drawing usable with an incomplete kit.
            if (!Plan.TryAddEdge(start, end, presence, out endsOnWall, out var removed, out reason)) return false;
            RemovedOpenings.UnionWith(removed);
            // Automatic connection selection reads heights; reconcile candidate slabs first.
            if (!PrepareSurfaces(out reason)) return false;
            RemoveDisplacedOpenings();
            return Validate(out reason);
        }

        internal bool ReplaceFootprint(HousePlan sourcePlan, out string reason)
        {
            ResetGeometry();
            if (!Plan.TryReplaceFootprintFrom(sourcePlan.Data, out reason)) return false;
            return PrepareSurfaces(out reason) && Validate(out reason);
        }

        // A topology proposal prepares both adjoining slabs before any fit query.
        // Validation and publication only read these records; they never assign IDs.
        internal bool PrepareSurfaces(out string reason)
        {
            reason = "";
            if (Building == null) return true;
            foreach (var boundary in new[] { Building.Below(Level), Building.Above(Level) })
            {
                if (boundary == null) continue;
                if (!boundary.DividerNodes.Any(n => n.Attachment != null)) continue;
                if (!ResolveAttachments(boundary, null, out reason)
                    || !boundary.TryMoveDividerNodes(boundary.DividerNodes.ToDictionary(n => n.Id, n => n.Position), out reason)) return false;
            }
            return HouseSurfaceEdit.TryReconcileLevel(originalBuilding, Building, Level.Id,
                OriginalPlanFor, PlanFor, out reason);
        }

        internal bool AddDivider(string boundaryId, Vector2 start, Vector2 end, out string reason)
            => AddDividerStroke(boundaryId, new[] { start, end }, false, out reason);

        internal bool AddDividerStroke(string boundaryId, IReadOnlyList<Vector2> points, bool requirePartition, out string reason)
        {
            ResetGeometry();
            requireEditedFit = false; // A floor-only boundary must remain editable with an incomplete wall kit.
            var boundary = SurfaceBoundary(boundaryId);
            reason = "Choose a slab adjoining the active level.";
            if (boundary == null) return false;
            reason = "Draw at least two floor-divider points.";
            if (points == null || points.Count < 2) return false;
            var owner = Building.Find(boundary.UpperId) ?? Building.Find(boundary.LowerId);
            var floor = PlanFor(owner);
            var footprint = SlabGeometry.Union(new Clipper2Lib.PathsD(floor.Cells.SelectMany(room =>
                SlabGeometry.Shape(floor.GetCellBoundary(room)))));
            var usable = SlabGeometry.Union(new Clipper2Lib.PathsD(HouseSlabLayout.Patches(Building, PlanFor, boundaryId)
                .SelectMany(patch => SlabGeometry.VisibleShape(patch))));
            var original = originalBuilding.Boundaries.First(b => b.Id == boundaryId);
            var stroke = new List<(Vector2 a, Vector2 b)>();
            for (int index = 1; index < points.Count; index++)
            {
                var line = (a: points[index - 1], b: points[index]);
                if (!OutlineGeometry.ValidatePoints(new[] { line.a, line.b }, out reason)) return false;
                // Keep logical intervals beneath holes; only exterior remnants are
                // clipped. The entire chain remains one isolated candidate.
                foreach (var piece in SurfacePartition.ClipLine(line, footprint)
                    .Where(piece => SurfacePartition.ClipLine(piece, usable).Any()))
                {
                    if (!boundary.TryAddDivider(piece.a, piece.b, out reason)) return false;
                    HouseDividerAttachments.CaptureEndpoints(original, boundary, floor, owner.Id, piece.a, piece.b);
                    stroke.Add(piece);
                }
            }
            if (!ReconcileSurface(boundary, false, out reason) || !Validate(out reason)) return false;
            DividerPartitionsFloor = HouseSurfaceEdit.StrokePartitionsFloor(original, boundary,
                OriginalPlanFor(originalBuilding.Find(original.LowerId)), OriginalPlanFor(originalBuilding.Find(original.UpperId)), stroke);
            if (!requirePartition || DividerPartitionsFloor) return true;
            Valid = false;
            reason = "Finish the divider across a floor region or close a loop. Dangling strokes do not divide the floor.";
            return false;
        }
        internal bool MoveDividerNodes(string boundaryId, IReadOnlyDictionary<string, Vector2> positions, out string reason)
        {
            ResetGeometry();
            var boundary = SurfaceBoundary(boundaryId);
            reason = "Choose a slab adjoining the active level.";
            return boundary != null && ResolveAttachments(boundary, positions, out reason)
                && boundary.TryMoveDividerNodes(boundary.DividerNodes.ToDictionary(n => n.Id, n => n.Position), out reason)
                && ReconcileSurface(boundary, true, out reason) && Validate(out reason);
        }
        internal bool DeleteDividers(string boundaryId, IEnumerable<string> dividerIds, out string reason)
        {
            ResetGeometry();
            requireEditedFit = false;
            var boundary = SurfaceBoundary(boundaryId);
            reason = "Choose a slab adjoining the active level.";
            return boundary != null && boundary.TryRemoveDividers(dividerIds, out reason)
                && ResolveAttachments(boundary, null, out reason)
                && ReconcileSurface(boundary, false, out reason, retainConflictingDividers: false) && Validate(out reason);
        }
        internal bool InsertDividerNode(string boundaryId, string dividerId, Vector2 point, out string addedId, out string reason)
        {
            ResetGeometry();
            requireEditedFit = false;
            addedId = null;
            var boundary = SurfaceBoundary(boundaryId);
            reason = "Choose a divider on a slab adjoining the active level.";
            return boundary != null && boundary.TryInsertDividerNode(dividerId, point, out addedId, out reason)
                && ReconcileSurface(boundary, false, out reason) && Validate(out reason);
        }

        private HouseStack.Boundary SurfaceBoundary(string id) => Building?.Boundaries.FirstOrDefault(item => item.Id == id
            && (item.LowerId == Level.Id || item.UpperId == Level.Id));
        private FloorPlan SurfacePlan(string id) => Building.Find(id) is HouseStack.Level owner ? PlanFor(owner) : null;

        private bool ResolveAttachments(HouseStack.Boundary boundary, IReadOnlyDictionary<string, Vector2> requested, out string reason)
            => HouseDividerAttachments.Resolve(boundary, originalBuilding.Boundaries.First(b => b.Id == boundary.Id),
                SurfacePlan, id => OriginalPlanFor(originalBuilding.Find(id)), requested, out reason);
        internal bool ResolveCutAttachments(HouseStack.Boundary boundary, out string reason)
            => ResolveAttachments(boundary, null, out reason)
                && boundary.TryMoveDividerNodes(boundary.DividerNodes.ToDictionary(n => n.Id, n => n.Position), out reason);
        private bool ReconcileSurface(HouseStack.Boundary target, bool movingDividers, out string reason,
            bool retainConflictingDividers = true)
        {
            var original = originalBuilding.Boundaries.First(item => item.Id == target.Id);
            return HouseSurfaceEdit.TryReconcile(original, target, SurfacePlan(target.LowerId), SurfacePlan(target.UpperId),
                SurfacePlan(target.LowerId), SurfacePlan(target.UpperId), out reason, movingDividers, retainConflictingDividers);
        }

        private void RemoveDisplacedOpenings()
        {
            // Most layout drags contain no openings. Fitting two entire wall layouts
            // cannot remove anything in that case.
            if (!Plan.Walls.Any(wall => wall.Openings.Count > 0)) return;
            var oldPlan = originals[source];
            var oldSpans = OpeningSpans(oldPlan, originalKits[source]);
            var newSpans = OpeningSpans(Plan, builder);
            foreach (var wall in Plan.Walls)
                foreach (var opening in wall.Openings.ToArray())
                {
                    var oldWall = oldPlan.Walls.FirstOrDefault(w => w.Openings.Any(o => o.Id == opening.Id));
                    var oldOpening = oldWall?.Openings.First(o => o.Id == opening.Id);
                    // Do not erase existing kit problems. Only newly lost horizontal space
                    // (including a junction's clearance) is part of this geometry edit.
                    if (oldOpening == null || !HasOpeningSpace(oldPlan, oldWall, oldOpening, oldSpans)
                        || HasOpeningSpace(Plan, wall, opening, newSpans)) continue;
                    RemovedOpenings.Add(opening.Id);
                    Plan.TryRemoveOpening(wall.Id, opening.Id);
                }
        }

        private static HouseWallLayout.Result OpeningSpans(FloorPlan plan, WallKit builder)
        {
            if (builder == null) return null;
            var layout = HouseWallLayout.Calculate(plan, builder, false);
            // Obtain clearance from usable families even when another kit assignment is missing.
            return layout.IsValid ? layout : HouseWallLayout.CalculateRequiredWalls(plan, builder, false);
        }

        private static bool HasOpeningSpace(FloorPlan plan, FloorPlan.Wall wall, FloorPlan.Opening opening, HouseWallLayout.Result layout)
        {
            float length = Vector2.Distance(plan.GetPosition(wall.StartId), plan.GetPosition(wall.EndId));
            if (opening.Distance - opening.Width / 2 < -.0001f || opening.Distance + opening.Width / 2 > length + .0001f) return false;
            var span = layout?.IsValid == true ? layout.Spans.Find(s => s.WallId == wall.Id) : null;
            if (span == null) return true; // Missing kit data must not delete authored openings.
            var center = plan.GetPosition(wall.StartId) + (plan.GetPosition(wall.EndId) - plan.GetPosition(wall.StartId)).normalized * opening.Distance;
            float along = Vector2.Dot(center - span.Start, (span.End - span.Start).normalized);
            return along - opening.Width / 2 >= span.StartClearance - .0001f
                && along + opening.Width / 2 <= Vector2.Distance(span.Start, span.End) - span.EndClearance + .0001f;
        }
        public bool MoveOpenings(IReadOnlyDictionary<string, float> distances, out string reason)
        {
            ResetGeometry();
            return Plan.TryMoveOpenings(distances, out reason) && Validate(out reason);
        }
        public bool SetWallType(IEnumerable<string> ids, FloorPlan.WallType type, out string reason)
        {
            ResetGeometry();
            reason = "Choose a valid wall type.";
            if (!Enum.IsDefined(typeof(FloorPlan.WallType), type)) return false;
            var selected = ids.Distinct().ToArray();
            reason = "Choose existing walls.";
            if (selected.Length == 0 || selected.Any(id => !Plan.Walls.Any(w => w.Id == id))) return false;
            foreach (string id in selected)
                if (!Plan.TrySetWallType(Plan.Walls.ToList().FindIndex(w => w.Id == id), type)) return false;
            return Validate(out reason);
        }
        internal bool SetWallPresence(IEnumerable<string> ids, FloorPlan.PhysicalPresence presence, out string reason)
        {
            ResetGeometry();
            requireEditedFit = false;
            reason = "Choose existing wall or room-boundary edges.";
            if (!Plan.TrySetWallPresence(ids, presence)) return false;
            // Presence changes construction, never the graph or its room identities.
            return Validate(out reason);
        }
        internal bool SetRoomOutdoor(IEnumerable<string> ids, bool outdoor, out string reason)
        {
            ResetGeometry();
            requireEditedFit = false;
            reason = "Choose existing rooms.";
            if (!Plan.TrySetRoomOutdoor(ids, outdoor)) return false;
            return PrepareSurfaces(out reason) && Validate(out reason);
        }
        public bool InsertNode(string wallId, float fraction, out string reason)
        {
            ResetGeometry();
            int index = Plan.Walls.ToList().FindIndex(w => w.Id == wallId);
            return Plan.TryInsertCorner(index, fraction, out reason) && PrepareSurfaces(out reason) && Validate(out reason);
        }
        public bool SetOpeningKind(IEnumerable<string> ids, FloorPlan.OpeningKind kind, out string reason)
        {
            ResetGeometry(); reason = "Choose a valid opening type.";
            if (!Enum.IsDefined(typeof(FloorPlan.OpeningKind), kind)) return false;
            reason = "Assign a compatible module for this opening type.";
            foreach (string id in ids)
            {
                var wall = Plan.Walls.FirstOrDefault(w => w.Openings.Any(o => o.Id == id));
                if (wall == null || builder == null) return false;
                var opening = wall.Openings.First(o => o.Id == id);
                var module = HouseWallLayout.OpeningFor(Plan, wall, builder, kind, opening.Width, opening.Shape);
                reason = $"Assign a {opening.Width:0.####} m {kind.ToString().ToLowerInvariant()} segment. Changing opening type must retain the saved segment width.";
                if (module == null || !Plan.TryChangeOpening(wall.Id, id, kind, opening.Width, out reason)) return false;
            }
            return Validate(out reason);
        }
        public bool Commit(string action, out string reason)
        {
            var result = HouseEditCommands.CommitPreview(this, action);
            reason = result.Message;
            return result.Applied;
        }
        // Match each native adapter's serialized envelope. Overwrite deserializes fresh
        // owned lists, so a retained candidate cannot mutate accepted data after publication.
        internal string PlanJson() => "{\"data\":" + JsonUtility.ToJson(Plan) + "}";
        internal string BuildingJson() => "{\"data\":" + JsonUtility.ToJson(Building) + "}";
        public void Dispose() { disposed = true; }
    }
}
