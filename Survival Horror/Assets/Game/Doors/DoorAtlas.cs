using HouseToolkit;
using UnityEngine;

namespace HorrorGame.Doors
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DoorAtlas : MonoBehaviour
    {
        [SerializeField] private MeshRenderer visual;
        [Range(-1, 7), Tooltip("-1 chooses a stable design for this toolkit opening. 0 through 7 choose an atlas cell.")]
        public int design = -1;
        private static readonly int Tile = Shader.PropertyToID("_AtlasTile");
        private MaterialPropertyBlock block;

        private void OnEnable() => Apply();

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= Refresh;
            UnityEditor.EditorApplication.delayCall += Refresh;
#endif
        }

#if UNITY_EDITOR
        private void Refresh()
        {
            if (this && isActiveAndEnabled) Apply();
        }
#endif

        public void Apply()
        {
            if (!visual) return;
            int index = Mathf.Clamp(design, 0, 7);
            if (design < 0)
            {
                var opening = GetComponentInParent<InsertAttachment>();
                string key = opening ? opening.openingId : null;
                if (string.IsNullOrEmpty(key))
                {
                    key = gameObject.scene.path;
                    for (var part = transform; part; part = part.parent)
                        key += "/" + part.GetSiblingIndex();
                }
                index = Hash128.Compute(key).GetHashCode() & 7;
            }
            block ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(block);
            block.SetFloat(Tile, index);
            visual.SetPropertyBlock(block);
        }
    }
}
