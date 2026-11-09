using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace EnvironmentAuthoring.Editor
{
    // Accepted pre-cut segments are shared by output, picking and vertical validation.
    internal static class HouseSlabLayout
    {
        internal sealed class Patch
        {
            public HouseStack.Boundary Boundary;
            public HouseStack.Level Lower, Upper;
            public string LowerRoom, UpperRoom;
            public HouseStack.SurfaceRegion Floor, Ceiling;
            public HouseStack.SurfaceRegion Region(bool ceiling) => ceiling ? Ceiling : Floor;
            // Output subdivisions have no authored identity of their own.
            public string Key => Floor.Id + ":" + Ceiling.Id;
            public PathsD Shape;
            public float WalkingHeight;
        }

        // One headroom measurement per ceiling patch; material regions do not change height.
        internal sealed class ClearanceSlice
        {
            public Patch Surface;
            public PathsD Shape;
            public float FloorHeight, CeilingHeight;
            public float Height => CeilingHeight - FloorHeight;
        }

        internal static List<Patch> Patches(HouseBuilding building, string boundaryId = null)
            => Patches(building.Data, level => level.Plan?.Data, boundaryId);

        internal static List<Patch> Patches(HouseStack building, Func<HouseStack.Level, FloorPlan> planFor, string boundaryId = null)
        {
            if (!TryPatches(building, planFor, out var patches, out string reason, boundaryId))
                throw new InvalidOperationException(reason);
            return patches;
        }

        internal static bool TryPatches(HouseStack building, Func<HouseStack.Level, FloorPlan> planFor,
            out List<Patch> patches, out string reason, string boundaryId = null)
        {
            patches = new List<Patch>();
            reason = "";
            if (building == null) return true;
            if (boundaryId != null && !building.Boundaries.Any(boundary => boundary.Id == boundaryId))
            {
                reason = "The slab boundary no longer exists.";
                return false;
            }
            foreach (var boundary in building.Boundaries)
            {
                if (boundaryId != null && boundary.Id != boundaryId) continue;
                var lower = building.Find(boundary.LowerId);
                var upper = building.Find(boundary.UpperId);
                var lowerPlan = lower == null ? null : planFor(lower);
                var upperPlan = upper == null ? null : planFor(upper);
                if ((lower == null && upper == null)
                    || (!string.IsNullOrEmpty(boundary.LowerId) && lower == null)
                    || (!string.IsNullOrEmpty(boundary.UpperId) && upper == null)
                    || (lower != null && lowerPlan == null) || (upper != null && upperPlan == null))
                {
                    reason = "Each slab boundary needs its adjoining level layouts.";
                    patches.Clear();
                    return false;
                }
                float nominalTop = HouseVerticalLayout.BoundaryTop(building, lower, upper);
                if (!float.IsFinite(nominalTop))
                {
                    reason = "Keep finite level elevations before interpreting slab segments.";
                    patches.Clear();
                    return false;
                }
                if (!HouseSurfaceLayout.TryRead(boundary, lowerPlan, upperPlan, out var faces, out reason))
                {
                    // A failed read must not expose a partly interpreted House to a builder.
                    patches.Clear();
                    return false;
                }
                if (!TrySurfaceHeight(building, lower, upper, out float walkingHeight, out reason))
                { patches.Clear(); return false; }
                foreach (var floor in faces.Where(face => !face.Ceiling))
                    foreach (var ceiling in faces.Where(face => face.Ceiling))
                    {
                        var shape = SlabGeometry.Intersect(floor.Shape, ceiling.Shape);
                        if (shape.Count == 0) continue;
                        // Outdoor classification affects physical roof coverage only.
                        if (floor.RoomId == null && lowerPlan?.Cells.FirstOrDefault(room => room.Id == ceiling.RoomId)?.IsOutdoor == true)
                            continue;
                        patches.Add(new Patch {
                            Boundary = boundary, Lower = lower, Upper = upper,
                            LowerRoom = ceiling.RoomId, UpperRoom = floor.RoomId,
                            Floor = floor.Record, Ceiling = ceiling.Record, Shape = shape,
                            WalkingHeight = walkingHeight
                        });
                    }
            }
            return true;
        }

        // Validate the actual derived slab planes before exposing patches to
        // queries/output. Uniform minimum headroom belongs to HouseStack.
        internal static bool TrySurfaceHeight(HouseStack building, HouseStack.Level lower, HouseStack.Level upper,
            out float walkingHeight, out string reason)
        {
            walkingHeight = HouseVerticalLayout.BoundaryTop(building, lower, upper);
            var slab = HouseVerticalLayout.SlabAt(building, walkingHeight);
            reason = "Keep finite finished-floor, ceiling and slab heights.";
            if (!float.IsFinite(walkingHeight) || !float.IsFinite(slab.Bottom) || !float.IsFinite(slab.FinishBottom)
                || (lower != null && !float.IsFinite(slab.Bottom - HouseVerticalLayout.FloorSurface(building, lower)))
                || (upper != null && !float.IsFinite(walkingHeight - HouseVerticalLayout.FloorSurface(building, upper)))) return false;
            reason = "";
            return true;
        }

        internal static List<ClearanceSlice> Clearances(HouseStack building, Func<HouseStack.Level, FloorPlan> planFor,
            IReadOnlyList<Patch> patches = null)
        {
            if (!TryClearances(building, planFor, out var clearances, out string reason, patches))
                throw new InvalidOperationException(reason);
            return clearances;
        }

        internal static bool TryClearances(HouseStack building, Func<HouseStack.Level, FloorPlan> planFor,
            out List<ClearanceSlice> clearances, out string reason, IReadOnlyList<Patch> patches = null)
        {
            clearances = new List<ClearanceSlice>();
            reason = "";
            if (patches == null)
            {
                if (!TryPatches(building, planFor, out var interpreted, out reason)) return false;
                patches = interpreted;
            }
            foreach (var ceiling in patches)
            {
                if (ceiling.LowerRoom == null) continue;
                float floor = HouseVerticalLayout.FloorSurface(building, ceiling.Lower);
                float underside = HouseVerticalLayout.SlabAt(building, ceiling.WalkingHeight).Bottom;
                if (!float.IsFinite(underside - floor))
                { reason = "Keep finite level heights."; clearances.Clear(); return false; }
                clearances.Add(new ClearanceSlice { Surface = ceiling, Shape = ceiling.Shape,
                    FloorHeight = floor, CeilingHeight = underside });
            }
            return true;
        }
    }
}
