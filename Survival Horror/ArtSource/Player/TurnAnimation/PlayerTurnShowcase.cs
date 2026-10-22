using System;
using System.IO;
using System.Collections.Generic;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
namespace HorrorGame.Weapons.Editor
{
 [InitializeOnLoad] public static class PlayerTurnShowcase
 {
  const string Key="PlayerTurnShowcase";static string Output=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,"PlayerTurnShowcase");
  static Keyboard keyboard;static Mouse mouse;static Camera camera;static RenderTexture target;static int stage,startFrame,lastFrame;static double started;static Vector3 origin,previousLeft,previousRight;static float initialYaw,maxStep;static PlayerTurnAnimation turn;static Transform left,right;static readonly Dictionary<ScriptableRendererFeature,bool> Features=new Dictionary<ScriptableRendererFeature,bool>();
  static Actor Player=>GameManager.Instance.Player;
  static PlayerTurnShowcase(){EditorApplication.playModeStateChanged+=OnPlay;}
  public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
  static void OnPlay(PlayModeStateChange s){if(s!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;Directory.CreateDirectory(Output+"/Frames");foreach(var f in Directory.GetFiles(Output+"/Frames","*.png",SearchOption.TopDirectoryOnly))File.Delete(f);started=EditorApplication.timeSinceStartup;stage=0;lastFrame=-1;EditorApplication.update+=Tick;}
  static void Input(params UnityEngine.InputSystem.Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());}
  static void Tick(){try{
   if(EditorApplication.timeSinceStartup-started>130)throw new Exception("Turn showcase timeout");
   if(stage==0&&GameManager.Exists&&Player&&Time.timeSinceLevelLoad>3){
    Application.runInBackground=true;Time.captureFramerate=60;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
    var inv=GameManager.Instance.Inventory;var data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>("Assets/Game/Weapons/Data/MP5.asset");if(!inv.TryGet(data,out var entry))entry=inv.Add(data,1,30);inv.Equip(entry);turn=Player.MainAnimator.GetComponent<PlayerTurnAnimation>();left=Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);right=Player.MainAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
    foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!r.transform.IsChildOf(Player.transform))r.enabled=false;
    foreach(var g in AssetDatabase.FindAssets("t:UniversalRendererData"))foreach(var feature in AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(g)).rendererFeatures)if(feature&&feature.GetType().Name.Contains("FullScreen")&&!Features.ContainsKey(feature)){Features.Add(feature,feature.isActive);feature.SetActive(false);}
    RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.18f,.20f,.23f);var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.position=Player.transform.position-Vector3.up*.005f;floor.transform.localScale=Vector3.one*5;var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.11f,.13f,.15f);floor.GetComponent<Renderer>().material=material;UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
    var light=new GameObject("Turn showcase key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.2f;light.transform.rotation=Quaternion.Euler(40,-35,0);var fill=new GameObject("Turn showcase fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.color=new Color(.65f,.78f,1);fill.transform.rotation=Quaternion.Euler(25,140,0);
    camera=new GameObject("Turn showcase camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=38;camera.nearClipPlane=.02f;camera.cullingMask=~(1<<5);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.065f);var focus=Player.transform.position+Vector3.up*.92f;camera.transform.position=focus+Player.transform.forward*2.3f+Player.transform.right*2+Vector3.up*.2f;camera.transform.LookAt(focus);target=new RenderTexture(1024,768,24);camera.targetTexture=target;Input();startFrame=Time.frameCount;stage=1;
   }
   if(stage==0||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;int f=Time.frameCount-startFrame;
   if(f==20){origin=Player.transform.position;initialYaw=Player.transform.eulerAngles.y;previousLeft=left.position;previousRight=right.position;maxStep=0;}
   if(f>=30&&stage==1){Input(UnityEngine.InputSystem.Key.A);stage=2;}else if(f>=78&&stage==2){Input(UnityEngine.InputSystem.Key.D);stage=3;}else if(f>=126&&stage==3){Input();stage=4;}
   if(f>20){maxStep=Mathf.Max(maxStep,Vector3.Distance(previousLeft,left.position),Vector3.Distance(previousRight,right.position));previousLeft=left.position;previousRight=right.position;}
   if(f%3==0){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,768),0,0);image.Apply();File.WriteAllBytes(Output+"/Frames/"+f.ToString("D4")+".png",image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);}
   if(f>=174){if(maxStep>.08f||Vector3.ProjectOnPlane(Player.transform.position-origin,Vector3.up).magnitude>.035f||turn.TurningWeight>.01f)throw new Exception("Turn reversal/settle discontinuity step="+maxStep);File.WriteAllText(Output+"/Validation.json","{\"status\":\"passed\",\"maxFootStepMetres\":"+maxStep+",\"endingYawDifference\":"+Mathf.DeltaAngle(initialYaw,Player.transform.eulerAngles.y)+"}");Debug.Log("PLAYER_TURN_REVERSAL_SHOWCASE_OK maxStep="+maxStep);Finish(0);}
  }catch(Exception e){Debug.LogException(e);Finish(1);}}
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Time.captureFramerate=0;foreach(var f in Features)if(f.Key)f.Key.SetActive(f.Value);Features.Clear();if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);EditorApplication.Exit(code);}
 }
}