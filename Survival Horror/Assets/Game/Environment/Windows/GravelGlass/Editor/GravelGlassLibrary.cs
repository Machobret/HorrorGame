using System;
using System.Linq;
using EnvironmentAuthoring;
using EnvironmentAuthoring.Editor;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Environment.Editor
{
    public static class GravelGlassLibrary
    {
        public const string Folder = "Assets/Game/Environment/Windows/GravelGlass";
        public const string Sources = Folder + "/Sources";
        public const string WallPath = Folder + "/GravelGlass_WallFamily.asset";
        public const string WindowPath = Folder + "/GravelGlass_WindowFamily.asset";
        public const string GlassPath = Folder + "/GravelGlass.mat";
        public const string FramePath = "Assets/EnvironmentAuthoring/Content/Materials/ModelSpecific/Openings/M_Window_Clean_01.mat";
        private const string BaseWallPath = "Assets/EnvironmentAuthoring/Content/WallKits/CompleteFamilies/Sources/Wall_01.asset";

        [MenuItem("Tools/Environment/Windows/Import gravel glass family")]
        public static void Import()
        {
            var source = Required<WallFamily>(BaseWallPath);
            var frame = Required<Material>(FramePath);
            var glass = Required<Material>(GlassPath);
            var models = WallFamilyImporter.ImportAll(Sources).OfType<WallOpeningInsert>().OrderBy(m => m.Shape).ToArray();
            if (models.Length != 2) throw new InvalidOperationException("Gravel glass requires exactly two window inserts.");
            foreach (var model in models)
            {
                if (model.Kind != FloorPlan.OpeningKind.Window || model.Mesh.subMeshCount != 2)
                    throw new InvalidOperationException("Export one frame/glass mesh with two submeshes per window.");
                model.Material = frame;
                model.GlassMaterial = glass;
                EditorUtility.SetDirty(model);
            }
            var windows = AssetDatabase.LoadAssetAtPath<OpeningFamily>(WindowPath);
            if (windows == null)
            {
                windows = ScriptableObject.CreateInstance<OpeningFamily>();
                AssetDatabase.CreateAsset(windows, WindowPath);
            }
            windows.Kind = FloorPlan.OpeningKind.Window;
            windows.Sets = new[] { new OpeningFamily.ModelSet {
                Name = "Gravel glass", State = OpeningFamily.Condition.Clean, Models = models } };
            EditorUtility.SetDirty(windows);

            var wall = AssetDatabase.LoadAssetAtPath<WallFamily>(WallPath);
            if (wall == null)
            {
                // This catalog owns its selection, while complete wall geometry stays shared.
                // The ID must remain the ID of those shared module definitions.
                wall = UnityEngine.Object.Instantiate(source);
                wall.name = "GravelGlass_WallFamily";
                wall.OpeningFamilies = source.OpeningFamilies.Where(f => f != null && f.Kind != FloorPlan.OpeningKind.Window)
                    .Append(windows).ToArray();
                AssetDatabase.CreateAsset(wall, WallPath);
            }
            if (!wall.IsValid(out string reason)) throw new InvalidOperationException(reason);
            var hosts = new[] { wall, source, Required<WallFamily>(WallFamilyExamples.ExteriorFamilyPath) };
            foreach (var host in hosts)
                foreach (var model in models)
                    foreach (float width in new[] { 1f, 2f })
                        if (!model.Fits(host, FloorPlan.OpeningKind.Window, host.OpeningFor(FloorPlan.OpeningKind.Window, width, model.Shape)))
                            throw new InvalidOperationException("Incompatible gravel glass window: " + model.name + " in " + host.name);
            foreach (var host in hosts)
            {
                var catalogs = host.OpeningFamilies ?? Array.Empty<OpeningFamily>();
                if (catalogs.Contains(windows)) continue;
                host.OpeningFamilies = catalogs.Append(windows).ToArray();
                EditorUtility.SetDirty(host);
            }
            AssetDatabase.SaveAssets();
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing gravel glass dependency: " + path);
    }
}
