using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class StorageBoxBuilder
    {
        public const string Root = "Assets/Game/Environment/StorageBoxes/";

        [MenuItem("Horror Game/Environment/Assign Storage Box Atlas")]
        public static void AssignAtlas()
        {
            const string atlasPath = Root + "StorageBox_Albedo.png";
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            Require(atlas && atlas.width == atlas.height && atlas.width % 8 == 0, "Square 8x8 atlas exists");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "StorageBox_Atlas.mat");
            Require(material, "Shared storage box material exists");
            material.SetTexture("_BaseMap", atlas);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            Validate();
            Require(material.GetTexture("_BaseMap") == atlas, "Supplied atlas is assigned");
            Debug.Log("STORAGE_BOX_ATLAS_ASSIGNED: " + atlasPath + " (" + atlas.width + "x" + atlas.height + ")");
        }

        [MenuItem("Horror Game/Environment/Build Storage Boxes")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "StorageBox_Atlas.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Storage atlas shader failed to import.");
            var mesh = RefreshModelMesh();
            string materialPath = Root + "StorageBox_Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(shader) { name = "StorageBox_Atlas" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var root = new GameObject("StorageBox");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                root.AddComponent<StorageBoxAtlas>();
                var collider = root.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "StorageBox.prefab");
                var variant = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    variant.name = "StorageBox_NoCollision";
                    Object.DestroyImmediate(variant.GetComponent<BoxCollider>());
                    PrefabUtility.SaveAsPrefabAsset(variant, Root + "StorageBox_NoCollision.prefab");
                }
                finally { Object.DestroyImmediate(variant); }
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Validate();
        }

        static Mesh RefreshModelMesh()
        {
            const string path = Root + "StorageBox_Model.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer, "Supplied FBX exists");
            if (importer.importAnimation || importer.importCameras || importer.importLights
                || importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var filters = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length == 1 && filters[0].sharedMesh.subMeshCount == 1, "One source mesh/submesh for instancing");
            var source = filters[0].sharedMesh;
            var baked = Object.Instantiate(source);
            try
            {
                baked.name = "StorageBox_Mesh";
                var matrix = filters[0].transform.localToWorldMatrix;
                var positions = source.vertices.Select(p => matrix.MultiplyPoint3x4(p)).ToArray();
                var bounds = new Bounds(positions[0], Vector3.zero);
                foreach (var point in positions) bounds.Encapsulate(point);
                float scale = 1f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                // Bake FBX axis/unit conversion; keep the existing 1 m footprint and bottom pivot.
                baked.vertices = positions.Select(p => (p - origin) * scale).ToArray();
                baked.normals = source.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                baked.RecalculateBounds();
                baked.RecalculateTangents();
                // Primary UVs remain exactly as authored. No atlas re-unwrapping or added insets.
                string meshPath = Root + "StorageBox_Mesh.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (!mesh)
                {
                    mesh = Object.Instantiate(baked);
                    mesh.name = "StorageBox_Mesh";
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                else EditorUtility.CopySerialized(baked, mesh);
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(mesh);
                return mesh;
            }
            finally { Object.DestroyImmediate(baked); }
        }

        // Refresh the shared mesh in place without overwriting prefab customization or texture.
        [MenuItem("Horror Game/Environment/Update Storage Box From Model")]
        public static void UpdateAtlasLayout()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            RefreshModelMesh();
            ValidateRendering();
        }

        [MenuItem("Horror Game/Environment/Validate Storage Boxes")]
        public static void Validate()
        {
            var solid = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "StorageBox.prefab");
            var decorative = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "StorageBox_NoCollision.prefab");
            Require(solid && decorative, "Both prefab assets exist");
            Require(solid.GetComponent<BoxCollider>() && decorative.GetComponentsInChildren<Collider>().Length == 0, "Collision choices");
            Require(PrefabUtility.GetPrefabAssetType(decorative) == PrefabAssetType.Variant, "No-collision prefab is a variant");
            var mesh = solid.GetComponent<MeshFilter>().sharedMesh;
            var material = solid.GetComponent<MeshRenderer>().sharedMaterial;
            Require(mesh && mesh.vertexCount == 24 && mesh.triangles.Length == 36, "24 vertices, 12 triangles");
            Require((mesh.bounds.size - Vector3.one).magnitude < .0001f && Mathf.Abs(mesh.bounds.min.y) < .0001f, "One metre cube with bottom pivot");
            var collider = solid.GetComponent<BoxCollider>();
            Require((collider.center - mesh.bounds.center).magnitude < .0001f
                && (collider.size - mesh.bounds.size).magnitude < .0001f, "Collision matches imported mesh bounds");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "StorageBox_Model.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
            Require(mesh.uv.SequenceEqual(source.uv), "Authored FBX UVs preserved exactly");
            Vector3[] faceNormals = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            for (int face = 0; face < 6; face++)
            {
                int faceTile = face < 2 ? face : face < 4 ? 2 : 3;
                float minU = faceTile / 8f, maxU = (faceTile + 1) / 8f;
                var faceUV = mesh.uv.Where((p, i) => Vector3.Dot(mesh.normals[i], faceNormals[face]) > .99f).ToArray();
                Require(faceUV.Length == 4 && faceUV.Distinct().Count() == 4
                    && faceUV.All(p => p.x >= minU && p.x <= maxU && p.y >= .875f && p.y <= 1),
                    "Face " + face + " uses its upper-left atlas cell");
            }
            Require(material && material.enableInstancing && !ShaderUtil.ShaderHasError(material.shader), "Instancing enabled and shader imported");
            Require(decorative.GetComponent<MeshFilter>().sharedMesh == mesh
                && decorative.GetComponent<MeshRenderer>().sharedMaterial == material, "Shared mesh/material");
            GameObject a = null, b = null;
            try
            {
                a = (GameObject)PrefabUtility.InstantiatePrefab(solid);
                a.GetComponent<StorageBoxAtlas>().SendMessage("RefreshEditorPlacement");
                b = Object.Instantiate(a);
                b.GetComponent<StorageBoxAtlas>().SendMessage("RefreshEditorPlacement");
                string aIdentity = new SerializedObject(a.GetComponent<StorageBoxAtlas>()).FindProperty("placementIdentity").stringValue;
                string bIdentity = new SerializedObject(b.GetComponent<StorageBoxAtlas>()).FindProperty("placementIdentity").stringValue;
                Require(!string.IsNullOrEmpty(aIdentity) && aIdentity != bIdentity, "Duplication receives a separate placement identity");
                foreach (var box in new[] { a, b })
                {
                    var component = box.GetComponent<StorageBoxAtlas>();
                    for (int i = 0; i < 64; i++)
                    {
                        component.RandomizeTile();
                        var block = new MaterialPropertyBlock();
                        box.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                        Require(component.TileIndex >= 0 && component.TileIndex < 16
                            && block.GetFloat("_AtlasTile") == component.TileIndex, "Random tile is delivered through property block");
                        Require(box.GetComponent<MeshRenderer>().sharedMaterial == material, "No material clones");
                    }
                }
            }
            finally
            {
                if (a) Object.DestroyImmediate(a);
                if (b) Object.DestroyImmediate(b);
            }
            Directory.CreateDirectory("Exports/Reviews/StorageBoxes");
            File.WriteAllText("Exports/Reviews/StorageBoxes/Validation.txt",
                "PASS: prefab variant, collision, mesh dimensions/topology, four-tile face UVs, shared material, instancing flag, shader import, duplicate identity, random design range and renderer property blocks.\n"
                + "GPU draw grouping still requires Frame Debugger verification in a representative scene.\n");
            Debug.Log("STORAGE_BOX_VALIDATION_PASS");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("Storage box validation failed: " + message);
        }

        [MenuItem("Horror Game/Environment/Validate Storage Box Rendering")]
        public static void ValidateRendering()
        {
            Validate();
            var preview = new PreviewRenderUtility();
            var atlas = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "StorageBox.prefab");
            var material = new Material(source.GetComponent<MeshRenderer>().sharedMaterial);
            Texture2D capture = null;
            bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        atlas.SetPixel(x, y, Color.HSVToRGB((x / 8 + (y / 8) * 8) / 64f, .85f, .8f));
                atlas.Apply();
                material.SetTexture("_BaseMap", atlas);
                material.SetFloat("_Smoothness", 0);
                preview.ambientColor = Color.black;
                preview.lights[0].color = Color.white;
                preview.lights[0].intensity = 1;
                preview.lights[0].transform.rotation = Quaternion.identity;
                preview.lights[1].intensity = 0;
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = 3.25f;
                camera.transform.position = new Vector3(2.25f, 2.75f, -10);
                camera.transform.rotation = Quaternion.identity;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                var boxes = new GameObject[16];
                for (int tile = 0; tile < 16; tile++)
                {
                    var box = new GameObject("Tile " + tile);
                    box.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                    var renderer = box.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var block = new MaterialPropertyBlock();
                    block.SetFloat("_AtlasTile", tile);
                    renderer.SetPropertyBlock(block);
                    box.transform.position = new Vector3((tile % 4) * 1.5f, (tile / 4) * 1.5f, 0);
                    preview.AddSingleGO(box);
                    boxes[tile] = box;
                }
                ShaderUtil.allowAsyncCompilation = false;
                Directory.CreateDirectory("Exports/Reviews/StorageBoxes");
                Vector3[] faceNormals = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
                string[] faceNames = { "Front", "Back", "Right", "Left", "Top", "Bottom" };
                for (int face = 0; face < 6; face++)
                {
                    int faceTile = face < 2 ? face : face < 4 ? 2 : 3;
                    for (int design = 0; design < 16; design++)
                    {
                        Quaternion rotation = Quaternion.FromToRotation(faceNormals[face], Vector3.back);
                        boxes[design].transform.rotation = rotation;
                        boxes[design].transform.position = new Vector3((design % 4) * 1.5f, (design / 4) * 1.5f + .5f, 0)
                            - rotation * new Vector3(0, .5f, 0);
                    }
                    preview.BeginStaticPreview(new Rect(0, 0, 512, 512));
                    // URP explicitly disables instancing for CameraType.Preview. Exercise a game camera.
                    camera.cameraType = CameraType.Game;
                    preview.Render(true);
                    capture = preview.EndStaticPreview();
                    File.WriteAllBytes("Exports/Reviews/StorageBoxes/Atlas_" + faceNames[face] + ".png", capture.EncodeToPNG());
                    Require(!ShaderUtil.ShaderHasError(material.shader), "Shader compiles for rendering");
                    for (int design = 0; design < 16; design++)
                    {
                        Vector3 point = camera.WorldToViewportPoint(new Vector3((design % 4) * 1.5f, (design / 4) * 1.5f + .5f, -.5f));
                        Color color = capture.GetPixel(Mathf.RoundToInt(point.x * 511), Mathf.RoundToInt(point.y * 511));
                        int atlasCell = (7 - design / 2) * 8 + (design % 2) * 4 + faceTile;
                        Color expected = Color.HSVToRGB(atlasCell / 64f, .85f, .8f);
                        Color.RGBToHSV(color.linear, out float hue, out float saturation, out float value);
                        Color.RGBToHSV(expected.linear, out float expectedHue, out _, out _);
                        float distance = Mathf.Abs(Mathf.DeltaAngle(hue * 360, expectedHue * 360)) / 360;
                        Require(distance < .003f && saturation > .35f && value > .01f,
                            "Design " + design + " " + faceNames[face] + " samples cell " + atlasCell + "; got " + color + ", hue error=" + distance);
                    }
                    Object.DestroyImmediate(capture);
                    capture = null;
                }
                File.AppendAllText("Exports/Reviews/StorageBoxes/Validation.txt", "PASS: all 16 designs on all six faces (96 rendered samples), covering all 64 atlas cells.\n");
                Debug.Log("STORAGE_BOX_RENDER_VALIDATION_PASS");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
                preview.Cleanup();
                if (capture) Object.DestroyImmediate(capture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(atlas);
            }
        }
    }
}
