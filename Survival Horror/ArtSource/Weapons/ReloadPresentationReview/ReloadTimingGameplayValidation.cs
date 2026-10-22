using System;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace HorrorGame.Weapons.Editor
{
 [InitializeOnLoad]
 public static class ReloadTimingGameplayValidation
 {
  const string Key="HorrorGame.ReloadTimingGameplayValidation";
  public const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/ReloadTimingGameplayReview";
  static string[] paths={"Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset","Assets/Game/Weapons/Data/HuntingRifle.asset","Assets/Game/Weapons/Data/Revolver.asset","Assets/Game/Weapons/Data/FlareGun.asset"};
  static int stage,index,frame,shotSample;static double started,phase;static float firedAt,reserveBefore,sampleTime;static Vector3 position,foot;static Mouse mouse;static Keyboard keyboard;static ActorStateController states;static ReloadableWeaponData data;static InventoryEntry gun,ammo;static UIInventory ui;static Camera camera;static RenderTexture target;static bool sawCase;
  static ReloadTimingGameplayValidation(){EditorApplication.playModeStateChanged+=OnState;}
  public static void RunBatch(){try{InventoryReloadChecks.RunInventoryChecks();EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
  static void OnState(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){started=phase=EditorApplication.timeSinceStartup;stage=index=0;Directory.CreateDirectory(Output);EditorApplication.update+=Tick;}}
  static Inventory Inv=>GameManager.Instance.Inventory;
  static Actor Player=>GameManager.Instance.Player;
  static void Check(bool ok,string text){if(!ok)throw new Exception(data?.name+": "+text);}
  static void Next(int value){stage=value;phase=EditorApplication.timeSinceStartup;}
  static void Tick()
  {
   try{
    double now=EditorApplication.timeSinceStartup,dt=now-phase;if(now-started>160)throw new Exception("Reload gameplay timeout stage="+stage+" gun="+data?.name);
    if(stage==0&&GameManager.Exists&&Player&&Time.timeSinceLevelLoad>3){
     Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
     mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();states=Player.GetComponentInChildren<ActorStateController>();ui=UIManager.Get<UIInventory>();
     camera=new GameObject("Reload review camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=35;camera.nearClipPlane=.02f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.07f,.08f);camera.cullingMask=~(1<<5);target=new RenderTexture(1200,900,24);camera.targetTexture=target;
     var key=new GameObject("Reload key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.transform.rotation=Quaternion.Euler(40,-35,0);Next(1);
    }
    else if(stage==1&&dt>.3){
     data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(paths[index]);if(!Inv.TryGet(data,out gun))gun=Inv.Add(data,1,1);gun.SecondaryCount=1;Inv.Equip(gun);gun=Inv.GetEquippedWeapon();if(!Inv.TryGet(data.AmmoItem,out ammo))ammo=Inv.Add(data.AmmoItem,data.MaxAmmo*3+3);else ammo.Count=Mathf.Max(ammo.Count,data.MaxAmmo*3+3);reserveBefore=ammo.Count;
     InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState());Next(2);
    }
    else if(stage==2&&dt>.5){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));Next(3);}
    else if(stage==3&&dt>.5){Check(states.m_CurrentState is PlayerStateAiming,"failed to aim");InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));frame=Time.frameCount;firedAt=Time.time;shotSample=0;sawCase=false;Next(4);}
    else if(stage==4&&Time.frameCount>frame){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));Next(5);}
    else if(stage==5){
     if(data.name=="HuntingRifle"){
      var visual=Player.GetComponentInChildren<RifleBoltVisual>();if(visual&&visual.IsCycling&&shotSample<4&&Time.time-firedAt>.2f+shotSample*.3f){Capture("Rifle_Bolt_"+shotSample);Debug.Log("RIFLE_BOLT_SAMPLE "+shotSample+" pos="+visual.Bolt.localPosition+" rotation="+visual.Bolt.localRotation.eulerAngles);++shotSample;}
      if(GameObject.Find("HuntingRifle_SpentBrass"))sawCase=true;
     }
     if(dt>3.2){Check(gun.SecondaryCount==0&&ammo.Count==reserveBefore,"last round auto-reloaded or reserve changed");Check(!(states.m_CurrentState is PlayerStateReload),"last round entered reload");if(data.name=="HuntingRifle")Check(sawCase,"no rifle brass ejected");InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));frame=Time.frameCount;Next(6);}
    }
    else if(stage==6&&Time.frameCount>frame){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));Next(7);}
    else if(stage==7&&dt>.3){Check(gun.SecondaryCount==0&&!(states.m_CurrentState is PlayerStateReload),"empty trigger auto-reloaded");InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.R,UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftShift));Next(8);}
    else if(stage==8&&states.m_CurrentState is PlayerStateReload){InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftShift));position=Player.transform.position;sampleTime=Time.time;foot=Player.transform.InverseTransformPoint(Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position);Next(9);}
    else if(stage==8&&dt>3)throw new Exception("Manual reload failed "+data.name);
    else if(stage==9&&dt>.5){
     Check(states.m_CurrentState is PlayerStateReload,"reload ended too early");Check(gun.SecondaryCount==0&&ammo.Count==reserveBefore,"ammo granted before animation finished");var movement=Player.GetComponent<PlayerMovement>();float distance=Vector3.ProjectOnPlane(Player.transform.position-position,Vector3.up).magnitude;
     if(data.name=="HuntingRifle")Check(!movement.enabled&&distance<.035f,"rifle reload permits movement");else{
      Check(movement.enabled&&movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Running),"reload doesn't enforce walking");Check(distance>.1f,"cannot walk during reload");float speed=new SerializedObject(movement).FindProperty("m_MovementSpeed").floatValue;Check(distance<=speed*(Time.time-sampleTime)*1.3f+.05f,"sprint still active during reload");Check(Vector3.Distance(foot,Player.transform.InverseTransformPoint(Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position))>.002f,"feet are not animating during walking reload");}
     Capture(data.name+"_Reload");InputSystem.QueueStateEvent(keyboard,new KeyboardState());Next(10);
    }
    else if(stage==10&&!(states.m_CurrentState is PlayerStateReload)&&dt>.2){
     Check(gun.SecondaryCount==data.MaxAmmo,"reload did not fill magazine");Check(ammo.Count==reserveBefore-data.MaxAmmo,"reload reserve consumption incorrect");Check(Player.GetComponent<IPlayerInput>().IsAimingHeld(),"held aim was suppressed after aimed reload");Check(states.m_CurrentState is PlayerStateAiming,"aimed reload did not return to aim");Check(!Player.GetComponent<PlayerMovement>().Constrain.HasFlag(PlayerMovement.MovementConstrain.Running),"walk constraint leaked after reload");InputSystem.QueueStateEvent(mouse,new MouseState());Next(11);
    }
    else if(stage==11&&dt>.15){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));Next(12);}
    else if(stage==12&&dt>.25){
     Check(states.m_CurrentState is PlayerStateAiming,"fresh aim press was not accepted");InputSystem.QueueStateEvent(mouse,new MouseState());ui.Show();Check(PauseController.Instance.IsPaused,"inventory did not pause gameplay");gun.SecondaryCount=0;reserveBefore=ammo.Count;Inv.Combine(ammo,gun);Next(13);
    }
    else if(stage==13&&dt>.35){Check(gun.SecondaryCount==0&&ammo.Count==reserveBefore,"combine instantly loaded ammunition");Check(Inv.HasPendingEquippedWeaponReload(),"combine did not queue reload");Check(!(states.m_CurrentState is PlayerStateReload),"reload started inside inventory");ui.Hide();Next(14);}
    else if(stage==14&&states.m_CurrentState is PlayerStateReload){Check(gun.SecondaryCount==0,"inventory close skipped reload animation");Next(15);}
    else if(stage==14&&dt>3)throw new Exception("Inventory reload did not start after close "+data.name);
    else if(stage==15&&!(states.m_CurrentState is PlayerStateReload)&&dt>.2){
     Check(gun.SecondaryCount==data.MaxAmmo&&ammo.Count==reserveBefore-data.MaxAmmo,"queued reload did not consume exactly once");Check(!Inv.HasPendingEquippedWeaponReload(),"queued reload remained pending");Debug.Log("RELOAD_GAMEPLAY_OK "+data.name+" manual-only, "+(data.name=="HuntingRifle"?"stationary":"walk-only with animated feet")+", held aim restored, real inventory-close reload");
     if(ammo.Item)Inv.Remove(ammo,ammo.Count);if(++index==paths.Length){Debug.Log("ALL_WEAPON_RELOAD_GAMEPLAY_OK");Finish(0);}else Next(1);
    }
   }catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  static void Capture(string name){var focus=Player.transform.position+Vector3.up*1.3f+Player.transform.forward*.3f;camera.transform.position=focus+Player.transform.right*1.5f+Player.transform.forward*.5f+Vector3.up*.15f;camera.transform.LookAt(focus);camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);}
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);EditorApplication.Exit(code);}
 }
}
