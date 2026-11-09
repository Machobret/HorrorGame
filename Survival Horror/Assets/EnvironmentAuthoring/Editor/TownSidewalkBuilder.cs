using System.Collections.Generic;

namespace EnvironmentAuthoring.Editor
{
    internal static class TownSidewalkBuilder
    {
        internal static void Build(TownSidewalk walk, List<TownRebuild.Part> parts)
        {
            var shape = TownPathGeometry.Footprint(walk.Path, walk.Width / 2, walk.Width / 2);
            var mesh = TownPathGeometry.Surface(walk.Path, shape, walk.Thickness, walk.MetresPerRepeat);
            if (mesh != null) parts.Add(new TownRebuild.Part { Key = "sidewalk", Mesh = mesh,
                Materials = new[] { walk.Material }, Collision = walk.GenerateCollision });
            if (walk.Curbs) TownCurbBuilder.Build(walk.Path, shape, 0, walk.CurbHeight, walk.MetresPerRepeat,
                walk.CurbMaterial != null ? walk.CurbMaterial : walk.Material, walk.GenerateCollision, parts);
        }
    }
}
