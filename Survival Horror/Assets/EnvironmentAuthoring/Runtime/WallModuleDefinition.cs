using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring
{
    [CreateAssetMenu(menuName = "Environment/Wall Module", fileName = "WallModule")]
    public sealed class WallModuleDefinition : ScriptableObject
    {
        public string DisplayName = "Wall module";
        [Tooltip("Keep manually edited dimensions and opening metadata when importing this source again.")]
        public bool PreserveImportOverrides;
        public string FamilyId, OpeningShape;
        [Min(0.01f)] public float Width = 2f;
        [Min(0.01f)] public float Height = 2.8f;
        [Min(0.01f), Tooltip("Full connection thickness about Z=0. Decorative projections do not enlarge this footprint.")]
        public float Thickness = 0.15f;
        [Tooltip("Extend the solid wall head above ceiling height to conceal the slab edge. Disable for framed/glazed modules that must retain their authored height.")]
        public bool CoverSlabEdge = true;
        public Vector2 ConnectionDepths => new Vector2(-Thickness / 2, Thickness / 2);
        [Tooltip("Only enable for plain bodies whose length can change without distorting details.")]
        public bool AllowLengthStretch;
        [Tooltip("Clear opening outline in module-local X/Y metres; used by placement previews. Empty for blanks.")]
        public Rect OpeningOutline;
        public Mesh Body;
        [Tooltip("Optional fixed body slots for mixed frame/glass modules. Two assigned materials override House side finishes; leave empty for ordinary +Z/-Z wall finishes.")]
        public Material[] BodyMaterials = System.Array.Empty<Material>();
        public bool IsValid(out string reason)
        {
            reason = "";
            if (BodyMaterials != null && BodyMaterials.Length > 0
                && (BodyMaterials.Length != 2 || BodyMaterials[0] == null || BodyMaterials[1] == null))
            { reason = "Fixed body materials require two assigned slots, or an empty array for inherited wall finishes."; return false; }
            if (!PositiveFinite(Width) || !PositiveFinite(Height) || !PositiveFinite(Thickness))
                reason = "Module dimensions must be finite and greater than zero.";
            else if (!AllowLengthStretch && (!float.IsFinite(OpeningOutline.x) || !float.IsFinite(OpeningOutline.y)
                || !float.IsFinite(OpeningOutline.width) || !float.IsFinite(OpeningOutline.height)
                || OpeningOutline.width <= 0 || OpeningOutline.height <= 0 || OpeningOutline.xMin < 0 || OpeningOutline.yMin < 0
                || OpeningOutline.xMax > Width || OpeningOutline.yMax > Height))
                reason = "Complete opening modules need a finite aperture inside their width and height.";
            else if (Body != null && Body.subMeshCount != 2)
                reason = "Complete modules need two material slots.";
            else if (Body == null)
                reason = "Assign a closed whole body mesh.";
            else if (!HasRequiredMeshData(Body, "Body", out reason)) return false;
            return reason.Length == 0;
        }

        // These headers are available even when imported meshes disable CPU Read/Write.
        // Generation assumes triangles, normals and UV0; detailed authoring advice stays in the editor.
        private static bool HasRequiredMeshData(Mesh mesh, string label, out string reason)
        {
            reason = label + ": export a non-empty triangle mesh with two material slots (+Z first, -Z second).";
            if (mesh.vertexCount == 0 || mesh.subMeshCount != 2) return false;
            for (int slot = 0; slot < mesh.subMeshCount; slot++)
                if (mesh.GetTopology(slot) != MeshTopology.Triangles || mesh.GetIndexCount(slot) == 0
                    || mesh.GetIndexCount(slot) % 3 != 0) return false;
            reason = label + ": vertex positions are missing; export geometry with positions.";
            if (!mesh.HasVertexAttribute(VertexAttribute.Position)) return false;
            reason = label + ": normals are missing; export or calculate normals for lighting.";
            if (!mesh.HasVertexAttribute(VertexAttribute.Normal)) return false;
            reason = label + ": UV0 is missing; export authored UVs for details, reveals and caps.";
            if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) return false;
            reason = "";
            return true;
        }

        private static bool PositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
