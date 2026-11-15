// Subtract-only BSP adaptation of csg.js, Copyright (c) 2011 Evan Wallace.
// https://github.com/evanw/csg.js (MIT; see HouseBooleanMesh.LICENSE.txt).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vertex = HouseToolkit.Editor.CutMeshData.Vertex;

namespace HouseToolkit.Editor
{
    // Destruction geometry only. Wall joining and authored source meshes never use this path.
    internal static class HouseBooleanMesh
    {
        private const float Epsilon = .00001f;
        internal static readonly Vector3[][] IcosphereFaces = CreateIcosphere();
        internal static readonly Vector3[] IcosphereEdges = CreateIcosphereEdges();

        private static Vector3[][] CreateIcosphere()
        {
            // The shortest edges between the twelve golden-ratio vertices define
            // twenty outward triangles. One subdivision gives 80 fairly even faces.
            // Build once; cutting, wire feedback and picking use this same solid.
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var points = new[] { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) }
                .Select(point => point.normalized * .5f).ToArray();
            float edge = points.Skip(1).Min(point => (point - points[0]).sqrMagnitude);
            bool Neighbours(int a, int b) => Mathf.Abs((points[a] - points[b]).sqrMagnitude - edge) < 1e-6f;
            var faces = new List<Vector3[]>();
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 1; j < points.Length; j++)
                    for (int k = j + 1; k < points.Length; k++)
                    {
                        if (!Neighbours(i, j) || !Neighbours(j, k) || !Neighbours(k, i)) continue;
                        var a = points[i]; var b = points[j]; var c = points[k];
                        if (Vector3.Dot(Vector3.Cross(b - a, c - a), a) < 0) (b, c) = (c, b);
                        var ab = (a + b).normalized * .5f;
                        var bc = (b + c).normalized * .5f;
                        var ca = (c + a).normalized * .5f;
                        faces.Add(new[] { a, ab, ca }); faces.Add(new[] { b, bc, ab });
                        faces.Add(new[] { c, ca, bc }); faces.Add(new[] { ab, bc, ca });
                    }
            return faces.ToArray();
        }

        private static Vector3[] CreateIcosphereEdges()
        {
            var points = IcosphereFaces.SelectMany(face => face).Distinct().ToArray();
            var indices = points.Select((point, index) => (point, index)).ToDictionary(pair => pair.point, pair => pair.index);
            var edges = new HashSet<(int, int)>();
            foreach (var face in IcosphereFaces)
                for (int i = 0; i < 3; i++)
                {
                    int a = indices[face[i]], b = indices[face[(i + 1) % 3]];
                    edges.Add((Mathf.Min(a, b), Mathf.Max(a, b)));
                }
            return edges.SelectMany(edge => new[] { points[edge.Item1], points[edge.Item2] }).ToArray();
        }

        internal static bool RaycastIcosphere(Ray ray, out float distance)
        {
            // Clip the ray against the convex face planes. A sphere approximation
            // would select empty space beyond the visible faceted silhouette.
            float enter = 0, exit = float.PositiveInfinity;
            foreach (var face in IcosphereFaces)
            {
                var normal = Vector3.Cross(face[1] - face[0], face[2] - face[0]).normalized;
                float origin = Vector3.Dot(normal, ray.origin - face[0]);
                float direction = Vector3.Dot(normal, ray.direction);
                if (Mathf.Abs(direction) < 1e-7f)
                { if (origin > Epsilon) { distance = 0; return false; } continue; }
                float next = -origin / direction;
                if (direction < 0) enter = Mathf.Max(enter, next);
                else exit = Mathf.Min(exit, next);
                if (enter > exit) { distance = 0; return false; }
            }
            distance = enter > 0 ? enter : exit;
            return distance >= 0 && float.IsFinite(distance);
        }

        private sealed class Budget
        {
            private int operations;
            internal void Check(int depth)
            {
                if (depth > 512 || ++operations > 500000)
                    throw new InvalidOperationException($"Boolean cut exceeds the polygon budget (depth {depth}, operations {operations}). Use a simpler source or fewer overlapping shapes.");
            }
        }

        private sealed class Polygon
        {
            internal readonly List<Vertex> Vertices;
            internal readonly int Slot;
            internal Vector3 Normal;
            internal float Distance;
            private Polygon(List<Vertex> vertices, int slot, Vector3 normal)
            { Vertices = vertices; Slot = slot; Normal = normal; Distance = Vector3.Dot(normal, vertices[0].Position); }

            internal static Polygon Create(List<Vertex> vertices, int slot)
            {
                for (int i = vertices.Count - 1; i >= 0; i--)
                    if ((vertices[i].Position - vertices[(i + 1) % vertices.Count].Position).sqrMagnitude < 1e-14f) vertices.RemoveAt(i);
                Vector3 normal = Vector3.zero;
                for (int i = 1; i + 1 < vertices.Count; i++)
                {
                    var candidate = Vector3.Cross(vertices[i].Position - vertices[0].Position, vertices[i + 1].Position - vertices[0].Position);
                    if (candidate.sqrMagnitude > normal.sqrMagnitude) normal = candidate;
                }
                return normal.sqrMagnitude > 1e-14f ? new Polygon(vertices, slot, normal.normalized) : null;
            }

            internal Polygon InSlot(int slot) => slot == Slot ? this : new Polygon(Vertices, slot, Normal);

            internal void Flip()
            {
                Vertices.Reverse();
                for (int i = 0; i < Vertices.Count; i++)
                {
                    var vertex = Vertices[i]; vertex.Normal = -vertex.Normal; vertex.Tangent.w = -vertex.Tangent.w; Vertices[i] = vertex;
                }
                Normal = -Normal; Distance = -Distance;
            }
        }

        private sealed class Node
        {
            private Vector3 normal;
            private float distance;
            private bool hasPlane;
            private Node front, back;
            private List<Polygon> polygons = new List<Polygon>();
            private readonly Budget budget;
            internal Node(Budget budget) { this.budget = budget; }

            private void Split(Polygon polygon, List<Polygon> coplanarFront, List<Polygon> coplanarBack,
                List<Polygon> forward, List<Polygon> behind)
            {
                budget.Check(0);
                int kind = 0;
                var sides = new int[polygon.Vertices.Count];
                for (int i = 0; i < sides.Length; i++)
                {
                    float d = Vector3.Dot(normal, polygon.Vertices[i].Position) - distance;
                    kind |= sides[i] = d < -Epsilon ? 2 : d > Epsilon ? 1 : 0;
                }
                if (kind == 0) (Vector3.Dot(normal, polygon.Normal) > 0 ? coplanarFront : coplanarBack).Add(polygon);
                else if (kind == 1) forward.Add(polygon);
                else if (kind == 2) behind.Add(polygon);
                else
                {
                    var a = new List<Vertex>(); var b = new List<Vertex>();
                    for (int i = 0; i < sides.Length; i++)
                    {
                        int j = (i + 1) % sides.Length;
                        var vi = polygon.Vertices[i]; var vj = polygon.Vertices[j];
                        if (sides[i] != 2) a.Add(vi);
                        if (sides[i] != 1) b.Add(vi);
                        if ((sides[i] | sides[j]) != 3) continue;
                        float t = (distance - Vector3.Dot(normal, vi.Position)) / Vector3.Dot(normal, vj.Position - vi.Position);
                        var vertex = Vertex.Lerp(vi, vj, t); a.Add(vertex); b.Add(vertex);
                    }
                    var first = Polygon.Create(a, polygon.Slot); var second = Polygon.Create(b, polygon.Slot);
                    // A split fragment remains on its source plane. Recomputing
                    // that plane from a very thin float triangle amplifies error.
                    if (first != null) { first.Normal = polygon.Normal; first.Distance = polygon.Distance; forward.Add(first); }
                    if (second != null) { second.Normal = polygon.Normal; second.Distance = polygon.Distance; behind.Add(second); }
                }
            }

            internal void Build(List<Polygon> source, int depth = 0)
            {
                budget.Check(depth);
                if (source.Count == 0) return;
                int first = 0;
                if (!hasPlane)
                {
                    normal = source[0].Normal; distance = source[0].Distance; hasPlane = true;
                    // This polygon defines the plane, so retain it here. Float
                    // rounding on thin fragments must not send it to a new child
                    // defining the same plane indefinitely.
                    polygons.Add(source[0]); first = 1;
                }
                var a = new List<Polygon>(); var b = new List<Polygon>();
                for (int i = first; i < source.Count; i++) Split(source[i], polygons, polygons, a, b);
                if (a.Count > 0) { front ??= new Node(budget); front.Build(a, depth + 1); }
                if (b.Count > 0) { back ??= new Node(budget); back.Build(b, depth + 1); }
            }

            internal void Invert()
            {
                foreach (var polygon in polygons) polygon.Flip();
                normal = -normal; distance = -distance;
                front?.Invert(); back?.Invert();
                (front, back) = (back, front);
            }

            internal List<Polygon> Clip(List<Polygon> source, int depth = 0)
            {
                budget.Check(depth);
                if (!hasPlane) return source;
                var a = new List<Polygon>(); var b = new List<Polygon>();
                foreach (var polygon in source) Split(polygon, a, b, a, b);
                if (front != null) a = front.Clip(a, depth + 1);
                if (back != null) a.AddRange(back.Clip(b, depth + 1));
                return a;
            }

            internal void ClipTo(Node other)
            { polygons = other.Clip(polygons); front?.ClipTo(other); back?.ClipTo(other); }

            internal List<Polygon> All()
            {
                var result = new List<Polygon>(polygons);
                if (front != null) result.AddRange(front.All());
                if (back != null) result.AddRange(back.All());
                return result;
            }
        }
        internal static Mesh SubtractAll(Mesh source, IReadOnlyList<BooleanCut> volumes, Matrix4x4 houseToMesh,
            bool solidSlab = false, bool frameAndGlass = false, AuthoredPanel panel = null)
        {
            if (!source) throw new ArgumentException("Choose an existing source mesh.");
            int cutSlot = source.subMeshCount;
            List<(List<Polygon> faces, bool solid)> parts = null;
            foreach (var volume in volumes)
            {
                if (volume == null || !volume.IsValid(out _)) throw new ArgumentException("Choose a valid Boolean shape.");
                var matrix = houseToMesh * volume.Matrix;
                if (!source.bounds.Intersects(PrimitiveBounds(matrix))) continue;
                if (parts == null)
                {
                    if (panel && panel.source && panel.source != source)
                        parts = CroppedPanelParts(panel);
                    else
                    {
                        var faces = Read(source);
                        if (faces.Count == 0) return source;
                        parts = new List<(List<Polygon>, bool)>();
                        if (solidSlab || (!frameAndGlass && IsClosed(faces))) parts.Add((faces, true));
                        else foreach (var group in faces.GroupBy(face => face.Slot))
                        {
                            var surface = group.ToList();
                            parts.Add((surface, IsClosed(surface)));
                        }
                    }
                }
                for (int i = 0; i < parts.Count; i++)
                {
                    var (faces, solid) = parts[i];
                    if (faces.Count == 0) continue;
                    var budget = new Budget();
                    var cutter = new Node(budget);
                    cutter.Build(Primitive(volume.Shape, matrix, cutSlot, volume.Matrix));
                    if (solid)
                    {
                        var body = new Node(budget);
                        body.Build(faces);
                        body.Invert(); body.ClipTo(cutter); cutter.ClipTo(body);
                        cutter.Invert(); cutter.ClipTo(body); cutter.Invert();
                        body.Build(cutter.All()); body.Invert();
                        parts[i] = (body.All(), true);
                    }
                    else parts[i] = (cutter.Clip(faces), false);
                }
            }
            return parts == null ? source : Write("Boolean cut", parts.SelectMany(p => p.faces).ToList(), cutSlot + 1);
        }

        static List<(List<Polygon>, bool)> CroppedPanelParts(AuthoredPanel panel)
        {
            // Rendering crops triangles cheaply. For a Boolean, establish solidity on the
            // authored frame first and intersect it with the same panel limits as a volume.
            var parts = new List<(List<Polygon>, bool)>();
            var source = panel.source;
            var bounds = source.bounds;
            var groups = Read(source).ToLookup(p => p.Slot);
            var frame = groups[0].ToList();
            Add(frame, panel.limits.x, panel.limits.y, 0, IsClosed(frame));
            var glass = groups[1].ToList();
            if (panel.glazing.x >= panel.glazing.y) Add(glass, panel.limits.x, panel.limits.y, 0, false);
            else
            {
                Add(glass, panel.limits.x, panel.glazing.x, 0, false);
                Add(glass, panel.glazing.x, panel.glazing.y, 1, false);
                Add(glass, panel.glazing.y, panel.limits.y, 0, false);
            }
            return parts;

            void Add(List<Polygon> faces, float left, float right, int slot, bool solid)
            {
                if (faces.Count == 0 || right-left < .000001f) return;
                var min = bounds.min - Vector3.one;
                var max = bounds.max + Vector3.one;
                if (left > bounds.min.x + Epsilon) min.x = left;
                if (right < bounds.max.x - Epsilon) max.x = right;
                if (panel.limits.z < bounds.max.y - Epsilon) max.y = panel.limits.z;
                var box = Matrix4x4.TRS((min+max)*.5f, Quaternion.identity, max-min);
                var budget = new Budget();
                var crop = new Node(budget);
                crop.Build(Primitive(CutShape.Box, box, slot));
                if (solid)
                {
                    var body = new Node(budget);
                    body.Build(faces);
                    body.Invert(); crop.ClipTo(body); crop.Invert();
                    body.ClipTo(crop); crop.ClipTo(body); body.Build(crop.All()); body.Invert();
                    parts.Add((body.All(), true));
                }
                else
                {
                    crop.Invert();
                    var clipped = crop.Clip(faces);
                    if (clipped.Count > 0)
                        parts.Add((clipped.Select(p => p.InSlot(slot)).ToList(), false));
                }
            }
        }

        internal static Mesh ColliderBox(BoxCollider box) =>
            Write("Boolean collider source", Primitive(CutShape.Box, Matrix4x4.TRS(box.center, Quaternion.identity, box.size)), 1);

        private static bool IsClosed(List<Polygon> polygons)
        {
            // Imported UV/material seams duplicate vertices. Weld positions only
            // for this check; never rewrite the source's attributes or indices.
            // A two-sided pane may have perfectly paired edges yet enclose no volume.
            if (polygons.Count == 0) return false;
            var plane = polygons[0];
            if (polygons.All(p => p.Vertices.All(v => Mathf.Abs(Vector3.Dot(plane.Normal, v.Position) - plane.Distance) < Epsilon)))
                return false;
            var points = new Dictionary<Vector3Int, List<int>>();
            var positions = new List<Vector3>();
            var edges = new Dictionary<(int, int), int>();
            int Point(Vector3 position)
            {
                var key = new Vector3Int(Mathf.RoundToInt(position.x / Epsilon),
                    Mathf.RoundToInt(position.y / Epsilon), Mathf.RoundToInt(position.z / Epsilon));
                int Nearby(Vector3Int cell)
                {
                    if (points.TryGetValue(cell, out var ids))
                        foreach (int candidate in ids)
                            if ((positions[candidate] - position).sqrMagnitude <= Epsilon * Epsilon) return candidate;
                    return -1;
                }
                int id = Nearby(key);
                if (id >= 0) return id;
                // Equivalent clipped coordinates can round to adjacent bins.
                // Compare their positions rather than treating the bin as identity.
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++)
                        {
                            if (x == 0 && y == 0 && z == 0) continue;
                            id = Nearby(key + new Vector3Int(x, y, z));
                            if (id >= 0) return id;
                        }
                id = positions.Count;
                positions.Add(position);
                if (!points.TryGetValue(key, out var bucket)) points[key] = bucket = new List<int>();
                bucket.Add(id);
                return id;
            }
            foreach (var polygon in polygons)
                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    int a = Point(polygon.Vertices[i].Position), b = Point(polygon.Vertices[(i + 1) % polygon.Vertices.Count].Position);
                    if (a == b) continue;
                    var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
                    edges.TryGetValue(edge, out var usage);
                    edges[edge] = usage + (a < b ? 1 : -1);
                }
            if (edges.Count == 0) return false;
            // Touching frame bars can contribute several balanced pairs at the same edge.
            var boundary = edges.Where(pair => pair.Value != 0).ToArray();
            if (boundary.Length == 0) return true;

            // Cropping and cap tessellation can split one side of a shared edge
            // without splitting its neighbour. Check those unmatched edges as
            // smaller segments; this interprets closure without repairing meshes.
            var ends = boundary.SelectMany(pair => new[] { pair.Key.Item1, pair.Key.Item2 }).Distinct().ToArray();
            var segments = new Dictionary<(int, int), int>();
            foreach (var pair in boundary)
            {
                int a = pair.Key.Item1, b = pair.Key.Item2;
                var start = positions[a]; var delta = positions[b] - start;
                float lengthSquared = delta.sqrMagnitude;
                var splits = ends.Where(id => id != a && id != b).Select(id =>
                    (Id: id, T: Vector3.Dot(positions[id] - start, delta) / lengthSquared))
                    .Where(point => point.T > 0 && point.T < 1
                        && (positions[point.Id] - start - delta * point.T).sqrMagnitude <= Epsilon * Epsilon)
                    .OrderBy(point => point.T).Select(point => point.Id).Prepend(a).Append(b).ToArray();
                for (int i = 1; i < splits.Length; i++)
                {
                    int from = splits[i - 1], to = splits[i];
                    var edge = (Mathf.Min(from, to), Mathf.Max(from, to));
                    segments.TryGetValue(edge, out var usage);
                    segments[edge] = usage + pair.Value * (from < to ? 1 : -1);
                }
            }
            return segments.Values.All(winding => winding == 0);
        }

        internal static Bounds PrimitiveBounds(Matrix4x4 matrix)
        {
            var bounds = new Bounds(matrix.MultiplyPoint3x4(-Vector3.one / 2), Vector3.zero);
            for (int i = 1; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                (i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f)));
            return bounds;
        }

        private static List<Polygon> Read(Mesh source)
        {
            using var data = MeshUtility.AcquireReadOnlyMeshData(source);
            var vertices = CutMeshData.ReadVertices(data[0]);
            var result = new List<Polygon>();
            for (int slot = 0; slot < source.subMeshCount; slot++)
            {
                if (source.GetTopology(slot) != UnityEngine.MeshTopology.Triangles) throw new ArgumentException("Boolean sources must contain triangles.");
                var indices = CutMeshData.Read<int>((int)data[0].GetSubMesh(slot).indexCount, values => data[0].GetIndices(values, slot));
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var polygon = Polygon.Create(new List<Vertex> { vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]] }, slot);
                    if (polygon != null) result.Add(polygon);
                }
            }
            return result;
        }

        private static List<Polygon> Primitive(CutShape shape, Matrix4x4 matrix, int slot = 0, Matrix4x4? uvMatrix = null)
        {
            var result = new List<Polygon>();
            var normalMatrix = matrix.inverse.transpose;
            var mapping = uvMatrix ?? matrix;
            var mappingNormals = mapping.inverse.transpose;
            void Add(Vector3[] positions, Vector3[] normals)
            {
                // One planar basis per cutter face, measured in House metres.
                // X/Y alone collapses side and horizontal faces; normalized cutter
                // coordinates also stretch the finish whenever the shape is resized.
                var faceNormal = mappingNormals.MultiplyVector(Vector3.Cross(positions[1] - positions[0], positions[2] - positions[0])).normalized;
                var absolute = new Vector3(Mathf.Abs(faceNormal.x), Mathf.Abs(faceNormal.y), Mathf.Abs(faceNormal.z));
                bool side = absolute.x >= absolute.y && absolute.x >= absolute.z;
                bool horizontal = !side && absolute.y >= absolute.z;
                var houseToMesh = matrix * mapping.inverse;
                var uAxis = houseToMesh.MultiplyVector(side ? Vector3.forward : Vector3.right);
                var vAxis = houseToMesh.MultiplyVector(horizontal ? Vector3.forward : Vector3.up);
                Vector2 UV(Vector3 point)
                {
                    point = mapping.MultiplyPoint3x4(point);
                    return side ? new Vector2(point.z, point.y)
                        : horizontal ? new Vector2(point.x, point.z) : new Vector2(point.x, point.y);
                }
                var vertices = positions.Select((point, i) =>
                {
                    var normal = normalMatrix.MultiplyVector(normals[i]).normalized;
                    var tangent = (uAxis - normal * Vector3.Dot(normal, uAxis)).normalized;
                    float sign = Vector3.Dot(Vector3.Cross(normal, tangent), vAxis) < 0 ? -1 : 1;
                    return new Vertex { Position = matrix.MultiplyPoint3x4(point), Normal = normal,
                        Tangent = new Vector4(tangent.x, tangent.y, tangent.z, sign), UV = UV(point), Color = Color.white };
                }).ToList();
                if (matrix.determinant < 0) vertices.Reverse();
                var polygon = Polygon.Create(vertices, slot);
                if (polygon != null) result.Add(polygon);
            }
            if (shape == CutShape.Box)
            {
                int[][] faces = { new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 },
                    new[] { 2, 6, 7, 3 }, new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 } };
                foreach (var face in faces)
                {
                    var points = face.Select(i => new Vector3((i & 1) == 0 ? -.5f : .5f,
                        (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f)).ToArray();
                    var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
                    Add(points, Enumerable.Repeat(normal, 4).ToArray());
                }
            }
            else if (shape == CutShape.Icosphere)
            {
                foreach (var face in IcosphereFaces)
                {
                    var normal = Vector3.Cross(face[1] - face[0], face[2] - face[0]).normalized;
                    Add(face, new[] { normal, normal, normal });
                }
            }
            return result;
        }

        private static Mesh Write(string key, List<Polygon> polygons, int slots)
        {
            var vertices = new List<Vertex>();
            var triangles = Enumerable.Range(0, slots).Select(_ => new List<int>()).ToArray();
            foreach (var polygon in polygons) CutMeshData.Append(vertices, triangles[polygon.Slot], polygon.Vertices);
            return CutMeshData.CreateMesh(key, vertices, triangles);
        }
    }
}

