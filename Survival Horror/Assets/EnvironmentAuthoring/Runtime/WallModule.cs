using UnityEngine;

namespace EnvironmentAuthoring
{
    // Retained bindings for one generated closed body and its optional opening model.
    // Builders configure new output; enabling saved output never reapplies source meshes over paint.
    [DisallowMultipleComponent]
    public sealed class WallModule : MonoBehaviour
    {
        [SerializeField] private WallModuleDefinition definition;
        [SerializeField] private float fittedLength;
        [SerializeField] private MeshFilter body, openingInsert;
        [SerializeField] private string openingId;
        [SerializeField] private WallOpeningInsert selectedInsert;
        internal enum LengthFit { ScaleNative, Cropped }

        public WallModuleDefinition Definition => definition;
        public MeshFilter Body => body;
        public MeshFilter OpeningInsert => openingInsert;
        public float Length => fittedLength;
        internal string OpeningId => openingId;
        internal WallOpeningInsert Insert => selectedInsert;
        internal void SetOpeningId(string value) => openingId = value;

        internal bool TryConfigureGenerated(WallModuleDefinition next, float length, LengthFit fit,
            Mesh closedMesh, MeshFilter bodyPart, MeshFilter insertPart, out string reason)
        {
            reason = "Assign a valid wall module.";
            if (next == null || !next.IsValid(out reason)) return false;
            reason = "Length must be finite and positive; opening modules retain their authored width.";
            if (!float.IsFinite(length) || length <= 0
                || !next.AllowLengthStretch && Mathf.Abs(length - next.Width) > .0001f) return false;
            reason = "Generated modules need their fitted closed body and a body renderer.";
            if (closedMesh == null || bodyPart == null || bodyPart.GetComponent<MeshRenderer>() == null) return false;
            reason = "An opening insert needs a separate mesh renderer.";
            if (insertPart != null && insertPart.GetComponent<MeshRenderer>() == null) return false;

            // All input checks precede mutation, even when configuring an existing binding.
            definition = next; fittedLength = length; body = bodyPart; openingInsert = insertPart;
            body.sharedMesh = closedMesh;
            SetPose(body.transform);
            body.transform.localScale = new Vector3(fit == LengthFit.ScaleNative ? length / next.Width : 1, 1, 1);
            if (openingInsert != null) SetPose(openingInsert.transform);
            SetInsert(null);
            reason = ""; return true;
        }

        internal void SetInsert(WallOpeningInsert value)
        {
            selectedInsert = value;
            if (openingInsert == null) return;
            openingInsert.sharedMesh = value != null && !value.UsesPrefab ? value.Mesh : null;
            openingInsert.gameObject.SetActive(openingInsert.sharedMesh != null);
            ApplyInsertMaterial();
        }

        public void SetSideMaterials(Material sideA, Material sideB)
        {
            var fixedSlots = definition != null ? definition.BodyMaterials : null;
            if (fixedSlots != null && fixedSlots.Length == 2)
                Bind(body, fixedSlots[0], fixedSlots[1]);
            else Bind(body, sideA, sideB);
        }

        public void SetRoleMaterials(Material slot0, Material slot1) => Bind(body, slot0, slot1);

        // Appearance refresh changes bindings only; it does not reapply geometry.
        internal void ApplyInsertMaterial(Material role0 = null, Material role1 = null)
        {
            if (openingInsert == null) return;
            openingInsert.transform.localPosition = selectedInsert != null
                ? new Vector3((definition.Width - selectedInsert.SegmentWidth) / 2, 0, 0) : Vector3.zero;
            var renderer = openingInsert.GetComponent<MeshRenderer>();
            int count = selectedInsert != null ? selectedInsert.Mesh.subMeshCount : 0;
            var current = renderer.sharedMaterials;
            Material MaterialFor(int index) => selectedInsert.UseWallMaterialRoles
                && (index == 0 ? role0 : role1) != null ? (index == 0 ? role0 : role1) : selectedInsert.MaterialFor(index);
            bool same = current.Length == count;
            for (int i = 0; same && i < count; i++) same = current[i] == MaterialFor(i);
            if (same) return;
            var materials = new Material[count];
            for (int i = 0; i < count; i++) materials[i] = MaterialFor(i);
            renderer.sharedMaterials = materials;
        }

        private static void Bind(MeshFilter part, Material a, Material b)
        {
            var renderer = part.GetComponent<MeshRenderer>();
            var materials = part.sharedMesh != null && part.sharedMesh.subMeshCount == 2 ? new[] { a, b } : new[] { a };
            var current = renderer.sharedMaterials;
            if (current.Length == materials.Length && current[0] == materials[0]
                && (current.Length == 1 || current[1] == materials[1])) return;
            renderer.sharedMaterials = materials;
        }

        private static void SetPose(Transform part)
        {
            part.localPosition = Vector3.zero; part.localRotation = Quaternion.identity; part.localScale = Vector3.one;
        }
    }
}
