using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Sample content only. User-authored families are ordinary editable mesh collections.
    public static class TrimFamilyExamples
    {
        public const string Folder = "Assets/EnvironmentAuthoring/Content/WallKits/TrimFamilies";
        public const string Sources = Folder + "/Sources";
        public static readonly string[] SourceNames = {
            "Trim_Independent_Plain_W1000_H0200_D0150",
            "Trim_Independent_Bevel_W1000_H0200_D0150",
            "Trim_Townhouse_Cornice_W1000_H0300_D0200"
        };

        public static TrimFamily[] CreateAssets()
        {
            var meshes = SourceNames.Select(name => AssetDatabase.LoadAllAssetsAtPath(Sources + "/" + name + ".fbx")
                .OfType<Mesh>().Single()).ToArray();
            var independent = Family("Independent", new[] { meshes[0], meshes[1] });
            // A family may reuse another collection's profile without copying its mesh.
            var townhouse = Family("Townhouse", new[] { meshes[2], meshes[1] });
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleBox_01.mat") == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat");
                if (source == null) throw new InvalidOperationException("Create the complete wall samples first.");
                var material = new Material(source) { name = "M_Plaster_SampleBox_01" };
                material.SetFloat("_Mapping",3); // One construction finish works on every face.
                AssetDatabase.CreateAsset(material, "Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleBox_01.mat");
            }
            AssetDatabase.SaveAssets();
            return new[] { independent, townhouse };
        }
        private static TrimFamily Family(string name, Mesh[] meshes)
        {
            string path = Folder + "/" + name + ".asset";
            var family = AssetDatabase.LoadAssetAtPath<TrimFamily>(path);
            if (family != null) return family; // Never replace edited sample catalogs.
            family = ScriptableObject.CreateInstance<TrimFamily>();
            family.Meshes = meshes;
            AssetDatabase.CreateAsset(family, path);
            return family;
        }
    }
}
