using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    // Persistent teaching assets, created once. This is not a runtime mesh generator or a migration.
    public static class WallFamilyExamples
    {
        public const string CompleteFolder = "Assets/EnvironmentAuthoring/Content/WallKits/CompleteFamilies";
        public const string CompleteSources = CompleteFolder + "/Sources";
        public const string CompleteFamilyPath = CompleteSources + "/Wall_01.asset";
        public const string ExteriorFamilyPath = CompleteSources + "/Wall_02.asset";

        private static string FinishPath(string name) => name == "Interior" ? "Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat" : "Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Brick_SampleWall_01.mat";
        public static WallFamily[] CreateCompleteAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<WallFamily>(CompleteFamilyPath) == null) WallFamilyImporter.ImportAll(CompleteSources);
            foreach (string name in new[] { "Interior", "Exterior" })
                if (AssetDatabase.LoadAssetAtPath<Material>(FinishPath(name)) == null)
                    throw new InvalidOperationException("Missing bundled example material: " + FinishPath(name));
            AssetDatabase.SaveAssets();
            return AssetDatabase.FindAssets("t:WallFamily", new[] { CompleteFolder }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WallFamily>).OrderBy(f => f.Id).ToArray();
        }

        public static WallOpeningInsert[] CompleteModels() => AssetDatabase.FindAssets("t:WallOpeningInsert", new[] { CompleteFolder })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<WallOpeningInsert>).ToArray();

        public static void UpdateReferenceScene()
        {
            if (!System.IO.File.Exists(".environment-authoring-test-project"))
                throw new InvalidOperationException("Reference regeneration runs in the disposable verification project; copy the reviewed result afterwards.");
            if (SceneManager.GetSceneByPath(HouseWallExamples.ModuleReferenceScenePath).isLoaded)
                throw new InvalidOperationException("Close the reference scene before regenerating its sample copy.");
            var families = CreateCompleteAssets();
            var models = CompleteModels();
            var trims = TrimFamilyExamples.CreateAssets();
            var scene = EditorSceneManager.OpenScene(HouseWallExamples.ModuleReferenceScenePath, OpenSceneMode.Additive);
            try
            {
                // This generated reference keeps its lighting/floor, not obsolete
                // open-body/cap demonstrations from earlier authoring contracts.
                foreach (var previous in scene.GetRootGameObjects().Where(g => g.name == "Complete wall families"))
                    UnityEngine.Object.DestroyImmediate(previous);
                var root = new GameObject("Complete wall families"); SceneManager.MoveGameObjectToScene(root, scene);
                int row = 0;
                foreach (var family in families)
                {
                    var group = new GameObject("Family " + family.Id); group.transform.SetParent(root.transform, false);
                    group.transform.localPosition = new Vector3(0, 0, row * 4);
                    int column = 0;
                    foreach (var definition in family.Definitions().OrderBy(d => d.AllowLengthStretch ? 0 : d.OpeningOutline.yMin > 0 ? 1 : 2)
                        .ThenBy(d => d.OpeningShape).ThenBy(d => d.Width))
                    {
                        var item = new GameObject(definition.DisplayName); item.transform.SetParent(group.transform, false);
                        item.transform.localPosition = new Vector3(column++ * 2.5f, 0, 0);
                        var filter = WallModuleExamples.CreatePart(item, "Complete authored module"); filter.sharedMesh = definition.Body;
                        filter.GetComponent<MeshRenderer>().sharedMaterials = new[] {
                            AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Brick_SampleWall_01.mat"),
                            AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat") };
                        Label(item, $"Family {family.Id} / {definition.Width} m / {definition.Thickness * 1000:0} mm\n"
                            + (definition.AllowLengthStretch ? "Solid" : definition.OpeningOutline.yMin > 0 ? "Window " + definition.OpeningShape : "Door " + definition.OpeningShape));
                    }
                    row++;
                }
                var modelGroup = new GameObject("Shared door and window models"); modelGroup.transform.SetParent(root.transform, false);
                modelGroup.transform.localPosition = new Vector3(0, 0, row * 4);
                int modelColumn = 0;
                foreach (var model in models.OrderBy(m => m.name))
                {
                    var item = new GameObject(model.name); item.transform.SetParent(modelGroup.transform, false);
                    item.transform.localPosition = new Vector3(modelColumn++ * 2.5f, 0, 0);
                    var filter = WallModuleExamples.CreatePart(item, "Shared framed model"); filter.sharedMesh = model.Mesh;
                    filter.GetComponent<MeshRenderer>().sharedMaterials = Enumerable.Range(0, model.Mesh.subMeshCount).Select(model.MaterialFor).ToArray();
                    Label(item, $"Shared {model.Kind} {model.Shape} / {model.name.Split('_').Last()}\n"
                        + $"Opening {model.Opening.width:0.##} x {model.Opening.height:0.##} m");
                }
                foreach (var family in trims)
                {
                    var group = new GameObject("Trim family " + family.name); group.transform.SetParent(root.transform, false);
                    group.transform.localPosition = new Vector3(0, 0, ++row * 4);
                    int column = 0;
                    foreach (var mesh in family.Meshes)
                    {
                        string name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh));
                        var item = new GameObject(name); item.transform.SetParent(group.transform, false);
                        item.transform.localPosition = new Vector3(column++ * 4, 0, 0);
                        for (int repeat = 0; repeat < 3; repeat++)
                        {
                            var filter = WallModuleExamples.CreatePart(item, "Trim repeat " + (repeat + 1));
                            filter.transform.localPosition = Vector3.right * repeat * mesh.bounds.size.x;
                            filter.sharedMesh = mesh;
                            filter.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat");
                        }
                        Label(item, family.name + " / " + name + "\n"
                            + $"{mesh.bounds.size.x:0.##} m repeat / {mesh.bounds.size.y:0.##} m height / {mesh.bounds.size.z:0.##} m projection");
                    }
                }
                var plan = HouseStructureCommands.CreateHouse(scene); HouseBuilding.For(plan).transform.SetParent(root.transform, false);
                HouseBuilding.For(plan).gameObject.name = "Assembled shared-model example";
                HouseBuilding.For(plan).transform.localPosition = new Vector3(-11, 0, -9);
                plan.Data.TryCreateRoom(Vector2.zero, new Vector2(8, 6), out _);
                var kit = plan.GetComponent<HouseWallBuilder>().Data;
                // This assembled reference deliberately mixes one base family
                // with an explicit alternate wall; ordinary Houses use role defaults.
                kit.DefaultFamily = families[0]; kit.ExteriorFamily = null; kit.Inserts = models;
                plan.Data.InsideMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat");
                plan.Data.OutsideMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Brick_SampleWall_01.mat");
                var wall = plan.Data.Walls[0]; plan.Data.TryAddOpening(wall.Id, 3, 2, FloorPlan.OpeningKind.Window, out _);
                var opening = wall.Openings[0]; opening.SetAssets("01", models.First(i => i.Kind == opening.Kind && i.Shape == "01"));
                var other = plan.Data.Walls[1]; other.SetFamily(families[1]);
                plan.Data.TryAddOpening(other.Id, 3, 2, FloorPlan.OpeningKind.Window, out _);
                other.Openings[0].SetAssets("01", opening.Insert);
                var profiles = trims.SelectMany(family => family.Meshes).Distinct().ToArray();
                for (int index = 0; index < plan.Data.Walls.Count; index++)
                {
                    var mesh = profiles[index % profiles.Length];
                    plan.Data.Walls[index].SetTrim(new WallTrim { Mesh = mesh,
                        Material = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat"),
                        Height = mesh.bounds.size.y, Depth = mesh.bounds.size.z, Offset = 0 });
                }
                var building = HouseBuilding.For(plan);
                foreach (var boundary in building.Boundaries)
                {
                    boundary.ReplaceRegions(Array.Empty<HouseStack.SurfaceRegion>());
                    HouseSurfaceLayout.Seed(boundary, string.IsNullOrEmpty(boundary.LowerId) ? null : plan.Data,
                        string.IsNullOrEmpty(boundary.UpperId) ? null : plan.Data);
                }
                HouseRebuildCoordinator.RefreshNow(HouseBuilding.For(plan));
                if (!string.IsNullOrEmpty(plan.GetComponent<HouseWallBuilder>().Output.Problem)) throw new InvalidOperationException(plan.GetComponent<HouseWallBuilder>().Output.Problem);
                var floor = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Display floor");
                if (floor != null)
                {
                    floor.transform.position = new Vector3(6, -.15f, 2);
                    floor.transform.localScale = new Vector3(38, .1f, 33);
                }
                var camera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
                if (camera != null)
                {
                    camera.transform.position = new Vector3(27, 26, -32);
                    camera.transform.LookAt(new Vector3(6, 1, 0));
                }
                var guide = new GameObject("Authoring guide"); guide.transform.SetParent(root.transform, false);
                guide.transform.localPosition = new Vector3(10, 0, -6);
                Label(guide, "Complete closed modules / metres\nOutside: slot 0, +Z / Inside: slot 1, -Z\n"
                    + "Solid: 0.5 / 1 / 2 m; openings: rigid 1 / 2 m\n"
                    + "UV V: full height; wall materials project at physical scale\n"
                    + "FBX templates: Assets/EnvironmentAuthoring/Content/WallKits/CompleteFamilies/Sources\nTrim templates: Assets/EnvironmentAuthoring/Content/WallKits/TrimFamilies/Sources");
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void Label(GameObject owner, string value)
        {
            var label = new GameObject("Label"); label.transform.SetParent(owner.transform, false);
            // Floor labels stay beside their module instead of obscuring every
            // neighbouring row when viewing the kit from the opposite side.
            label.transform.localPosition = new Vector3(0, .02f, -.3f);
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var text = label.AddComponent<TextMesh>(); text.text = value; text.characterSize = .06f; text.fontSize = 48;
            text.anchor = TextAnchor.UpperLeft; text.color = new Color(.08f, .1f, .12f);
        }

    }
}
