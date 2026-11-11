using System;
using System.Linq;
using HorrorEngine;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;

namespace HorrorGame.Doors.Editor
{
    public static class DoorKeypadBuilder
    {
        const string Folder="Assets/Game/Doors/Keypad/";
        const string Output=Folder+"Door_Keypad.prefab";
        [MenuItem("Horror Game/Doors/Build and Attach Storage Keypad")]
        public static void BuildAndAttach()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var selected=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<TwoSidedDoor>():null;
            var candidates=selected?new[]{selected}:Object.FindObjectsByType<TwoSidedDoor>(FindObjectsSortMode.None)
                .Where(d=>d.transform.parent && d.transform.parent.name=="Door attachment f233288b971841838d03d771922562d9").ToArray();
            if(candidates.Length!=1)throw new InvalidOperationException("Expected exactly one storage Door_Interior_05 in the loaded scene.");
            var door=candidates[0];
            var existing=door.GetComponentInChildren<DoorKeypad>(true);
            if(existing)
            {
                Build();
                foreach(var id in door.GetComponentsInChildren<ObjectUniqueId>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(id);
                EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
                Selection.activeGameObject=existing.gameObject;return;
            }
            var oldLock=door.GetComponent<DoorLock>();
            if(oldLock)throw new InvalidOperationException("Door already has a lock; keep its existing setup and wire the new keypad manually.");
            Build();
            var panel=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output),door.transform);
            Undo.RegisterCreatedObjectUndo(panel,"Attach storage keypad");
            panel.transform.localPosition=new Vector3(.68f,1.35f,-.13f);
            panel.transform.localRotation=Quaternion.Euler(0,90,0); // Native keypad faces local +X, toward side A (-Z).
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.transform);
            var gate=Undo.AddComponent<KeypadDoorLock>(door.gameObject);
            gate.Keypad=panel.GetComponent<DoorKeypad>();
            gate.Keypad.DoorLock=gate;
            foreach(var id in door.GetComponentsInChildren<ObjectUniqueId>(true))
            {
                Undo.RecordObject(id,"Assign keypad save ID");id.RegenerateId();
                PrefabUtility.RecordPrefabInstancePropertyModifications(id);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate.Keypad);
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
            Selection.activeGameObject=panel;EditorGUIUtility.PingObject(panel);
            Debug.Log("DOOR_KEYPAD_ATTACHED_OK: storage Door_Interior_05, native keypad PIN 1998. Scene is unsaved.");
        }

        public static void Build()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/AssetStore/HorrorEngine/ExampleContent/Scenes/SamplePuzzleKeypad.unity");
            GameObject root=null;
            try
            {
                var source=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SequencePuzzle>(true)).Single();
                root=Object.Instantiate(source.gameObject);root.name="Door_Keypad";
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var puzzle=root.GetComponent<SequencePuzzle>();
                puzzle.OnSolved=new UnityEvent();puzzle.OnLoadedSolved=new UnityEvent();
                var context=root.GetComponentInChildren<Interaction3DContext>(true);context.OnCancel=new UnityEvent();
                var audio=context.GetComponent<AudioSource>();audio.playOnAwake=false;
                // Replace the sample's dangling/legacy camera reference with a Unity 6 camera.
                foreach(var old in root.GetComponentsInChildren<CameraCloseup>(true))Object.DestroyImmediate(old.gameObject);
                foreach(Transform t in root.transform.Cast<Transform>().ToArray())
                    if(t.name=="Cube"&&!t.GetComponent<Interaction3DContext>())Object.DestroyImmediate(t.gameObject);
                foreach(var old in root.GetComponentsInChildren<Interactive>(true).Where(i=>!i.name.StartsWith("Number",StringComparison.Ordinal)).ToArray())
                    Object.DestroyImmediate(old.gameObject);

                var viewObject=new GameObject("Keypad_Closeup");viewObject.transform.SetParent(root.transform,false);
                var view=viewObject.AddComponent<CameraCloseup>();
                var camObject=new GameObject("Keypad_Camera");camObject.transform.SetParent(viewObject.transform,false);
                camObject.transform.localPosition=new Vector3(2.4f,.10f,0);
                camObject.transform.localRotation=Quaternion.LookRotation(new Vector3(-2.4f,0,0));
                var camera=camObject.AddComponent<CinemachineCamera>();camera.Priority=1000;
                var lens=camera.Lens;lens.FieldOfView=45;lens.NearClipPlane=.01f;camera.Lens=lens;
                var viewData=new SerializedObject(view);viewData.FindProperty("m_Camera").objectReferenceValue=camera;
                viewData.FindProperty("m_HidePlayer").boolValue=true;viewData.ApplyModifiedPropertiesWithoutUndo();

                var approachObject=new GameObject("Keypad_Interaction");approachObject.transform.SetParent(root.transform,false);
                approachObject.layer=LayerMask.NameToLayer("InteractionSystem");
                var box=approachObject.AddComponent<BoxCollider>();box.isTrigger=true;
                box.center=new Vector3(1,0,0);box.size=new Vector3(5,5,5);
                var approach=approachObject.AddComponent<Interactive>();
                var prompt=AssetDatabase.LoadAssetAtPath<InteractionData>(Folder+"Keypad_Interaction.asset");
                if(!prompt)
                {
                    prompt=Object.Instantiate(AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/Game/Items/CigarettePack/CigarettePack_Interaction.asset"));
                    AssetDatabase.CreateAsset(prompt,Folder+"Keypad_Interaction.asset");
                }
                prompt.Prompt=new LocalizableText {IsLocalized=false,Unlocalized="Use keypad"};
                prompt.AnimState=null;prompt.InteractionDuration=0;prompt.InteractionDelay=0;prompt.RotateToInteractive=false;
                EditorUtility.SetDirty(prompt);
                approach.Data=prompt;approach.OnInteract=new OnInteractionEvent();
                approach.OnFinishInteract=new UnityEvent();approach.OnDataChanged=new UnityEvent();
                var control=root.AddComponent<DoorKeypad>();control.Puzzle=puzzle;control.Input=context;control.View=view;
                control.Approach=approach;control.Audio=audio;
                var display=root.GetComponentInChildren<SequencePuzzleTextBase>(true);
                if(display)control.Display=display.GetComponent<TMP_Text>();
                control.SuccessSound=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("66993392d1259784dbc30fda3c533f5d"));
                UnityEventTools.AddVoidPersistentListener(approach.OnInteract,control.Open);
                AddBottomRow(root,puzzle,context);
                ApplyPresentation(root,control);
                var state=root.AddComponent<SavableObjectState>();
                var stateData=new SerializedObject(state);stateData.FindProperty("m_ApplySavedTransform").boolValue=false;stateData.ApplyModifiedPropertiesWithoutUndo();
                var id=root.GetComponent<ObjectUniqueId>();
                var previous=AssetDatabase.LoadAssetAtPath<GameObject>(Output);
                if(previous)id.SetId(previous.GetComponent<ObjectUniqueId>().Id);else id.RegenerateId();
                id.IsUniqueInstance=false;
                PrefabUtility.SaveAsPrefabAsset(root,Output);AssetDatabase.SaveAssets();
            }
            finally{if(root)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        }

        static void AddBottomRow(GameObject root,SequencePuzzle puzzle,Interaction3DContext context)
        {
            var buttons=root.GetComponentsInChildren<Interactive>(true).Where(i=>i.name.StartsWith("Number")).ToArray();
            float minY=buttons.Min(i=>i.transform.localPosition.y);
            float step=buttons.Select(i=>i.transform.localPosition.y).Distinct().OrderBy(y=>y).Zip(buttons.Select(i=>i.transform.localPosition.y).Distinct().OrderBy(y=>y).Skip(1),(a,b)=>b-a).First();
            float[] z=buttons.Select(i=>i.transform.localPosition.z).Distinct().OrderBy(v=>v).ToArray();
            for(int i=0;i<3;i++)
            {
                var copy=Object.Instantiate(buttons[0].gameObject,buttons[0].transform.parent);copy.name=i==1?"Number0":i==0?"Clear":"Backspace";
                var pos=copy.transform.localPosition;pos.y=minY-step;pos.z=z[i];copy.transform.localPosition=pos;
                var text=copy.GetComponentInChildren<TMP_Text>(true);if(text)text.text=i==1?"0":i==0?"C":"<";
                var key=copy.GetComponent<Interactive>();key.OnInteract=new OnInteractionEvent();
                if(i==1)UnityEventTools.AddStringPersistentListener(key.OnInteract,puzzle.Add,"0");
                else UnityEventTools.AddVoidPersistentListener(key.OnInteract,i==0?(UnityAction)puzzle.Clear:puzzle.Remove);
            }
            var data=new SerializedObject(context);var list=data.FindProperty("m_Interactives");
            var keys=context.GetComponentsInChildren<Interactive>(true);list.arraySize=keys.Length;
            for(int i=0;i<keys.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=keys[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Material(string name,Color color)
        {
            string path=Folder+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.2f);
            EditorUtility.SetDirty(material);return material;
        }
        static void ApplyPresentation(GameObject root,DoorKeypad control)
        {
            var body=Material("Keypad_Case",new Color(.18f,.20f,.19f));
            var key=Material("Keypad_Keys",new Color(.68f,.67f,.57f));
            var screen=Material("Keypad_Display",new Color(.025f,.04f,.025f));
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if(!renderer.GetComponent<TMP_Text>())renderer.sharedMaterial=renderer.GetComponent<Interactive>()?key:body;
            foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))sprite.color=new Color(1,.7f,.16f);
            var display=control.Display;
            display.transform.SetParent(root.transform,false);
            display.transform.localPosition=new Vector3(.545f,.4f,0);
            display.transform.localRotation=Quaternion.Euler(0,-90,0);
            display.transform.localScale=Vector3.one;
            display.rectTransform.sizeDelta=new Vector2(.7f,.16f);
            display.fontSize=1.7f;display.alignment=TextAlignmentOptions.Center;
            display.color=new Color(.5f,1,.45f);
            var background=GameObject.CreatePrimitive(PrimitiveType.Cube);background.name="Display_Backplate";
            background.transform.SetParent(root.transform,false);
            background.transform.localPosition=new Vector3(.515f,.4f,0);
            background.transform.localScale=new Vector3(.025f,.18f,.73f);
            background.GetComponent<Renderer>().sharedMaterial=screen;
            Object.DestroyImmediate(background.GetComponent<Collider>());
        }
    }
}
