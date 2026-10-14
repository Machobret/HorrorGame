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
 public static class WeaponProgressCapture
 {
  const string Key="HorrorGame.WeaponProgressCapture";
  static WeaponProgressCapture(){EditorApplication.playModeStateChanged+=OnState;}
  public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
  static void OnState(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))new GameObject("Weapon media capture").AddComponent<WeaponProgressCaptureDriver>();}
  public static void Finish(int code){SessionState.SetBool(Key,false);Time.captureFramerate=0;EditorApplication.Exit(code);}
 }
 [DefaultExecutionOrder(1000)]
 public sealed class WeaponProgressCaptureDriver:MonoBehaviour
 {
  const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/WeaponProgressMedia";
  string[] paths={"Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset","Assets/Game/Weapons/Data/Revolver.asset","Assets/Game/Weapons/Data/HuntingRifle.asset","Assets/Game/Weapons/Data/FlareGun.asset"};
  int stage,index,frame,shotFrame;float phase,recordStart;double started;Mouse mouse;Camera camera;RenderTexture target;Light key,fill;ReloadableWeaponData data;bool recording,seenFlare,seenLanded;float maxDistance;Vector3 initialFlare;
  void Awake(){started=EditorApplication.timeSinceStartup;Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/Frames");Application.runInBackground=true;Time.captureFramerate=24;}
  void LateUpdate()
  {
   try{
    if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Media capture timeout");
    if(stage==0&&GameManager.Exists&&GameManager.Instance.Player&&Time.timeSinceLevelLoad>3){
     InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
     mouse=InputSystem.AddDevice<Mouse>();camera=new GameObject("Weapon media camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=35;camera.nearClipPlane=.02f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.07f,.08f);camera.cullingMask=~(1<<5);target=new RenderTexture(1200,900,24);camera.targetTexture=target;
     key=new GameObject("Presentation key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.transform.rotation=Quaternion.Euler(40,-35,0);
     fill=new GameObject("Presentation fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(10,135,0);
     stage=1;phase=Time.time;
    }
    else if(stage==1&&Time.time-phase>.35f){
     data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(paths[index]);if(!data)throw new Exception("Weapon missing: "+paths[index]);var inv=GameManager.Instance.Inventory;if(!inv.TryGet(data,out var entry))entry=inv.Add(data,1,data.MaxAmmo);if(entry==null)throw new Exception("Inventory full");inv.Equip(entry);InputSystem.QueueStateEvent(mouse,new MouseState());stage=2;phase=Time.time;
    }
    else if(stage==2&&Time.time-phase>.65f){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));stage=3;phase=Time.time;}
    else if(stage==3&&Time.time-phase>.8f){
     var player=GameManager.Instance.Player;if(!(player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateAiming)){if(Time.time-phase>5)throw new Exception("Not aiming: "+data.name);return;}
     var state=player.GetComponentInChildren<ActorStateController>().m_CurrentState as PlayerStateAiming;var sensor=state.GetComponent<IPlayerVerticalAiming>();if(sensor is Behaviour b)b.enabled=false;if(sensor!=null)sensor.Verticality=0;player.MainAnimator.SetFloat(PlayerStateAiming.k_AimVerticalityHash,0);stage=4;phase=Time.time;
    }
    else if(stage==4&&Time.time-phase>.3f){
     var player=GameManager.Instance.Player;var focus=player.transform.position+Vector3.up*1.3f+player.transform.forward*.25f;camera.transform.position=focus+player.transform.right*2f+player.transform.forward*.65f+Vector3.up*.2f;camera.transform.LookAt(focus);Save(data.name+"_InGame.png");Debug.Log("WEAPON_MEDIA_STILL "+data.name+" scale="+data.CharacterAttachment.Scale);
     if(++index<paths.Length){stage=1;phase=Time.time;InputSystem.QueueStateEvent(mouse,new MouseState());}
     else{
      key.intensity=.2f;fill.intensity=.08f;camera.targetTexture=null;target.Release();Destroy(target);target=new RenderTexture(960,540,24);camera.targetTexture=target;camera.fieldOfView=44;
      var focusWide=player.transform.position+player.transform.forward*2.7f+Vector3.up*.95f;camera.transform.position=focusWide+player.transform.right*7f-player.transform.forward*1.4f+Vector3.up*1.7f;camera.transform.LookAt(focusWide);recording=true;recordStart=Time.time;frame=0;stage=5;
     }
    }
    else if(stage==5&&Time.time-recordStart>.6f){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));shotFrame=Time.frameCount;stage=6;}
    else if(stage==6&&Time.frameCount>shotFrame){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));stage=7;}
    if(recording){
     var flares=FindObjectsByType<SignalFlare>(FindObjectsSortMode.None);
     foreach(var flare in flares){if(!seenFlare){seenFlare=true;initialFlare=flare.transform.position;Debug.Log("MEDIA_REAL_PLAYER_SHOT flare spawned");}maxDistance=Mathf.Max(maxDistance,Vector3.Distance(initialFlare,flare.transform.position));if(flare.HasLanded)seenLanded=true;}
     Save("Frames/Flare_"+(frame++).ToString("D4")+".png");
     if(Time.time-recordStart>5){
      if(!seenFlare||!seenLanded||maxDistance<1)throw new Exception("Real flare flight/impact was not captured");
      Save("Flare_Illumination_InGame.png");File.WriteAllText(Output+"/Capture.txt","Actual player aim/fire input; 24 fps; "+frame+" frames; live flare observed; impact observed; travel="+maxDistance+" metres; flare attachment scale="+data.CharacterAttachment.Scale);
      Debug.Log("WEAPON_PROGRESS_MEDIA_OK five stills and "+frame+" real-shot frames; travel="+maxDistance+" impact="+seenLanded);InputSystem.RemoveDevice(mouse);WeaponProgressCapture.Finish(0);
     }
    }
   }catch(Exception e){Debug.LogException(e);WeaponProgressCapture.Finish(1);}
  }
  void Save(string name){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name,tex.EncodeToPNG());RenderTexture.active=old;Destroy(tex);}
 }
}
