using System;
using System.IO;
using System.Reflection;
using System.Text;
using HorrorEngine;
using HorrorGame.Environment;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace HorrorGame.Tests
{
    // Native end-to-end animation/event check; generated fixtures exist only during Play Mode.
    public static class CrateClimbPlayCheck
    {
        static Actor actor;
        static GameObject floor,box;
        static Interactive interactive;
        static CrateClimbRig rig;
        static Vector3 savedPosition;
        static Quaternion savedRotation;
        static int face,stage;
        static float started;
        static bool entered,edgeMode;
        static float resistanceStart;
        static PlayerMovement movement;
        static IPlayerMovementSettings previousSettings;
        static readonly FieldInfo Settings=typeof(PlayerMovement).GetField("m_Settings",BindingFlags.NonPublic|BindingFlags.Instance);
        static ForwardSettings forced;
        sealed class ForwardSettings : IPlayerMovementSettings
        {
            public float Rate;
            public PlayerMovementType GetMovementType()=>default;
            public float GetFwdRate(PlayerMovement m)=>Rate;
            public float GetRightRate(PlayerMovement m)=>0;
            public void GetRotation(PlayerMovement m,out float sign,out float rate){sign=0;rate=0;}
            public void ResetMovement() { }
        }
        static readonly StringBuilder report=new StringBuilder();
        static readonly Vector3[] Directions={Vector3.back,Vector3.right,Vector3.forward,Vector3.left};
        [MenuItem("Horror Game/Tests/Check Small Crate Climbing")]
        public static void Run() { Start(false); }
        [MenuItem("Horror Game/Tests/Check Crate Edge Delay")]
        public static void RunEdges() { Start(true); }
        static void Start(bool edges)
        {
            if (!EditorApplication.isPlaying || !GameManager.Exists || !GameManager.Instance.Player)
                throw new Exception("Enter Play Mode with a spawned native player first.");
            if (actor) throw new Exception("Check already running.");
            actor=GameManager.Instance.Player;savedPosition=actor.transform.position;savedRotation=actor.transform.rotation;
            edgeMode=edges;
            movement=actor.GetComponent<PlayerMovement>();previousSettings=(IPlayerMovementSettings)Settings.GetValue(movement);
            forced=new ForwardSettings();
            if(edgeMode)Settings.SetValue(movement,forced);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Climb check floor";
            floor.transform.SetPositionAndRotation(new Vector3(1000,-.25f,1000),Quaternion.identity);floor.transform.localScale=new Vector3(20,.5f,20);
            box=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/PushableCrate/PushableCrate1.prefab"),new Vector3(1000,.02f,1000),Quaternion.Euler(0,27,0));
            foreach(var id in box.GetComponentsInChildren<ObjectUniqueId>(true)) id.RegenerateId();
            rig=box.GetComponentInChildren<CrateClimbRig>();interactive=rig.GetComponent<Interactive>();
            face=0;stage=0;started=Time.time;entered=false;report.Clear();
            EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || !actor) { Finish(false,"Play ended"); return; }
                if (stage==0)
                {
                    if (Time.time-started<.35f) return;
                    Vector3 outward=box.transform.TransformDirection(Directions[face]);
                    Teleport(box.transform.position+outward*.95f+Vector3.up*.01f,Quaternion.LookRotation(-outward));
                    interactive.Interact(actor.GetComponent<PlayerInteractor>());
                    stage=1;started=Time.time;entered=false;return;
                }
                bool climbing=actor.StateController.CurrentState is PlayerStateClimbing;
                if(climbing) entered=true;
                if(Time.time-started>5) { Finish(false,"Timeout face="+face+" stage="+stage+" state="+actor.StateController.CurrentState?.GetType().Name+" y="+actor.transform.position.y);return; }
                if(stage>=10)
                {
                    var guard=actor.GetComponent<CrateEdgeClimbGuard>();
                    if(!guard || !guard.IsGuarding) { if(climbing && stage==12) { } else { Finish(false,"Missing edge guard stage="+stage);return; } }
                    if(stage==10)
                    {
                        if(climbing) { Finish(false,"Dropped before short hold release check");return; }
                        if(guard.HeldTime<.10f)return;
                        forced.Rate=0;stage=11;started=Time.time;return;
                    }
                    if(stage==11)
                    {
                        if(Time.time-started<.18f)return;
                        if(climbing || guard.HeldTime>.001f || Mathf.Abs(actor.transform.position.y-box.transform.position.y-1)>.12f)
                        {Finish(false,"Short hold/release must retain top and reset timer");return;}
                        report.AppendLine("PASS edge short hold/release face="+face);
                        forced.Rate=1;stage=12;resistanceStart=-1;started=Time.time;return;
                    }
                    if(resistanceStart<0 && guard && guard.HeldTime>0)resistanceStart=Time.time-guard.HeldTime;
                    if(!climbing)return;
                    float held=Time.time-resistanceStart;
                    if(resistanceStart<0 || held<.21f || held>.5f){Finish(false,"Incorrect edge delay: "+held);return;}
                    report.AppendLine("PASS automatic edge drop face="+face+" hold="+held.ToString("F3"));
                    forced.Rate=0;stage=3;started=Time.time;entered=true;return;
                }
                if(!entered || climbing || Time.time-started<.3f) return;
                float y=actor.transform.position.y-box.transform.position.y;
                if(stage==1)
                {
                    if(Mathf.Abs(y-1)>.12f) { Finish(false,"Incorrect top landing face="+face+" height="+y);return; }
                    report.AppendLine("PASS climb up face="+face+" height="+y.ToString("F3"));
                    // Native climbing exit and contact-rig release complete in LateUpdate.
                    stage=2;started=Time.time;return;
                }
                if(stage==2)
                {
                    if(Time.time-started<.15f)return;
                    Vector3 outward=box.transform.TransformDirection(Directions[face]);
                    actor.transform.rotation=Quaternion.LookRotation(outward);
                    if(edgeMode)
                    {
                        forced.Rate=1;stage=10;started=Time.time;return;
                    }
                    interactive.Interact(actor.GetComponent<PlayerInteractor>());
                    stage=3;started=Time.time;entered=false;return;
                }
                if(Mathf.Abs(y)>.12f) { Finish(false,"Incorrect bottom landing face="+face+" height="+y);return; }
                report.AppendLine("PASS climb down face="+face+" height="+y.ToString("F3"));
                if(++face==4) { Finish(true,"All four faces, up and down");return; }
                stage=0;started=Time.time;entered=false;
            }
            catch(Exception ex) { Finish(false,ex.ToString()); }
        }
        static void Teleport(Vector3 pos,Quaternion rot)
        {
            var cc=actor.GetComponent<CharacterController>();if(cc)cc.enabled=false;
            actor.PlaceAt(pos,rot);var body=actor.GetComponent<Rigidbody>();if(body){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            if(cc)cc.enabled=true;Physics.SyncTransforms();
        }
        static void Finish(bool ok,string detail)
        {
            EditorApplication.update-=Tick;
            report.AppendLine((ok?"PASS: ":"FAIL: ")+detail);
            Directory.CreateDirectory("Exports/Reviews/PushableCrate");File.WriteAllText("Exports/Reviews/PushableCrate/"+(edgeMode?"EdgeDelayValidation.txt":"ClimbValidation.txt"),report.ToString());
            Debug.Log("CRATE_CLIMB_CHECK "+report);
            if(movement && previousSettings!=null)Settings.SetValue(movement,previousSettings);
            movement=null;previousSettings=null;
            if(box)Object.DestroyImmediate(box);if(floor)Object.DestroyImmediate(floor);
            if(actor)Teleport(savedPosition,savedRotation);
            actor=null;box=null;floor=null;
        }
    }
}
