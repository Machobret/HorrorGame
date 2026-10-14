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
 public static class WeaponGripAudit
 {
  const string Key="HorrorGame.WeaponGripAudit";
  const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/GripAudit";
  static string[] paths={"Assets/Game/Weapons/Data/FlareGun.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset","Assets/Game/Weapons/Data/Revolver.asset","Assets/Game/Weapons/Data/HuntingRifle.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset"};
  static int stage,index;static double started,phase;static Mouse mouse;static Camera camera;static RenderTexture target;static ReloadableWeaponData data;
  static WeaponGripAudit(){EditorApplication.playModeStateChanged+=OnState;}
  public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
  static void OnState(PlayModeStateChange state)
  {if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;started=phase=EditorApplication.timeSinceStartup;stage=index=0;EditorApplication.update+=Tick;Directory.CreateDirectory(Output);}
  static void Tick()
  {
   try
   {
    double now=EditorApplication.timeSinceStartup;if(now-started>75)throw new Exception("Grip audit timed out");
    if(stage==0&&GameManager.Exists&&GameManager.Instance.Player&&Time.timeSinceLevelLoad>3)
    {
     Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
     InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
     mouse=InputSystem.AddDevice<Mouse>();var go=new GameObject("Grip audit camera");camera=go.AddComponent<Camera>();camera.fieldOfView=35;camera.nearClipPlane=.02f;
     camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.07f,.08f);target=new RenderTexture(1200,900,24);camera.targetTexture=target;camera.enabled=false;camera.cullingMask=~(1<<5);
     var key=new GameObject("Grip audit light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.transform.rotation=Quaternion.Euler(40,-35,0);
     var fill=new GameObject("Grip audit fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(10,135,0);
     stage=1;phase=now;
    }
    else if(stage==1&&now-phase>.35)
    {
     data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(paths[index]);
     var inventory=GameManager.Instance.Inventory;
     if(!inventory.TryGet(data,out var entry))entry=inventory.Add(data,1,data.MaxAmmo);inventory.Equip(entry);
     phase=now;stage=2;
    }
    else if(stage==2&&now-phase>.4)
    {
     Capture("Idle");InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));stage=3;phase=EditorApplication.timeSinceStartup;
    }
    else if(stage==3&&now-phase>.65)
    {
     if(!(GameManager.Instance.Player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateAiming)) { if(now-phase>5) throw new Exception("Aim pose was not active for "+data.name); return; }
     Capture("Aim");InputSystem.QueueStateEvent(mouse,new MouseState());++index;
     if(index>=paths.Length){Debug.Log("WEAPON_GRIP_AUDIT_OK all five weapons idle and aiming captured");Finish(0);return;}
     stage=1;phase=now;
    }
   }
   catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  static void Capture(string pose)
  {
   var player=GameManager.Instance.Player;var animator=player.MainAnimator;var right=animator.GetBoneTransform(HumanBodyBones.RightHand);var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   var weapon=player.GetComponentInChildren<Weapon>();var center=(right.position+left.position)*.5f;
   camera.transform.position=center+player.transform.right*1.35f+player.transform.forward*.75f+Vector3.up*.25f;camera.transform.LookAt(center+player.transform.forward*.08f);Save(data.name+"_"+pose+"_Side.png");
   camera.transform.position=center+player.transform.forward*1.7f-player.transform.right*.55f+Vector3.up*.15f;camera.transform.LookAt(center);Save(data.name+"_"+pose+"_Front.png");
   var text=$"{data.name} {pose} rightHand={right.position} leftHand={left.position} weaponRoot={weapon.transform.position} visuals={weapon.Visuals.transform.position} state={player.GetComponentInChildren<ActorStateController>().m_CurrentState.GetType().Name} inputAim={player.GetComponent<IPlayerInput>().IsAimingHeld()} attachment={data.CharacterAttachment.Position} rotation={data.CharacterAttachment.Rotation}\n";
   foreach(var m in weapon.GetComponentsInChildren<MeshRenderer>())text+=$"mesh {m.name} center={m.bounds.center} size={m.bounds.size}\n";
   File.WriteAllText(Path.Combine(Output,data.name+"_"+pose+".txt"),text);Debug.Log("GRIP_CAPTURE "+text);
  }
  static void Save(string name)
  {
   camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
   image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);
  }
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(mouse!=null)InputSystem.RemoveDevice(mouse);EditorApplication.Exit(code);}
 }
}
