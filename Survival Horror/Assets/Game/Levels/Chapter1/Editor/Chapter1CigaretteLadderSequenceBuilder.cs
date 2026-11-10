#if UNITY_EDITOR
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Chapter1.Editor
{
    /// <summary>Creates/updates the cigarette signal assets and places them beside the existing ClimbableLadder.</summary>
    public static class Chapter1CigaretteLadderSequenceBuilder
    {
        private const string Root = "Assets/Game/Levels/Chapter1";
        private const string AssetsPath = Root + "/Assets";
        private const string PrefabsPath = Root + "/Prefabs";

        [MenuItem("Horror Game/Chapter 1/Install Cigarette Ladder Sequence")]
        public static void Install()
        {
            const string scenePath = Root + "/Scenes/Electricity_Puzzle.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ItemData cigarettes = CreateItem("CigarettePack", "Cigarette Pack", "A damp pack of cigarettes. It needs a lighter to make a signal.");
            ItemData lighter = CreateItem("Lighter", "Lighter", "A working lighter. It can ignite the cigarette signal.");
            CigaretteLadderInventoryCombination combination = CreateCombination(cigarettes, lighter);
            CreatePickupPrefab("CigarettePackPickup", cigarettes, new Color(.78f, .72f, .53f));
            CreatePickupPrefab("LighterPickup", lighter, new Color(.82f, .22f, .06f));
            GameObject areaPrefab = CreateAreaPrefab(combination);

            GameObject ladder = GameObject.Find("ClimbableLadder");
            if (!ladder)
            {
                Debug.LogError("Could not find the existing scene object named ClimbableLadder. Open Electricity_Puzzle and run this command again.");
                return;
            }

            Transform sequenceRoot = FindOrCreate("Cigarette Signal Ladder Sequence").transform;
            sequenceRoot.position = ladder.transform.position;
            sequenceRoot.rotation = ladder.transform.rotation;
            Chapter1LadderSignalCutscene sequence = sequenceRoot.GetComponent<Chapter1LadderSignalCutscene>();
            if (!sequence) sequence = sequenceRoot.gameObject.AddComponent<Chapter1LadderSignalCutscene>();
            sequence.Ladder = ladder.transform;
            sequence.Duration = 3f;

            Transform loweredPosition = sequenceRoot.Find("Ladder Lowered Position — move this to set final placement");
            if (!loweredPosition)
            {
                loweredPosition = new GameObject("Ladder Lowered Position — move this to set final placement").transform;
                loweredPosition.SetParent(sequenceRoot);
                loweredPosition.SetPositionAndRotation(ladder.transform.position + Vector3.down * 4f, ladder.transform.rotation);
            }
            CreatePositionMarker(loweredPosition, new Color(.2f, .85f, 1f));
            sequence.LadderLoweredPosition = loweredPosition;

            Transform target = FindOrCreateChild(sequenceRoot, "Highlighted Run Point");
            target.localPosition = ladder.transform.InverseTransformPoint(ladder.transform.position - ladder.transform.forward * 1.5f);
            CreateHighlight(target);
            sequence.HighlightedPoint = target;

            Transform existingArea = sequenceRoot.Find("Cigarette Signal Area — combine items here");
            if (!existingArea) existingArea = sequenceRoot.Find("Cigarette Signal Area — Use both items here");
            if (existingArea) Object.DestroyImmediate(existingArea.gameObject);
            GameObject area = PrefabUtility.InstantiatePrefab(areaPrefab, sequenceRoot) as GameObject;
            area.name = "Cigarette Signal Area — combine items here";
            area.transform.localPosition = ladder.transform.InverseTransformPoint(ladder.transform.position - ladder.transform.forward * 3.3f);
            area.transform.localRotation = Quaternion.identity;
            CigaretteLadderSignalArea areaController = area.GetComponent<CigaretteLadderSignalArea>();
            areaController.Sequence = sequence; areaController.Combination = combination;

            CreateOrReplacePickup(sequenceRoot, "Cigarette Pack Pickup", PrefabsPath + "/CigarettePackPickup.prefab", ladder.transform.position - ladder.transform.right * 3.5f - ladder.transform.forward * 4f);
            CreateOrReplacePickup(sequenceRoot, "Lighter Pickup", PrefabsPath + "/LighterPickup.prefab", ladder.transform.position + ladder.transform.right * 3.5f - ladder.transform.forward * 4f);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Installed cigarette signal ladder sequence beside ClimbableLadder.", sequenceRoot);
        }

        private static ItemData CreateItem(string filename, string title, string description)
        {
            string path = AssetsPath + "/" + filename + ".asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (!item) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, path); }
            item.Name.Unlocalized = title; item.Description.Unlocalized = description;
            item.InventoryAction = InventoryMainAction.None; item.Flags = ItemFlags.UseOnInteractive | ItemFlags.Combinable;
            EditorUtility.SetDirty(item); return item;
        }

        private static InteractionData CreateInteraction(string filename, string prompt)
        {
            string path = AssetsPath + "/" + filename + ".asset";
            InteractionData data = AssetDatabase.LoadAssetAtPath<InteractionData>(path);
            if (!data) { data = ScriptableObject.CreateInstance<InteractionData>(); AssetDatabase.CreateAsset(data, path); }
            data.Prompt.IsLocalized = false; data.Prompt.Unlocalized = prompt;
            InteractionData standard = AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/AssetStore/HorrorEngine/ExampleContent/Interaction/Interaction_Use.asset");
            data.Icon = standard ? standard.Icon : null; data.InteractionDuration = standard ? standard.InteractionDuration : .2f; data.InteractionDelay = standard ? standard.InteractionDelay : .2f;
            EditorUtility.SetDirty(data); return data;
        }

        private static void CreatePickupPrefab(string name, ItemData item, Color colour)
        {
            const string pickupBasePath = "Assets/AssetStore/HorrorEngine/Prefabs/PickupItem.prefab";
            GameObject root = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(pickupBasePath)) as GameObject;
            if (!root) { Debug.LogError("Missing Horror Engine PickupItem prefab at " + pickupBasePath); return; }
            root.name = name;
            PickupItem pickup = root.GetComponent<PickupItem>(); pickup.Entry = new InventoryEntry { Item = item, Count = 1 }; pickup.ShowItemSplash = true;
            Transform oldVisual = root.transform.Find("Chapter 1 Visual"); if (oldVisual) Object.DestroyImmediate(oldVisual.gameObject);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "Chapter 1 Visual"; visual.transform.SetParent(root.transform); visual.transform.localPosition = Vector3.up * .1f; visual.transform.localScale = new Vector3(.35f, .12f, .22f);
            Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name, colour);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabsPath + "/" + name + ".prefab"); Object.DestroyImmediate(root);
        }

        private static CigaretteLadderInventoryCombination CreateCombination(ItemData cigarettes, ItemData lighter)
        {
            const string path = AssetsPath + "/CigaretteLadderCombination.asset";
            CigaretteLadderInventoryCombination combination = AssetDatabase.LoadAssetAtPath<CigaretteLadderInventoryCombination>(path);
            if (!combination) { combination = ScriptableObject.CreateInstance<CigaretteLadderInventoryCombination>(); AssetDatabase.CreateAsset(combination, path); }
            combination.Item1 = cigarettes; combination.Item2 = lighter; EditorUtility.SetDirty(combination); return combination;
        }

        private static GameObject CreateAreaPrefab(CigaretteLadderInventoryCombination combination)
        {
            GameObject root = new GameObject("CigaretteSignalArea"); root.transform.localScale = new Vector3(3f, 2f, 2.5f);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            CigaretteLadderSignalArea area = root.AddComponent<CigaretteLadderSignalArea>(); area.Combination = combination;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsPath + "/CigaretteSignalArea.prefab"); Object.DestroyImmediate(root); return saved;
        }

        private static GameObject FindOrCreate(string name) { GameObject found = GameObject.Find(name); return found ? found : new GameObject(name); }
        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name); if (child) return child;
            return new GameObject(name).transform.WithParent(parent);
        }
        private static void CreateHighlight(Transform target)
        {
            Transform old = target.Find("Highlight"); if (old) Object.DestroyImmediate(old.gameObject);
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder); glow.name = "Highlight"; glow.transform.SetParent(target); glow.transform.localPosition = Vector3.zero; glow.transform.localScale = new Vector3(.75f, .03f, .75f);
            glow.GetComponent<Collider>().enabled = false; glow.GetComponent<Renderer>().sharedMaterial = CreateMaterial("CigaretteSignalHighlight", new Color(1f, .55f, .05f));
        }
        private static void CreatePositionMarker(Transform target, Color colour)
        {
            Transform old = target.Find("Placement Marker"); if (old) Object.DestroyImmediate(old.gameObject);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name = "Placement Marker"; marker.transform.SetParent(target); marker.transform.localPosition = Vector3.zero; marker.transform.localScale = new Vector3(.45f, .05f, .45f);
            marker.GetComponent<Collider>().enabled = false; marker.GetComponent<Renderer>().sharedMaterial = CreateMaterial("LadderLoweredPositionMarker", colour);
        }
        private static void CreateOrReplacePickup(Transform parent, string name, string prefabPath, Vector3 position)
        {
            Transform old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            GameObject pickup = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), parent) as GameObject;
            pickup.name = name; pickup.transform.position = position;
        }
        private static Material CreateMaterial(string name, Color colour)
        {
            string path = AssetsPath + "/" + name + ".mat"; Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = colour; return mat;
        }
        private static Transform WithParent(this Transform child, Transform parent) { child.SetParent(parent); return child; }
    }
}
#endif
