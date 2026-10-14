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
  const string Key="HorrorGame.WeaponHandgunClipping";
  const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/HandgunGripFinal";
  static string[] paths={"Assets/Game/Weapons/Data/FlareGun.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset","Assets/Game/Weapons/Data/Revolver.asset"};
  static int stage,index,pressFrame;static double started,phase;static Mouse mouse;static Camera camera;static RenderTexture target;static ReloadableWeaponData data;
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
     Capture("Aim");SetVertical(1);stage=4;phase=now;
    }
    else if(stage==4&&now-phase>.4)
    {
     Capture("AimUp");SetVertical(-.5f);stage=5;phase=now;
    }
    else if(stage==5&&now-phase>.4)
    {
     Capture("AimMidDown");SetVertical(-1);stage=55;phase=now;
    }
    else if(stage==55&&now-phase>.4)
    {
     Capture("AimDown");SetVertical(0);stage=6;phase=now;
    }
    else if(stage==6&&now-phase>.4)
    {
     InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
     pressFrame=Time.frameCount;stage=7;
    }
    else if(stage==7&&Time.frameCount>pressFrame)
    {
     InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));stage=8;phase=now;
    }
    else if(stage==8&&now-phase>.12)
    {
     Capture("FireEarly");stage=9;phase=now;
    }
    else if(stage==9&&now-phase>.18)
    {
     Capture("FireMiddle");stage=10;phase=now;
    }
    else if(stage==10&&now-phase>.18)
    {
     Capture("FireLate");InputSystem.QueueStateEvent(mouse,new MouseState());stage=11;phase=now;
    }
    else if(stage==11&&now-phase>3)
    {
     ++index;if(index>=paths.Length){Debug.Log("WEAPON_GRIP_AUDIT_OK all three handguns idle, neutral/up/down aiming and firing captured");Finish(0);return;}
     stage=1;phase=now;
    }
   }
   catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  static void SetVertical(float value)
  {
   var player=GameManager.Instance.Player;var state=player.GetComponentInChildren<ActorStateController>().m_CurrentState as PlayerStateAiming;
   var sensor=state.GetComponent<IPlayerVerticalAiming>();if(sensor is Behaviour b)b.enabled=false;
   if(sensor!=null)sensor.Verticality=value;
   player.MainAnimator.SetFloat(PlayerStateAiming.k_AimVerticalityHash,value);
  }
  static void Capture(string pose)
  {
   var player=GameManager.Instance.Player;var animator=player.MainAnimator;var right=animator.GetBoneTransform(HumanBodyBones.RightHand);var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   var weapon=player.GetComponentInChildren<Weapon>();var center=(right.position+left.position)*.5f;
   var focus=player.transform.position+Vector3.up*1.3f+player.transform.forward*.25f;
   camera.transform.position=focus+player.transform.right*2f+player.transform.forward*.65f+Vector3.up*.2f;
   camera.transform.LookAt(focus);Save(data.name+"_"+pose+"_Side.png");
   camera.transform.position=center+player.transform.right*.6f+player.transform.forward*.22f+Vector3.up*.13f;camera.transform.LookAt(center+Vector3.up*.04f);Save(data.name+"_"+pose+"_CloseSide.png");
   camera.transform.position=center+player.transform.forward*.65f-player.transform.right*.25f+Vector3.up*.14f;camera.transform.LookAt(center+Vector3.up*.04f);Save(data.name+"_"+pose+"_CloseFront.png");
   ExportMeshes(player,weapon,right,left,data.name+"_"+pose);
   if(pose=="Aim") Debug.Log("FIXED_SUPPORT "+data.name+" offset="+(Quaternion.Inverse(right.rotation)*(left.position-right.position)).ToString("F9")+" rotation="+(Quaternion.Inverse(right.rotation)*left.rotation).eulerAngles.ToString("F6"));
   Debug.Log("RIGHT_BONE_ROT "+data.name+" "+pose+" "+right.rotation.ToString("F9"));
   var text=$"{data.name} {pose} rightHand={right.position} leftHand={left.position.ToString("F9")} weaponRoot={weapon.transform.position.ToString("F9")} visuals={weapon.Visuals.transform.position} state={player.GetComponentInChildren<ActorStateController>().m_CurrentState.GetType().Name} inputAim={player.GetComponent<IPlayerInput>().IsAimingHeld()} attachment={data.CharacterAttachment.Position} rotation={data.CharacterAttachment.Rotation}\n";
   foreach(var m in weapon.GetComponentsInChildren<MeshRenderer>())text+=$"mesh {m.name} center={m.bounds.center} size={m.bounds.size}\n";
   File.WriteAllText(Path.Combine(Output,data.name+"_"+pose+".txt"),text);Debug.Log("GRIP_CAPTURE "+text);
  }
  static void ExportMeshes(Actor player,Weapon weapon,Transform right,Transform left,string name)
  {
   var obj=new System.Text.StringBuilder();int offset=1;
   foreach(var skinned in player.GetComponentsInChildren<SkinnedMeshRenderer>())
   {
    if(skinned.transform.IsChildOf(weapon.transform))continue;
    var baked=new Mesh();skinned.BakeMesh(baked);var weights=skinned.sharedMesh.boneWeights;var bones=skinned.bones;var vertices=baked.vertices;var triangles=baked.triangles;
    foreach(var hand in new[]{right,left})
    {
     obj.AppendLine("o "+(hand==right?"RightHand":"LeftHand"));
     foreach(var v in vertices){var w=skinned.transform.TransformPoint(v);obj.AppendLine($"v {w.x:R} {w.y:R} {w.z:R}");}
     var selected=new bool[vertices.Length];
     for(int i=0;i<weights.Length;++i)
     {
      var b=weights[i];float sum=0;
      if(bones[b.boneIndex0].IsChildOf(hand))sum+=b.weight0;
      if(bones[b.boneIndex1].IsChildOf(hand))sum+=b.weight1;
      if(bones[b.boneIndex2].IsChildOf(hand))sum+=b.weight2;
      if(bones[b.boneIndex3].IsChildOf(hand))sum+=b.weight3;
      selected[i]=sum>.5f;
     }
     for(int i=0;i<triangles.Length;i+=3)if(selected[triangles[i]]&&selected[triangles[i+1]]&&selected[triangles[i+2]])obj.AppendLine($"f {offset+triangles[i]} {offset+triangles[i+1]} {offset+triangles[i+2]}");
     offset+=vertices.Length;
    }
    UnityEngine.Object.Destroy(baked);
   }
   foreach(var filter in weapon.GetComponentsInChildren<MeshFilter>())
   {
    obj.AppendLine("o Weapon_"+filter.name);
    foreach(var v in filter.sharedMesh.vertices){var w=filter.transform.TransformPoint(v);obj.AppendLine($"v {w.x:R} {w.y:R} {w.z:R}");}
    var t=filter.sharedMesh.triangles;for(int i=0;i<t.Length;i+=3)obj.AppendLine($"f {offset+t[i]} {offset+t[i+1]} {offset+t[i+2]}");offset+=filter.sharedMesh.vertexCount;
   }
   File.WriteAllText(Path.Combine(Output,name+".obj"),obj.ToString());
  }
  static void Save(string name)
  {
   camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
   image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);
  }
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(mouse!=null)InputSystem.RemoveDevice(mouse);EditorApplication.Exit(code);}
 }
}
