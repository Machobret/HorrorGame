using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HorrorEngine;
using HorrorGame.Doors;
using HorrorGame.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Cinemachine;
using Object=UnityEngine.Object;

namespace HorrorGame.Tests
{
    // Noninteractive: Unity -batchmode -projectPath ... -executeMethod HorrorGame.Tests.KeypadTraversalBatchChecks.Run
    public static class KeypadTraversalBatchChecks
    {
        static readonly StringBuilder Report=new StringBuilder();
        static int count;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string description)
        {if(!value)throw new Exception(description);Report.AppendLine("PASS: "+description);count++;}
        static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
        static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);

        public static void Run()
        {
            int exit=0;
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Run these checks in a separate batch editor, not the working editor.");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var path="Assets/Game/Doors/Keypad/Door_Keypad.prefab";
                var contents=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    contents.GetComponent<DoorKeypad>().Approach.gameObject.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(contents,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(contents);}
                try{KeypadChecks();}catch(Exception e){exit=1;Report.AppendLine("KEYPAD FAIL: "+e);}
                try{ClearanceChecks();}catch(Exception e){exit=1;Report.AppendLine("CLEARANCE FAIL: "+e);}
                Report.AppendLine(exit==0?$"ALL {count} CHECKS PASSED":$"{count} checks passed; failures above");
            }
            catch(Exception e){exit=1;Report.AppendLine("FAIL: "+e);Debug.LogException(e);}
            finally
            {
                string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/TraversalFix");
                Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"BatchChecks.txt"),Report.ToString());
                Debug.Log(Report.ToString());EditorApplication.Exit(exit);
            }
        }
        sealed class IdleUI : IUIInput
        {
            public Vector2 GetPrimaryAxis()=>Vector2.zero;
            public bool IsConfirmDown()=>false;public bool IsCancelDown()=>false;public bool IsTogglePauseDown()=>false;
            public bool IsToggleInventoryDown()=>false;public bool IsToggleMapDown()=>false;public bool IsToggleMapListDown()=>false;
            public bool IsPrevSubmapDown()=>false;public bool IsNextSubmapDown()=>false;public float ConsumeScrollDelta()=>0;public void Flush(){}
        }
        static void StepInput()
        {
            // Editor-only synthetic events must use the Editor state buffer, not the Play buffer.
            typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null)
                .Invoke(null,new object[]{InputUpdateType.Editor});
        }
        static void KeypadChecks()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Doors/Keypad/Door_Keypad.prefab"));
            var panel=root.GetComponent<DoorKeypad>();
            var keyboard=InputSystem.AddDevice<Keyboard>();
            StepInput();foreach(var key in keyboard.allKeys){_ = key.wasPressedThisFrame;}
            GameObject cameraObject=null;
            try
            {
                Call(panel,"Awake");panel.Input.enabled=true;
                var keys=panel.Input.GetComponentsInChildren<Interactive>(true);
                foreach(var key in keys)key.enabled=true;
                Check(!panel.Approach.gameObject.activeSelf,"Panel has no competing world interaction");
                panel.Input.Select(keys.Single(k=>k.name=="Number5"));
                panel.Navigate(Vector2.right);Check(panel.Input.Selected.name=="Number6","Right moves 5 -> 6");
                panel.Navigate(Vector2.left);Check(panel.Input.Selected.name=="Number5","Left moves 6 -> 5");
                panel.Navigate(Vector2.up);Check(panel.Input.Selected.name=="Number2","Up moves 5 -> 2");
                panel.Navigate(Vector2.down);Check(panel.Input.Selected.name=="Number5","Down moves 2 -> 5");
                panel.Navigate(Vector2.down);panel.Navigate(Vector2.down);Check(panel.Input.Selected.name=="Number0","Down reaches bottom row");
                panel.Navigate(Vector2.left);Check(panel.Input.Selected.name=="Clear","Bottom row left selects clear");
                panel.Navigate(Vector2.right);panel.Navigate(Vector2.right);Check(panel.Input.Selected.name=="Backspace","Bottom row right selects backspace");
                string pressed="";
                foreach(var key in keys){string name=key.name;key.OnInteract=new OnInteractionEvent();key.OnInteract.AddListener(_=>pressed+=name+",");}
                Set(panel,"opened",true);Set(panel,"ready",true);Set(panel,"acceptInputAfter",-1f);Set(panel,"uiInput",new IdleUI());
                Key[] digits={Key.Digit0,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9};
                Key[] numpad={Key.Numpad0,Key.Numpad1,Key.Numpad2,Key.Numpad3,Key.Numpad4,Key.Numpad5,Key.Numpad6,Key.Numpad7,Key.Numpad8,Key.Numpad9};
                for(int digit=0;digit<10;digit++)foreach(var key in new[]{digits[digit],numpad[digit]})
                {
                    pressed="";InputSystem.QueueStateEvent(keyboard,new KeyboardState());StepInput();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));StepInput();Call(panel,"Update");
                    Check(pressed=="Number"+digit+",","Physical "+key+" presses exactly one matching button (received: "+pressed+", current="+(Keyboard.current==keyboard)+", held="+keyboard[key].isPressed+", down="+keyboard[key].wasPressedThisFrame+", time="+Time.unscaledTime+")");
                }
                foreach(var command in new[]{(Key.Backspace,"Backspace"),(Key.Delete,"Clear")})
                {
                    pressed="";InputSystem.QueueStateEvent(keyboard,new KeyboardState());StepInput();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(command.Item1));StepInput();Call(panel,"Update");
                    Check(pressed==command.Item2+",","Physical "+command.Item1+" edit command");
                }
                Set(panel,"ready",false);pressed="";InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));StepInput();Call(panel,"Update");
                Check(pressed=="","Opening transition ignores premature number input");
                cameraObject=new GameObject("Framing test camera",typeof(Camera),typeof(CinemachineBrain));cameraObject.tag="MainCamera";
                var camera=cameraObject.GetComponent<Camera>();var virtualCamera=panel.View.GetComponentInChildren<CinemachineCamera>(true);
                foreach(float aspect in new[]{.75f,1f,4f/3f,16f/9f,21f/9f})
                {
                    Call(panel,"FramePanel",aspect);camera.aspect=aspect;camera.fieldOfView=virtualCamera.Lens.FieldOfView;
                    camera.transform.SetPositionAndRotation(virtualCamera.transform.position,virtualCamera.transform.rotation);
                    bool fits=true;
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    for(int corner=0;corner<8;corner++)
                    {
                        var bounds=renderer.bounds;
                        var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        var viewport=camera.WorldToViewportPoint(point);
                        fits&=viewport.z>camera.nearClipPlane&&viewport.x>=.01f&&viewport.x<=.99f&&viewport.y>=.01f&&viewport.y<=.99f;
                    }
                    Check(fits,"Entire keypad fits aspect "+aspect.ToString("F2"));
                }
                Time.timeScale=0;Call(panel,"LateUpdate");Call(panel,"LateUpdate");
                Check(Vector3.Distance(camera.transform.position,virtualCamera.transform.position)<.001f,"Paused close-up cuts to complete camera pose");
                Call(panel,"ReleaseCamera");Check(!cameraObject.GetComponent<CinemachineBrain>().IgnoreTimeScale,"Camera time setting restored");
            }
            finally{Time.timeScale=1;InputSystem.RemoveDevice(keyboard);Object.DestroyImmediate(root);if(cameraObject)Object.DestroyImmediate(cameraObject);}
        }

        static GameObject Box(string name,Vector3 position,Vector3 scale)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.position=position;box.transform.localScale=scale;return box;
        }
        static void ClearanceChecks()
        {
            var floor=Box("Floor",new Vector3(0,-.1f,0),new Vector3(20,.2f,20));
            var player=new GameObject("Capsule fixture",typeof(Actor),typeof(CharacterController),typeof(Rigidbody));
            var actor=player.GetComponent<Actor>();var capsule=player.GetComponent<CharacterController>();
            capsule.height=1.75f;capsule.center=new Vector3(0,.875f,0);capsule.radius=.22f;
            player.GetComponent<Rigidbody>().isKinematic=true;
            var crate=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/PushableCrate/PushableCrate1.prefab"));
            var rig=crate.GetComponentInChildren<CrateClimbRig>();Call(rig,"Awake");rig.CrateBody.isKinematic=true;
            try
            {
                foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                {
                    player.transform.SetPositionAndRotation(Vector3.up,Quaternion.LookRotation(direction));Physics.SyncTransforms();
                    Check(rig.CanStartClimb(actor),"Clear drop allowed toward "+direction);
                    Call(rig,"Release");
                    var obstacle=Box("Blocked landing",direction*.9f+Vector3.up*.6f,new Vector3(.55f,1.2f,.55f));Physics.SyncTransforms();
                    Check(!rig.CanStartClimb(actor),"Blocked drop denied toward "+direction);
                    Check(capsule.enabled&&rig.CrateBody.isKinematic,"Denied drop keeps controller and crate state intact");
                    Object.DestroyImmediate(obstacle);
                }
                player.transform.SetPositionAndRotation(Vector3.up,Quaternion.identity);
                var low=Box("Low box",new Vector3(0,.2f,.8f),new Vector3(.3f,.4f,.3f));Physics.SyncTransforms();
                Check(!rig.CanStartClimb(actor),"Low solid prop blocks climb-down corridor");Object.DestroyImmediate(low);
                var overhead=Box("Overhang",new Vector3(0,2,.75f),new Vector3(.4f,.3f,.3f));Physics.SyncTransforms();
                Check(!rig.CanStartClimb(actor),"Overhang along capsule path blocks drop");Object.DestroyImmediate(overhead);
                floor.SetActive(false);Physics.SyncTransforms();Check(!rig.CanStartClimb(actor),"Drop without supported landing denied");floor.SetActive(true);
                var trigger=Box("Interaction trigger",new Vector3(0,.5f,.9f),Vector3.one);trigger.GetComponent<Collider>().isTrigger=true;Physics.SyncTransforms();
                Check(rig.CanStartClimb(actor),"Non-solid interaction trigger does not block drop");Call(rig,"Release");Object.DestroyImmediate(trigger);
                Physics.SyncTransforms();Check(rig.CanStartClimb(actor),"Clear descent starts before moving obstacle");
                capsule.enabled=false;
                var moving=Box("New obstacle",new Vector3(0,1.6f,.45f),new Vector3(.2f,.4f,.15f));Physics.SyncTransforms();
                Check(!rig.TryMoveClimbing(actor,Vector3.up,new Vector3(0,1,.65f),out var safe)&&safe==Vector3.up,"Obstacle arriving during animation aborts to entry pose");
                Object.DestroyImmediate(moving);Call(rig,"Release");capsule.enabled=true;
                player.transform.position=new Vector3(5,0,0);
                var thin=Box("Thin wall",new Vector3(5,1,1),new Vector3(1,2,.02f));Physics.SyncTransforms();
                Check(!ClimbClearance.CanMove(capsule,new Vector3(5,0,0),new Vector3(5,0,2),null),"Swept capsule catches thin wall between clear endpoints");Object.DestroyImmediate(thin);
                Physics.SyncTransforms();Check(ClimbClearance.IsClear(capsule,player.transform.position,null)&&ClimbClearance.HasSupport(capsule,player.transform.position),"Supporting floor is not mistaken for obstruction");
            }
            finally{Object.DestroyImmediate(crate);Object.DestroyImmediate(player);Object.DestroyImmediate(floor);}
        }
    }
}
