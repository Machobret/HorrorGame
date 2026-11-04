#if UNITY_EDITOR
using HorrorEngine;
using HorrorGame.Chapter1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Chapter1.Editor
{
    /// <summary>Repeatable authoring entry point for the greybox puzzle slice and reusable Chapter 1 prefabs.</summary>
    public static class Chapter1PuzzleAssetBuilder
    {
        private const string Root = "Assets/Game/Levels/Chapter1";
        private const string AssetPath = Root + "/Assets";
        private const string PrefabPath = Root + "/Prefabs";
        private const string ScenePath = Root + "/Scenes";

        [MenuItem("Horror Game/Chapter 1/Rebuild District Puzzle Assets")]
        public static void Build()
        {
            EnsureFolders();
            ItemData chemical = CreateItem("CorrosiveChemical", "Corrosive Chemical", "A refrigerated industrial cleaner. The laundromat notice warns it will ruin a running washer.");
            DistrictPowerGridData gridData = CreateGridData();
            InteractionData tabletInteraction = CreateInteraction("Interaction_UseElectricianTablet", "Use electrical panel");
            InteractionData useDeviceInteraction = CreateInteraction("Interaction_UsePoweredDevice", "Use");
            PowerPanelLayout panelLayout = CreatePowerPanelLayout();
            CreateDecorativePrefab("WashingMachine", new Vector3(1.4f, 1.2f, .8f), new Color(.55f, .57f, .6f));
            CreateDecorativePrefab("PharmacyFridge", new Vector3(1.3f, 2.2f, .8f), new Color(.78f, .86f, .9f));
            CreateDecorativePrefab("StreetLamp", new Vector3(.18f, 4f, .18f), new Color(.18f, .18f, .2f));
            CreateDecorativePrefab("AbandonedTruck", new Vector3(2.8f, 1.6f, 6.2f), new Color(.28f, .22f, .16f));
            CreateDecorativePrefab("PharmacyShelves", new Vector3(3.6f, 2f, .45f), new Color(.38f, .34f, .28f));
            CreateShutterPrefab();
            CreateLampPrefab();
            CreateKeyItemPrefab("PoweredKeyItemInteractable", useDeviceInteraction);
            CreatePowerPanelPrefab(panelLayout);
            CreateScene(chemical, tabletInteraction, panelLayout, gridData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Chapter 1 district puzzle assets rebuilt.");
        }

        [MenuItem("Horror Game/Chapter 1/Create District Power Map UI Variant")]
        public static void CreateDistrictPowerMapUiVariant()
        {
            EnsureFolders();
            PowerPanelLayout layout = AssetDatabase.LoadAssetAtPath<PowerPanelLayout>(AssetPath + "/DistrictPowerPanelLayout.asset");
            if (!layout) layout = CreatePowerPanelLayout();
            CreatePowerPanelPrefab(layout);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created Chapter 1 DistrictPowerGridMapUI variant from Horror Engine's UIMap prefab.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, AssetPath, PrefabPath, ScenePath })
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        }

        private static ItemData CreateItem(string fileName, string displayName, string description)
        {
            string path = AssetPath + "/" + fileName + ".asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (!item) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, path); }
            item.Name.Unlocalized = displayName;
            item.Description.Unlocalized = description;
            item.InventoryAction = InventoryMainAction.None;
            item.Flags = ItemFlags.UseOnInteractive;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static InteractionData CreateInteraction(string fileName, string prompt)
        {
            string path = AssetPath + "/" + fileName + ".asset";
            InteractionData interaction = AssetDatabase.LoadAssetAtPath<InteractionData>(path);
            if (!interaction) { interaction = ScriptableObject.CreateInstance<InteractionData>(); AssetDatabase.CreateAsset(interaction, path); }
            interaction.Prompt.IsLocalized = false;
            interaction.Prompt.Unlocalized = prompt;
            // Reuse the engine's standard Use presentation so prompts retain the normal input icon.
            InteractionData standardUse = AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/AssetStore/HorrorEngine/ExampleContent/Interaction/Interaction_Use.asset");
            interaction.Icon = standardUse ? standardUse.Icon : null;
            interaction.InteractionDuration = standardUse ? standardUse.InteractionDuration : .2f;
            interaction.InteractionDelay = standardUse ? standardUse.InteractionDelay : .2f;
            interaction.RotateToInteractive = true;
            EditorUtility.SetDirty(interaction);
            return interaction;
        }

        private static PowerPanelLayout CreatePowerPanelLayout()
        {
            const string path = AssetPath + "/DistrictPowerPanelLayout.asset";
            PowerPanelLayout layout = AssetDatabase.LoadAssetAtPath<PowerPanelLayout>(path);
            if (!layout) { layout = ScriptableObject.CreateInstance<PowerPanelLayout>(); AssetDatabase.CreateAsset(layout, path); }
            layout.CanvasSize = new Vector2(800, 500);
            layout.Circuits.Clear();
            string[] ids = { "washing_machines", "pharmacy_refrigerator", "apartment_emergency_exit", "laundromat_lights", "pharmacy_lights", "street_lamp_1", "street_lamp_2", "street_lamp_3", "street_lamp_4", "laundromat_shutter", "pharmacy_shutter" };
            string[] labels = { "Washing machines", "Pharmacy refrigerator", "Apartment emergency exit", "Laundromat lights", "Pharmacy lights", "Street lamp 1", "Street lamp 2", "Street lamp 3", "Street lamp 4", "Laundromat shutter", "Pharmacy shutter" };
            for (int i = 0; i < ids.Length; i++)
            {
                int column = i % 3; int row = i / 3;
                Rect bounds = new Rect(35 + column * 250, 70 + row * 92, 220, 65);
                layout.Circuits.Add(new PowerPanelCircuitShape
                {
                    CircuitId = ids[i], Label = labels[i], Bounds = bounds,
                    BorderPoints = new System.Collections.Generic.List<Vector2> { bounds.min, new Vector2(bounds.xMax, bounds.yMin), bounds.max, new Vector2(bounds.xMin, bounds.yMax) }
                });
            }
            EditorUtility.SetDirty(layout);
            return layout;
        }

        private static DistrictPowerGridData CreateGridData()
        {
            const string path = AssetPath + "/DistrictPowerGridData.asset";
            DistrictPowerGridData data = AssetDatabase.LoadAssetAtPath<DistrictPowerGridData>(path);
            if (!data) { data = ScriptableObject.CreateInstance<DistrictPowerGridData>(); AssetDatabase.CreateAsset(data, path); }
            data.MaximumCapacity = 100;
            data.Circuits = new System.Collections.Generic.List<PowerCircuit>
            {
                Circuit("washing_machines", "Washing machines", 50), Circuit("pharmacy_refrigerator", "Pharmacy refrigerator", 50), Circuit("apartment_emergency_exit", "Apartment emergency exit", 50),
                Circuit("laundromat_lights", "Laundromat lights", 20), Circuit("pharmacy_lights", "Pharmacy lights", 20), Circuit("street_lamp_1", "Street lamp 1", 20), Circuit("street_lamp_2", "Street lamp 2", 20), Circuit("street_lamp_3", "Street lamp 3", 20), Circuit("street_lamp_4", "Street lamp 4", 20), Circuit("laundromat_shutter", "Laundromat shutter", 10), Circuit("pharmacy_shutter", "Pharmacy shutter", 10)
            };
            EditorUtility.SetDirty(data);
            return data;
        }

        private static GameObject CreateDecorativePrefab(string name, Vector3 scale, Color color)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = scale;
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name, color);
            Object.DestroyImmediate(root.GetComponent<Collider>());
            return SavePrefab(root, AssetPath + "/" + name + ".prefab");
        }

        private static void CreateShutterPrefab()
        {
            GameObject root = new GameObject("ActivatableDoorShutter");
            CircuitDrivenShutter shutter = root.AddComponent<CircuitDrivenShutter>();
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "ShutterPanel"; panel.transform.SetParent(root.transform); panel.transform.localScale = new Vector3(3f, 2.5f, .12f);
            panel.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Shutter", new Color(.25f, .28f, .3f));
            shutter.MovingPart = panel.transform; shutter.ClosedLocalPosition = Vector3.zero; shutter.OpenLocalPosition = Vector3.up * 2.6f;
            shutter.BlockingColliders = new[] { panel.GetComponent<Collider>() };
            SavePrefab(root, PrefabPath + "/ActivatableDoorShutter.prefab");
        }

        private static void CreateLampPrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "ActivatableLamp"; root.transform.localScale = new Vector3(.12f, 2f, .12f);
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial("LampPost", new Color(.08f, .08f, .09f));
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bulb.name = "Bulb"; bulb.transform.SetParent(root.transform); bulb.transform.localPosition = Vector3.up * .52f; bulb.transform.localScale = Vector3.one * 1.5f;
            bulb.GetComponent<Renderer>().sharedMaterial = CreateMaterial("LampBulb", new Color(.9f, .6f, .15f));
            Light light = bulb.AddComponent<Light>(); light.type = LightType.Point; light.range = 10; light.intensity = 2;
            CircuitDrivenLamp lamp = root.AddComponent<CircuitDrivenLamp>(); lamp.Lights = new[] { light }; lamp.EmissiveRenderers = new[] { bulb.GetComponent<Renderer>() };
            SavePrefab(root, PrefabPath + "/ActivatableLamp.prefab");
        }

        private static void CreateKeyItemPrefab(string name, InteractionData interactionData)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube); root.name = name;
            root.AddComponent<Interactive>().Data = interactionData;
            root.AddComponent<Chapter1KeyItemInteractable>();
            SavePrefab(root, PrefabPath + "/" + name + ".prefab");
        }

        private static void CreatePowerPanelPrefab(PowerPanelLayout layout)
        {
            const string mapPrefabPath = "Assets/AssetStore/HorrorEngine/Prefabs/UI/UIMap.prefab";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(mapPrefabPath);
            if (!source)
            {
                Debug.LogError("Could not find the Horror Engine UIMap prefab at " + mapPrefabPath);
                return;
            }

            // This is a new prefab instance, never an edit to the vendor UIMap prefab.
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            root.name = "DistrictPowerGridMapUI";
            UIMap vendorMapBehaviour = root.GetComponent<UIMap>();
            if (vendorMapBehaviour) Object.DestroyImmediate(vendorMapBehaviour);
            UIContext context = root.GetComponent<UIContext>();
            if (context) Object.DestroyImmediate(context);
            GameObject rendererPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/Mapping/MapRenderer.prefab");
            MapRenderer renderer = rendererPrefab ? ((GameObject)PrefabUtility.InstantiatePrefab(rendererPrefab, root.transform)).GetComponent<MapRenderer>() : null;
            ElectricalPanelDisplay display = root.AddComponent<ElectricalPanelDisplay>();
            display.Layout = layout;
            PowerMapUI powerMap = root.AddComponent<PowerMapUI>();
            powerMap.LocalRenderer = renderer;
            powerMap.Layout = layout;
            powerMap.MapImage = root.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            SavePrefab(root, PrefabPath + "/DistrictPowerGridMapUI.prefab");
        }

        private static void CreateScene(ItemData chemical, InteractionData tabletInteraction, PowerPanelLayout panelLayout, DistrictPowerGridData gridData)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject baseplate = GameObject.CreatePrimitive(PrimitiveType.Plane); baseplate.name = "District Greybox Baseplate"; baseplate.transform.localScale = new Vector3(5, 1, 4);
            baseplate.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Asphalt", new Color(.08f, .09f, .1f));

            GameObject systems = new GameObject("Puzzle Systems");
            DistrictPowerGrid grid = systems.AddComponent<DistrictPowerGrid>(); grid.Data = gridData; grid.MaximumCapacity = 100;
            grid.Circuits.AddRange(new[] {
                Circuit("washing_machines", "Washing machines", 50), Circuit("pharmacy_refrigerator", "Pharmacy refrigerator", 50), Circuit("apartment_emergency_exit", "Apartment emergency exit", 50),
                Circuit("laundromat_lights", "Laundromat lights", 20), Circuit("pharmacy_lights", "Pharmacy lights", 20), Circuit("street_lamp_1", "Street lamp 1", 20), Circuit("street_lamp_2", "Street lamp 2", 20), Circuit("street_lamp_3", "Street lamp 3", 20), Circuit("street_lamp_4", "Street lamp 4", 20), Circuit("laundromat_shutter", "Laundromat shutter", 10), Circuit("pharmacy_shutter", "Pharmacy shutter", 10) });
            CreatePowerPanelAuthoringLayer(systems.transform, panelLayout);

            GameObject map = new GameObject("District Puzzle Map Shapes"); map.transform.SetParent(systems.transform);
            DistrictPuzzleMap shapes = map.AddComponent<DistrictPuzzleMap>();
            shapes.Shapes.Add(Rectangle("Power room", -14, -5, 5, 4, Color.yellow)); shapes.Shapes.Add(Rectangle("Pharmacy", -7, 2, 7, 5, Color.green)); shapes.Shapes.Add(Rectangle("Laundromat / apartments", 3, 2, 10, 7, Color.cyan)); shapes.Shapes.Add(Rectangle("Truck blockade", 10, -5, 4, 3, Color.red));

            GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cube); fuse.name = "Power Room — Fuse Box & Electrician Tablet"; fuse.transform.position = new Vector3(-14, 1.2f, -5); fuse.transform.localScale = new Vector3(2, 2.4f, .5f); fuse.GetComponent<Renderer>().sharedMaterial = CreateMaterial("FuseBox", new Color(.2f, .22f, .19f));
            GameObject panelObject = CreateInstance("District Power Map UI", PrefabPath + "/DistrictPowerGridMapUI.prefab", Vector3.zero, Vector3.one); ElectricalPanelDisplay panel = panelObject.GetComponent<ElectricalPanelDisplay>(); panel.Grid = grid; panel.Layout = panelLayout;
            GameObject tablet = GameObject.CreatePrimitive(PrimitiveType.Cube); tablet.name = "Electrician Tablet (Interactable)"; tablet.layer = 8; tablet.transform.SetParent(fuse.transform); tablet.transform.localPosition = new Vector3(0, .15f, -.35f); tablet.transform.localScale = new Vector3(.8f, .55f, .07f); tablet.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Tablet", new Color(.04f, .22f, .25f));
            BoxCollider tabletCollider = tablet.GetComponent<BoxCollider>(); tabletCollider.isTrigger = true; tabletCollider.size = new Vector3(1.35f, 1.35f, 8f);
            Interactive tabletInteractive = tablet.AddComponent<Interactive>(); tabletInteractive.Data = tabletInteraction; tabletInteractive.ShowPrompt = true;
            DistrictPowerTablet tabletPanel = tablet.AddComponent<DistrictPowerTablet>(); tabletPanel.Grid = grid; tabletPanel.Panel = panel; tabletPanel.PanelLayout = panelLayout;

            CreateBuilding("Pharmacy", new Vector3(-7, 1.75f, 3), new Vector3(7, 3.5f, 5), new Color(.19f, .3f, .25f));
            CreateBuilding("Laundromat + Apartments", new Vector3(6, 3f, 3), new Vector3(10, 6f, 7), new Color(.27f, .23f, .25f));
            CreateInstance("AbandonedTruck", AssetPath + "/AbandonedTruck.prefab", new Vector3(10, .8f, -4), new Vector3(1, 1, 1));
            CreateCircuitObject("Pharmacy Shutter", PrefabPath + "/ActivatableDoorShutter.prefab", new Vector3(-7, 1.25f, .4f), grid, "pharmacy_shutter");
            CreateCircuitObject("Laundromat Shutter", PrefabPath + "/ActivatableDoorShutter.prefab", new Vector3(4.5f, 1.25f, -.55f), grid, "laundromat_shutter");
            for (int i = 0; i < 4; i++) CreateCircuitObject("Street Lamp " + (i + 1), PrefabPath + "/ActivatableLamp.prefab", new Vector3(-10 + i * 6, 2f, -2f), grid, "street_lamp_" + (i + 1));

            Chapter1KeyItemInteractable fridge = CreateKeyInteraction("Pharmacy Fridge — Refrigerated Chemical", new Vector3(-7, 1.2f, 4), grid, "pharmacy_refrigerator"); fridge.GivesKeyItem = chemical;
            CreateInstance("Pharmacy Shelf Decor", AssetPath + "/PharmacyShelves.prefab", new Vector3(-8.5f, 1f, 4.2f), Vector3.one);
            Chapter1KeyItemInteractable washer = CreateKeyInteraction("Washer — Insert Corrosive Chemical", new Vector3(6, 1.1f, 4), grid, "washing_machines"); washer.NeedsKeyItem = chemical; washer.ConsumeRequiredItem = true; washer.StartsFireAlarm = true;
            CreateInstance("Washing Machine Decor", AssetPath + "/WashingMachine.prefab", new Vector3(6, .6f, 4), Vector3.one);
            GameObject notice = GameObject.CreatePrimitive(PrimitiveType.Cube); notice.name = "Maintenance Notice: corrosive chemical damages washer, starts electrical fire; stair lowers only during powered confirmed alarm"; notice.transform.position = new Vector3(8, 2.1f, -.55f); notice.transform.localScale = new Vector3(2.5f, 1.3f, .08f);

            GameObject ladder = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/ClimbableLadder.prefab")); ladder.name = "Exterior Emergency Stairway (Climbable)"; ladder.transform.position = new Vector3(10, 1.7f, 5); ladder.SetActive(false);
            GameObject ladderControl = new GameObject("Motorized Emergency Stairway Controller"); FireSafetyLadderController controller = ladderControl.AddComponent<FireSafetyLadderController>(); controller.Grid = grid; controller.Ladder = ladder;
            GameObject exit = GameObject.CreatePrimitive(PrimitiveType.Cube); exit.name = "Visible Apartment Rear Exit — District Destination"; exit.transform.position = new Vector3(11, 4.2f, 5); exit.transform.localScale = new Vector3(1.5f, 2.5f, .2f); exit.GetComponent<Renderer>().sharedMaterial = CreateMaterial("ExitDoor", new Color(.12f, .5f, .28f));

            EditorSceneManager.SaveScene(scene, ScenePath + "/Electricity_Puzzle.unity");
        }

        private static PowerCircuit Circuit(string id, string label, int cost) => new PowerCircuit { Id = id, DisplayName = label, Consumption = cost };
        private static void CreatePowerPanelAuthoringLayer(Transform parent, PowerPanelLayout layout)
        {
            GameObject root = new GameObject("Power Panel Controller — Author Circuit Objects Here"); root.transform.SetParent(parent);
            PowerPanelController controller = root.AddComponent<PowerPanelController>(); controller.Data = layout; controller.CanvasSize = layout.CanvasSize;
            foreach (PowerPanelCircuitShape shape in layout.Circuits)
            {
                GameObject child = new GameObject("Circuit — " + shape.Label); child.transform.SetParent(root.transform); child.transform.localPosition = new Vector3(shape.Bounds.center.x, 0, shape.Bounds.center.y);
                PowerCircuitPanelElement element = child.AddComponent<PowerCircuitPanelElement>(); element.CircuitId = shape.CircuitId; element.Label = shape.Label; element.Size = shape.Bounds.size; element.BorderPoints = new System.Collections.Generic.List<Vector2>(shape.BorderPoints);
            }
        }
        private static DistrictMapShape Rectangle(string label, float x, float z, float width, float height, Color color) => new DistrictMapShape { Label = label, Color = color, Points = new System.Collections.Generic.List<Vector3> { new Vector3(x, .02f, z), new Vector3(x + width, .02f, z), new Vector3(x + width, .02f, z + height), new Vector3(x, .02f, z + height) } };
        private static void CreateBuilding(string name, Vector3 position, Vector3 scale, Color color) { GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube); building.name = name; building.transform.position = position; building.transform.localScale = scale; building.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name, color); }
        private static Chapter1KeyItemInteractable CreateKeyInteraction(string name, Vector3 position, DistrictPowerGrid grid, string circuit)
        {
            GameObject instance = CreateInstance(name, PrefabPath + "/PoweredKeyItemInteractable.prefab", position, Vector3.one);
            Chapter1KeyItemInteractable result = instance.GetComponent<Chapter1KeyItemInteractable>(); result.Grid = grid; result.RequiredCircuitId = circuit; return result;
        }
        private static void CreateCircuitObject(string name, string path, Vector3 position, DistrictPowerGrid grid, string circuit)
        {
            GameObject instance = CreateInstance(name, path, position, Vector3.one); CircuitDrivenObject objectWithCircuit = instance.GetComponent<CircuitDrivenObject>(); objectWithCircuit.Grid = grid; objectWithCircuit.CircuitId = circuit;
        }
        private static GameObject CreateInstance(string name, string path, Vector3 position, Vector3 scale) { GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); instance.name = name; instance.transform.position = position; instance.transform.localScale = scale; return instance; }
        private static GameObject SavePrefab(GameObject root, string path) { GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return saved; }
        private static Material CreateMaterial(string name, Color color)
        {
            string path = AssetPath + "/" + name + ".mat"; Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; return material;
        }
    }
}
#endif
