using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class ConstructionMaterialCatalog
    {
        internal static readonly string[] WallSurfaces =
            { "Brick", "Concrete", "Plaster", "Stone", "Tiles", "Wallpaper", "Wood", "Marble", "Metal" };
        internal const string ShaderName = "Environment Authoring/Horizontal Wall (Prototype)";
        private const string Surfaces = "Assets/EnvironmentAuthoring/Content/Materials/Surfaces/";
        private const string Masonry = "Assets/Game/Environment/MasonrySurfaces/";
        private const string Review = "Exports/Reviews/WallMaterials";
        private static readonly Regex RepeatingWall = new Regex(
            @"^Classic64_(?:Brick|Bricks|WallPlaster|WallStucco|WallHalfconcrete|ConcreteWall|MossyBrick|MasonrySurfaces_MossyBrick)");

        internal static string WallQuery(string surface = null) =>
            "l:WallFinish" + (string.IsNullOrEmpty(surface) ? "" : "_" + surface);

        // Explicit requests only: a domain reload never rewrites materials by itself.
        [InitializeOnLoadMethod]
        private static void ConsumeRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = Review + "/request.txt";
                if (!File.Exists(request) || File.ReadAllText(request).Trim() != "apply") return;
                File.Delete(request);
                try { File.WriteAllText(Review + "/result.txt", Refresh()); }
                catch (Exception exception)
                {
                    File.WriteAllText(Review + "/result.txt", exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Tools/Environment/Materials/Refresh wall finish catalog")]
        private static void RefreshFromMenu() => Debug.Log(Refresh());

        internal static bool CanConvert(Material material)
        {
            if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit") return false;
            if (material.GetFloat("_Surface") != 0 || material.GetFloat("_AlphaClip") != 0
                || material.GetFloat("_Metallic") != 0 || material.GetFloat("_WorkflowMode") != 1
                || material.GetFloat("_SmoothnessTextureChannel") != 0) return false;
            if (material.IsKeywordEnabled("_EMISSION") || material.GetFloat("_ReceiveShadows") != 1) return false;
            foreach (string property in new[] { "_MetallicGlossMap", "_OcclusionMap", "_ParallaxMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap" })
                if (material.GetTexture(property) != null) return false;
            return material.GetTexture("_BaseMap") != null;
        }

        internal static void Convert(Material material)
        {
            var replacement = HorizontalWallMaterialPrototype.CopyBaseColor(material);
            replacement.name = material.name;
            replacement.enableInstancing = material.enableInstancing;
            replacement.doubleSidedGI = material.doubleSidedGI;
            replacement.globalIlluminationFlags = material.globalIlluminationFlags;
            replacement.SetFloat("_Mapping", 1); // One projection, rather than three blended texture lookups.
            EditorUtility.CopySerialized(replacement, material);
            UnityEngine.Object.DestroyImmediate(replacement);
            EditorUtility.SetDirty(material);
        }

        private static string OwnedWallSurface(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (path == "Assets/Game/Environment/ToolsEquipment/Classic64_WallGrungeWhite.mat") return "Plaster";
            if (!path.StartsWith(Masonry, StringComparison.Ordinal) || !RepeatingWall.IsMatch(name)) return null;
            if (name.Contains("Brick")) return "Brick";
            return name.Contains("concrete") || name.Contains("Concrete") ? "Concrete" : "Plaster";
        }

        internal static string Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before refreshing wall materials.");
            if (Shader.Find(ShaderName) == null) throw new InvalidOperationException("Construction shader is unavailable.");
            Directory.CreateDirectory(Review + "/Before");
            var report = new List<string>();
            int converted = 0, catalogued = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] {
                "Assets/EnvironmentAuthoring/Content/Materials/Surfaces", "Assets/Game/Environment" }).OrderBy(id => id))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                string surface = OwnedWallSurface(path);
                if (surface != null && material.shader.name != ShaderName)
                {
                    if (!CanConvert(material)) { report.Add("Skipped unsupported: " + path); continue; }
                    string backup = Review + "/Before/" + guid + ".mat";
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    Convert(material);
                    converted++;
                    report.Add("Converted (Wall projection): " + path);
                }
                if (path.StartsWith(Surfaces, StringComparison.Ordinal))
                {
                    string category = path.Substring(Surfaces.Length).Split('/')[0];
                    if (WallSurfaces.Contains(category) && !path.Contains("PathUV")) surface = category;
                }
                // Also expose existing owned projected finishes, with their established surface labels.
                var labels = AssetDatabase.GetLabels(material).ToList();
                if (surface == null && material.shader.name == ShaderName && path.StartsWith("Assets/Game/", StringComparison.Ordinal))
                    surface = WallSurfaces.FirstOrDefault(category => labels.Contains("Surface_" + category));
                if (surface == null) continue;
                catalogued++;
                var next = labels.Concat(new[] { "Construction", "WallFinish", "WallFinish_" + surface, "Surface_" + surface }).Distinct().ToArray();
                if (next.Length != labels.Count) AssetDatabase.SetLabels(material, next);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            string summary = $"Converted {converted} materials; catalogued {catalogued} wall finishes.";
            File.WriteAllLines(Review + "/catalog.txt", new[] { summary }.Concat(report));
            return summary;
        }
    }
}
