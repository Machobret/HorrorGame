using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Editing groups only. Atomic records still own both inherited room
    // finishes and output identities; no additional footprint or group ID is saved.
    internal static class HouseSurfaceAreas
    {
        internal sealed class Area
        {
            internal string UpperRoom;
            internal string RegionId;
            internal PathsD Shape;
        }

        internal static bool TryRead(HouseStack.Boundary boundary, FloorPlan lower, FloorPlan upper,
            out List<Area> areas, out string reason)
        {
            areas = null;
            if (!HouseSurfaceLayout.TryRead(boundary, lower, upper, out var faces, out reason)) return false;
            areas = faces.Where(face => !face.Ceiling).Select(face => AreaFor(face.RoomId, face.Record, face.Shape)).ToList();
            return true;
        }

        // Cached output patches may subdivide one authored region. Join by its ID,
        // never by finish similarity or opposite-room correspondence.
        internal static List<Area> FromPatches(HouseStack.Boundary boundary, IReadOnlyList<HouseSlabLayout.Patch> patches)
            => patches.Where(patch => patch.Boundary.Id == boundary.Id).GroupBy(patch => patch.Floor.Id)
                .Select(group => AreaFor(group.First().UpperRoom, group.First().Floor,
                    SlabGeometry.Union(new PathsD(group.SelectMany(patch => patch.Shape))))).ToList();

        private static Area AreaFor(string roomId, HouseStack.SurfaceRegion region, PathsD shape)
            => new Area { UpperRoom = roomId, RegionId = region.Id, Shape = shape };

    }
}
