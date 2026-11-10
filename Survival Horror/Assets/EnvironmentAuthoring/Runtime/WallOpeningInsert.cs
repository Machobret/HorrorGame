using UnityEngine;

namespace EnvironmentAuthoring
{
    [CreateAssetMenu(menuName = "Environment/Wall Opening Insert")]
    public sealed class WallOpeningInsert : ScriptableObject
    {
        public string Shape = "01";
        public FloorPlan.OpeningKind Kind;
        public float SegmentWidth = 2;
        public Rect Opening;
        [Tooltip("Optional door prefab, with its scripts and exit transforms already configured. Overrides the mesh representation. Root origin: doorway centre at floor height; +Z faces the module front.")]
        public GameObject Prefab;
        internal bool UsesPrefab => Kind == FloorPlan.OpeningKind.Doorway && Prefab != null;
        public Mesh Mesh;
        [Tooltip("Opaque/frame finish (submesh 0). Existing single-material inserts use this slot alone.")]
        public Material Material;
        [Tooltip("Optional glass finish for submesh 1; blank uses Material on both slots. Use a transparent shader for glass.")]
        public Material GlassMaterial;
        [Tooltip("For a two-slot framed insert, use the host family's named material roles (slots 0/1), including per-wall overrides.")]
        public bool UseWallMaterialRoles;

        internal bool HasValidMaterials => Mesh != null && Material != null
            && (Mesh.subMeshCount == 1 || Mesh.subMeshCount == 2);
        internal Material MaterialFor(int submesh) => submesh == 1 && GlassMaterial != null ? GlassMaterial : Material;
        [Min(0)] public float MinimumWallThickness;
        [Min(0), Tooltip("Zero leaves the upper wall-thickness limit unrestricted.")] public float MaximumWallThickness;

        public bool Fits(WallFamily family, FloorPlan.OpeningKind kind, WallModuleDefinition module)
        {
            if (family == null || module == null || Kind != kind || (Prefab != null ? !UsesPrefab || Prefab.scene.IsValid() : !HasValidMaterials)) return false;
            float thickness = module.ConnectionDepths.y - module.ConnectionDepths.x;
            // Inserts are authored in their segment coordinates. Compare apertures about the segment centre,
            // so the same framed model can mount in compatible 1 m and 2 m wall segments.
            return Shape == module.OpeningShape && (Opening.size - module.OpeningOutline.size).sqrMagnitude < .00000001f
                && Mathf.Abs(Opening.yMin - module.OpeningOutline.yMin) < .0001f
                && Mathf.Abs(Opening.center.x - SegmentWidth / 2 - module.OpeningOutline.center.x + module.Width / 2) < .0001f
                && float.IsFinite(MinimumWallThickness) && float.IsFinite(MaximumWallThickness)
                && MinimumWallThickness >= 0 && MaximumWallThickness >= 0
                && thickness >= MinimumWallThickness - .0001f
                && (MaximumWallThickness == 0 || thickness <= MaximumWallThickness + .0001f);

        }
    }
}
