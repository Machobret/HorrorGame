using System;
using System.IO;
using System.Linq;
using Game.Environment;
using HorrorEngine;
using HorrorGame.Chapter1;
using HorrorGame.Lighting;
using HouseToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run on an isolated project containing the current scene and asset metadata, then promote the reviewed assets.
public static class BuildIntegratedBreaker
{
    private const string Box = "Assets/Game/Environment/ElectricalBox/";
    private const string Chapter = "Assets/Game/Levels/Chapter1/";
    private const string BoxPrefab = Box + "ElectricityBox.prefab";
    private const string CabinetModel = Box + "ElectricityBox_Model.fbx";
    private const string LeverModel = Box + "ElectricityBox_Lever_Model.fbx";
    private const string IntegratedScene = Chapter + "Scenes/1stLevel_Integrated.unity";

    public static void Run()
    {
        string source = System.Environment.GetEnvironmentVariable("BREAKER_ASSET_SOURCE");
        string cabinet = Path.Combine(source, "SM_ElectricityBox_01.fbx");
        string lever = Path.Combine(source, "SM_ElectricityBox_Lever_01.fbx");
        if (!File.Exists(cabinet) || !File.Exists(lever))
            throw new FileNotFoundException("Supply the revised cabinet and lever before building.");

        BuildPrefab(cabinet, lever, ConfigureLeverPrompt());
        WireScene();
        AssetDatabase.SaveAssets();
    }

