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
    public static class MP5PoseReview
    {
        const string Key="HorrorGame.MP5PoseReview";
        static string Output=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,"MP5PoseRevisionReview");
        static Mouse mouse; static Keyboard keyboard; static ActorStateController states;
        static MP5HandlingPose pose; static Camera camera; static RenderTexture target;
        static int stage,frame,assertions; static float phase; static double started;
        static Actor Player=>GameManager.Instance.Player;
        static MP5PoseReview(){EditorApplication.playModeStateChanged+=OnPlay;}
        public static void Apply()
        {
            string path="Assets/Game/Weapons/Prefabs/MP5.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{
                var hold=root.GetComponent<MP5HandlingPose>();
                hold.ForeEndWrist=new Vector3(-.055f,.005f,.175f);
                hold.LoweredWrist=new Vector3(.18f,1.38f,.22f);
                hold.AimedWrist=new Vector3(.18f,1.25f,.325f);
                hold.ElbowHint=new Vector3(-.27f,1.08f,.12f);
                hold.RightElbowHint=new Vector3(.35f,1.10f,.10f);
                var reload=root.GetComponent<MP5ReloadVisual>();
                reload.ReloadHandPosition=new Vector3(.23f,1.27f,.26f);
                reload.PocketPosition=new Vector3(-.25f,1.06f,.18f);
                reload.LeftElbowHint=new Vector3(-.35f,1.10f,.16f);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void RunIconBatch(){Apply();MP5IntegrationValidation.RunIconBatch();}
        public static void RunBatch(){try{Apply();EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static void OnPlay(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            Directory.CreateDirectory(Output);started=EditorApplication.timeSinceStartup;stage=frame=assertions=0;Time.captureFramerate=60;
            File.WriteAllText(Output+"/MotionMetrics.csv","phase,time,leftSupportError,receiverForward,muzzleForward,gunForwardDot,stockShoulderX,stockShoulderY,stockShoulderZ\n");
            EditorApplication.update+=Tick;
        }
        static void Input(bool aim=false,params UnityEngine.InputSystem.Key[] keys){InputSystem.QueueStateEvent(mouse,aim?new MouseState().WithButton(MouseButton.Right):new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
        static void Next(int value){stage=value;phase=Time.time;}
        static void Check(bool yes,string text){++assertions;if(!yes)throw new Exception(text);}
        static void Tick()
        {
            try{
                if(EditorApplication.timeSinceStartup-started>120)throw new Exception("MP5 motion review timeout stage="+stage);
                float dt=Time.time-phase;
                if(stage==0&&GameManager.Exists&&Player&&Time.timeSinceLevelLoad>3){
                    Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
                    var inv=GameManager.Instance.Inventory;var data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>("Assets/Game/Weapons/Data/MP5.asset");if(!inv.TryGet(data,out var entry))entry=inv.Add(data,1,30);inv.Equip(entry);
                    states=Player.StateController;camera=new GameObject("MP5 pose revision review").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=35;camera.nearClipPlane=.02f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.05f,.055f);camera.cullingMask=~(1<<5);target=new RenderTexture(960,720,24);camera.targetTexture=target;
                    var light=new GameObject("Pose review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,35,0);Input();Next(1);
                }else if(stage==1&&dt>.8f){pose=Player.GetComponentInChildren<MP5HandlingPose>();Check(pose,"MP5 not equipped");Capture("Idle",-1);Capture("Idle",1);Input(false,UnityEngine.InputSystem.Key.W);Next(2);}
                else if(stage==2){Sample("Forward",dt>.3f);if(dt>.8f){Capture("Forward",-1);Input(false,UnityEngine.InputSystem.Key.S);Next(3);}}
                else if(stage==3){Sample("Backward",dt>.3f);if(dt>1.3f){Capture("Backward",-1);Capture("Backward",1);Input();Next(4);}}
                else if(stage==4&&dt>.4f){Input(true);Next(5);}
                else if(stage==5){if(dt>.5f){Check(states.m_CurrentState is PlayerStateAiming,"Cannot aim");Sample("Aimed",true);Capture("Aimed",-1);Capture("Aimed",1);Debug.Log("MP5_MOTION_REVISION_OK assertions="+assertions);Finish(0);}}
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Sample(string label,bool strict)
        {
            if(!pose)return;
            var left=Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftHand);var shoulder=Player.MainAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            float error=Vector3.Distance(left.position,pose.ModelFrame.TransformPoint(pose.ForeEndWrist));
            var receiver=Player.transform.InverseTransformPoint(pose.ModelFrame.TransformPoint(new Vector3(0,.04f,.10f)));
            var muzzle=Player.transform.InverseTransformPoint(pose.ModelFrame.TransformPoint(new Vector3(0,.0815f,.3824f)));
            var delta=Player.transform.InverseTransformVector(pose.ModelFrame.TransformPoint(new Vector3(0,.045f,-.2976f))-shoulder.position);
            float dot=Vector3.Dot(pose.ModelFrame.forward,Player.transform.forward);
            File.AppendAllText(Output+"/MotionMetrics.csv",string.Join(",",label,Time.time.ToString("F4"),error.ToString("F4"),receiver.z.ToString("F4"),muzzle.z.ToString("F4"),dot.ToString("F4"),delta.x.ToString("F4"),delta.y.ToString("F4"),delta.z.ToString("F4"))+"\n");
            if(strict){Check(receiver.z>.20f&&muzzle.z>.43f&&dot>.85f,"MP5 swung through torso in "+label);Check(error<.09f,"Support hand cannot reach handguard "+label+" error="+error);if(label=="Aimed")Check(Mathf.Abs(delta.y)<.12f,"Stock still above shoulder: "+delta);}
        }
        static void Capture(string label,int side)
        {
            if(side==1){string probe="";foreach(var b in new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightHand})probe+=b+"="+Player.transform.InverseTransformPoint(Player.MainAnimator.GetBoneTransform(b).position).ToString("F4")+"; ";probe+="Target="+Player.transform.InverseTransformPoint(pose.ModelFrame.TransformPoint(pose.ForeEndWrist)).ToString("F4");File.WriteAllText(Output+"/"+label+"_Bones.txt",probe);}
            var focus=Player.transform.position+Vector3.up*1.30f+Player.transform.forward*.24f;camera.transform.position=focus+Player.transform.right*(side*1.4f)+Player.transform.forward*.45f+Vector3.up*.08f;camera.transform.LookAt(focus);camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(960,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes(Output+"/"+label+(side<0?"_Left":"_Right")+".png",image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Time.captureFramerate=0;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);EditorApplication.Exit(code);}
    }
}