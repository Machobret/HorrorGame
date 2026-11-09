using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseWallOutputBuilder
    {
        private static HashSet<string> UpdateTrimGroups(HouseWallOutput output, Dictionary<string, HouseWallOutput.Group> previous,
            List<HouseWallOutput.Group> next, HouseWallBuilder builder, HouseGenerationData data)
        {
            var plan = builder.GetComponent<HousePlan>();
            var failed = new HashSet<string>();
            foreach (var wall in plan.Data.Walls.Where(w => HouseTrimLayout.Resolve(plan, w)?.Mesh != null && HouseTrimLayout.Eligible(plan.Data, w)))
            {
                var trim = HouseTrimLayout.Resolve(plan, wall);
                if (!HouseTrimLayout.TryPieces(plan.Data, wall, builder.Data, out var pieces, out string reason, data.WallLayout(plan, builder), HouseTrimLayout.Default(plan)))
                {
                    failed.Add(wall.Id);
                    output.Problem += (string.IsNullOrEmpty(output.Problem) ? "" : "\n") + "Trim on wall " + wall.Id + ": " + reason;
                    // A diagnostic is not an authored removal. Keep just the current
                    // group hidden so recovery can retain its paint and shared meshes.
                    if (previous.TryGetValue("trim:" + wall.Id, out var retained) && retained.Root != null)
                    {
                        retained.Root.SetActive(false);
                        next.Add(retained);
                    }
                    continue;
                }
                if (pieces.Count == 0) continue;
                var inputs = new StringBuilder("trim-v1:").Append(data.MeshSignature(trim.Mesh));
                foreach (var piece in pieces)
                {
                    for (int i = 0; i < 16; i++) inputs.Append(':').Append(piece.Matrix[i].ToString("R", CultureInfo.InvariantCulture));
                    inputs.Append(WallHeightClip.WidthInputs(trim.Mesh, piece.From, piece.To));
                    inputs.Append(':').Append(piece.From.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':').Append(piece.To.ToString("R", CultureInfo.InvariantCulture));
                }
                var group = RetainOrCreate(output, previous, "trim:" + wall.Id, inputs.ToString(), out bool changed);
                group.Root.SetActive(true);
                if (changed)
                    foreach (var piece in pieces)
                    {
                        var filter = WallModuleExamples.CreatePart(group.Root, "Trim piece");
                        var mesh = WallHeightClip.TrimWidth(trim.Mesh, piece.From, piece.To);
                        filter.sharedMesh = mesh;
                        filter.transform.localPosition = piece.Matrix.GetColumn(3);
                        filter.transform.localRotation = piece.Matrix.rotation;
                        filter.transform.localScale = piece.Matrix.lossyScale;
                    }
                var material = HouseFinishResolution.RenderMaterial(HouseFinishResolution.Trim(trim, plan));
                foreach (var renderer in group.Root.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = material;
                next.Add(group);
            }
            return failed;
        }
    }
}
