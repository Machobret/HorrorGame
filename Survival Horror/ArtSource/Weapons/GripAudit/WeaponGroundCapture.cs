using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace HorrorGame.Weapons.Editor
{
 public static class WeaponGroundCapture
 {
  static Camera camera;static RenderTexture target;static int warmup;static GameObject[] guns=new GameObject[5],boxes=new GameObject[5];
  const string Output="C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/Weapons_And_Ammo_Ground.png";
  public static void RunBatch()
  {
   try{
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    // Preview-only full-resolution capture; the models retain their point-filtered PSX textures.
    var retro=AssetDatabase.LoadAssetAtPath<Material>("Assets/General/Materials/RetroComposite.mat");
    retro.SetVector("_VirtualResolution",new Vector4(1440,1280,0,0));retro.SetFloat("_DitherStrength",.12f);retro.SetFloat("_PixelWobble",0);retro.SetFloat("_PixelShiftStrength",0);retro.SetFloat("_LensWarp",0);
    RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.52f,.56f);RenderSettings.ambientIntensity=1;
    var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Dark concrete floor";floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(12,.1f,7);
    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.065f,.068f,.073f));material.SetFloat("_Smoothness",.06f);floor.GetComponent<Renderer>().sharedMaterial=material;
    var key=new GameObject("Soft key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=3.2f;key.color=new Color(1,.92f,.83f);key.transform.rotation=Quaternion.Euler(48,-28,0);key.shadows=LightShadows.Soft;
    var fill=new GameObject("Cool fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1.2f;fill.color=new Color(.65f,.78f,1);fill.transform.rotation=Quaternion.Euler(35,145,0);
    string[] names={"Pistol","Shotgun","Revolver","HuntingRifle","FlareGun"};string[] ammo={"PistolAmmo","ShotgunAmmo","RevolverAmmo","HuntingRifleAmmo","FlareAmmo"};
    for(int i=0;i<5;++i){
     float x=(i-2)*1.6f;
     var gun=Load(names[i]);guns[i]=gun;var b=BoundsOf(gun);Vector3 size=b.size;
     int thin=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;
     int longer=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
     Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};
     Quaternion lay=Quaternion.FromToRotation(axes[thin],Vector3.up);Vector3 along=lay*axes[longer];lay=Quaternion.AngleAxis(Vector3.SignedAngle(along,Vector3.right,Vector3.up),Vector3.up)*lay;
     gun.transform.rotation=(i==0?Quaternion.Euler(0,180,0):Quaternion.identity)*lay;Place(gun,new Vector3(x,0,.35f));
     var box=Load(ammo[i]);boxes[i]=box;box.transform.rotation=Quaternion.Euler(0,i==2?168:-12,0);Place(box,new Vector3(x+.04f,0,.35f-BoundsOf(gun).size.z*.5f-BoundsOf(box).size.z*.5f-.09f));
     Debug.Log("GROUND_MODEL "+names[i]+" gun="+BoundsOf(gun).size+" ammo="+BoundsOf(box).size);
    }
    camera=new GameObject("Ground lineup camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.75f;camera.nearClipPlane=.05f;camera.farClipPlane=30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.038f,.044f);camera.transform.position=new Vector3(0,6,-5.5f);camera.transform.LookAt(new Vector3(0,0,0));
    target=new RenderTexture(1440,1280,24);camera.targetTexture=target;warmup=0;EditorApplication.update+=Capture;
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static GameObject Load(string name){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Weapons/Prefabs/"+name+"_Pickup.prefab");if(!prefab)throw new Exception("Missing pickup "+name);var go=UnityEngine.Object.Instantiate(prefab);go.name=name;foreach(var particle in go.GetComponentsInChildren<ParticleSystem>())UnityEngine.Object.DestroyImmediate(particle.gameObject);return go;}
  static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)throw new Exception("No model for "+go.name);var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
  static void Place(GameObject go,Vector3 location){var b=BoundsOf(go);go.transform.position+=new Vector3(location.x-b.center.x,.008f-b.min.y,location.z-b.center.z);}
  static void Capture()
  {
   if(++warmup<6)return;EditorApplication.update-=Capture;
   try{
    for(int i=0;i<5;++i){
     var combined=BoundsOf(guns[i]);combined.Encapsulate(BoundsOf(boxes[i]));var focus=combined.center;
     camera.transform.position=focus+new Vector3(0,6,-5.5f);camera.transform.LookAt(focus);
     Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
     for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var point=combined.center+Vector3.Scale(combined.extents,new Vector3(x,y,z));var c=camera.transform.InverseTransformPoint(point);min=Vector2.Min(min,new Vector2(c.x,c.y));max=Vector2.Max(max,new Vector2(c.x,c.y));}
     camera.orthographicSize=Mathf.Max((max.y-min.y)*.5f,(max.x-min.x)*.5f/(1440f/1280f))*1.22f;
     camera.transform.position+=camera.transform.right*((max.x+min.x)*.5f)+camera.transform.up*((max.y+min.y)*.5f);
     camera.Render();RenderTexture.active=target;var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();File.WriteAllBytes(Output.Replace(".png","_"+guns[i].name+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
    }
    Debug.Log("WEAPONS_GROUND_CAPTURE_OK five pairs of actual pickup models on floor");EditorApplication.Exit(0);
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
