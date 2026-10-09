using System.Collections.Generic;
using HorrorGame.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HorrorGame.Lighting.Editor
{
    /// <summary>Creates the reusable lighting library and five GI-ready horror mood test scenes.</summary>
    public static class HorrorLightingContentCreator
    {
        private const string Root = "Assets/General/Lighting";
        private const string PrefabPath = Root + "/Light Sources";
        private const string PresetPath = Root + "/Presets";
        private const string ScenePath = Root + "/Scenes";
        private const string MaterialPath = Root + "/Materials";
        private const string SkyboxPath = Root + "/Skyboxes";

        [MenuItem("Horror Game/Lighting/Create Horror Lighting Library")]
        public static void CreateLibrary()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolders();
                var materials = CreateMaterials();
                CreatePresets();
                CreateLightSourcePrefabs(materials.emissive);
                CreateMoodScenes(materials);
                UpdateSkyboxesAndSunLightsInternal();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Created horror lighting library in " + Root + ". Use Horror Game/Lighting/Bake All Lighting Test Scenes after changing lighting or geometry.");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        [MenuItem("Horror Game/Lighting/Bake All Lighting Test Scenes")]
        public static void BakeAllScenes()
        {
            if (!AssetDatabase.IsValidFolder(ScenePath)) CreateLibrary();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { ScenePath });
                foreach (string guid in scenes)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Lightmapping.Bake();
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Baked " + scenes.Length + " horror lighting scenes.");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        [MenuItem("Horror Game/Lighting/Create and Bake Horror Lighting Library")]
        public static void CreateAndBakeLibrary()
        {
            CreateLibrary();
            BakeAllScenes();
        }

        [MenuItem("Horror Game/Lighting/Update Skyboxes and Sun Lights")]
        public static void UpdateSkyboxesAndSunLights()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try { UpdateSkyboxesAndSunLightsInternal(); }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previousScenes); }
        }

        [MenuItem("Horror Game/Lighting/Create or Refresh Light Source Prefabs")]
        public static void CreateOrRefreshLightSourcePrefabs()
        {
            EnsureFolders();
            CreateLightSourcePrefabs(CreateMaterials().emissive);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/General", "Lighting");
            EnsureFolder(Root, "Editor"); EnsureFolder(Root, "Scripts"); EnsureFolder(Root, "Light Sources");
            EnsureFolder(Root, "Presets"); EnsureFolder(Root, "Scenes"); EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Skyboxes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, child);
        }

        private static (Material wall, Material floor, Material debris, Material emissive) CreateMaterials()
        {
            return (CreateMaterial("DampConcrete", new Color(.08f, .1f, .11f), .86f),
                CreateMaterial("WetAsphalt", new Color(.018f, .022f, .026f), .56f),
                CreateMaterial("BrokenMasonry", new Color(.13f, .115f, .1f), .9f),
                CreateMaterial("WarmEmissive", new Color(.95f, .28f, .045f), .35f, new Color(1f, .12f, .01f)));
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = MaterialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission.Value * 4f); }
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void CreatePresets()
        {
            CreatePreset("Evening", new Color(.09f, .07f, .09f), new Color(.035f, .025f, .035f), new Color(.009f, .007f, .01f), .38f, new Color(.15f, .09f, .16f), .008f, new Color(.55f, .24f, .18f), .4f);
            CreatePreset("Night", new Color(.012f, .022f, .05f), new Color(.006f, .009f, .018f), new Color(.002f, .003f, .006f), .22f, new Color(.015f, .025f, .05f), .016f, new Color(.2f, .35f, .8f), .2f);
            CreatePreset("Fog", new Color(.045f, .065f, .07f), new Color(.025f, .035f, .04f), new Color(.01f, .012f, .014f), .28f, new Color(.17f, .2f, .19f), .035f, new Color(.28f, .35f, .38f), .16f);
            CreatePreset("DestroyedZone", new Color(.06f, .027f, .012f), new Color(.028f, .011f, .006f), new Color(.009f, .004f, .002f), .3f, new Color(.1f, .045f, .018f), .024f, new Color(.7f, .1f, .03f), .22f);
            CreatePreset("Abandoned", new Color(.04f, .052f, .038f), new Color(.02f, .028f, .019f), new Color(.008f, .01f, .007f), .3f, new Color(.07f, .09f, .066f), .018f, new Color(.26f, .32f, .28f), .12f);
        }

        private static void CreatePreset(string name, Color sky, Color equator, Color ground, float ambient, Color fog, float density, Color key, float keyIntensity)
        {
            string path = PresetPath + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<HorrorLightingPreset>(path) != null) return;
            var p = ScriptableObject.CreateInstance<HorrorLightingPreset>();
            p.ambientSky = sky; p.ambientEquator = equator; p.ambientGround = ground; p.ambientIntensity = ambient; p.fogColor = fog; p.fogDensity = density; p.keyColor = key; p.keyIntensity = keyIntensity;
            AssetDatabase.CreateAsset(p, path);
        }

        private static void CreateLightSourcePrefabs(Material emissive)
        {
            CreateLightPrefab("Fire", LightType.Point, new Color(1f, .16f, .015f), 7f, 5f, 0f, true, .22f, emissive);
            CreateLightPrefab("StreetLamp", LightType.Spot, new Color(.58f, .72f, 1f), 10f, 17f, 70f, true, .07f, emissive);
            CreateLightPrefab("Lantern", LightType.Point, new Color(1f, .42f, .08f), 4.5f, 6f, 0f, true, .16f, emissive);
            CreateLightPrefab("Flashlight", LightType.Spot, new Color(.82f, .9f, 1f), 9f, 22f, 38f, false, 0f, emissive);
            CreateLightPrefab("Lighter", LightType.Point, new Color(1f, .22f, .025f), 2.2f, 2.8f, 0f, true, .22f, emissive);
            CreateLightPrefab("TableLamp", LightType.Point, new Color(1f, .55f, .2f), 5f, 6f, 0f, true, .04f, emissive);
            CreateLightPrefab("Chandelier", LightType.Point, new Color(1f, .7f, .36f), 8f, 10f, 0f, true, .045f, emissive);
            CreateLightPrefab("CandleHolder", LightType.Point, new Color(1f, .3f, .045f), 3.2f, 4.5f, 0f, true, .2f, emissive);
            CreateLightPrefab("EmergencyRed", LightType.Point, new Color(1f, .01f, .005f), 5f, 8f, 0f, true, .35f, emissive);
            CreateLightPrefab("FlickerTube", LightType.Point, new Color(.48f, .75f, .62f), 4.5f, 8f, 0f, true, .28f, emissive);
            CreateLightPrefab("MoonWindow", LightType.Spot, new Color(.26f, .47f, 1f), 4f, 18f, 62f, false, 0f, emissive);
        }

        private static void CreateLightPrefab(string name, LightType type, Color color, float intensity, float range, float angle, bool flicker, float flickerAmount, Material emissive)
        {
            string path = PrefabPath + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                if (name == "Chandelier") EnsureChandelierSwing(path);
                return;
            }
            var root = new GameObject(name);
            // Portable source prefabs must illuminate immediately when spawned in gameplay.
            // Static scene bounce is supplied by the separately baked test-scene lights.
            var light = root.AddComponent<Light>(); light.type = type; light.color = color; light.intensity = intensity; light.range = range; light.spotAngle = angle; light.shadows = LightShadows.Soft; light.lightmapBakeType = LightmapBakeType.Realtime;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere); mesh.name = "Emitter"; mesh.transform.SetParent(root.transform); mesh.transform.localScale = Vector3.one * .16f; mesh.GetComponent<Renderer>().sharedMaterial = emissive; Object.DestroyImmediate(mesh.GetComponent<Collider>());
            var source = root.AddComponent<HorrorLightSource>(); source.animateFlicker = flicker; source.flickerAmount = flickerAmount;
            if (name == "Chandelier") root.AddComponent<HorrorChandelierSwing>();
            PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        }

        private static void EnsureChandelierSwing(string path)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot.GetComponent<HorrorChandelierSwing>() == null)
            {
                prefabRoot.AddComponent<HorrorChandelierSwing>();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void CreateMoodScenes((Material wall, Material floor, Material debris, Material emissive) materials)
        {
            CreateScene("Evening", "A dusk street with long silhouettes and sparse amber practicals.", "Evening", materials, SceneStyle.Street);
            CreateScene("Night", "A moonlit alley with narrow pools of light and deep cover.", "Night", materials, SceneStyle.Alley);
            CreateScene("Fog", "A cold, dense-fog route that relies on close-range navigation lights.", "Fog", materials, SceneStyle.Street);
            CreateScene("DestroyedZone", "A collapsed block with warm fire contrast and dangerous shadow pockets.", "DestroyedZone", materials, SceneStyle.Destroyed);
            CreateScene("AbandonedZone", "A decayed interior where failed warm lights cut through stale green darkness.", "Abandoned", materials, SceneStyle.Interior);
        }

        private static void UpdateSkyboxesAndSunLightsInternal()
        {
            EnsureFolders();
            var styles = new[]
            {
                new SkyStyle("Evening", "Dusk Sun", new Color(.52f, .09f, .12f), new Color(.018f, .005f, .012f), .62f, .62f, new Color(1f, .28f, .16f), .48f, new Vector3(16f, -38f, 0f)),
                new SkyStyle("Night", "Cold Moon", new Color(.012f, .035f, .13f), new Color(.002f, .004f, .015f), .16f, .28f, new Color(.32f, .48f, 1f), .19f, new Vector3(44f, -32f, 0f)),
                new SkyStyle("Fog", "Clouded Moon", new Color(.17f, .22f, .22f), new Color(.035f, .05f, .047f), .22f, 1.05f, new Color(.5f, .6f, .62f), .12f, new Vector3(64f, 18f, 0f)),
                new SkyStyle("DestroyedZone", "Smoke Sun", new Color(.27f, .035f, .008f), new Color(.025f, .004f, .001f), .22f, .82f, new Color(1f, .13f, .025f), .19f, new Vector3(25f, 42f, 0f)),
                new SkyStyle("AbandonedZone", "Sickly Overcast", new Color(.055f, .09f, .06f), new Color(.007f, .012f, .008f), .17f, .72f, new Color(.34f, .43f, .32f), .10f, new Vector3(56f, -72f, 0f))
            };

            foreach (var style in styles)
            {
                string scenePath = ScenePath + "/" + style.sceneName + ".unity";
                if (!System.IO.File.Exists(scenePath)) continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RenderSettings.skybox = CreateSkybox(style);
                var sun = FindDirectionalLight();
                if (sun == null)
                {
                    var sunObject = new GameObject(style.lightName);
                    sun = sunObject.AddComponent<Light>();
                    sun.type = LightType.Directional;
                }
                sun.name = style.lightName;
                sun.color = style.lightColor;
                sun.intensity = style.lightIntensity;
                sun.shadows = LightShadows.Soft;
                sun.lightmapBakeType = LightmapBakeType.Baked;
                sun.transform.rotation = Quaternion.Euler(style.euler);
                RenderSettings.sun = sun;
                DynamicGI.UpdateEnvironment();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Light FindDirectionalLight()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light;
            return null;
        }

        private static Material CreateSkybox(SkyStyle style)
        {
            string path = SkyboxPath + "/" + style.sceneName + "_Skybox.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Skybox/Procedural")) { name = style.sceneName + " Skybox" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_SkyTint", style.skyTint);
            material.SetColor("_GroundColor", style.groundTint);
            material.SetFloat("_Exposure", style.exposure);
            material.SetFloat("_AtmosphereThickness", style.atmosphereThickness);
            material.SetFloat("_SunSize", .025f);
            material.SetFloat("_SunSizeConvergence", 7f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private readonly struct SkyStyle
        {
            public readonly string sceneName, lightName;
            public readonly Color skyTint, groundTint, lightColor;
            public readonly float exposure, atmosphereThickness, lightIntensity;
            public readonly Vector3 euler;
            public SkyStyle(string sceneName, string lightName, Color skyTint, Color groundTint, float exposure, float atmosphereThickness, Color lightColor, float lightIntensity, Vector3 euler)
            {
                this.sceneName = sceneName; this.lightName = lightName; this.skyTint = skyTint; this.groundTint = groundTint; this.exposure = exposure; this.atmosphereThickness = atmosphereThickness; this.lightColor = lightColor; this.lightIntensity = lightIntensity; this.euler = euler;
            }
        }

        private enum SceneStyle { Street, Alley, Destroyed, Interior }

        private static void CreateScene(string name, string description, string presetName, (Material wall, Material floor, Material debris, Material emissive) m, SceneStyle style)
        {
            string path = ScenePath + "/" + name + ".unity";
            if (System.IO.File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplySettings(presetName);
            BuildGeometry(m, style);
            CreateKeyLight();
            var manager = new GameObject("Horror Lighting - " + name + " (Baked) - " + description);
            var applier = manager.AddComponent<HorrorLightingPresetApplier>(); applier.preset = AssetDatabase.LoadAssetAtPath<HorrorLightingPreset>(PresetPath + "/" + presetName + ".asset"); applier.applyOnAwake = false;
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void ApplySettings(string presetName)
        {
            var p = AssetDatabase.LoadAssetAtPath<HorrorLightingPreset>(PresetPath + "/" + presetName + ".asset");
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = p.ambientSky; RenderSettings.ambientEquatorColor = p.ambientEquator; RenderSettings.ambientGroundColor = p.ambientGround; RenderSettings.ambientIntensity = p.ambientIntensity;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = p.fogColor; RenderSettings.fogDensity = p.fogDensity;
        }

        private static void BuildGeometry((Material wall, Material floor, Material debris, Material emissive) m, SceneStyle style)
        {
            Block("Ground", new Vector3(0, -.25f, 0), new Vector3(28, .5f, 48), m.floor);
            if (style == SceneStyle.Interior) { Block("Ceiling", new Vector3(0, 7.5f, 0), new Vector3(16, .4f, 32), m.wall); }
            if (style != SceneStyle.Interior)
            {
                Block("BuildingLeft", new Vector3(-8, 5, 0), new Vector3(2, 10, 48), m.wall);
                Block("BuildingRight", new Vector3(8, 5, 0), new Vector3(2, 10, 48), m.wall);
            }
            else { Block("WallLeft", new Vector3(-8, 3.6f, 0), new Vector3(.5f, 7.2f, 32), m.wall); Block("WallRight", new Vector3(8, 3.6f, 0), new Vector3(.5f, 7.2f, 32), m.wall); }
            for (int z = -18; z <= 18; z += 9) { if (style == SceneStyle.Interior) Place("Chandelier", new Vector3(0, 6.7f, z)); else Place("StreetLamp", new Vector3((z % 18 == 0) ? -6.5f : 6.5f, 5.6f, z), Quaternion.Euler(90, 0, 0)); }
            if (style == SceneStyle.Alley) { Place("EmergencyRed", new Vector3(-6.2f, 3f, 6)); Place("FlickerTube", new Vector3(6.2f, 4.2f, -7)); }
            if (style == SceneStyle.Destroyed) { for (int i = 0; i < 20; i++) { Block("Debris", new Vector3(-6 + (i % 6) * 2.1f, .35f + (i % 3) * .22f, -12 + (i / 6) * 5f), new Vector3(.7f, .5f, .9f), m.debris, new Vector3(12 * i, 25 * i, 8 * i)); } Place("Fire", new Vector3(-2, .7f, -3)); Place("Fire", new Vector3(4, .7f, 8)); }
            if (style == SceneStyle.Interior) { Place("Lantern", new Vector3(-4, 1.3f, -4)); Place("TableLamp", new Vector3(4, 1.4f, 5)); Place("FlickerTube", new Vector3(0, 6.7f, 12)); }
        }

        private static void CreateKeyLight()
        {
            var go = new GameObject("Moon / Key Light"); var light = go.AddComponent<Light>(); light.type = LightType.Directional; light.color = new Color(.35f, .47f, .8f); light.intensity = .18f; light.shadows = LightShadows.Soft; light.lightmapBakeType = LightmapBakeType.Baked; go.transform.rotation = Quaternion.Euler(45, -35, 0);
        }

        private static void Place(string prefabName, Vector3 position, Quaternion? rotation = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/" + prefabName + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); instance.transform.position = position; if (rotation.HasValue) instance.transform.rotation = rotation.Value;
        }

        private static void Block(string name, Vector3 position, Vector3 scale, Material material, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale; if (euler.HasValue) go.transform.eulerAngles = euler.Value; go.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic);
        }
    }
}
