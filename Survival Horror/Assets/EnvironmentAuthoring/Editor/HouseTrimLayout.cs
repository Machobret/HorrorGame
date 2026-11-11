using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // Interprets attachments; it neither edits the plan nor creates scene objects.
    internal static class HouseTrimLayout
    {
        internal static WallTrim Default(WallKit kit, HouseStack building)
            => kit != null && kit.OverrideTrim ? kit.DefaultTrim : building?.DefaultTrim;
        internal static WallTrim Default(HousePlan plan)
            => Default(plan.GetComponent<HouseWallBuilder>()?.Data, HouseBuilding.For(plan)?.Data);
        internal static WallTrim Resolve(FloorPlan.Wall wall, WallTrim defaults)
            => wall.IsFence || wall.TrimDisabled ? null : wall.Trim ?? defaults;
        internal static WallTrim Resolve(HousePlan plan, FloorPlan.Wall wall) => Resolve(wall, Default(plan));

        internal readonly struct Piece
        {
            internal readonly Matrix4x4 Matrix;
            internal readonly float From, To;
            internal Piece(Matrix4x4 matrix, float from, float to)
            { Matrix = matrix; From = from; To = to; }
        }

        internal static bool Eligible(FloorPlan plan, FloorPlan.Wall wall)
            => wall != null && wall.IsPhysical && !wall.IsFence && plan.TryGetExteriorOutward(wall, out _);

        // The back reaches the wall datum; Projection still measures beyond its outside face.
        internal static Vector2 DepthRange(FloorPlan plan, FloorPlan.Wall wall, WallKit kit, float projection)
            => new Vector2(0, HouseWallLayout.WallDepths(plan, wall, kit).y + projection);

        internal static string Problem(HousePlan plan, FloorPlan.Wall wall, HouseGenerationData data = null)
        {
            var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            data ??= HouseGenerationData.Create(plan);
            if (HouseWallLayout.DefinitionFor(plan.Data, wall, kit) == null) return Problem(plan.Data, wall, Default(plan));
            TryPieces(plan.Data, wall, kit, out _, out string reason, data.WallLayout(plan, plan.GetComponent<HouseWallBuilder>()), Default(plan));
            return reason;
        }

        internal static bool ValidateProfile(WallTrim trim, out string reason)
        {
            reason = "Trim height and depth must be positive finite metres; offset must be finite.";
            bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            if (trim == null) { reason = ""; return true; }
            if (!Finite(trim.Height) || !Finite(trim.Depth) || !Finite(trim.Offset) || !Finite(trim.PatternOffset)
                || trim.Height < .001f || trim.Depth < .001f) return false;
            // Empty attachments retain their settings until the user chooses an asset.
            if (trim.Mesh == null) { reason = ""; return true; }
            var mesh = trim.Mesh;
            var bounds = mesh.bounds;
            reason = "Choose a solid trim mesh with one triangle submesh, normals, UVs and positive X/Y/Z dimensions.";
            if (mesh.subMeshCount != 1 || mesh.vertexCount < 3 || mesh.GetTopology(0) != MeshTopology.Triangles
                || mesh.GetIndexCount(0) < 3 || !mesh.HasVertexAttribute(VertexAttribute.Normal)
                || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0)
                || !Finite(bounds.min.x) || !Finite(bounds.min.y) || !Finite(bounds.min.z)
                || !Finite(bounds.max.x) || !Finite(bounds.max.y) || !Finite(bounds.max.z)
                || bounds.size.x < .001f || bounds.size.y < .001f || bounds.size.z < .001f
                || !Finite(trim.Height / bounds.size.y) || !Finite(trim.Depth / bounds.size.z)) return false;
            using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
            if (WallMeshData.ReadVertices(data[0]).Any(v => !Finite(v.Position.x) || !Finite(v.Position.y) || !Finite(v.Position.z))) return false;
            reason = "";
            return true;
        }

        internal static string Problem(FloorPlan plan, FloorPlan.Wall wall, WallTrim defaults = null)
        {
            var trim = Resolve(wall, defaults);
            if (!ValidateProfile(trim, out string reason)) return reason;
            if (!Eligible(plan, wall) || trim?.Mesh == null) return "";
            if (Vector2.Distance(plan.GetPosition(wall.StartId), plan.GetPosition(wall.EndId)) / trim.Mesh.bounds.size.x > 4096)
                return "Use a wider trim module: a wall can contain at most 4096 authored repeats.";
            return "";
        }

        private static Vector2 Direction(FloorPlan plan, FloorPlan.Wall wall, string corner)
            => (plan.GetPosition(wall.StartId == corner ? wall.EndId : wall.StartId) - plan.GetPosition(corner)).normalized;

        private static float Join(FloorPlan plan, FloorPlan.Wall wall, string corner, WallKit kit,
            HouseWallLayout.Result layout, WallTrim defaults)
        {
            var incident = plan.Walls.Where(w => w.IsPhysical && (w.StartId == corner || w.EndId == corner)).ToArray();
            // A partition does not change the facade pair's wall or trim corner.
            if (incident.Length > 2)
                incident = incident.Where(w => plan.GetEffectiveWallType(w) == FloorPlan.WallType.Exterior).ToArray();
            if (incident.Length != 2 || !incident.Contains(wall)) return 0;
            var other = incident.First(w => w != wall);
            var a = Direction(plan, wall, corner); var b = Direction(plan, other, corner);
            plan.TryGetExteriorOutward(wall, out var outsideA);
            if (!plan.TryGetExteriorOutward(other, out var outsideB)) return 0;
            Vector2 Band(FloorPlan.Wall item, Vector2 direction, Vector2 outside)
            {
                var depths = HouseWallLayout.WallDepths(plan, item, kit);
                // An untrimmed neighbour contributes its wall section. The same
                // corner owner positions both bands; no separate trim join solver.
                var trim = Resolve(item, defaults);
                if (trim?.Mesh != null) depths = DepthRange(plan, item, kit, trim.Depth);
                return Vector2.Dot(new Vector2(-direction.y, direction.x), outside) > 0
                    ? depths : HouseWallLayout.ReverseDepths(depths);
            }
            HouseWallLayout.CornerEnds(a, b, Band(wall, a, outsideA), Band(other, b, outsideB), out float cut, out _);
            // Keep the same trim-band fitting at the translated wall datums.
            cut += Vector2.Dot(layout.JunctionOffset(corner) - layout.PlacementOffset(wall.Id), a);
            return cut;
        }

        internal static bool TryPieces(FloorPlan plan, FloorPlan.Wall wall, WallKit kit,
            out List<Piece> result, out string reason, HouseWallLayout.Result layout = null, WallTrim defaults = null)
        {
            result = new List<Piece>();
            var trim = Resolve(wall, defaults);
            reason = Problem(plan, wall, defaults);
            if (reason.Length > 0) return false;
            if (trim?.Mesh == null || !Eligible(plan, wall)) return true;
            plan.TryGetExteriorOutward(wall, out var outward);
            var storedStart = plan.GetPosition(wall.StartId);
            var storedEnd = plan.GetPosition(wall.EndId);
            var along = new Vector2(outward.y, -outward.x);
            float length = Vector2.Distance(storedStart, storedEnd);
            bool reverse = Vector2.Dot(storedEnd - storedStart, along) < 0;
            var start = reverse ? storedEnd : storedStart;
            layout ??= HouseWallLayout.CalculateRequiredWalls(plan, kit);
            start += layout.PlacementOffset(wall.Id);
            float startJoin = Join(plan, wall, reverse ? wall.EndId : wall.StartId, kit, layout, defaults);
            float endJoin = Join(plan, wall, reverse ? wall.StartId : wall.EndId, kit, layout, defaults);
            var bounds = trim.Mesh.bounds;
            double fittedLength = (double)length - startJoin - endJoin;
            reason = "This trim fit is too short or too large. Use smaller projections or a wider module (at most 4096 fitted pieces per wall).";
            if (fittedLength <= .00001 || fittedLength / bounds.size.x > 4096) return false;
            var rotation = Quaternion.LookRotation(new Vector3(outward.x, 0, outward.y));
            var depths = DepthRange(plan, wall, kit, trim.Depth);
            var scale = new Vector3(1, trim.Height / bounds.size.y, (depths.y - depths.x) / bounds.size.z);
            if (!float.IsFinite(scale.z) || scale.z <= 0) return false;
            float phase = reverse ? -trim.PatternOffset - length : trim.PatternOffset;
            var intervals = new List<(float From, float To)> { (startJoin, length - endJoin) };
            foreach (var opening in wall.Openings)
            {
                var definition = HouseWallLayout.OpeningFor(plan, wall, kit, opening);
                if (definition == null) continue;
                var aperture = definition.OpeningOutline;
                float bottom = trim.Offset;
                if (bottom >= aperture.yMax - .00001f
                    || bottom + trim.Height <= aperture.yMin + .00001f) continue;
                // OpeningOutline is authored in the outward-facing module's X
                // direction, even when its logical wall was drawn backwards.
                float centre = reverse ? length - opening.Distance : opening.Distance;
                float a = centre - definition.Width / 2 + aperture.xMin;
                float b = centre - definition.Width / 2 + aperture.xMax;
                intervals = intervals.SelectMany(i => Subtract(i, a, b)).ToList();
            }
            foreach (var interval in intervals)
            {
                // Continue the authored repeat through openings and logical wall splits.
                float position = interval.From;
                while (position < interval.To - .00001f)
                {
                    float native = Mathf.Repeat(phase + position, bounds.size.x);
                    if (bounds.size.x - native < .00001f) native = 0;
                    float width = Mathf.Min(bounds.size.x - native, interval.To - position);
                    float next = position + width;
                    // Guard the actual fitted intervals, including corner extensions,
                    // and float precision. Never publish a partial attachment or loop.
                    if (result.Count >= 4096 || !float.IsFinite(next) || next <= position)
                    { result.Clear(); return false; }
                    float cropFrom = bounds.min.x + native;
                    var p = start + along * (position - cropFrom)
                        + outward * (depths.x - bounds.min.z * scale.z);
                    var matrix = Matrix4x4.TRS(new Vector3(p.x, trim.Offset - bounds.min.y * scale.y, p.y), rotation, scale);
                    for (int i = 0; i < 16; i++)
                        if (!float.IsFinite(matrix[i])) { result.Clear(); return false; }
                    result.Add(new Piece(matrix, cropFrom, cropFrom + width));
                    position = next;
                }
            }

            reason = "";
            return true;
        }

        private static IEnumerable<(float From, float To)> Subtract((float From, float To) interval, float a, float b)
        {
            if (a > interval.From) yield return (interval.From, Mathf.Min(a, interval.To));
            if (b < interval.To) yield return (Mathf.Max(b, interval.From), interval.To);
        }
    }
}
