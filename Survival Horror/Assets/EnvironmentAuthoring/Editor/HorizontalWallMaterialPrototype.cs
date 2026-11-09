using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HorizontalWallMaterialPrototype
    {
        // A new asset is deliberate: trying the mapping must not rewrite a shared
        // source material or silently replace finishes on the user's building.
        [MenuItem("Assets/Create/Environment Authoring/Construction Material")]
        private static void Create()
        {
            var material = CopyBaseColor(Selection.activeObject as Material);
            material.SetFloat("_Mapping", 3);
            material.name = "M_Surface_Custom_01";
            ProjectWindowUtil.CreateAsset(material, "M_Surface_Custom_01.mat");
        }

        internal static Material CopyBaseColor(Material source)
        {
            var material = new Material(Shader.Find("Environment Authoring/Horizontal Wall (Prototype)"))
            { name = "Projected Wall", enableInstancing = true };
            if (source != null && source.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                if (source.HasProperty("_BaseColor")) material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            }
            // Existing imported normals are not packed RGBA surface data. Leave
            // the neutral default rather than reinterpret and corrupt their channels.
            return material;
        }
    }
}