    private static void BuildPrefab(string cabinetSource, string leverSource, InteractionData prompt)
    {
        var original = PrefabUtility.LoadPrefabContents(BoxPrefab);
        try
        {
            var existing = original.transform.Find("Cabinet");
            var power = original.GetComponent<ElectricalBoxPower>();
            if (!power && existing)
            {
                var children = existing.GetComponentsInChildren<IndicatorLamp>(true);
                foreach (var lamp in children) lamp.transform.SetParent(original.transform, true);
                Object.DestroyImmediate(existing.gameObject);
                PrefabUtility.SaveAsPrefabAsset(original, BoxPrefab);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(original); }

        var cabinet = Import(cabinetSource, CabinetModel);
        var lever = Import(leverSource, LeverModel);
        var root = PrefabUtility.LoadPrefabContents(BoxPrefab);
        try
        {
            var view = root.GetComponent<ElectricalBoxIndicator>();
            var indication = new SerializedObject(view);
            var red = (IndicatorLamp)indication.FindProperty("redIndicator").objectReferenceValue;
            var green = (IndicatorLamp)indication.FindProperty("greenIndicator").objectReferenceValue;
            var yaw = Quaternion.Euler(0f, 180f, 0f);
            var cabinetRenderer = SetMesh(root.transform, "Cabinet", cabinet, yaw);
            var slots = cabinetRenderer.sharedMaterials;
            BindLamp(green, cabinetRenderer, Array.IndexOf(slots, AssetDatabase.LoadAssetAtPath<Material>(Box + "ElectricityBox_IndicatorOff.mat")));
            BindLamp(red, cabinetRenderer, Array.IndexOf(slots, AssetDatabase.LoadAssetAtPath<Material>(Box + "ElectricityBox_IndicatorRed.mat")));
            var collider = root.GetComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(cabinetRenderer.bounds.center);
            collider.size = cabinetRenderer.bounds.size;

            var hinge = Child(root.transform, "Lever Pivot");
            hinge.localPosition = yaw * (lever.transform.position - cabinet.transform.position);
            hinge.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            SetMesh(hinge, "Lever", lever, yaw);
            var switchAnchor = Child(root.transform, "Breaker interaction");
            switchAnchor.localPosition = hinge.localPosition + Vector3.forward * .28f;
            var control = AddInteraction(switchAnchor.gameObject, prompt, new Vector3(.62f, .68f, .66f));
            view.SetAntennaEnabled(false);
            var configured = GetOrAdd<ElectricalBoxPower>(root);
            SetReference(configured, "leverPivot", hinge);
            SetReference(configured, "leverInteraction", control);
            PrefabUtility.SaveAsPrefabAsset(root, BoxPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static MeshFilter Import(string source, string destination)
    {
        File.Copy(source, destination, true);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(destination);
        importer.bakeAxisConversion = true;
        importer.preserveHierarchy = true;
        importer.importAnimation = false;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ElectricalBox"),
            AssetDatabase.LoadAssetAtPath<Material>(Box + "ElectricityBox_Surface.mat"));
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(destination).GetComponentsInChildren<MeshFilter>().Single();
    }

    private static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(IntegratedScene);
        var roots = scene.GetRootGameObjects();
        T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        var opening = All<Chapter1Opening>().Single();
        var source = All<ElectricalBoxPower>().Single();
        var supply = new SerializedObject(source);
        supply.FindProperty("startsOn").boolValue = false;
        supply.FindProperty("requiresTabletAuthorization").boolValue = true;
        ((Transform)supply.FindProperty("leverPivot").objectReferenceValue).localRotation = Quaternion.Euler(45f, 0f, 0f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(supply.FindProperty("leverPivot").objectReferenceValue);
        supply.ApplyModifiedPropertiesWithoutUndo();
        var saver = GetOrAdd<SavableObjectState>(source.gameObject);
        source.GetComponent<ObjectUniqueId>().SetId("61eea3cc-438d-47cd-9fe0-24b366cbdb8d");
        var saveSettings = new SerializedObject(saver);
        saveSettings.FindProperty("m_ApplySavedTransform").boolValue = false;
        saveSettings.ApplyModifiedPropertiesWithoutUndo();
        opening.Grid.Supply = source;

        var intercom = opening.IntercomDevice ? opening.IntercomDevice.GetComponent<DeviceIndicator>() :
            All<DeviceIndicator>().Single(device => device.name == "IntercomSpeaker");
        var devices = All<DeviceIndicator>().Where(device => device != intercom && Connected(device.transform.position)).ToArray();
        if (intercom.TryGetComponent(out ElectricalDeviceFeed intercomFeed)) Object.DestroyImmediate(intercomFeed);
        foreach (var intercomLamp in intercom.GetComponentsInChildren<ShutterIndicatorLamp>(true))
            Object.DestroyImmediate(intercomLamp);
        foreach (var device in devices)
        {
            var feed = GetOrAdd<ElectricalDeviceFeed>(device.gameObject);
            SetReference(feed, "supply", source);
            SetReference(feed, "indicator", device);
        }

        foreach (var oldControl in opening.GetComponentsInChildren<Interactive>(true).Where(control =>
                     control.name == "Courtyard intercom interaction" || control.name == "Alley Gate - Confirmation Button").ToArray())
            oldControl.gameObject.SetActive(false);
        foreach (var oldLamp in source.GetComponentsInChildren<ShutterIndicatorLamp>(true))
            Object.DestroyImmediate(oldLamp);

        opening.IntercomDevice = intercom.transform;
        ConfigureGatePrompt(opening);

        var shutterLamps = All<ShutterIndicatorLamp>().Where(lamp => Connected(lamp.transform.position) &&
            !lamp.GetComponentInParent<DeviceIndicator>()).ToArray();
        foreach (var lamp in shutterLamps) SetReference(lamp, "supply", source);
        var emitters = All<Light>().Where(light => Connected(light.transform.position) &&
            light.type != LightType.Directional && !light.GetComponentInParent<ElectricalBoxPower>() &&
            !light.GetComponentInParent<DeviceIndicator>() && !light.GetComponentInParent<ShutterIndicatorLamp>() &&
            !light.GetComponentInParent<CircuitDrivenLamp>()).ToArray();
        var lighting = GetOrAdd<ElectricalFixtureFeed>(opening.gameObject);
        SetReference(lighting, "supply", source);
        SetReferences(lighting, "emitters", emitters);
        SetReferences(lighting, "fogVolumes", All<FogClearingLampLight>().Where(fog =>
            fog.SourceLight && emitters.Contains(fog.SourceLight)).ToArray());
        Debug.Log($"Connected {devices.Length} devices, {emitters.Length} room lights and {shutterLamps.Length} shutter lamps to the integrated breaker.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool Connected(Vector3 world) => world.x >= 9f && world.z <= -17f;

    private static void ConfigureGatePrompt(Chapter1Opening opening)
    {
        const string promptPath = Chapter + "Assets/CourtyardGateLocked.asset";
        var prompt = AssetDatabase.LoadAssetAtPath<InteractionData>(promptPath);
        if (!prompt)
        {
            AssetDatabase.CopyAsset(Chapter + "Assets/DepotShutterLocked.asset", promptPath);
            prompt = AssetDatabase.LoadAssetAtPath<InteractionData>(promptPath);
        }
        prompt.name = "CourtyardGateLocked";
        prompt.Prompt.Unlocalized = "Gate locked";
        EditorUtility.SetDirty(prompt);

        SetReference(opening, "lockedGateInteraction", prompt);
        EditorUtility.SetDirty(opening);
    }

    private static void BindLamp(IndicatorLamp lamp, MeshRenderer renderer, int slot)
    {
        if (!lamp || slot < 0) throw new InvalidDataException("The cabinet must have both authored indicator material slots.");
        var view = new SerializedObject(lamp);
        view.FindProperty("lensRenderer").objectReferenceValue = renderer;
        view.FindProperty("emissionSlot").intValue = slot;
        view.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MeshRenderer SetMesh(Transform parent, string name, MeshFilter model, Quaternion yaw)
    {
        Transform child = Child(parent, name);
        child.SetLocalPositionAndRotation(Vector3.zero, yaw * model.transform.rotation);
        child.localScale = model.transform.lossyScale;
        GetOrAdd<MeshFilter>(child.gameObject).sharedMesh = model.sharedMesh;
        var renderer = GetOrAdd<MeshRenderer>(child.gameObject);
        renderer.sharedMaterials = model.GetComponent<MeshRenderer>().sharedMaterials;
        return renderer;
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Interactive AddInteraction(GameObject target, InteractionData data, Vector3 size)
    {
        target.layer = LayerMask.NameToLayer("InteractionSystem");
        var trigger = GetOrAdd<BoxCollider>(target);
        trigger.isTrigger = true;
        trigger.size = size;
        var interactive = GetOrAdd<Interactive>(target);
        interactive.Data = data;
        interactive.OnInteract ??= new OnInteractionEvent();
        interactive.OnDataChanged ??= new UnityEngine.Events.UnityEvent();
        interactive.OnFinishInteract ??= new UnityEngine.Events.UnityEvent();
        return interactive;
    }

    private static InteractionData ConfigureLeverPrompt()
    {
        const string path = Box + "ElectricityBox_LeverInteraction.asset";
        var template = AssetDatabase.LoadAssetAtPath<InteractionData>(
            "Assets/AssetStore/HorrorEngine/ExampleContent/Interaction/Interaction_PickUp.asset");
        var data = AssetDatabase.LoadAssetAtPath<InteractionData>(path);
        if (!data)
        {
            data = ScriptableObject.CreateInstance<InteractionData>();
            data.Prompt = new LocalizableText { Unlocalized = "Toggle breaker" };
            data.Icon = template.Icon;
            AssetDatabase.CreateAsset(data, path);
        }
        data.AnimState = template.AnimState;
        data.InteractionDelay = template.InteractionDelay;
        data.InteractionDuration = template.InteractionDuration;
        data.RotateToInteractive = template.RotateToInteractive;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();

    private static void SetReference(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReferences<T>(Object target, string property, T[] values) where T : Object
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
