using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HouseToolkit
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ScatterCell : MonoBehaviour
    {
        internal const float Width = 12f;
        [Serializable]
        internal struct Piece
        {
            public ScatterPreset preset;
            public Mesh mesh;
            public Material material;
            public Vector3 position;
            public Quaternion rotation;
            public float scale;
            public bool solid;
        }

        [SerializeField, HideInInspector] internal List<Piece> pieces = new();
        Mesh output;
        bool dirty;
        void OnEnable() => Rebuild();
        void OnValidate() => dirty = true;
#if UNITY_EDITOR
        void Update() { if (dirty) Rebuild(); }
#endif
        void OnDestroy()
        {
            if (!output) return;
            if (Application.isPlaying) Destroy(output);
            else DestroyImmediate(output);
        }

        internal void Rebuild()
        {
            dirty = false;
            if (!output)
                output = new Mesh { name = "Scatter cell", hideFlags = HideFlags.DontSave };
            output.Clear();
            var slots = new Dictionary<Material, int>();
            var materials = new List<Material>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<List<int>>();
            var sources = new Dictionary<Mesh, (Vector3[] points, Vector3[] normals, Vector2[] uvs, int[] indices)>();
            foreach (var p in pieces)
            {
                if (!p.mesh || !p.material || !p.mesh.isReadable) continue;
                if (!sources.TryGetValue(p.mesh, out var source))
                    sources.Add(p.mesh, source = (p.mesh.vertices, p.mesh.normals, p.mesh.uv, p.mesh.GetTriangles(0)));
                if (!slots.TryGetValue(p.material, out int slot))
                {
                    slot = materials.Count;
                    slots.Add(p.material, slot);
                    materials.Add(p.material);
                    indices.Add(new List<int>());
                }
                int start = vertices.Count;
                for (int i = 0; i < source.points.Length; i++)
                {
                    vertices.Add(p.position + p.rotation * (source.points[i] * p.scale));
                    normals.Add(p.rotation * (i < source.normals.Length ? source.normals[i] : Vector3.up));
                    uvs.Add(i < source.uvs.Length ? source.uvs[i] : Vector2.zero);
                }
                foreach (int index in source.indices) indices[slot].Add(start + index);
            }
            output.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            output.SetVertices(vertices);
            output.SetNormals(normals);
            output.SetUVs(0, uvs);
            output.subMeshCount = materials.Count;
            for (int i = 0; i < indices.Count; i++) output.SetTriangles(indices[i], i, false);
            output.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = output;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials.ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
