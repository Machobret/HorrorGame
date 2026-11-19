// Run in the isolated Unity snapshot, using AssetHealthScope.json from the workspace inventory.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PrefabHealthAudit
{
    [Serializable] class Scope { public string[] prefabs; }
    [Serializable] class Prefab
    {
        public string path, guid;
        public Vector3 size;
        public int solidColliders;
        public Part[] parts;
    }
    [Serializable] class Part
    {
        public string name, mesh;
        public bool mirrored, emitter;
        public int triangles, degenerates, opposed, missingNormals, inwardClosedComponents, closedComponents;
        public float opposedAreaFraction;
        public Slot[] materials;
    }
    [Serializable] class Slot
    {
        public string path, name, shader, baseTexture;
        public bool missing, transparent, depthWrite, doubleSided;
        public int alphaSamples;
        public float opaqueFraction;
    }
    [Serializable] class Report { public bool verified; public Prefab[] prefabs; public int uniqueMeshes; public long uniqueTriangles; }
    sealed class Geometry
    {
        public Vector3[] vertices;
        public Vector2[] uv;
        public int[][] indices;
        public int triangles, degenerates, opposed, missingNormals, inwardClosedComponents, closedComponents;
        public float opposedAreaFraction;
    }
    sealed class Alpha
    {
        public byte[] pixels;
        public int width, height;
    }
    struct Edge
    {
        public int count, direction;
    }
    static readonly Dictionary<Mesh, Geometry> Meshes = new();
    static readonly Dictionary<Texture2D, Alpha> Alphas = new();
    static readonly Dictionary<(Mesh, Material, int), Slot> Samples = new();

    public static void RunBatch()
    {
        try
        {
            var scope = JsonUtility.FromJson<Scope>(File.ReadAllText("AssetHealthScope.json"));
            var rows = scope.prefabs.Select(ScanPrefab).ToArray();
            var report = new Report { verified = true, prefabs = rows, uniqueMeshes = Meshes.Count,
                uniqueTriangles = Meshes.Values.Sum(mesh => (long)mesh.triangles) };
            File.WriteAllText("../../AssetHealth/UnityAudit.json", JsonUtility.ToJson(report, true));
            Debug.Log($"PREFAB_HEALTH_DONE {rows.Length} prefabs, {report.uniqueMeshes} unique meshes, {report.uniqueTriangles} triangles");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    static Prefab ScanPrefab(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) throw new InvalidOperationException("Missing staged prefab " + path);
        var result = new Prefab { path = path, guid = AssetDatabase.AssetPathToGUID(path),
            solidColliders = asset.GetComponentsInChildren<Collider>(true).Count(c => c.enabled && !c.isTrigger) };
        bool hasBounds = false;
        var extent = new Bounds();
        var parts = new List<Part>();
        foreach (var renderer in asset.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<Canvas>()) continue;
            Mesh mesh;
            if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            else continue;
            bool emitter = renderer.name.Equals("Emitter", StringComparison.OrdinalIgnoreCase);
            if (!mesh)
            {
                parts.Add(new Part {name = renderer.name, mesh = "", materials = Array.Empty<Slot>()});
                continue;
            }
            if (!Meshes.TryGetValue(mesh, out var geometry)) Meshes.Add(mesh, geometry = ScanGeometry(mesh));
            var materials = renderer.sharedMaterials;
            var slotCount = Math.Max(mesh.subMeshCount, materials.Length);
            var slots = new Slot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var mat = i < materials.Length ? materials[i] : null;
                if (!mat)
                {
                    slots[i] = new Slot { missing = true };
                    continue;
                }
                var key = (mesh, mat, i);
                if (!Samples.TryGetValue(key, out var sample)) Samples.Add(key, sample = ScanMaterial(mat, geometry, i));
                slots[i] = sample;
            }
            var transform = renderer.transform.localToWorldMatrix;
            parts.Add(new Part { name = renderer.name, mesh = AssetDatabase.GetAssetPath(mesh) + "#" + mesh.name,
                mirrored = transform.determinant < 0, emitter = emitter, triangles = geometry.triangles,
                degenerates = geometry.degenerates, opposed = geometry.opposed, missingNormals = geometry.missingNormals,
                closedComponents = geometry.closedComponents, inwardClosedComponents = geometry.inwardClosedComponents,
                opposedAreaFraction = geometry.opposedAreaFraction, materials = slots });
            if (emitter || !renderer.enabled) continue;
            var bounds = mesh.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var point = transform.MultiplyPoint3x4(corner);
                        if (!hasBounds) { extent = new Bounds(point, Vector3.zero); hasBounds = true; }
                        else extent.Encapsulate(point);
                    }
        }
        result.size = extent.size;
        result.parts = parts.ToArray();
        return result;
    }

    static Geometry ScanGeometry(Mesh mesh)
    {
        var geometry = new Geometry { vertices = mesh.vertices, uv = mesh.uv, indices = new int[mesh.subMeshCount][] };
        var vertices = geometry.vertices;
        var normals = mesh.normals;
        if (normals.Length != vertices.Length) geometry.missingNormals = vertices.Length;
        else geometry.missingNormals = normals.Count(value => !float.IsFinite(value.x) || !float.IsFinite(value.y) ||
            !float.IsFinite(value.z) || value.sqrMagnitude < 1e-8f);
        bool hasNormals = normals.Length == vertices.Length;
        // Weld coincident positions at UV and normal seams for the closed-surface winding test.
        float extent = Mathf.Max(mesh.bounds.size.magnitude, .0001f);
        float unit = extent * 1e-6f;
        var weld = new Dictionary<Vector3Int, int>();
        var ids = new int[vertices.Length];
        var parents = new int[vertices.Length];
        var origin = mesh.bounds.center;
        for (int i = 0; i < vertices.Length; i++)
        {
            var point = Vector3Int.RoundToInt((vertices[i] - origin) / unit);
            if (!weld.TryGetValue(point, out int id)) weld.Add(point, id = i);
            ids[i] = id;
            parents[i] = i;
        }
        var edges = new Dictionary<(int, int), Edge>();
        var volumes = new double[vertices.Length];
        double area = 0, opposedArea = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var indices = mesh.GetTopology(s) == MeshTopology.Triangles ? mesh.GetTriangles(s) : Array.Empty<int>();
            geometry.indices[s] = indices;
            geometry.triangles += indices.Length / 3;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                float weight = cross.magnitude;
                if (!(weight > unit * unit)) { geometry.degenerates++; continue; }
                area += weight;
                if (hasNormals && Vector3.Dot(cross / weight, (normals[a] + normals[b] + normals[c]) / 3) < -.15f)
                {
                    geometry.opposed++;
                    opposedArea += weight;
                }
                int wa = ids[a], wb = ids[b], wc = ids[c];
                if (wa == wb || wb == wc || wa == wc) continue;
                AddEdge(wa, wb); AddEdge(wb, wc); AddEdge(wc, wa);
                volumes[wa] += Vector3.Dot(vertices[a] - origin, cross) / 6.0;
            }
        }
        var open = new HashSet<int>();
        var sums = new Dictionary<int, double>();
        foreach (var pair in edges)
        {
            int component = Find(pair.Key.Item1);
            if (pair.Value.count != 2 || pair.Value.direction != 0) open.Add(component);
            else sums.TryAdd(component, 0);
        }
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i] == 0) continue;
            int component = Find(i);
            if (sums.TryGetValue(component, out double value)) sums[component] = value + volumes[i];
        }
        double tolerance = Math.Pow(extent, 3) * 1e-9;
        geometry.closedComponents = sums.Count(x => !open.Contains(x.Key) && Math.Abs(x.Value) > tolerance);
        geometry.inwardClosedComponents = sums.Count(x => !open.Contains(x.Key) && x.Value < -tolerance);
        geometry.opposedAreaFraction = area > 0 ? (float)(opposedArea / area) : 0;
        return geometry;

        int Find(int i)
        {
            while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; }
            return i;
        }
        void AddEdge(int a, int b)
        {
            int ia = Find(a), ib = Find(b);
            if (ia != ib) parents[ia] = ib;
            var key = (Math.Min(a, b), Math.Max(a, b));
            edges.TryGetValue(key, out var edge);
            edge.count++;
            edge.direction += a < b ? 1 : -1;
            edges[key] = edge;
        }
    }

    static Slot ScanMaterial(Material mat, Geometry mesh, int slot)
    {
        var shader = mat.shader;
        var baseMap = mat.mainTexture as Texture2D;
        var result = new Slot { path = AssetDatabase.GetAssetPath(mat), name = mat.name,
            shader = shader ? shader.name : "", baseTexture = baseMap ? AssetDatabase.GetAssetPath(baseMap) : "",
            missing = !shader || shader.name == "Hidden/InternalErrorShader",
            transparent = mat.renderQueue >= 3000 || mat.GetTag("RenderType", false) == "Transparent",
            depthWrite = mat.HasProperty("_ZWrite") && mat.GetFloat("_ZWrite") > .5,
            doubleSided = mat.HasProperty("_Cull") && mat.GetFloat("_Cull") == 0 };
        if (!result.transparent || !baseMap || mesh.uv.Length != mesh.vertices.Length ||
            !mat.HasProperty("_BaseMap") || !mat.HasProperty("_BaseColor") || slot >= mesh.indices.Length) return result;
        if (!Alphas.TryGetValue(baseMap, out var alpha)) Alphas.Add(baseMap, alpha = ReadAlpha(baseMap));
        if (alpha == null) return result;
        var uv = mesh.uv;
        var scale = mat.GetTextureScale("_BaseMap");
        var offset = mat.GetTextureOffset("_BaseMap");
        var tint = mat.GetColor("_BaseColor").a;
        var indices = mesh.indices[slot];
        int step = Math.Max(1, indices.Length / 3 / 512);
        int opaqueSamples = 0;
        for (int i = 0; i < indices.Length; i += step * 3)
        {
            var a = uv[indices[i]];
            var b = uv[indices[i + 1]];
            var c = uv[indices[i + 2]];
            Test((a + b + c) / 3);
            Test(.7f * a + .15f * (b + c));
            Test(.7f * b + .15f * (a + c));
            Test(.7f * c + .15f * (a + b));
        }
        if (result.alphaSamples > 0) result.opaqueFraction = (float)opaqueSamples / result.alphaSamples;
        return result;

        void Test(Vector2 point)
        {
            point = Vector2.Scale(point, scale) + offset;
            int x = Texel(point.x, alpha.width, baseMap.wrapModeU);
            int y = Texel(point.y, alpha.height, baseMap.wrapModeV);
            if (alpha.pixels[y * alpha.width + x] * tint >= 250) opaqueSamples++;
            result.alphaSamples++;
        }
    }

    static int Texel(float uv, int length, TextureWrapMode wrap)
    {
        float normalized = wrap == TextureWrapMode.Clamp ? Mathf.Clamp01(uv) :
            wrap == TextureWrapMode.Mirror ? Mathf.PingPong(uv, 1) : Mathf.Repeat(uv, 1);
        return Mathf.Clamp(Mathf.FloorToInt(normalized * length), 0, length - 1);
    }

    static Alpha ReadAlpha(Texture2D source)
    {
        var path = AssetDatabase.GetAssetPath(source);
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
        var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(decoded, File.ReadAllBytes(path))) return null;
            return new Alpha { width = decoded.width, height = decoded.height,
                pixels = decoded.GetPixels32().Select(color => color.a).ToArray() };
        }
        finally { Object.DestroyImmediate(decoded); }
    }
}
