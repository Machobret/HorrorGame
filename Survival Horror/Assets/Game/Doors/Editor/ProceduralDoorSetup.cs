using System;
using System.Linq;
using EnvironmentAuthoring;
using HorrorEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Game.Doors.Editor
{
    // The toolkit owns placement; this adapter alone supplies Horror Engine behaviour.
    public static class ProceduralDoorSetup
    {
        private const string Prefabs = "Assets/Game/Doors/Prefabs";
        private const string BasePath = Prefabs + "/Door_Base.prefab";
        private const string Models = "Assets/EnvironmentAuthoring/Content/WallKits/Waterpixels/Models";

        [MenuItem("Tools/Game/Doors/Set up procedural door prefabs")]
        public static void Install()
        {
            // Importing remains passive. This explicit first setup initializes only toolkit defaults.
            if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../ProjectSettings/EnvironmentAuthoring.asset")))
                Require(EditorApplication.ExecuteMenuItem("Tools/Environment/Use bundled defaults"), "Bundled defaults setup is unavailable.");
            if (!AssetDatabase.IsValidFolder(Prefabs)) AssetDatabase.CreateFolder("Assets/Game/Doors", "Prefabs");
            var presets = AssetDatabase.FindAssets("t:WallOpeningInsert", new[] { Models })
                .Select(g => AssetDatabase.LoadAssetAtPath<WallOpeningInsert>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p.Kind == FloorPlan.OpeningKind.Doorway).OrderBy(p => p.name).ToArray();
            Require(presets.Length == 16, "Expected the sixteen bundled door visuals.");
            var first = presets.First(p => p.name == "Door Interior 01");
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            if (basePrefab == null) basePrefab = CreateBase(first);
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect procedural door prefabs");
            foreach (var preset in presets)
            {
                // A rerun must never replace a user's custom prefab assignment or edit its contents.
                if (preset.Prefab != null) continue;
                string path = Prefabs + "/" + preset.name.Replace(' ', '_') + ".prefab";
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (variant == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                    try
                    {
                        instance.name = preset.name.Replace(' ', '_');
                        SetVisual(instance.transform.Find("Visual"), preset);
                        variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                Undo.RecordObject(preset, "Connect procedural door prefab");
                preset.Prefab = variant;
                EditorUtility.SetDirty(preset);
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log("PROCEDURAL_DOORS_INSTALLED: base + 16 native variants; existing family presets retained.");
        }

        private static GameObject CreateBase(WallOpeningInsert visualPreset)
        {
            int layer = LayerMask.NameToLayer("InteractionSystem");
            Require(layer >= 0, "The Horror Engine InteractionSystem layer is required.");
            var data = AssetDatabase.LoadAssetAtPath<InteractionData>(AssetDatabase.GUIDToAssetPath("ae3c70015dc01284ebe0ebd182e71263"));
            Require(data != null, "The shared Door interaction data is missing.");
            var root = new GameObject("Door_Base") { layer = layer };
            try
            {
                var door = root.AddComponent<TwoSidedDoor>();
                door.ExitPoint = Child(root, "Exit_A", Vector3.back * 1.5f);
                door.ExitPoint.localRotation = Quaternion.Euler(0, 180, 0);
                door.OtherExitPoint = Child(root, "Exit_B", Vector3.forward * 1.5f);
                door.OnOpened = new UnityEvent(); door.OnLocked = new UnityEvent();
                var interaction = root.AddComponent<Interactive>();
                interaction.Data = data;
                interaction.OnInteract = new OnInteractionEvent();
                interaction.OnFinishInteract = new UnityEvent(); interaction.OnDataChanged = new UnityEvent();
                UnityEventTools.AddPersistentListener<IInteractor>(interaction.OnInteract, door.Use);
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true; trigger.center = Vector3.up * 1.05f;
                trigger.size = new Vector3(.9f, 2.1f, .4f);
                // The interaction ray reaches the trigger before the solid leaf, from either side.
                var blocker = Child(root, "BodyCollider", Vector3.zero).gameObject.AddComponent<BoxCollider>();
                blocker.center = trigger.center; blocker.size = new Vector3(.9f, 2.1f, .12f);
                var visual = Child(root, "Visual", Vector3.zero);
                visual.gameObject.AddComponent<MeshFilter>(); visual.gameObject.AddComponent<MeshRenderer>();
                SetVisual(visual, visualPreset);
                return PrefabUtility.SaveAsPrefabAsset(root, BasePath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Child(GameObject parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent.transform, false); child.localPosition = position;
            return child;
        }

        private static void SetVisual(Transform visual, WallOpeningInsert preset)
        {
            visual.localPosition = new Vector3(-preset.Opening.center.x, -preset.Opening.yMin, 0);
            visual.GetComponent<MeshFilter>().sharedMesh = preset.Mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterial = preset.Material;
            if (!PrefabUtility.IsPartOfPrefabInstance(visual)) return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.GetComponent<MeshFilter>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.GetComponent<MeshRenderer>());
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
    }
}
