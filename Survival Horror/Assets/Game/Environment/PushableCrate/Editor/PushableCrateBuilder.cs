using System;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class PushableCrateBuilder
    {
        const string Folder = "Assets/Game/Environment/PushableCrate/";
        const string Sample = "Assets/AssetStore/HorrorEngine/ExampleContent/Scenes/SampleScene2.unity";

        [MenuItem("Horror Game/Environment/Build Pushable Crate")]
        public static void Build()
        {
            string legacy = Folder + "PushableCrate.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacy) && !AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PushableCrate1.5.prefab"))
            {
                string error = AssetDatabase.MoveAsset(legacy, Folder + "PushableCrate1.5.prefab");
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            }
            BuildSize("PushableCrate1", .5f);
            BuildSize("PushableCrate1.5", .75f);
        }

        static void BuildSize(string name, float scale)
        {
            string Output = Folder + name + ".prefab";
            var scene = EditorSceneManager.OpenPreviewScene(Sample);
            GameObject copy = null;
            try
            {
                var original = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Pushable>(true))
                    .FirstOrDefault(p => p.name == "PushableWithWeight" && p.transform.Find("Crate"));
                if (!original) throw new Exception("SampleScene2 pushable crate was not found.");
                copy = Object.Instantiate(original.gameObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
                if (PrefabUtility.IsPartOfPrefabInstance(copy))
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var crateMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "PushableCrate_Wood.mat");
                if (!crateMaterial) throw new Exception("Owned crate material missing.");
                copy.transform.Find("Crate").GetComponent<MeshRenderer>().sharedMaterial = crateMaterial;
                copy.name = name;
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                copy.transform.localScale = Vector3.one * scale;
                var previous = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
                foreach (var identity in copy.GetComponentsInChildren<ObjectUniqueId>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(identity.transform, copy.transform);
                    var oldTransform = previous ? (string.IsNullOrEmpty(path) ? previous.transform : previous.transform.Find(path)) : null;
                    var oldId = oldTransform ? oldTransform.GetComponent<ObjectUniqueId>() : null;
                    if (oldId) identity.SetId(oldId.Id); else identity.RegenerateId();
                    identity.IsUniqueInstance = false; identity.IntentionallyDuplicated = false;
                }
                // Scene-wired puzzle destinations do not belong in a reusable crate.
                copy.GetComponent<Pushable>().OnStartPush = new UnityEngine.Events.UnityEvent();
                copy.GetComponent<Pushable>().OnStopPush = new UnityEngine.Events.UnityEvent();
                if (name == "PushableCrate1")
                {
                    ConfigureClimbing(copy);
                    SmallCrateAppearance.Apply(copy);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, Output);
                if (!prefab) throw new Exception("Could not save pushable crate.");
                var pushable = prefab.GetComponent<Pushable>();
                var body = prefab.GetComponent<Rigidbody>();
                var visual = prefab.transform.Find("Crate");
                if (!body || body.isKinematic || !pushable || pushable.PushAxis.Length == 0 || !visual)
                    throw new Exception("Crate pushing setup incomplete.");
                var mesh = visual.GetComponent<MeshFilter>();
                var renderer = visual.GetComponent<MeshRenderer>();
                if (!mesh || !mesh.sharedMesh || !renderer || renderer.sharedMaterials.Any(m => !m))
                    throw new Exception("Crate visual missing mesh/material.");
                if (!visual.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger))
                    throw new Exception("Crate requires a solid collider.");
                if (!prefab.GetComponentsInChildren<Weight>(true).Any())
                    throw new Exception("Crate missing pressure-plate weight.");
                var ids = prefab.GetComponentsInChildren<ObjectUniqueId>(true).Select(i => i.Id).ToArray();
                if (ids.Any(string.IsNullOrEmpty) || ids.Distinct().Count() != ids.Length)
                    throw new Exception("Crate IDs invalid.");
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
                    throw new Exception("Crate contains missing scripts.");
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("PUSHABLE_CRATE_OK: SampleScene2 crate mesh/material, solid collider, pushing axes, Rigidbody, weight and independent IDs. Saved " + Output, prefab);
            }
            finally
            {
                if (copy) Object.DestroyImmediate(copy);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        [MenuItem("Horror Game/Environment/Enable Small Crate Climbing")]
        public static void EnableSmallCrateClimbing()
        {
            string path=Folder+"PushableCrate1.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { ConfigureClimbing(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("CRATE_CLIMB_SETUP_OK: 1m crate, native up/down animations, dynamic edge markers and hand contacts.");
        }

        static void ConfigureClimbing(GameObject root)
        {
            var setup=AssetDatabase.LoadAssetAtPath<ClimbableSetup>(Folder+"CrateClimbSetup.asset");
            if (!setup)
            {
                setup=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ClimbableSetup>("Assets/AssetStore/HorrorEngine/ExampleContent/Platforming/ClimbableSetup_1mObstacle.asset"));
                setup.name="CrateClimbSetup";AssetDatabase.CreateAsset(setup,Folder+"CrateClimbSetup.asset");
            }
            var data=AssetDatabase.LoadAssetAtPath<InteractionData>(Folder+"CrateClimbInteraction.asset");
            if (!data)
            {
                data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/AssetStore/HorrorEngine/ExampleContent/Interaction/Interaction_Climb.asset"));
                data.name="CrateClimbInteraction";AssetDatabase.CreateAsset(data,Folder+"CrateClimbInteraction.asset");
            }
            var rigTransform=root.transform.Find("ClimbRig");
            var rig=rigTransform ? rigTransform.gameObject : new GameObject("ClimbRig");
            rig.transform.SetParent(root.transform,false);
            rig.transform.localPosition=Vector3.zero;rig.transform.localRotation=Quaternion.identity;
            rig.transform.localScale=Vector3.one*2f;
            rig.layer=LayerMask.NameToLayer("InteractionSystem");
            var trigger=rig.GetComponent<BoxCollider>(); if (!trigger) trigger=rig.AddComponent<BoxCollider>();
            trigger.isTrigger=true;trigger.size=new Vector3(1.2f,1.7f,1.2f);trigger.center=new Vector3(0,.85f,0);
            var climb=rig.GetComponent<Climbable>(); if (!climb) climb=rig.AddComponent<Climbable>();
            climb.Setup=setup;
            climb.ExitTop=Marker(rig.transform,"ExitTop",new Vector3(0,1,.5f),Quaternion.identity);
            climb.ExitBottom=Marker(rig.transform,"ExitBottom",new Vector3(0,0,-.65f),Quaternion.Euler(0,180,0));
            var contact=rig.GetComponent<CrateClimbRig>(); if (!contact) contact=rig.AddComponent<CrateClimbRig>();
            contact.CrateBody=root.GetComponent<Rigidbody>();
            contact.LeftHandContact=Marker(rig.transform,"LeftHandContact",new Vector3(-.22f,1.015f,.035f),Quaternion.identity);
            contact.RightHandContact=Marker(rig.transform,"RightHandContact",new Vector3(.22f,1.015f,.035f),Quaternion.identity);
            var interactive=rig.GetComponent<Interactive>();interactive.Data=data;interactive.ShowPrompt=true;
            interactive.OnInteract=new OnInteractionEvent();
            interactive.OnFinishInteract=new UnityEngine.Events.UnityEvent();interactive.OnDataChanged=new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddPersistentListener<IInteractor>(interactive.OnInteract,contact.PrepareClimb);
            if (!setup.ClimbAnimation || !setup.DropAnimation) throw new Exception("Native crate climb animation handles missing.");
        }
        static Transform Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var marker=parent.Find(name);
            if (!marker) { marker=new GameObject(name).transform;marker.SetParent(parent,false); }
            marker.localPosition=position;marker.localRotation=rotation;marker.localScale=Vector3.one;
            return marker;
        }
    }
}
