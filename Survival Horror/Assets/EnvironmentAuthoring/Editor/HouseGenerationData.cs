using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // One synchronous build pass shares its interpretation. This is not a second saved model.
    // Lazy domains preserve scoped Refresh APIs: collision-only refresh does not build slabs.
    internal sealed class HouseGenerationData
    {
        private IReadOnlyDictionary<HousePlan, HouseWallLayout.Result> validatedLayouts;
        private string booleanInputs;
        internal string BooleanInputs => booleanInputs ??= BooleanSignature(Building);
        internal static string BooleanSignature(HouseBuilding building) => building == null || building.Data.BooleanVolumes.Count == 0 ? ""
            : "boolean-v5|" + string.Join("|", building.Data.BooleanVolumes.Select(volume => JsonUtility.ToJson(volume)));
        internal HouseBuilding Building { get; }
        internal bool HasOutputChanges { get; set; }
        private readonly Dictionary<HousePlan, HouseWallLayout.Result> wallLayouts = new Dictionary<HousePlan, HouseWallLayout.Result>();
        private readonly Dictionary<(HousePlan, string, Vector2, Vector2), List<HouseHeightWalls.Section>> heightSections
            = new Dictionary<(HousePlan, string, Vector2, Vector2), List<HouseHeightWalls.Section>>();
        private readonly Dictionary<(HousePlan, Vector2, Vector2), List<HouseHeightWalls.Section>> logicalSections
            = new Dictionary<(HousePlan, Vector2, Vector2), List<HouseHeightWalls.Section>>();
        private readonly Dictionary<HouseStack.Level, Clipper2Lib.PathsD> ceilingCoverage = new Dictionary<HouseStack.Level, Clipper2Lib.PathsD>();
        private readonly Dictionary<HouseStack.Boundary, Clipper2Lib.PathsD> slabCuts = new Dictionary<HouseStack.Boundary, Clipper2Lib.PathsD>();
        private readonly Dictionary<HouseSlabLayout.Patch, Clipper2Lib.PathsD> visibleShapes = new Dictionary<HouseSlabLayout.Patch, Clipper2Lib.PathsD>();
        private List<HouseSlabLayout.Patch> patches;
        private string slabProblem;
        private string slabInputs, sharedWallHeightInputs;
        private readonly Dictionary<HousePlan, string> wallHeightInputs = new Dictionary<HousePlan, string>();
        private readonly Dictionary<WallModuleDefinition, string> kitSignatures = new Dictionary<WallModuleDefinition, string>();
        private readonly Dictionary<Mesh, string> meshSignatures = new Dictionary<Mesh, string>();

        private HouseGenerationData(HouseBuilding building) => Building = building;
        internal static HouseGenerationData Create(HousePlan plan) => new HouseGenerationData(HouseBuilding.For(plan));
        internal static HouseGenerationData Create(HouseBuilding building) => new HouseGenerationData(building);

        internal static HouseGenerationData ForPublication(HouseEditPreview accepted,
            IReadOnlyDictionary<HousePlan, HouseWallLayout.Result> layouts)
        {
            // Only the final exact validation hands off this read. Rebind copied
            // logical owners after the source write; shapes/finishes are unchanged.
            // No candidate result survives this synchronous publication as a cache.
            var data = Create(accepted.SourceBuilding);
            data.validatedLayouts = layouts;
            data.patches = accepted.SurfacePatches.Select(patch => new HouseSlabLayout.Patch
            {
                Boundary = data.Building.Boundaries.First(boundary => boundary.Id == patch.Boundary.Id),
                Lower = data.Building.Find(patch.Lower?.Id), Upper = data.Building.Find(patch.Upper?.Id),
                LowerRoom = patch.LowerRoom, UpperRoom = patch.UpperRoom, Floor = patch.Floor, Ceiling = patch.Ceiling,
                Shape = patch.Shape, WalkingHeight = patch.WalkingHeight
            }).ToList();
            data.slabProblem = "";
            return data;
        }

        internal List<HouseSlabLayout.Patch> Patches { get { ReadSurfaces(); return patches; } }
        internal string SlabProblem { get { ReadSurfaces(); return slabProblem; } }

        internal Clipper2Lib.PathsD VisibleShape(HouseSlabLayout.Patch patch)
        {
            if (!visibleShapes.TryGetValue(patch, out var shape))
            {
                // Floor and ceiling read the same cut patch. Both reads belong to
                // this pass; neither should rerun the boundary's polygon operations.
                if (!slabCuts.TryGetValue(patch.Boundary, out var cuts))
                    slabCuts.Add(patch.Boundary, cuts = SlabGeometry.Cuts(patch.Boundary));
                visibleShapes.Add(patch, shape = SlabGeometry.VisibleShape(patch, cuts));
            }
            return shape;
        }


        private void ReadSurfaces()
        {
            if (patches != null) return;
            if (Building != null && !Building.TryValidateLayout(out slabProblem))
            {
                patches = new List<HouseSlabLayout.Patch>();
                return;
            }
            HouseSlabLayout.TryPatches(Building?.Data, level => level.Plan?.Data, out patches, out slabProblem);
        }
        // Builders change derived output only. Reuse source signatures within this pass;
        // the next refresh gets a new snapshot and rereads imported or edited kit assets.
        internal string SlabInputs => slabInputs ?? (slabInputs = Building == null
            ? "" : HouseInputSignatures.SlabOutputInputs(Building, this));

        internal string SharedWallHeightInputs => sharedWallHeightInputs ?? (sharedWallHeightInputs =
            HouseInputSignatures.SharedWallHeightInputs(Building));

        internal string WallHeightInputs(HousePlan plan)
        {
            if (!wallHeightInputs.TryGetValue(plan, out string inputs))
                // Keep malformed shared surface data diagnostic even when the bad
                // boundary is outside this wall's ordinary height dependencies.
                wallHeightInputs.Add(plan, inputs = HouseInputSignatures.SharedWallHeightInputs(Building, plan) + SlabProblem);
            return inputs;
        }

        internal string KitSignature(WallModuleDefinition definition)
        {
            if (definition == null) return HouseInputSignatures.KitSignature(null);
            if (!kitSignatures.TryGetValue(definition, out string signature))
                kitSignatures.Add(definition, signature = HouseInputSignatures.KitSignature(definition, this));
            return signature;
        }

        internal string MeshSignature(Mesh mesh)
        {
            if (mesh == null) return GeneratedMeshResources.SourceSignature(null);
            if (!meshSignatures.TryGetValue(mesh, out string signature))
                meshSignatures.Add(mesh, signature = GeneratedMeshResources.SourceSignature(mesh));
            return signature;
        }

        internal HouseWallLayout.Result WallLayout(HousePlan plan, HouseWallBuilder builder)
        {
            if (wallLayouts.TryGetValue(plan, out var retained)) return retained;
            var result = validatedLayouts != null && validatedLayouts.TryGetValue(plan, out var validated)
                ? validated : HouseWallLayout.Calculate(plan.Data, builder.Data);
            if (result.IsValid)
            {
                string heightProblem = HouseHeightWalls.Validate(plan, result, this);
                if (heightProblem.Length > 0) result.Problem = heightProblem;
            }
            if (!plan.HasSupportedTransform)
                result.Problem = "Keep the House upright at world scale (1, 1, 1).";
            wallLayouts.Add(plan, result);
            return result;
        }

        internal List<HouseHeightWalls.Section> HeightSections(HousePlan plan, Vector2 start, Vector2 end)
        {
            var key = (plan, start, end);
            if (!logicalSections.TryGetValue(key, out var result))
            {
                result = HouseHeightWalls.Sections(Building?.Data, Building?.Find(plan), start, end,
                    plan.GetComponent<HouseWallBuilder>()?.Data.DefaultHeight ?? 2.8f, CeilingCoverage(plan));
                logicalSections.Add(key, result);
            }
            return result;
        }

        internal Clipper2Lib.PathsD CeilingCoverage(HousePlan plan)
        {
            var level = Building?.Find(plan);
            if (level == null) return new Clipper2Lib.PathsD();
            if (!ceilingCoverage.TryGetValue(level, out var shape))
                // A storey has one ceiling height. Room/material seams subdivide
                // finishes, but cannot introduce wall-height cuts inside its coverage.
                ceilingCoverage.Add(level, shape = SlabGeometry.Union(new Clipper2Lib.PathsD(
                    Patches.Where(patch => patch.Lower == level).SelectMany(patch => patch.Shape))));
            return shape;
        }

        internal List<HouseHeightWalls.Section> HeightSections(HousePlan plan, HouseWallLayout.Span span)
        {
            var key = (plan, span.WallId, span.Start, span.End);
            if (!heightSections.TryGetValue(key, out var result))
            {
                if (span.Definition.KeepNativeHeight)
                {
                    result = new List<HouseHeightWalls.Section> { new HouseHeightWalls.Section {
                        From = 0, To = 1, Top = span.Definition.Height, CoverageTop = span.Definition.Height } };
                    heightSections.Add(key, result);
                    return result;
                }
                var wall = plan.Data.Walls.First(w => w.Id == span.WallId);
                var start = plan.Data.GetPosition(wall.StartId);
                var end = plan.Data.GetPosition(wall.EndId);
                var logical = HeightSections(plan, start, end);
                result = HouseHeightWalls.FitSections(logical, start, end, span.Start, span.End);
                heightSections.Add(key, result);
            }
            return result;
        }
    }
}
