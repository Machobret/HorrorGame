using System;
using System.Collections.Generic;
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
    public static class PlayerTurnValidation
    {
        const string SessionKey="PlayerTurnValidation";
        static string Output=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,"PlayerTurnReview");
        [Serializable] sealed class Case { public string Name; public float Yaw,Drift,LeftLift,RightLift,MaxWeight,PlantSlip; public int Assertions; }
        [Serializable] sealed class Report { public string Status="running",Error=""; public int Assertions; public List<Case> Cases=new List<Case>(); }
        static Report report;static Case current;static int stage,index,frame,lastFrame,lastSwing;static float phase,lastYaw,baseLeftY,baseRightY;static double started;static Vector3 startPosition,previousPlant;static bool previousStable;
        static Keyboard keyboard;static Mouse mouse;static Camera camera;static RenderTexture target;static PlayerTurnAnimation turn;static Transform left,right;static ActorStateController states;
        static Actor Player=>GameManager.Instance.Player;static Inventory Inv=>GameManager.Instance.Inventory;
        static readonly string[] Names={"Unarmed_Left","Unarmed_Right","MP5_Left","MP5_Right","MP5_Aimed_Left","MP5_Aimed_Right","1911_Aimed_Left","1911_Aimed_Right"};
        static PlayerTurnValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
        public static void RunBatch(){try{EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");SessionState.SetBool(SessionKey,true);EditorApplication.isPlaying=true;}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static void OnPlay(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(SessionKey,false))return;Directory.CreateDirectory(Output);started=EditorApplication.timeSinceStartup;stage=index=frame=0;report=new Report();EditorApplication.update+=Tick;}
        static void Input(bool aim=false,params UnityEngine.InputSystem.Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,aim?new MouseState().WithButton(MouseButton.Right):new MouseState());}
        static void Next(int next){stage=next;phase=Time.time;lastFrame=-1;}
        static void Check(bool yes,string text){++report.Assertions;if(current!=null)++current.Assertions;if(!yes)throw new Exception((current?.Name??"Setup")+": "+text);}
        static void Tick()
        {
            try{
                if(EditorApplication.timeSinceStartup-started>220)throw new Exception("Turn review timeout stage="+stage);
                float dt=Time.time-phase;
                if(stage==0&&GameManager.Exists&&Player&&Time.timeSinceLevelLoad>3){
                    Application.runInBackground=true;Time.captureFramerate=60;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();turn=Player.MainAnimator.GetComponent<PlayerTurnAnimation>();Check(turn,"No installed turn animation");Check(Player.MainAnimator.GetComponents<PlayerTurnAnimation>().Length==1,"Duplicate turn animation");left=Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);right=Player.MainAnimator.GetBoneTransform(HumanBodyBones.RightFoot);states=Player.StateController;
                    camera=new GameObject("Turn animation review camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=40;camera.nearClipPlane=.02f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.05f,.055f);camera.cullingMask=~(1<<5);target=new RenderTexture(1024,768,24);camera.targetTexture=target;
                    var light=new GameObject("Turn review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-40,0);Next(1);
                }else if(stage==1){
                    Input();Inv.Unequip(Inv.WeaponSlot);if(index>=2){var path=index>=6?"Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset":"Assets/Game/Weapons/Data/MP5.asset";var data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(path);if(!Inv.TryGet(data,out var entry))entry=Inv.Add(data,1,30);Inv.Equip(entry);}Next(2);
                }else if(stage==2&&dt>.6f){Input(index>=4);Next(3);}
                else if(stage==3&&dt>.4f){
                    Check(index>=4?states.m_CurrentState is PlayerStateAiming:states.m_CurrentState is PlayerStateMotion,"Wrong source state");current=new Case{Name=Names[index]};report.Cases.Add(current);startPosition=Player.transform.position;lastYaw=Player.transform.eulerAngles.y;baseLeftY=left.position.y;baseRightY=right.position.y;previousStable=false;lastSwing=-1;Directory.CreateDirectory(Output+"/"+current.Name);
                    var focus=Player.transform.position+Vector3.up*.9f;camera.transform.position=focus+Player.transform.right*2.4f+Player.transform.forward*1.6f+Vector3.up*.25f;camera.transform.LookAt(focus);Input(index>=4,index%2==0?UnityEngine.InputSystem.Key.A:UnityEngine.InputSystem.Key.D);Next(4);
                }else if(stage==4){
                    if(lastFrame!=Time.frameCount){lastFrame=Time.frameCount;float yaw=Player.transform.eulerAngles.y;current.Yaw+=Mathf.DeltaAngle(lastYaw,yaw);lastYaw=yaw;current.Drift=Mathf.Max(current.Drift,Vector3.ProjectOnPlane(Player.transform.position-startPosition,Vector3.up).magnitude);current.LeftLift=Mathf.Max(current.LeftLift,left.position.y-baseLeftY);current.RightLift=Mathf.Max(current.RightLift,right.position.y-baseRightY);current.MaxWeight=Mathf.Max(current.MaxWeight,turn.TurningWeight);
                    int support=1-turn.SwingFoot;Vector3 planted=support==0?left.position:right.position;bool stable=turn.TurningWeight>.99f&&turn.IsStepping;if(stable&&previousStable&&lastSwing==turn.SwingFoot)current.PlantSlip=Mathf.Max(current.PlantSlip,Vector3.Distance(planted,previousPlant));previousStable=stable;lastSwing=turn.SwingFoot;previousPlant=planted;
                    if(++frame%3==0)Capture(current.Name+"/"+Time.frameCount.ToString("D6"));}
                    if(dt>1.05f){Check(index%2==0?current.Yaw<-100:current.Yaw>100,"Input did not rotate expected direction yaw="+current.Yaw);Check(current.Drift<.035f,"Turn introduced translation "+current.Drift);Check(current.MaxWeight>.99f,"Turn animation did not blend in");Check(current.LeftLift>.018f&&current.RightLift>.018f,"Feet never visibly stepped L="+current.LeftLift+" R="+current.RightLift);Check(current.PlantSlip<.03f,"Planted foot slides "+current.PlantSlip);Input(index>=4);Next(5);}
                }else if(stage==5&&dt>.6f){Check(turn.TurningWeight<.01f&&!turn.IsStepping,"Turn cycle did not settle after release");Debug.Log("PLAYER_TURN_CASE_OK "+JsonUtility.ToJson(current));if(++index<Names.Length)Next(1);else{Input(false,UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.D);Next(6);}}
                else if(stage==6&&dt>.6f){Check(turn.TurningWeight<.01f,"Pivot overrides normal walking");Input();report.Status="passed";Debug.Log("ALL_PLAYER_TURN_CHECKS_OK assertions="+report.Assertions);Finish(0);}
            }catch(Exception e){report.Status="failed";report.Error=e.ToString();Debug.LogException(e);Finish(1);}
        }
        static void Capture(string name){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,768),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);}
        static void Finish(int code){if(report!=null)File.WriteAllText(Output+"/Validation.json",JsonUtility.ToJson(report,true));SessionState.SetBool(SessionKey,false);EditorApplication.update-=Tick;Time.captureFramerate=0;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);EditorApplication.Exit(code);}
    }
}