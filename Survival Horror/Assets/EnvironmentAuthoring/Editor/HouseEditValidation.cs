using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Editing may retain an existing kit problem, but must not introduce another one or
    // lower a neighbour's ceiling through an opening. Compare logical diagnostics, not output.
    internal static class HouseEditValidation
    {
        internal enum WallFitCheck { None, RequiredFamilies, Complete }
        internal static bool Source(HousePlan plan, out string reason)
        {
            reason = "Choose an existing House plan.";
            if (plan == null) return false;
            var building = HouseBuilding.For(plan);
            reason = "Choose a registered House level. Create a House from the Environment Tools palette or GameObject > Environment > House.";
            if (building == null) return false;
            if (!building.TryValidateLayout(out reason)) return false;
            if (!HouseSlabLayout.TryPatches(building.Data, level => level.Plan.Data, out _, out reason)) return false;
            reason = ""; return true;
        }
        internal static bool Geometry(HouseEditPreview preview, bool requireEditedFit, out string reason)
            => Geometry(preview, requireEditedFit, out reason, out _);

        internal static bool Geometry(HouseEditPreview preview, bool requireEditedFit, out string reason,
            out Dictionary<HousePlan, HouseWallLayout.Result> layouts)
        {
            layouts = new Dictionary<HousePlan, HouseWallLayout.Result>();
            // Draw/delete can use an incomplete kit. Drawing candidates remove openings
            // that newly lose horizontal space; neighbour clearance is still mandatory.
            var plan = preview.Plan;
            reason = "Keep the House upright at unit scale.";
            if (!preview.HasSupportedTransform) return false;
            // Recheck the current candidate, including mutations made after preview validation.
            if (!plan.TryMoveCorners(plan.Corners.ToDictionary(c => c.Id, c => c.Position), out reason)) return false;
            if (preview.Building == null)
                return Plan(preview, preview.Source, requireEditedFit, layouts, out reason);
            if (!preview.Building.TryValidateLayout(out reason)) return false;
            if (!HouseSlabLayout.TryPatches(preview.Building, preview.PlanFor, out var patches, out reason)) return false;
            // Share this exact validated interpretation across wall checks. A later
            // publication validates again, so direct candidate edits cannot reuse it.
            preview.UseSurfaceLayout(patches);
            foreach (var captured in preview.CapturedPlans)
                if (!Plan(preview, captured, captured != preview.Source || requireEditedFit, layouts, out reason)) return false;
            reason = "";
            return true;
        }

        private static bool Plan(HouseEditPreview preview, HousePlan source, bool checkFit,
            Dictionary<HousePlan, HouseWallLayout.Result> layouts, out string reason)
        {
            var policy = checkFit ? WallFitCheck.Complete : WallFitCheck.RequiredFamilies;
            // Capture the baseline even for unchanged neighbours. A later preview
            // update must compare a newly invalid live mesh with this earlier result.
            var original = preview.OriginalIssues(source, policy);
            // Other storeys borrow their exact captured graph. If their height and
            // effective trim default also match, they cannot introduce a new issue.
            // The publication owner still checks live source/kit freshness first.
            if (ReferenceEquals(preview.OriginalPlan(source), preview.PlanFor(source))
                && preview.HasOriginalWallFitInputs(source)
                && preview.OriginalBuilding?.WallHeight(preview.OriginalBuilding.Find(source))
                    == preview.Building?.WallHeight(preview.Building.Find(source))
                && JsonUtility.ToJson(HouseTrimLayout.Default(preview.OriginalKit(source), preview.OriginalBuilding))
                    == JsonUtility.ToJson(HouseTrimLayout.Default(preview.OriginalKit(source), preview.Building)))
            { reason = ""; return true; }
            // Establish baseline diagnostics before interpreting a proposal. Native
            // assets stay live, so an unchanged graph still compares its fit inputs.
            var fit = preview.HasOriginalWallFitInputs(source) ? WallFitCheck.None : policy;
            var issues = Issues(preview.PlanFor(source), preview.OriginalKit(source),
                preview.Building?.WallHeight(preview.Building.Find(source)), fit, out var layout,
                HouseTrimLayout.Default(preview.OriginalKit(source), preview.Building));
            if (fit == WallFitCheck.Complete && layout != null) layouts.Add(source, layout);
            return PreservesIssues(original, issues, out reason);
        }

        internal static Dictionary<string, string> OriginalIssues(HouseEditPreview preview, HousePlan source, WallFitCheck fit)
            => Issues(preview.OriginalPlan(source), preview.OriginalKit(source),
                preview.OriginalBuilding?.WallHeight(preview.OriginalBuilding.Find(source)), fit, out _, HouseTrimLayout.Default(preview.OriginalKit(source), preview.OriginalBuilding));

        // Scalar level/slab gestures leave horizontal graphs fixed. The stack owns
        // minimum headroom; only openings on changed-height levels need a second rule.
        internal static bool UniformHeights(HouseEditPreview preview, out string reason)
        {
            if (!preview.Building.TryValidateLayout(out reason)) return false;
            foreach (var source in preview.CapturedPlans)
            {
                float before = preview.OriginalBuilding.WallHeight(preview.OriginalBuilding.Find(source));
                float after = preview.Building.WallHeight(preview.Building.Find(source));
                if (before == after) continue;
                if (!PreservesIssues(OpeningHeightIssues(preview.OriginalPlan(source), preview.OriginalKit(source), before),
                    OpeningHeightIssues(preview.OriginalPlan(source), preview.OriginalKit(source), after), out reason)) return false;
            }
            reason = "";
            return true;
        }

        private static bool PreservesIssues(IReadOnlyDictionary<string, string> original,
            Dictionary<string, string> candidate, out string reason)
        {
            foreach (var issue in candidate)
            {
                if (original.TryGetValue(issue.Key, out var before) && before == issue.Value) continue;
                reason = issue.Value;
                return false;
            }
            reason = "";
            return true;
        }

        private static Dictionary<string, string> Issues(FloorPlan plan, WallKit builder,
            float? levelHeight, WallFitCheck fit, out HouseWallLayout.Result layout, WallTrim defaults = null)
        {
            var result = new Dictionary<string, string>();
            // Interpret once when fitting or attached trims need the chosen connections.
            // An incomplete kit still needs a usable-family result for trims/diagnostics.
            bool needsLayout = fit != WallFitCheck.None || plan.Walls.Any(w => HouseTrimLayout.Resolve(w, defaults)?.Mesh != null);
            layout = builder == null || !needsLayout ? null : fit == WallFitCheck.Complete
                ? HouseWallLayout.Calculate(plan, builder)
                : HouseWallLayout.CalculateRequiredWalls(plan, builder);
            var requiredLayout = layout;
            if (layout != null && fit == WallFitCheck.Complete && !layout.IsValid)
                requiredLayout = HouseWallLayout.CalculateRequiredWalls(plan, builder);
            foreach (var wall in plan.Walls)
            {
                string trimProblem;
                if (HouseWallLayout.DefinitionFor(plan, wall, builder) != null)
                    HouseTrimLayout.TryPieces(plan, wall, builder, out _, out trimProblem, requiredLayout, defaults);
                else trimProblem = HouseTrimLayout.Problem(plan, wall, defaults);
                if (trimProblem.Length > 0) result["trim:" + wall.Id] = trimProblem;
            }
            if (fit != WallFitCheck.None && builder != null)
            {
                // An incomplete kit may permit drawing, but a usable wall family
                // must never accept a new fit failure that hides all wall output.
                if (!layout.IsValid)
                {
                    result[(fit == WallFitCheck.Complete ? "fit:" : "required-fit:") + string.Join("|", layout.ProblemPoints.Select(p => p.ToString("R")).OrderBy(p => p))] = layout.Problem;
                    // Invalid family metadata may stop before examining any wall. Retain its
                    // existing warning, but independently reject new fits in usable wall types.
                    if (requiredLayout != layout && !requiredLayout.IsValid)
                        result["required-fit:" + string.Join("|", requiredLayout.ProblemPoints.Select(p => p.ToString("R")).OrderBy(p => p))] = requiredLayout.Problem;
                }
            }
            // Models do not define wall fitting or collision, but incompatible model edits still reject publication.
            foreach (var wall in plan.Walls)
                foreach (var opening in wall.Openings)
                {
                    string insertProblem = HouseWallLayout.InsertProblem(plan, wall, builder, opening);
                    if (insertProblem.Length > 0) result["insert:" + opening.Id] = insertProblem;
                }
            foreach (var issue in OpeningHeightIssues(plan, builder, levelHeight)) result.Add(issue.Key, issue.Value);
            return result;
        }

        private static Dictionary<string, string> OpeningHeightIssues(FloorPlan plan, WallKit builder, float? levelHeight)
        {
            var result = new Dictionary<string, string>();
            if (!levelHeight.HasValue || builder == null) return result;
            foreach (var wall in plan.Walls.Where(w => w.IsPhysical))
                foreach (var opening in wall.Openings)
                {
                    var definition = HouseWallLayout.OpeningFor(plan, wall, builder, opening);
                    if (definition != null && levelHeight.Value < definition.OpeningOutline.yMax + .01f)
                        result["opening:" + opening.Id + ":" + levelHeight.Value.ToString("R")] = "The level ceiling intersects a door/window opening. Increase the level height or move/remove that opening.";
                }
            return result;
        }
    }
}
