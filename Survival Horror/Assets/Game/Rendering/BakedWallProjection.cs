using UnityEngine;

namespace Game.Rendering
{
    // Restores the per-renderer material property that Unity does not serialize on a baked wall.
    [ExecuteAlways, AddComponentMenu("")]
    public sealed class BakedWallProjection : MonoBehaviour
    {
        [SerializeField] private MeshFilter body;
        [SerializeField] private Vector4 verticalProjectionRange;
        private static readonly int Range = Shader.PropertyToID("_WallProjectionYRange");

        private void OnEnable()
        {
            if (!body || !body.TryGetComponent<MeshRenderer>(out var renderer)) return;
            var slots = renderer.sharedMaterials;
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i] || !slots[i].HasProperty(Range)) continue;
                renderer.GetPropertyBlock(block, i);
                block.SetVector(Range, verticalProjectionRange);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }
}
