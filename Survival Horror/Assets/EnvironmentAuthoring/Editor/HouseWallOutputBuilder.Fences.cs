using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseWallOutputBuilder
    {
        internal static string FenceInputs(FloorPlan.Wall wall, HouseGenerationData data = null)
        {
            var family = wall.Fence;
            if (family == null) return "";
            var panel = family.Panel;
            var post = family.DefaultPost;
            string Mesh(Mesh mesh) => data?.MeshSignature(mesh) ?? GeneratedMeshResources.SourceSignature(mesh);
            string Materials(Material[] materials) => string.Join(",", (materials ?? System.Array.Empty<Material>())
                .Select(HouseInputSignatures.AssetIdentity));
            return "house-fence:" + HouseInputSignatures.AssetIdentity(family)
                + ":" + (panel == null ? "missing-panel" : HouseInputSignatures.AssetIdentity(panel)
                    + ":" + panel.Kind + ":" + panel.AllowStretch + ":" + new Vector3(panel.Length, panel.Height, panel.Width).ToString("R")
                    + Mesh(panel.Mesh) + Materials(panel.Materials))
                + ":post:" + (post == null ? "none" : Mesh(post.Mesh) + Materials(post.Materials));
        }

        private static void UpdateFenceGroups(HouseWallOutput output, Dictionary<string, HouseWallOutput.Group> previous,
            List<HouseWallOutput.Group> next, HouseWallBuilder builder, HouseGenerationData data)
        {
            var plan = builder.GetComponent<HousePlan>().Data;
            foreach (var wall in plan.Walls.Where(w => w.IsPhysical && w.IsFence))
            {
                var a = plan.GetPosition(wall.StartId); var b = plan.GetPosition(wall.EndId);
                var delta = b - a;
                string key = "wall:" + wall.Id;
                // Fences keep authored finishes unless this edge explicitly overrides them.
                string inputs = FenceInputs(wall, data) + ":" + delta.ToString("R")
                    + ":" + HouseInputSignatures.AssetIdentity(wall.LeftMaterial)
                    + ":" + HouseInputSignatures.AssetIdentity(wall.RightMaterial);
                if (RetainEditedGroup(output, previous, next, key, inputs)) continue;
                var group = RetainOrCreate(output, previous, key, inputs, out bool changed);
                group.Root.SetActive(true);
                if (changed)
                {
                    var parts = new List<TownRebuild.Part>();
                    TownWallBuilder.BuildFence(wall.Id, wall.Fence, Vector3.zero, new Vector3(delta.x, 0, delta.y),
                        wall.LeftMaterial, wall.RightMaterial, false, parts);
                    foreach (var part in parts)
                    {
                        var child = new GameObject(part.Key);
                        child.transform.SetParent(group.Root.transform, false);
                        child.transform.localPosition = part.Position;
                        child.transform.localRotation = part.Rotation;
                        child.transform.localScale = part.Scale;
                        child.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                        child.AddComponent<MeshRenderer>().sharedMaterials = part.Materials;
                        var source = child.AddComponent<TownOutputPart>();
                        source.Key = part.Key; source.Source = part.Mesh;
                    }
                }
                group.Root.transform.localPosition = new Vector3(a.x, 0, a.y);
                next.Add(group);
            }
        }
    }
}
