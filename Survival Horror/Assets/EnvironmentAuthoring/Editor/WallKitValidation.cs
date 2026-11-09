using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // Read-only authoring advice, deliberately separate from the runtime fit checks.
    public static class WallKitValidation
    {
        private const float Tolerance = .001f; // One millimetre for exported connection geometry.

        public sealed class Issue
        {
            public readonly MessageType Severity;
            public readonly string Message;
            public Issue(MessageType severity, string message) { Severity = severity; Message = message; }
        }

        public static List<Issue> Check(WallModuleDefinition definition)
        {
            var issues = new List<Issue>();
            if (definition == null)
            {
                issues.Add(new Issue(MessageType.Error, "Assign a wall module definition."));
                return issues;
            }
            if (!definition.IsValid(out string reason))
            {
                issues.Add(new Issue(MessageType.Error, reason));
                // Still report missing tangents, invalid UVs and other advice in one pass.
                // Missing references cannot be inspected, but malformed mesh headers can.
                if (definition.Body == null) return issues;
            }
            var depths = definition.ConnectionDepths;
            issues.Add(new Issue(MessageType.Info, $"Declared connection depth: +Z {depths.y:0.####} m, -Z {-depths.x:0.####} m."));
            CheckOpening(definition, issues);
            CheckMesh(definition, definition.Body, issues);
            return issues;
        }

        public static List<Issue> CheckKit(HouseWallBuilder builder)
        {
            var issues = new List<Issue>();
            if (builder == null) return new List<Issue> { new Issue(MessageType.Error, "Assign a House Wall Builder.") };
            if (!HouseWallLayout.ValidateKit(builder.Data, out string reason)) issues.Add(new Issue(MessageType.Error, reason));
            foreach (var family in builder.Data.FamilyDefinitions(builder.GetComponent<HousePlan>()?.Data))
            {
                if (!family.IsValid(out reason)) issues.Add(new Issue(MessageType.Error, family.name + ": " + reason));
                foreach (var piece in family.Definitions().Distinct())
                    foreach (var issue in Check(piece)) issues.Add(new Issue(issue.Severity, piece.name + ": " + issue.Message));
            }
            return issues;
        }

        private static bool HasOpening(Rect rect) => rect != new Rect();

        private static void CheckOpening(WallModuleDefinition definition, List<Issue> issues)
        {
            Rect rect = definition.OpeningOutline;
            if (!HasOpening(rect)) return;
            if (!Finite(rect.x) || !Finite(rect.y) || !Finite(rect.width) || !Finite(rect.height)
                || rect.width <= 0 || rect.height <= 0 || rect.xMin < -Tolerance || rect.yMin < -Tolerance
                || rect.xMax > definition.Width + Tolerance || rect.yMax > definition.Height + Tolerance)
                issues.Add(new Issue(MessageType.Error, "Opening Outline: use a positive rectangle inside the module's width and height; door bottoms may be at Y=0."));
            if (definition.AllowLengthStretch)
                issues.Add(new Issue(MessageType.Error, "Opening Outline: a module with an opening must not allow length stretching."));
        }

        private static void CheckMesh(WallModuleDefinition definition, Mesh mesh, List<Issue> issues)
        {
            const string label = "Body";
            void Add(MessageType severity, string message) => issues.Add(new Issue(severity, label + ": " + message));
            if (mesh.vertexCount == 0 || mesh.subMeshCount != 2
                || Enumerable.Range(0, mesh.subMeshCount).Any(i => mesh.GetTopology(i) != MeshTopology.Triangles || mesh.GetIndexCount(i) == 0))
            {
                Add(MessageType.Error, "export a non-empty triangle mesh with two material slots (+Z first, -Z second).");
                return;
            }
            // Editor MeshUtility also reads imported meshes with Read/Write disabled.
            // Do not change importer settings or leave a CPU mesh copy in the player.
            using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var data = snapshot[0];
                var vertices = WallMeshData.Read<Vector3>(data.vertexCount, data.GetVertices);
                if (vertices.Any(v => !Finite(v))) { Add(MessageType.Error, "vertex positions contain NaN or infinity; repair the source mesh."); return; }
                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (var vertex in vertices) bounds.Encapsulate(vertex);
                if (Mathf.Abs(bounds.min.x) > Tolerance || Mathf.Abs(bounds.max.x - definition.Width) > Tolerance)
                    Add(MessageType.Error, "X bounds must run from 0 to Width. Apply export scale and place the origin at the bottom start of the datum.");
                if (Mathf.Abs(bounds.min.y) > Tolerance || Mathf.Abs(bounds.max.y - definition.Height) > Tolerance)
                    Add(MessageType.Error, "Y bounds must run from 0 to Height. Apply export scale; sloped tops are not supported yet.");
                var depths = definition.ConnectionDepths;
                if (bounds.min.z > depths.x + Tolerance || bounds.max.z < depths.y - Tolerance)
                    Add(MessageType.Error, "geometry must reach both declared connection faces; inspect the source and thickness metadata.");
                if (bounds.min.z < depths.x - Tolerance || bounds.max.z > depths.y + Tolerance)
                    Add(definition.AllowLengthStretch ? MessageType.Warning : MessageType.Info,
                        "geometry projects beyond the declared connection depth. Decoration is allowed, but overlap fitting does not prove its profile; inspect joins and stretching.");
                if (!HasConnectionCorners(vertices, 0, definition) || !HasConnectionCorners(vertices, definition.Width, definition))
                    Add(MessageType.Warning, "end vertices do not include all four declared rectangular connection corners. Check the closed end profiles manually.");

                if (!data.HasVertexAttribute(VertexAttribute.Normal)) Add(MessageType.Error, "normals are missing; export or calculate normals for lighting.");
                else
                {
                    var normals = WallMeshData.Read<Vector3>(data.vertexCount, data.GetNormals);
                    if (normals.Any(n => !Finite(n) || n.sqrMagnitude < .0001f)) Add(MessageType.Error, "normals contain zero or non-finite vectors; repair the source normals.");
                    else if (normals.Any(n => Mathf.Abs(n.magnitude - 1) > .01f)) Add(MessageType.Warning, "normalize the exported normals.");
                }
                if (!data.HasVertexAttribute(VertexAttribute.Tangent)) Add(MessageType.Warning, "tangents are missing; export or calculate them before using tangent-space normal maps.");
                else
                {
                    var tangents = WallMeshData.Read<Vector4>(data.vertexCount, data.GetTangents);
                    if (tangents.Any(t => !Finite(t.x) || !Finite(t.y) || !Finite(t.z) || !Finite(t.w)
                        || new Vector3(t.x, t.y, t.z).sqrMagnitude < .0001f || Mathf.Abs(Mathf.Abs(t.w) - 1) > .01f))
                        Add(MessageType.Warning, "tangents are invalid; recalculate them from the exported UVs and normals.");
                }
                if (!data.HasVertexAttribute(VertexAttribute.TexCoord0)) Add(MessageType.Error, "UV0 is missing; export authored UVs for details, reveals and caps.");
                else
                {
                    var uv = WallMeshData.Read<Vector2>(data.vertexCount, buffer => data.GetUVs(0, buffer));
                    if (uv.Any(v => !Finite(v.x) || !Finite(v.y))) Add(MessageType.Error, "UV0 contains NaN or infinity; repair the unwrap.");
                    else if (uv.All(v => (v - uv[0]).sqrMagnitude < .00000001f)) Add(MessageType.Warning, "UV0 is collapsed to one point; unwrap the mesh. Tiled coordinates may exceed 0..1.");
                }
                if (!data.HasVertexAttribute(VertexAttribute.Color)) Add(MessageType.Info, "no vertex colors. Fine for the current plain material; the proposed paint/mapping channels are not required yet.");
                else
                {
                    var colors = WallMeshData.Read<Color>(data.vertexCount, data.GetColors);
                    if (colors.Any(c => !Unit(c.r) || !Unit(c.g) || !Unit(c.b) || !Unit(c.a)))
                        Add(MessageType.Warning, "vertex colors contain non-finite or out-of-range values; export channels in 0..1. Paint channel semantics remain a proposal.");
                }
            }
        }

        private static bool HasConnectionCorners(Vector3[] vertices, float x, WallModuleDefinition definition)
        {
            var depths = definition.ConnectionDepths;
            foreach (float y in new[] { 0f, definition.Height })
            foreach (float z in new[] { depths.x, depths.y })
                if (!vertices.Any(v => (v - new Vector3(x, y, z)).sqrMagnitude <= Tolerance * Tolerance)) return false;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Unit(float value) => Finite(value) && value >= 0 && value <= 1;
    }
}
