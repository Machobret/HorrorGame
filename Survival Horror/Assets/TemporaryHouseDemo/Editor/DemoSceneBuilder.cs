using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TemporaryHouseDemo.Editor
{
    public static class DemoSceneBuilder
    {
        public const string Root = "Assets/TemporaryHouseDemo";
        public const string ScenePath = Root + "/Scenes/TemporaryHouseDemo.unity";
        private static JObject manifest;

        [MenuItem("Tools/Temporary House Demo/Rebuild isolated scene")]
        public static void Build()
        {
            try
            {
                manifest = JObject.Parse(File.ReadAllText(Root + "/Data/ImportManifest.json"));
                Directory.CreateDirectory(Root + "/Materials");
                Directory.CreateDirectory(Root + "/Scenes");
                Directory.CreateDirectory(Root + "/Prefabs");
                AssetDatabase.Refresh();
                ConfigureAssets();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.18f, .19f, .22f);
                RenderSettings.fog = false;
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/HouseLayout.fbx");
                if (!imported) throw new Exception("House FBX import failed");
                var house = (GameObject)PrefabUtility.InstantiatePrefab(imported);
                house.name = "Temporary House - Kitchen Bedroom Corridor";
                // Static triangle meshes retain open doorways and window geometry.
                foreach (var filter in house.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
                var origin = Find(house, "DemoOrigin").position;
                var x = Find(house, "DemoAxisX").position - origin;
                var y = Find(house, "DemoAxisY").position - origin;
                var z = Find(house, "DemoAxisZ").position - origin;
                Vector3 Map(float a, float b, float c) => origin + x * a + y * b + z * c;
                if (Mathf.Abs(x.magnitude - 1) > .01f || Vector3.Dot(z.normalized, Vector3.up) < .99f)
                    throw new Exception("Unexpected FBX scale/up axis: " + x + " / " + z);

                foreach (var data in (JArray)manifest["lights"])
                {
                    var light = new GameObject((string)data["name"]).AddComponent<Light>();
                    var p = (JArray)data["positionBlender"];
                    var c = (JArray)data["color"];
                    light.transform.position = Map((float)p[0], (float)p[1], (float)p[2]);
                    light.type = LightType.Point;
                    light.range = 8;
                    light.intensity = Mathf.Clamp((float)data["powerWatts"] / 80, .4f, 5);
                    light.color = new Color((float)c[0], (float)c[1], (float)c[2]);
                    light.shadows = LightShadows.Soft;
                }
                var player = new GameObject("Demo Player - independent walkthrough");
                player.transform.position = Map(5, 2, .06f);
                player.transform.rotation = Quaternion.LookRotation(-x.normalized, Vector3.up);
                var controller = player.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = .3f;
                controller.center = Vector3.up * .9f;
                controller.stepOffset = .2f;
                controller.skinWidth = .02f;
                var cameraObject = new GameObject("Demo Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.65f;
                var camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = .04f;
                camera.farClipPlane = 70;
                camera.fieldOfView = 70;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .03f, .045f);
                var walker = player.AddComponent<DemoWalker>();
                var serialized = new SerializedObject(walker);
                serialized.FindProperty("view").objectReferenceValue = cameraObject.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(house, Root + "/Prefabs/HouseLayout.prefab");
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                controller.enabled = false; // Query house collisions without hitting the query capsule itself.
                try { Physics.SyncTransforms(); Validate(scene, house, Map); }
                finally { controller.enabled = true; }
                Debug.Log("TEMP_HOUSE_BUILD_PASS " + ScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static Transform Find(GameObject root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
            if (found == null) throw new Exception("Missing coordinate marker: " + name);
            return found;
        }

        private static void ConfigureAssets()
        {
            var normalPaths = new HashSet<string>();
            foreach (var item in (JArray)manifest["materials"])
                if (item["textures"]?["Normal"] != null) normalPaths.Add(Root + "/" + (string)item["textures"]["Normal"]);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = normalPaths.Contains(path) ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var materialMap = new Dictionary<string, Material>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit shader unavailable");
            foreach (var item in (JArray)manifest["materials"])
            {
                string name = (string)item["name"];
                string path = Root + "/Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.SetFloat("_Cull", 0);
                material.SetFloat("_Smoothness", 1 - (float?)item["roughness"] ?? .3f);
                material.SetFloat("_Metallic", (float?)item["metallic"] ?? 0);
                var color = (JArray)item["color"];
                material.SetColor("_BaseColor", new Color((float)color[0], (float)color[1], (float)color[2], (float)color[3]));
                foreach (var pair in new[] { ("Base Color", "_BaseMap"), ("Normal", "_BumpMap"), ("Emission Color", "_EmissionMap") })
                {
                    var texture = item["textures"]?[pair.Item1];
                    if (texture == null) continue;
                    material.SetTexture(pair.Item2, AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + (string)texture));
                    if (pair.Item1 == "Base Color") material.SetColor("_BaseColor", Color.white);
                    if (pair.Item1 == "Normal") material.EnableKeyword("_NORMALMAP");
                    if (pair.Item1 == "Emission Color") { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.white * .3f); }
                }
                EditorUtility.SetDirty(material);
                materialMap[name] = material;
            }
            AssetDatabase.SaveAssets();
            foreach (string model in new[] { "HouseLayout", "AssetCatalog" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "/Models/" + model + ".fbx");
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var entry in materialMap)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.Key), entry.Value);
                importer.SaveAndReimport();
            }
        }

        private static void Validate(Scene scene, GameObject house, Func<float, float, float, Vector3> map)
        {
            var meshes = house.GetComponentsInChildren<MeshRenderer>(true);
            if (meshes.Length != 81) throw new Exception("Expected 81 furnished house meshes, found " + meshes.Length);
            if (meshes.Any(r => r.sharedMaterials.Any(m => !m || m.shader.name != "Universal Render Pipeline/Lit")))
                throw new Exception("Unmapped house material");
            if (!Physics.Raycast(map(5, 2, 1), Vector3.down, out _, 2)) throw new Exception("Missing corridor floor collider");
            var navigation = JObject.Parse(File.ReadAllText(Root + "/Data/Navigation.json"));
            var free = new bool[61,81];
            var reached = new bool[61,81];
            for (int a = 1; a < 60; a++)
                for (int b = 1; b < 80; b++)
                    free[a,b] = !Physics.CheckCapsule(map(a*.1f,b*.1f,.33f),map(a*.1f,b*.1f,1.5f),.3f,~0,QueryTriggerInteraction.Ignore);
            if (!free[50,20]) throw new Exception("Player spawn capsule is obstructed");
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(50,20));
            reached[50,20] = true;
            var moves = new[] { Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var move in moves)
                {
                    var next = current + move;
                    if (next.x < 1 || next.x >= 60 || next.y < 1 || next.y >= 80 || !free[next.x,next.y] || reached[next.x,next.y]) continue;
                    reached[next.x,next.y] = true;
                    queue.Enqueue(next);
                }
            }
            var routes = new JObject();
            foreach (var target in (JObject)navigation["targets"])
            {
                var requested = (JArray)target.Value["requested"];
                float best = float.MaxValue;
                for (int a=1; a<60; a++) for (int b=1; b<80; b++)
                    if (reached[a,b]) best = Mathf.Min(best,Vector2.Distance(new Vector2(a*.1f,b*.1f),new Vector2((float)requested[0],(float)requested[1])));
                routes[target.Key] = best;
                if (best > .35f) throw new Exception("Navigation target blocked in Unity physics: " + target.Key + " distance=" + best);
            }
            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            var outside = dependencies.Where(p => !p.StartsWith(Root + "/", StringComparison.Ordinal) && !p.StartsWith("Packages/", StringComparison.Ordinal) && p != "Resources/unity_builtin_extra" && p != "Library/unity default resources").ToArray();
            if (outside.Length > 0) throw new Exception("Scene dependencies outside demo: " + string.Join(", ", outside));
            var report = new JObject { ["status"] = "PASS", ["houseMeshes"] = meshes.Length,
                ["colliders"] = house.GetComponentsInChildren<MeshCollider>(true).Length,
                ["sceneDependencies"] = new JArray(dependencies), ["externalProjectAssetDependencies"] = new JArray(outside),
                ["floorColliderVerified"] = true, ["playerSpawn"] = map(5, 2, .06f).ToString(),
                ["navigationTargetDistances"] = routes, ["playerCapsule"] = "radius 0.3m, height 1.8m",
                ["axisX"] = (map(1,0,0)-map(0,0,0)).ToString(), ["axisY"] = (map(0,1,0)-map(0,0,0)).ToString() };
            File.WriteAllText(Root + "/Data/Validation.json", report.ToString());
            AssetDatabase.Refresh();
        }
    }
}
