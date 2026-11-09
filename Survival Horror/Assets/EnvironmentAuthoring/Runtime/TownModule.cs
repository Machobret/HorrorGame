using System;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Shared authored source. Placement changes transforms; it never bends this mesh.
    [CreateAssetMenu(menuName = "Environment/Town/Module", fileName = "TownModule")]
    public sealed class TownModule : ScriptableObject
    {
        public enum ModuleKind { WallPanel, Road }
        public ModuleKind Kind;
        public Mesh Mesh;
        [Tooltip("Plain panels may stretch in length/height. Detailed panels retain native proportions and crop only the length remainder.")]
        public bool AllowStretch = true;
        public Material[] Materials = Array.Empty<Material>();
        [Min(.01f)] public float Length = 2, Height = 2, Width = .2f;
        internal Material[] ResolveMaterials(Material front, Material back = null)
        {
            int count = Mesh.subMeshCount;
            var result = new Material[count];
            var defaults = Materials ?? Array.Empty<Material>();
            for (int i = 0; i < count; i++) result[i] = i == 1 && back != null ? back : i == 0 && front != null ? front
                : i < defaults.Length ? defaults[i] : defaults.Length > 0 ? defaults[0] : null;
            return result;
        }

        internal static void CheckCompatibility(TownModule model, ModuleKind kind)
        {
            if (model == null || model.Kind != kind || model.Mesh == null || model.Mesh.vertexCount == 0
                || !float.IsFinite(model.Length) || model.Length < .01f || !float.IsFinite(model.Height) || model.Height < .01f
                || !float.IsFinite(model.Width) || model.Width < .01f)
                throw new ArgumentException("Assign a compatible Town Module with a mesh and finite positive dimensions.");
        }
    }
}
