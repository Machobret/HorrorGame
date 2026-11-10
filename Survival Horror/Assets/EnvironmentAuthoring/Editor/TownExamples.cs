using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    internal static class TownExamples
    {
        internal const string Folder = "Assets/EnvironmentAuthoring/Content/TownKits/Basic";
        internal const string ScenePath = "Assets/EnvironmentAuthoring/Reference/TownToolkit/TownToolkit.unity";
        private static T Asset<T>(string name, Func<T> create) where T : UnityEngine.Object
        {
            string path = Folder + "/" + name + ".asset";
            var found = AssetDatabase.LoadAssetAtPath<T>(path); if (found != null) return found;
            var value = create(); value.name = name; AssetDatabase.CreateAsset(value, path); return value;
        }
        private static Material Material(string name)
        {
            string path = name switch { "Asphalt" => "Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Asphalt/M_Asphalt_PathUV_01.mat", "Cobblestone" => "Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Stone/M_Stone_CobblestonePathUV_01.mat", "Concrete" => "Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Concrete/M_Concrete_Grey_01.mat", "FenceWood" => "Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Wood_SampleUV_01.mat", _ => throw new ArgumentException("Unknown town sample finish: " + name) };
            var found = AssetDatabase.LoadAssetAtPath<Material>(path); if (found != null) return found;
            throw new InvalidOperationException("Missing bundled town material: " + path);
        }
        [MenuItem("Assets/Environment/Town/Create sample assets")]
        internal static void EnsureAssets()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var concrete = Material("Concrete"); var wood = Material("FenceWood");
            Material("Asphalt");
            var post = Asset("PostMesh", () => UnityEngine.Object.Instantiate(HouseBooleanMesh.Box(new Vector3(0, .5f, 0), Vector3.one)));
            var panel = Asset("FenceMesh", () =>
            {
                var pieces = Enumerable.Range(0, 11).Select(i => new CombineInstance { mesh = HouseBooleanMesh.Box(new Vector3(.06f + i * .188f, .6f, 0), new Vector3(.10f, 1.2f, .08f)), transform = Matrix4x4.identity }).ToList();
                foreach (float y in new[] { .3f, .9f }) pieces.Add(new CombineInstance { mesh = HouseBooleanMesh.Box(new Vector3(1, y, .06f), new Vector3(2, .12f, .08f)), transform = Matrix4x4.identity });
                var mesh = new Mesh(); mesh.CombineMeshes(pieces.ToArray(), true, true); return mesh;
            });
            Asset("Fence", () =>
            {
                var module = ScriptableObject.CreateInstance<TownModule>(); module.Mesh = panel; module.Length = 2; module.Height = 1.2f; module.Width = .14f; module.Materials = new[] { wood }; return module;
            });
            var wall = AssetDatabase.FindAssets("t:WallFamily", new[] { "Assets/EnvironmentAuthoring/Content/WallKits/CompleteFamilies" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WallFamily>).OrderBy(family => family.Id).FirstOrDefault(family => family.Blank != null)?.Blank;
            if (wall != null) Asset("Wall", () =>
            {
                var module = ScriptableObject.CreateInstance<TownModule>(); module.Mesh = wall.Body; module.Length = wall.Width; module.Height = wall.Height;
                module.Width = wall.Thickness;
                module.Materials = new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Brick_SampleWall_01.mat") ?? concrete,
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Plaster_SampleWall_01.mat") ?? concrete }; return module;
            });
            AssetDatabase.SaveAssets();
        }
        internal static void Assign(TownAuthoring target, bool fence = false)
        {
            if (target is TownWallRun wall)
            {
                wall.Model = AssetDatabase.LoadAssetAtPath<TownModule>(Folder + (fence ? "/Fence.asset" : "/Wall.asset"));
                if (wall.Model != null) wall.Height = wall.Model.Height;
                if (fence) { wall.PostMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/PostMesh.asset"); wall.PostMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Examples/M_Wood_SampleUV_01.mat") }; }
            }
            if (target is TownSidewalk walk) walk.Material = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Stone/M_Stone_CobblestonePathUV_01.mat");
            if (target is TownRoad road)
            {
                road.Material = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Asphalt/M_Asphalt_PathUV_01.mat");
                road.SidewalkMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Stone/M_Stone_CobblestonePathUV_01.mat");
            }
        }
        internal static Scene CreateReferenceScene()
        {
            EnsureAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            T Root<T>(string name, Vector3 position) where T : TownAuthoring
            {
                var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position;
                var authoring = go.AddComponent<T>(); Assign(authoring, name.Contains("Fence")); return authoring;
            }
            void Span(TownAuthoring target, Vector3 a, Vector3 b)
            {
                target.Path.Connect(target.Path.AddPoint(a), target.Path.AddPoint(b));
            }
            var wall = Root<TownWallRun>("Standalone wall — select and Edit layout", Vector3.zero);
            Span(wall, Vector3.zero, new Vector3(6, 0, 0)); Span(wall, new Vector3(6, 0, 0), new Vector3(6, 0, 4)); TownRebuild.Refresh(wall);
            var fence = Root<TownWallRun>("Fence — stepped panels and posts", new Vector3(0, 0, 7));
            Span(fence, Vector3.zero, new Vector3(6, .6f, 0)); Span(fence, new Vector3(6, .6f, 0), new Vector3(10, 1, 3)); TownRebuild.Refresh(fence);
            var walk = Root<TownSidewalk>("Sidewalk — curve and joined intersection", new Vector3(0, 0, 15));
            Span(walk, Vector3.zero, new Vector3(4, 0, 2)); Span(walk, new Vector3(4, 0, 2), new Vector3(8, 0, 0));
            Span(walk, new Vector3(8, 0, 0), new Vector3(12, 0, 0)); Span(walk, new Vector3(8, 0, 0), new Vector3(8, 0, 5)); TownRebuild.Refresh(walk);
            var road = Root<TownRoad>("Road — modular T, curved piece, generated sidewalks", new Vector3(0, 0, 29));
            Span(road, new Vector3(-8, 0, 0), Vector3.zero); Span(road, Vector3.zero, new Vector3(14, 0, 0));
            Span(road, Vector3.zero, new Vector3(0, 0, 9)); Span(road, new Vector3(14, 0, 0), new Vector3(14, 0, 9)); TownRebuild.Refresh(road);
            var ground = new GameObject("Ground — collider for point placement and settling"); SceneManager.MoveGameObjectToScene(ground, scene);
            var hillOutline = SlabGeometry.Shape(new[] { new Vector2(-1, 5), new Vector2(12, 5), new Vector2(12, 12), new Vector2(-1, 12) });
            ground.AddComponent<MeshFilter>().sharedMesh = Asset("GroundMesh", () => SlabGeometry.Extrude(SlabGeometry.Subtract(SlabGeometry.Shape(new[] { new Vector2(-14, -5), new Vector2(26, -5), new Vector2(26, 66), new Vector2(-14, 66) }), hillOutline), -.3f, -.01f, Vector2.one * 2));
            ground.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Concrete/M_Concrete_Grey_01.mat"); ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            var hill = new GameObject("Fence terrain — raycast support"); SceneManager.MoveGameObjectToScene(hill, scene);
            // Sampled terrain makes the stepped rigid panels visible. It is sample
            // content, not an implicit terrain-conformity feature in the generator.
            var hillShape = new Mesh(); var hillVertices = new System.Collections.Generic.List<Vector3>(); var hillIndices = new System.Collections.Generic.List<int>();
            for (int z = 0; z <= 14; z++) for (int x = 0; x <= 26; x++)
            {
                float px = -1 + x * .5f, pz = 5 + z * .5f;
                hillVertices.Add(new Vector3(px, .1f * Mathf.Clamp(px, 0, 10) * Mathf.Clamp01((12 - px) / 2) * Mathf.Clamp01((pz - 5) / 2) * Mathf.Clamp01((12 - pz) / 2) - .01f, pz));
                if (x < 26 && z < 14) { int a = z * 27 + x; hillIndices.AddRange(new[] { a, a + 27, a + 1, a + 1, a + 27, a + 28 }); }
            }
            hillShape.SetVertices(hillVertices); hillShape.SetTriangles(hillIndices, 0); hillShape.RecalculateNormals(); hillShape.RecalculateBounds();
            var hillMesh = Asset("FenceTerrainMesh", () => hillShape); if (hillMesh != hillShape) UnityEngine.Object.DestroyImmediate(hillShape);
            hill.AddComponent<MeshFilter>().sharedMesh = hillMesh; hill.AddComponent<MeshRenderer>().sharedMaterial = ground.GetComponent<MeshRenderer>().sharedMaterial; hill.AddComponent<MeshCollider>().sharedMesh = hillMesh;
            void Label(string text, Vector3 position)
            {
                var go = new GameObject(text); SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position; go.transform.rotation = Quaternion.Euler(90, 0, 0);
                var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 40; label.characterSize = .18f; label.anchor = TextAnchor.MiddleCenter; label.color = new Color(.88f, .88f, .80f);
            }
            Label("WALL RUN", new Vector3(3, .03f, -2)); Label("STEPPED FENCE / SETTLE DOWN", new Vector3(5, .03f, 5.5f));
            Label("SIDEWALK / FLAT PATH", new Vector3(5, .03f, 13)); Label("ROAD / SIDEWALKS", new Vector3(5, .03f, 25));
            Label("SHARED SOURCE PIECES", new Vector3(5, .03f, 43));
            string[] gallery = { "Fence", "Wall" };
            for (int i = 0; i < gallery.Length; i++)
            {
                var module = AssetDatabase.LoadAssetAtPath<TownModule>(Folder + "/" + gallery[i] + ".asset"); if (module == null) continue;
                var go = new GameObject(gallery[i] + " — shared source"); SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = new Vector3(-7 + i % 3 * 12, 0, 48 + i / 3 * 12);
                go.AddComponent<MeshFilter>().sharedMesh = module.Mesh; go.AddComponent<MeshRenderer>().sharedMaterials = module.Materials;
                Label(gallery[i], go.transform.position + new Vector3(1, .03f, -3));
            }
            var light = new GameObject("Sun"); SceneManager.MoveGameObjectToScene(light, scene); light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.AddComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 2;
            var camera = new GameObject("Toolkit camera"); SceneManager.MoveGameObjectToScene(camera, scene); var view = camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(45, 54, -32); camera.transform.LookAt(new Vector3(5, 0, 27)); view.farClipPlane = 200;
            view.orthographic = true; view.orthographicSize = 37;
            AssetDatabase.SaveAssets();
            return scene;
        }
    }
}
