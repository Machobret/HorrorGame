using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    // Offline asset maintenance only. Runtime choices and source mesh IDs stay intact.
    public static class FenceLibraryNaming
    {
        private const string PlanPath = "Exports/Reviews/FenceNaming/plan.json";
        [Serializable] private sealed class Entry { public string guid, path; public bool folder; }
        [Serializable] private sealed class Plan { public Entry[] assets; }

        [InitializeOnLoadMethod]
        private static void RunRequestedMaintenance()
        {
            // An explicit offline request lets the open editor run this without desktop input.
            // Check once after a script reload; never poll or run automatically on asset imports.
            const string request = "Exports/Reviews/FenceNaming/request.txt";
            if (!File.Exists(request)) return;
            string action = File.ReadAllText(request).Trim();
            if (action != "apply" && action != "verify") return;
            File.Delete(request);
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        throw new InvalidOperationException("Exit Play mode before asset maintenance.");
                    if (action == "apply") Apply(); else Verify();
                    File.WriteAllText(request + ".result", "PASS");
                }
                catch (Exception error)
                {
                    File.WriteAllText(request + ".result", error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Tools/Environment/Normalize Fence Library Names")]
        public static void Apply()
        {
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            if (plan?.assets == null || plan.assets.Length == 0)
                throw new InvalidOperationException("Prepare the fence naming plan first.");
            // Preflight the full plan before moving its first folder.
            foreach (var entry in plan.assets)
            {
                string current = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(current)) throw new InvalidOperationException("Missing asset: " + entry.guid);
                string occupied = AssetDatabase.AssetPathToGUID(entry.path);
                if (!string.IsNullOrEmpty(occupied) && occupied != entry.guid)
                    throw new InvalidOperationException("Destination is occupied: " + entry.path);
            }
            var before = plan.assets.ToDictionary(entry => entry.guid, Fingerprint);
            foreach (var entry in plan.assets)
            {
                string current = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (current != entry.path)
                {
                    string problem = AssetDatabase.MoveAsset(current, entry.path);
                    if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
                }
                if (entry.folder) continue;
                string name = Path.GetFileNameWithoutExtension(entry.path);
                if (entry.path.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    var root = PrefabUtility.LoadPrefabContents(entry.path);
                    try { root.name = name; PrefabUtility.SaveAsPrefabAsset(root, entry.path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else if (entry.path.EndsWith(".asset", StringComparison.Ordinal)
                    || entry.path.EndsWith(".mat", StringComparison.Ordinal))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(entry.path);
                    asset.name = name;
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var entry in plan.assets)
            {
                if (AssetDatabase.GUIDToAssetPath(entry.guid) != entry.path)
                    throw new InvalidOperationException("GUID changed: " + entry.path);
                if (Fingerprint(entry) != before[entry.guid])
                    throw new InvalidOperationException("Asset bindings changed: " + entry.path);
            }
            Verify();
        }

        [MenuItem("Tools/Environment/Verify Fence Library Names")]
        public static void Verify()
        {
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            int modules = 0, prefabs = 0, materials = 0, families = 0;
            foreach (var entry in plan.assets.Where(entry => !entry.folder))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(entry.path);
                if (asset == null) throw new InvalidOperationException("Failed import: " + entry.path);
                if (asset.name != Path.GetFileNameWithoutExtension(entry.path))
                    throw new InvalidOperationException("Main-object name mismatch: " + entry.path + " = " + asset.name);
                if (asset is TownModule module)
                {
                    TownModule.CheckCompatibility(module, TownModule.ModuleKind.WallPanel);
                    RequireSlots(module.Mesh, module.Materials); modules++;
                }
                else if (asset is FenceFamily family)
                {
                    if (family.Panels.Length == 0 || family.Posts.Length == 0
                        || family.Panels.Any(panel => panel == null) || family.Posts.Any(post => post.Mesh == null))
                        throw new InvalidOperationException("Incomplete family: " + family.name);
                    foreach (var post in family.Posts) RequireSlots(post.Mesh, post.Materials);
                    CheckDrawing(family); families++;
                }
                else if (entry.path.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    var prefab = (GameObject)asset;
                    RequireSlots(prefab.GetComponent<MeshFilter>().sharedMesh,
                        prefab.GetComponent<MeshRenderer>().sharedMaterials); prefabs++;
                }
                else if (asset is Material material)
                {
                    if (material.shader == null || material.GetTexture("_BaseMap") == null)
                        throw new InvalidOperationException("Missing shader or texture: " + entry.path);
                    materials++;
                }
            }
            if (modules != 39 || prefabs != 80 || families != 7 || materials != 13)
                throw new InvalidOperationException("Fence inventory changed.");
            string report = $"PASS: {plan.assets.Length} GUIDs; {families} families; {modules} modules; "
                + $"{prefabs} prefabs; {materials} materials; all seven families draw whole and cropped panels with posts.";
            File.WriteAllText("Exports/Reviews/FenceNaming/unity-verification.txt", report);
            Debug.Log(report);
        }

        private static void CheckDrawing(FenceFamily family)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Fence naming verification");
                SceneManager.MoveGameObjectToScene(root, scene);
                var wall = root.AddComponent<TownWallRun>();
                wall.Model = family.Panels[0]; wall.Height = wall.Model.Height;
                wall.PostMesh = family.Posts[0].Mesh; wall.PostMaterials = family.Posts[0].Materials;
                wall.PostWidth = wall.PostMesh.bounds.size.x; wall.PostHeight = wall.PostMesh.bounds.size.y;
                wall.Path.Connect(wall.Path.AddPoint(Vector3.zero),
                    wall.Path.AddPoint(Vector3.right * (wall.Model.Length * 2 + .04f)));
                var parts = new System.Collections.Generic.List<TownRebuild.Part>();
                TownWallBuilder.Build(wall, parts);
                if (parts.Count(part => part.Key.Contains("/panel-")) != 3
                    || parts.Count(part => part.Key.Contains("/post")) != 4)
                    throw new InvalidOperationException("Panel/post generation failed: " + family.name);
                foreach (var part in parts) RequireSlots(part.Mesh, part.Materials);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void RequireSlots(Mesh mesh, Material[] materials)
        {
            if (mesh == null || mesh.vertexCount == 0 || materials == null
                || materials.Length != mesh.subMeshCount || materials.Any(material => material == null))
                throw new InvalidOperationException("Missing mesh or mismatched material slots.");
        }

        private static string Fingerprint(Entry entry)
        {
            if (entry.folder) return "folder";
            var text = new StringBuilder();
            void Ref(UnityEngine.Object asset)
            {
                if (asset == null) throw new InvalidOperationException("Missing binding: " + entry.path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id);
                text.Append(guid).Append(':').Append(id).Append(';');
            }
            var target = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(entry.guid));
            if (target is TownModule module)
            {
                Ref(module.Mesh); foreach (var finish in module.Materials) Ref(finish);
                text.Append(module.Length).Append('/').Append(module.Height).Append('/').Append(module.Width)
                    .Append('/').Append(module.AllowStretch);
            }
            else if (target is FenceFamily family)
            {
                foreach (var panel in family.Panels) Ref(panel);
                foreach (var post in family.Posts) { Ref(post.Mesh); foreach (var finish in post.Materials) Ref(finish); }
            }
            else if (entry.path.EndsWith(".prefab", StringComparison.Ordinal))
            {
                var root = (GameObject)target;
                Ref(root.GetComponent<MeshFilter>().sharedMesh);
                foreach (var finish in root.GetComponent<MeshRenderer>().sharedMaterials) Ref(finish);
            }
            else if (target is Material material)
            {
                Ref(material.shader);
                foreach (string property in material.GetTexturePropertyNames())
                    if (material.GetTexture(property) != null) Ref(material.GetTexture(property));
            }
            else if (entry.path.EndsWith(".fbx", StringComparison.Ordinal))
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(target)).OfType<Mesh>()) Ref(mesh);
            return text.ToString();
        }
    }
}
