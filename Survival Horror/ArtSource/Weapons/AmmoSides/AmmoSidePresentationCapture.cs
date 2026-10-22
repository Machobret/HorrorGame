using System;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace HorrorGame.Weapons.Editor
{
 public static class AmmoSidePresentationCapture
 {
  const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/AmmoSidesPreview";
  const string Assets="Assets/Game/Weapons/Models/AmmoSides/";
  static string[] names={"PistolAmmo","ShotgunAmmo","RevolverAmmo","HuntingRifleAmmo"};
  static string[] dataPaths={"Assets/AssetStore/HorrorEngine/ExampleContent/Items/GunAmmo/GunAmmo.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Items/ShotgunAmmo/ShotgunAmmo.asset","Assets/Game/Weapons/Data/RevolverAmmo.asset","Assets/Game/Weapons/Data/HuntingRifleAmmo.asset"};
  static Camera camera;static RenderTexture target;static int warmup;
  public static void RunBatch()
  {
   try{
    AmmoBoxSideTextureBuilder.Apply();Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    foreach(var guid in AssetDatabase.FindAssets("t:UniversalRendererData")){var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));foreach(var f in renderer.rendererFeatures)if(f&&f.GetType().Name.Contains("FullScreen"))f.SetActive(false);}
    RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.28f,.30f,.33f);
    var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2.1f;key.color=new Color(1,.86f,.7f);key.transform.rotation=Quaternion.Euler(40,-40,0);key.shadows=LightShadows.Soft;
    var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.color=new Color(.65f,.8f,1);fill.transform.rotation=Quaternion.Euler(35,140,0);
    camera=new GameObject("Ammo inspection camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;target=new RenderTexture(1024,1024,24);camera.targetTexture=target;EditorApplication.update+=Capture;
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static void Capture()
  {
   if(++warmup<5)return;EditorApplication.update-=Capture;
   try{
    for(int i=0;i<names.Length;++i){
     var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Weapons/Prefabs/"+names[i]+"_Pickup.prefab"));
     var renderers=model.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)throw new Exception("Missing model");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
     foreach(var f in model.GetComponentsInChildren<MeshFilter>()){
      if(!f.sharedMesh||!AssetDatabase.GetAssetPath(f.sharedMesh).Contains("AmmoSides"))throw new Exception("Missing corrected mesh "+names[i]);
      var uv=f.sharedMesh.uv;var sides=f.sharedMesh.GetTriangles(f.sharedMesh.subMeshCount-1);int valid=0;
      for(int t=0;t<sides.Length;t+=3){Vector2 a=uv[sides[t]],c=uv[sides[t+1]],d=uv[sides[t+2]];float area=Mathf.Abs((c.x-a.x)*(d.y-a.y)-(c.y-a.y)*(d.x-a.x));if(area>1e-5f)++valid;}
      if(valid<4)throw new Exception("Degenerate side UVs "+names[i]);Debug.Log("AMMO_UV_VALID "+names[i]+" nondegenerate side triangles="+valid);
     }
     for(int view=0;view<2;++view){
      Vector3 direction=view==0?new Vector3(.9f,1.1f,-1.5f):new Vector3(-.9f,1.1f,1.5f);camera.transform.position=b.center+direction*b.size.magnitude*2;camera.transform.LookAt(b.center);camera.orthographicSize=Fit(b)*1.15f;
      Save(Output+"/"+names[i]+(view==0?"_Front.png":"_Back.png"));
      if(view==(i==2?1:0)){string icon=Assets+names[i]+"_PickupIcon.png";Save(icon);AssetDatabase.ImportAsset(icon,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(icon);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();var data=AssetDatabase.LoadAssetAtPath<AmmoData>(dataPaths[i]);data.Image=AssetDatabase.LoadAssetAtPath<Sprite>(icon);EditorUtility.SetDirty(data);}
     }
     Object.DestroyImmediate(model);
    }
    AssetDatabase.SaveAssets();Debug.Log("AMMO_SIDE_PRESENTATION_OK eight side views, four refreshed pickup icons");EditorApplication.Exit(0);
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static float Fit(Bounds b){float size=0;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=camera.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));size=Mathf.Max(size,Mathf.Abs(p.x),Mathf.Abs(p.y));}return size;}
  static void Save(string path){camera.Render();RenderTexture.active=target;var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);}
 }
}
