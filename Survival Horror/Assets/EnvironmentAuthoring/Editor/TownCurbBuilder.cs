using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Curbs are a border of the final joined sidewalk, not one strip per span.
    // The border keeps its own height and output mesh above the walk surface.
    internal static class TownCurbBuilder
    {
        internal static void Build(TownPath path, PathsD sidewalk, float elevation, float height, float repeat,
            Material material, bool collision, List<TownRebuild.Part> parts)
        {
            var inset = Clipper.InflatePaths(sidewalk, -.12, JoinType.Miter, EndType.Polygon, precision: 6);
            var border = SlabGeometry.Subtract(sidewalk, inset);
            var mesh = TownPathGeometry.Surface(path, border, height, repeat, elevation + height);
            if (mesh != null) parts.Add(new TownRebuild.Part { Key = "curbs", Mesh = mesh,
                Materials = new[] { material }, Collision = collision });
        }
    }
}
