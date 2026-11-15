using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways]
    public sealed class House : MonoBehaviour
    {
        // Projection is shared by all pieces and measured in world metres in the house's axes.
        static readonly int rowX = Shader.PropertyToID("_HouseRight");
        static readonly int rowY = Shader.PropertyToID("_HouseUp");
        static readonly int rowZ = Shader.PropertyToID("_HouseForward");
        readonly System.Collections.Generic.List<Renderer> projected = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<Material> slots = new System.Collections.Generic.List<Material>();
        MaterialPropertyBlock block;
        Vector3 lastPosition;
        Quaternion lastRotation;
        bool refreshProjection = true;

        public void InvalidateProjection() => refreshProjection = true;
        void OnEnable() => InvalidateProjection();
        void LateUpdate() => UpdateProjection();

        public void UpdateProjection()
        {
            transform.GetPositionAndRotation(out var position, out var rotation);
            if (!refreshProjection && position.Equals(lastPosition) && rotation.Equals(lastRotation)) return;
            if (refreshProjection)
            {
                GetComponentsInChildren(true, projected);
                int kept = 0;
                for (int i = 0; i < projected.Count; i++)
                {
                    projected[i].GetSharedMaterials(slots);
                    if (slots.Exists(IsProjected) && projected[i].GetComponentInParent<House>() == this)
                        projected[kept++] = projected[i];
                }
                projected.RemoveRange(kept, projected.Count - kept);
                refreshProjection = false;
            }
            lastPosition = position;
            lastRotation = rotation;
            if (projected.Count == 0) return;
            var frame = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            block ??= new MaterialPropertyBlock();
            foreach (var renderer in projected)
            {
                if (!renderer) continue;
                renderer.GetPropertyBlock(block);
                block.SetVector(rowX, frame.GetRow(0));
                block.SetVector(rowY, frame.GetRow(1));
                block.SetVector(rowZ, frame.GetRow(2));
                renderer.SetPropertyBlock(block);
            }
        }

        static bool IsProjected(Material material) => material && material.HasProperty(rowX);
        [Min(0)] public float exteriorThickness, interiorThickness;
        [HideInInspector] public bool wallsOnly;
        public WallFamily exteriorFamily;
        public WallFamily interiorFamily;
        public Material exteriorFinish, interiorFinish, floorFinish, ceilingFinish;
        public Material exteriorGlass, interiorGlass;
        public TrimFamily trim;
        [HideInInspector] public System.Collections.Generic.List<BooleanCut> booleanCuts = new System.Collections.Generic.List<BooleanCut>();
        [HideInInspector] public int booleanGeometryVersion;
        public Material cutMaterial;
        public const float DefaultSlabThickness = .18f;
        [Min(0.01f)] public float slabThickness = DefaultSlabThickness;
    }
}
