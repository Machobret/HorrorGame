using System;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HorrorGame.Weapons.Editor
{
 [InitializeOnLoad]
 public static class FlareLightingValidation
 {
  const string Key="HorrorGame.FlareLightingValidation";
  const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation";
  static int stage, frame;
  static double started;
  static Material style;
  static SignalFlare flare;
  static Camera camera;
  static RenderTexture target;
  static Color32[] baseline, inactive;
  static string errors="";
  static FlareLightingValidation(){EditorApplication.playModeStateChanged+=OnState;}
  public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
  static void OnState(PlayModeStateChange state)
  {
   if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
   started=EditorApplication.timeSinceStartup;stage=0;Application.logMessageReceived+=OnLog;EditorApplication.update+=Tick;
  }
  static void OnLog(string m,string stack,LogType type){if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=m+"\n";}
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  static Color32[] Capture(string name)
  {
   var previous=RenderTexture.active;RenderTexture.active=target;
   var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
   image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
   File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());var pixels=image.GetPixels32();RenderTexture.active=previous;UnityEngine.Object.Destroy(image);return pixels;
  }
  static void Tick()
  {
   try
   {
    if(EditorApplication.timeSinceStartup-started>45)throw new Exception("Lighting validation timeout "+errors);
    if(stage==0&&GameManager.Exists&&GameManager.Instance.Player&&Time.timeSinceLevelLoad>1)
    {
     camera=Camera.main;target=new RenderTexture(1280,720,24);camera.targetTexture=target;
     style=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Weapons/Materials/StylizedFlareLighting.mat");
     Check(style&&style.shader.isSupported,"Lighting material/shader unavailable");
     var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Weapons/Effects/SignalFlare.prefab");
     flare=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.12f,3),Quaternion.identity).GetComponent<SignalFlare>();
     flare.EnableDamage=false;flare.Launch(Vector3.zero,GameManager.Instance.Player.GetComponent<Combatant>());stage=1;
    }
    else if(stage==1&&flare.HasLanded)
    {
     Time.timeScale=0;style.SetFloat("_Strength",0);frame=Time.frameCount;stage=2;
    }
    else if(stage==2&&Time.frameCount>frame+4)
    {
     baseline=Capture("Flare_Lighting_Before.png");style.SetFloat("_Strength",1);frame=Time.frameCount;stage=3;
    }
    else if(stage==3&&Time.frameCount>frame+4)
    {
     var styled=Capture("Flare_Lighting_Stylized.png");int changed=0;
     for(int i=0;i<styled.Length;++i)if(Mathf.Abs(styled[i].r-baseline[i].r)+Mathf.Abs(styled[i].g-baseline[i].g)+Mathf.Abs(styled[i].b-baseline[i].b)>12)++changed;
     Check(changed>3000,"Surface lighting style did not render, changed="+changed);
     Check(Shader.GetGlobalInt("_StylizedFlareCount")==1,"Live flare registry incorrect");
     Debug.Log("STYLIZED_FLARE_SURFACES_OK changed pixels="+changed);
     UnityEngine.Object.Destroy(flare.gameObject);frame=Time.frameCount;stage=4;
    }
    else if(stage==4&&Time.frameCount>frame+4)
    {
     Check(Shader.GetGlobalInt("_StylizedFlareCount")==0,"Flare lighting leaked after destruction");
     style.SetFloat("_Strength",0);frame=Time.frameCount;stage=5;
    }
    else if(stage==5&&Time.frameCount>frame+4)
    {
     inactive=Capture("Flare_Lighting_NoFlare_Baseline.png");style.SetFloat("_Strength",1);frame=Time.frameCount;stage=6;
    }
    else if(stage==6&&Time.frameCount>frame+4)
    {
     var result=Capture("Flare_Lighting_NoFlare_Enabled.png");long difference=0;
     for(int i=0;i<result.Length;++i)difference+=Mathf.Abs(result[i].r-inactive[i].r)+Mathf.Abs(result[i].g-inactive[i].g)+Mathf.Abs(result[i].b-inactive[i].b);
     Check(difference<result.Length,"No-flare effect changed the scene, delta="+difference);
     Check(errors.Length==0,errors);Debug.Log("STYLIZED_FLARE_CLEANUP_OK no-flare mean channel delta="+((double)difference/result.Length));Finish(0);
    }
   }
   catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  static void Finish(int code)
  {
   SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.timeScale=1;
   if(style)style.SetFloat("_Strength",1);if(camera)camera.targetTexture=null;if(target)UnityEngine.Object.Destroy(target);EditorApplication.Exit(code);
  }
 }
}
