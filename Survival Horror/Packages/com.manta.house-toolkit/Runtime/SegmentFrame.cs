using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SegmentFrame : MonoBehaviour
    {
        [HideInInspector] public Bounds[] parts;
        SharedGeometry geometry;

        void OnEnable() => Refresh();
        void OnDisable() => SharedGeometry.Release(ref geometry);

        public void Refresh()
        {
            if (parts == null || parts.Length == 0) return;
            GetComponent<MeshFilter>().sharedMesh = SharedGeometry.Use(ref geometry, Hash128.Compute(parts), Build);
        }

        Mesh Build()
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var blocks = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                blocks[i] = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(parts[i].center, Quaternion.identity, parts[i].size) };
            var result = new Mesh {name = "Wall segment frame"};
            if (blocks.Length * (long)cube.vertexCount > ushort.MaxValue)
                result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            result.CombineMeshes(blocks);
            return result;
        }
    }
}
