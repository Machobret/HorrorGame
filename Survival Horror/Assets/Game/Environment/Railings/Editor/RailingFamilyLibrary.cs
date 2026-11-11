using System;
using System.IO;
using System.Linq;
using EnvironmentAuthoring;
using EnvironmentAuthoring.Editor;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Environment.Editor
{
    public static class RailingFamilyLibrary
    {
        public const string Sources = "Assets/Game/Environment/Railings/Sources";
        public const string FamilyPath = Sources + "/Wall_Railing.asset";
        public const string MaterialPath = "Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Metal/M_Metal_Rusty_04.mat";
        private const string Request = "Exports/Reviews/RailingFamily/import.request";

        [InitializeOnLoadMethod]
        private static void ConsumeExplicitRequest()
        {
            if (!File.Exists(Request)) return;
            string action = File.ReadAllText(Request).Trim();
            if (action != "import" && action != "import-only") return;
            File.Delete(Request);
            EditorApplication.delayCall += () =>
            {
                try { Import(action == "import"); File.WriteAllText(Request + ".result", "IMPORTED"); }
                catch (Exception error) { File.WriteAllText(Request + ".result", error.ToString()); Debug.LogException(error); }
            };
        }

        [MenuItem("Tools/Environment/Railings/Import railing wall family")]
        public static void ImportAndVerify() => Import(true);

        private static void Import(bool verify)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before importing the railing.");
            var metal = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (metal == null) throw new InvalidOperationException("Missing imported rusty metal material: " + MaterialPath);
            const string staging = "Exports/Reviews/RailingFamily/Models";
            foreach (string staged in Directory.GetFiles(staging, "Wall_Railing_*.fbx"))
            {
                string filename = Path.GetFileName(staged);
                if (!WallAssetName.TryParse(filename, out var metadata, out string problem))
                    throw new InvalidOperationException(problem);
                string destination = Sources + "/" + filename;
                string pattern = $"Wall_Railing_Solid_01_W{Mathf.RoundToInt(metadata.Width * 1000):D4}_H*_T100.fbx";
                var previous = Directory.GetFiles(Sources, pattern).Select(path => path.Replace('\\', '/'))
                    .Where(path => path != destination).ToArray();
                if (previous.Length > 1) throw new InvalidOperationException("Ambiguous previous railing exports.");
                if (previous.Length == 1)
                {
                    string previousModule = Path.ChangeExtension(previous[0], ".asset");
                    string destinationModule = Path.ChangeExtension(destination, ".asset");
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(previousModule)))
                    {
                        string moduleMoveProblem = AssetDatabase.MoveAsset(previousModule, destinationModule);
                        if (!string.IsNullOrEmpty(moduleMoveProblem)) throw new InvalidOperationException(moduleMoveProblem);
                    }
                    string moveProblem = AssetDatabase.MoveAsset(previous[0], destination);
                    if (!string.IsNullOrEmpty(moveProblem)) throw new InvalidOperationException(moveProblem);
                }
                File.Copy(staged, destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { Sources }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.bakeAxisConversion = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Outside"), metal);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Inside"), metal);
                importer.SaveAndReimport();
            }
            var family = verify ? WallFamilyImporter.Import(Sources) : AssetDatabase.LoadAssetAtPath<WallFamily>(FamilyPath);
            if (!verify)
                foreach (string path in Directory.GetFiles(Sources, "Wall_Railing_*.fbx"))
                {
                    WallAssetName.TryParse(path, out var metadata, out _);
                    var module = AssetDatabase.LoadAssetAtPath<WallModuleDefinition>(Path.ChangeExtension(path, ".asset"));
                    module.Width = metadata.Width; module.Height = metadata.Height; module.Thickness = metadata.Thickness;
                    module.Body = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    module.DisplayName = Path.GetFileNameWithoutExtension(path);
                    EditorUtility.SetDirty(module);
                }
            foreach (var module in family.Definitions())
            {
                module.KeepNativeHeight = true;
                module.CoverSlabEdge = false;
                module.BodyMaterials = new[] { metal, metal };
                EditorUtility.SetDirty(module);
            }
            family.name = "Wall_Railing";
            EditorUtility.SetDirty(family);
            if (!verify)
            {
                foreach (var module in family.Definitions()) AssetDatabase.SaveAssetIfDirty(module);
                AssetDatabase.SaveAssetIfDirty(family);
                Debug.Log("RAILING_FAMILY_IMPORTED: source updated with M_Metal_Rusty_04; verification skipped.");
                return;
            }
            if (!family.IsValid(out string reason)) throw new InvalidOperationException(reason);
            var modules = family.Blanks().ToArray();
            if (modules.Length != 3 || !modules.Select(m => m.Width).OrderBy(w => w).SequenceEqual(new[] { .5f, 1f, 2f }))
                throw new InvalidOperationException("The railing family needs 0.5, 1 and 2 metre modules.");
            foreach (var module in modules)
            {
                if (module.Height < .3f || Mathf.Abs(module.Thickness - .1f) > .0001f)
                    throw new InvalidOperationException("Unexpected railing dimensions: " + module.name);
                var errors = WallKitValidation.Check(module).Where(issue => issue.Severity == MessageType.Error).ToArray();
                if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.Message)));
                AssetDatabase.SaveAssetIfDirty(module);
            }
            AssetDatabase.SaveAssetIfDirty(family);
            Debug.Log($"RAILING_FAMILY_VERIFIED: 0.5/1/2 m closed modules, native {family.ReferenceBlank.Height:0.###} m height, T100, shared M_Metal_Rusty_04.");
        }
    }
}
