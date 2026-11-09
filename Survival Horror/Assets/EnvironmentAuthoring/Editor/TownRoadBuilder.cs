using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class TownRoadBuilder
    {
        internal static void Build(TownRoad road, List<TownRebuild.Part> parts)
        {
            var body = TownPathGeometry.Footprint(road.Path, road.Width / 2, road.Width / 2);
            var mesh = TownPathGeometry.Surface(road.Path, body, road.Thickness, road.MetresPerRepeat);
            if (mesh != null) parts.Add(new TownRebuild.Part { Key = "road", Mesh = mesh,
                Materials = new[] { road.Material }, Collision = road.GenerateCollision });
            if (!road.LeftSidewalk && !road.RightSidewalk) return;
            var outside = TownPathGeometry.Footprint(road.Path,
                road.Width / 2 + (road.LeftSidewalk ? road.SidewalkWidth : 0),
                road.Width / 2 + (road.RightSidewalk ? road.SidewalkWidth : 0));
            // Subtract the whole roadway, not each span, so curbs never cross
            // a T/X intersection or leave stacked surfaces inside a bend.
            var walks = SlabGeometry.Subtract(outside, body);
            mesh = TownPathGeometry.Surface(road.Path, walks, road.Thickness + road.SidewalkHeight,
                road.MetresPerRepeat, road.SidewalkHeight);
            if (mesh != null) parts.Add(new TownRebuild.Part { Key = "road-sidewalks", Mesh = mesh,
                Materials = new[] { road.SidewalkMaterial }, Collision = road.GenerateCollision });
            if (road.Curbs) TownCurbBuilder.Build(road.Path, walks, road.SidewalkHeight, road.CurbHeight, road.MetresPerRepeat,
                road.CurbMaterial != null ? road.CurbMaterial : road.SidewalkMaterial, road.GenerateCollision, parts);
        }
    }
}
