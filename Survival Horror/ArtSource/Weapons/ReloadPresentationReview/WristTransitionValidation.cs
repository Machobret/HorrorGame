using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HorrorEngine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace HorrorGame.Weapons.Editor {
 [InitializeOnLoad] public static class WristTransitionValidation {
  const string Key="WristTransitionValidation";
  static WristTransitionValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))new GameObject("Wrist transition review").AddComponent<WristTransitionRunner>();};}
  public static void RunBaseline(){SessionState.SetBool(Key+"FlareOnly",false);SessionState.SetString(Key+"Output","WristTransitionsBaseline");RunBatchInternal();}
  public static void RunBatch(){SessionState.SetBool(Key+"FlareOnly",false);SessionState.SetString(Key+"Output","WristTransitionsArmFinal");RunBatchInternal();}
  public static void RunFlarePreview(){SessionState.SetBool(Key+"FlareOnly",true);SessionState.SetString(Key+"Output","WristFlarePreviewFinal");RunBatchInternal();}
  static void RunBatchInternal(){EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
 }
 [DefaultExecutionOrder(1000)] public class WristTransitionRunner:MonoBehaviour {
  static readonly string[] paths={"Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset","Assets/Game/Weapons/Data/HuntingRifle.asset","Assets/Game/Weapons/Data/Revolver.asset","Assets/Game/Weapons/Data/FlareGun.asset"};
  Actor actor;ActorStateController states;Mouse mouse;Keyboard keyboard;InventoryEntry entry;ReloadableWeaponData data;Transform left,right;StreamWriter csv;string phase="startup",output;int ticks;Camera cam;RenderTexture rt;float captureUntil;IActorState previousState;bool capture;
  Inventory Inv=>GameManager.Instance.Inventory;
  IEnumerator Start(){
   Time.captureFramerate=60;Application.runInBackground=true;
   InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   while(!GameManager.Exists||!GameManager.Instance.Player||Time.timeSinceLevelLoad<3)yield return null;
   actor=GameManager.Instance.Player;states=actor.StateController;left=actor.MainAnimator.GetBoneTransform(HumanBodyBones.LeftHand);right=actor.MainAnimator.GetBoneTransform(HumanBodyBones.RightHand);
   mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
   output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,SessionState.GetString("WristTransitionValidationOutput","WristTransitionsArmFinal"));Directory.CreateDirectory(output);
   capture=output.EndsWith("Final");
   csv=new StreamWriter(Path.Combine(output,"WristPoses.csv"));csv.WriteLine("weapon,phase,frame,time,state,lx,ly,lz,lqx,lqy,lqz,lqw,r x,ry,rz,rqx,rqy,rqz,rqw,llqx,llqy,llqz,llqw,rlqx,rlqy,rlqz,rlqw");
   if(capture){cam=new GameObject("Wrist review camera").AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=35;cam.nearClipPlane=.02f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.07f,.08f);cam.cullingMask=~(1<<5);rt=new RenderTexture(640,480,24);cam.targetTexture=rt;var lamp=new GameObject("Wrist review key").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=2;lamp.transform.rotation=Quaternion.Euler(40,-35,0);}
   foreach(string path in (SessionState.GetBool("WristTransitionValidationFlareOnly",false)?new[]{paths[4]}:paths)){
    data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(path);phase="equip";Inputs(false);if(!Inv.TryGet(data,out entry))entry=Inv.Add(data,1,1);Inv.Equip(entry);entry=Inv.GetEquippedWeapon();entry.SecondaryCount=data.MaxAmmo;InventoryEntry ammo;if(!Inv.TryGet(data.AmmoItem,out ammo))Inv.Add(data.AmmoItem,data.MaxAmmo*3+3);else ammo.Count=data.MaxAmmo*3+3;
    yield return Wait(.8f);phase="aim";Inputs(true);yield return Wait(.65f);Require(states.m_CurrentState is PlayerStateAiming,"aim entry");
    phase="lower";Inputs(false);yield return Wait(.65f);Require(states.m_CurrentState is PlayerStateMotion,"aim exit");
    phase="aim-fire";Inputs(true);yield return Wait(.55f);Inputs(true,true);yield return Wait(.06f);Inputs(true);yield return Wait(2.4f);Require(!(states.m_CurrentState is PlayerStateAttack),"attack completion");
    phase="aim-release-after-fire";Inputs(false);yield return Wait(.65f);
    phase="reload-lowered";entry.SecondaryCount=0;Inputs(false,false,true);yield return Wait(.06f);Inputs(false);yield return Wait(data.ReloadDuration+.5f);Require(entry.SecondaryCount==data.MaxAmmo&&states.m_CurrentState is PlayerStateMotion,"lowered reload completion");
    phase="reload-aimed-held";Inputs(true);yield return Wait(.6f);entry.SecondaryCount=0;Inputs(true,false,true);yield return Wait(.06f);Inputs(true);yield return Wait(data.ReloadDuration+.5f);Require(states.m_CurrentState is PlayerStateAiming,"held reload return");
    phase="reload-aimed-release";entry.SecondaryCount=0;Inputs(true,false,true);yield return Wait(.06f);Inputs(true);yield return Wait(data.ReloadDuration*.55f);Inputs(false);yield return Wait(data.ReloadDuration*.45f+.5f);Require(states.m_CurrentState is PlayerStateMotion,"released reload return");
    phase="walk";InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return Wait(.6f);phase="stop";Inputs(false);yield return Wait(.5f);
    Debug.Log("WRIST_TRANSITIONS_OK "+data.name);
    if(Inv.TryGet(data.AmmoItem,out ammo))Inv.Remove(ammo,ammo.Count);
   }
   csv.Dispose();csv=null;Debug.Log("ALL_WRIST_TRANSITIONS_OK");InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);SessionState.SetBool("WristTransitionValidation",false);EditorApplication.Exit(0);
  }
  IEnumerator Wait(float duration){float end=Time.time+duration;while(Time.time<end)yield return null;}
  void Inputs(bool aim,bool fire=false,bool reload=false){var m=new MouseState();if(aim)m=m.WithButton(MouseButton.Right);if(fire)m=m.WithButton(MouseButton.Left);InputSystem.QueueStateEvent(mouse,m);InputSystem.QueueStateEvent(keyboard,reload?new KeyboardState(Key.R):new KeyboardState());}
  void Require(bool ok,string message){if(ok)return;Debug.LogError("WRIST_TRANSITION_FAILURE "+data.name+" "+message);csv?.Dispose();csv=null;SessionState.SetBool("WristTransitionValidation",false);EditorApplication.Exit(1);throw new Exception(message);}
  void LateUpdate(){if(csv==null||!data)return;var state=states.m_CurrentState;if(previousState!=state){captureUntil=Time.time+.40f;previousState=state;}var values=new List<string>{data.name,phase,(ticks++).ToString(),Time.time.ToString("F5",CultureInfo.InvariantCulture),state?.GetType().Name};Add(values,actor.transform.InverseTransformPoint(left.position));Add(values,Quaternion.Inverse(actor.transform.rotation)*left.rotation);Add(values,actor.transform.InverseTransformPoint(right.position));Add(values,Quaternion.Inverse(actor.transform.rotation)*right.rotation);Add(values,left.localRotation);Add(values,right.localRotation);csv.WriteLine(string.Join(",",values));
   if(capture&&Time.time<captureUntil){string folder=Path.Combine(output,data.name+"_"+phase);Directory.CreateDirectory(folder);var focus=actor.transform.position+Vector3.up*1.3f+actor.transform.forward*.15f;cam.transform.position=focus+actor.transform.right*1.4f+actor.transform.forward*1.2f+Vector3.up*.08f;cam.transform.LookAt(focus);cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(640,480,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,640,480),0,0);im.Apply();File.WriteAllBytes(Path.Combine(folder,ticks.ToString("D5")+".png"),im.EncodeToPNG());Destroy(im);RenderTexture.active=old;}
  }
  static void Add(List<string> a,Vector3 v){a.Add(v.x.ToString("F6",CultureInfo.InvariantCulture));a.Add(v.y.ToString("F6",CultureInfo.InvariantCulture));a.Add(v.z.ToString("F6",CultureInfo.InvariantCulture));}
  static void Add(List<string> a,Quaternion q){a.Add(q.x.ToString("F6",CultureInfo.InvariantCulture));a.Add(q.y.ToString("F6",CultureInfo.InvariantCulture));a.Add(q.z.ToString("F6",CultureInfo.InvariantCulture));a.Add(q.w.ToString("F6",CultureInfo.InvariantCulture));}
 }
}