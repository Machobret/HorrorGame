using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HorrorEngine;
using HorrorGame.Environment;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace HorrorGame.StorageFacility.Editor
{
    [InitializeOnLoad]
    public static class StorageFacilityValidation
    {
        const string Pending="StorageFacility.Validation.Pending";
        static readonly StringBuilder report=new StringBuilder();
        static int stage;
        static double next,deadline;
        static PlayerActor player;
        static CharacterController capsule;
        static PlayerMovement movement;
        static StorageAccessPanel[] panels;
        static int samples,hiddenSamples;
        static readonly List<string> failures=new List<string>();
        static readonly Dictionary<Vector2Int,bool> walkable=new Dictionary<Vector2Int,bool>();
        static readonly Vector3[] RouteSamples={new Vector3(-2,0,-17),new Vector3(-13.5f,0,-11.2f),new Vector3(-8.5f,0,-7),
            new Vector3(0,0,3),new Vector3(7.9f,0,4),new Vector3(1.8f,0,11.5f),new Vector3(10.5f,0,18),
            new Vector3(15.8f,0,22),new Vector3(10.5f,0,30),new Vector3(14.4f,0,42),new Vector3(11,0,48),
            new Vector3(4,0,48),new Vector3(4,0,54),new Vector3(-12,0,53.5f),new Vector3(-18.5f,0,58.5f),new Vector3(3,0,65),
            new Vector3(-2.2f,0,1.2f),new Vector3(4.2f,0,1.2f),new Vector3(-2.2f,0,8),new Vector3(4.2f,0,8),
            new Vector3(-2.2f,0,11),new Vector3(10.5f,0,11),new Vector3(9.5f,0,15),new Vector3(11.5f,0,18),
            new Vector3(9.5f,0,27),new Vector3(11.5f,0,35),new Vector3(-5,0,7)};

        static StorageFacilityValidation()
        {
            if(SessionState.GetBool(Pending,false))
            {EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+150;}
        }
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run in an isolated batch project.");
            var scene=EditorSceneManager.OpenScene(StorageFacilityBlockoutBuilder.ScenePath);
            StaticChecks(scene);
            SessionState.SetBool(Pending,true);EditorApplication.playModeStateChanged+=OnPlayState;
            EditorApplication.EnterPlaymode();
        }
        public static void BuildAndRunBatch()
        {StorageFacilityBlockoutBuilder.BuildForValidation();RunBatch();}
        static void StaticChecks(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            if(all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))!=0)throw new Exception("Missing scripts in storage scene");
            if(all.Count(t=>t.GetComponent<HECore>())!=1)throw new Exception("Expected exactly one native HECore");
            if(all.Count(t=>t.GetComponent<PlayerSpawnPoint>())!=1)throw new Exception("Expected exactly one player spawn");
            var ids=all.Select(t=>t.GetComponent<ObjectUniqueId>()).Where(id=>id&&!id.IntentionallyDuplicated).ToArray();
            if(ids.GroupBy(id=>id.Id).Any(g=>g.Count()>1))throw new Exception("Duplicate save IDs");
            File.WriteAllText(Output("StaticChecks.txt"),$"PASS: one native HECore, one spawn, no missing scripts, {ids.Length} unique save IDs.\n"+
                $"{all.Count(t=>t.GetComponent<CameraPOV>())} native camera zones; {all.Count(t=>t.GetComponent<StorageAccessPanel>())} authored interaction points.\n");
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode)return;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+180;
            Application.logMessageReceived+=OnLog;stage=-3;next=EditorApplication.timeSinceStartup+2;
        }
        static void OnLog(string message,string stack,LogType type)
        {
            // Unity's editor search-index startup can fail independently of the running scene.
            if(stack.Contains("UnityEditor.Search."))return;
            if(type==LogType.Exception||type==LogType.Error)failures.Add(message);
        }
        static void Check(bool value,string message)
        {report.AppendLine((value?"PASS: ":"FAIL: ")+message);if(!value)throw new Exception(message);}
        static StorageAccessPanel Panel(string name)=>panels.Single(p=>p.name==name);
        static void Use(string name)
        {
            var panel=Panel(name);panel.GetComponent<Interactive>().Interact(player.GetComponentInChildren<PlayerInteractor>(true));
            Check(panel.State.IsFlagged,name+" completes through native Interactive");
            var dialog=UIManager.Get<UIDialog>();if(dialog&&dialog.gameObject.activeSelf)dialog.Hide();
            Physics.SyncTransforms();walkable.Clear();
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish("Timeout waiting for play test");return;}
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(stage==-3)
                {
                    if(!GameManager.Exists||!GameManager.Instance.Player){next=EditorApplication.timeSinceStartup+.5;return;}
                    player=GameManager.Instance.Player;capsule=player.GetComponent<CharacterController>();
                    panels=Object.FindObjectsByType<StorageAccessPanel>(FindObjectsSortMode.None);
                    player.transform.rotation=Quaternion.identity;
                    Warp(new Vector3(-13.5f,0,-11.2f));stage=-2;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==-2)
                {
                    var detector=player.GetComponentInChildren<InteractionColliderDetector>();
                    Check(detector.FocusedInteractive==Panel("Tablet_CheckIn").GetComponent<Interactive>(),"Walking up to tablet focuses native interaction detector");
                    stage=0;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==0)
                {
                    if(!GameManager.Exists||!GameManager.Instance.Player){next=EditorApplication.timeSinceStartup+.5;return;}
                    player=GameManager.Instance.Player;capsule=player.GetComponent<CharacterController>();
                    movement=player.GetComponent<PlayerMovement>();movement.enabled=false;
                    panels=Object.FindObjectsByType<StorageAccessPanel>(FindObjectsSortMode.None);
                    Check(Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length==1,"Only one native GameManager spawned");
                    Check(Object.FindObjectsByType<PlayerActor>(FindObjectsSortMode.None).Length==1,"Template character spawned exactly once");
                    Check(!Reachable(new Vector3(-2,0,-17),new Vector3(0,0,3)),"Unpowered entrance cannot be bypassed");
                    Check(Reachable(new Vector3(-2,0,-17),new Vector3(-13.5f,0,-11.2f)),"Arrival reaches tablet");
                    Check(Reachable(new Vector3(-13.5f,0,-11.2f),new Vector3(-8.5f,0,-7)),"Tablet spur reaches power cabinet");
                    Check(!Panel("PowerGrid1").CanUse,"Power requires tablet pairing");Use("Tablet_CheckIn");Use("PowerGrid1");
                    Check(Reachable(new Vector3(-8.5f,0,-7),new Vector3(0,0,3)),"Powered entrance is traversable");
                    Check(Reachable(new Vector3(0,0,3),new Vector3(7.9f,0,4)),"Office staging area is accessible");
                    Check(!Reachable(new Vector3(0,0,3),new Vector3(10.5f,0,18)),"Puzzle 1 blocks the storage spine");
                    Check(!Panel("Puzzle2_FeederB").CanUse,"Feeder requires service module and first shutter");
                    Use("Office_ServiceModule");Use("Puzzle1_LocalAccess");
                    Check(Reachable(new Vector3(0,0,3),new Vector3(15.8f,0,22)),"Open unit B102 is reachable after Puzzle 1");
                    Check(!Reachable(new Vector3(10.5f,0,18),new Vector3(10.5f,0,30)),"Puzzle 2 cannot be bypassed through adjacent units");
                    Use("Puzzle2_FeederB");Check(Reachable(new Vector3(15.8f,0,22),new Vector3(14.4f,0,42)),"North storage corridor reaches distribution bay");
                    Check(!Reachable(new Vector3(14.4f,0,42),new Vector3(4,0,48)),"Puzzle 3 blocks warehouse including side-wall shortcuts");
                    Use("Puzzle3_Distribution");
                    Check(Reachable(new Vector3(14.4f,0,42),new Vector3(4,0,48)),"Warehouse opens after distribution release");
                    Check(Reachable(new Vector3(4,0,48),new Vector3(-12,0,53.5f)),"First warehouse cross-aisle is navigable");
                    Check(Reachable(new Vector3(-12,0,53.5f),new Vector3(3,0,65)),"Rack-end circulation reaches dispatch end");
                    foreach(var panel in panels)
                    {
                        string saved=panel.State.GetSavableData();
                        panel.State.SetFromSavedData(saved);
                        Check(panel.ReleasedBarriers.All(b=>!b.activeSelf),panel.name+" restores released barrier state");
                    }
                    var map=Object.FindFirstObjectByType<MapController>();
                    Check(map&&map.Data.Rooms.Count>=11,"Native map contains facility layout");
                    var mapPickup=Object.FindFirstObjectByType<PickupMap>();mapPickup.Take();
                    Check(GameManager.Instance.Inventory.Maps.Contains(map.Data),"Office map pickup grants native map");
                    var mapUI=UIManager.Get<UIMap>();typeof(UIMap).GetMethod("Hide",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(mapUI,null);
                    var obtainedDialog=UIManager.Get<UIDialog>();if(obtainedDialog.gameObject.activeSelf)obtainedDialog.Hide();
                    stage=1;samples=0;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==1)
                {
                    if(samples>=RouteSamples.Length){Finish(null);return;}
                    Warp(RouteSamples[samples]);stage=2;next=EditorApplication.timeSinceStartup+.5;return;
                }
                var camera=Camera.main;
                Check(camera!=null,"Gameplay camera available at checkpoint "+samples);
                Vector3 target=player.transform.position+Vector3.up;
                var viewport=camera.WorldToViewportPoint(target);
                bool visible=viewport.z>0&&viewport.x>.02f&&viewport.x<.98f&&viewport.y>.02f&&viewport.y<.98f;
                var delta=target-camera.transform.position;
                var blockers=Physics.RaycastAll(camera.transform.position,delta.normalized,delta.magnitude-.25f,~0,QueryTriggerInteraction.Ignore)
                    .Where(hit=>!hit.collider.transform.IsChildOf(player.transform)).ToArray();
                bool clear=visible&&blockers.Length==0;
                report.AppendLine($"CAMERA {samples}: {RouteSamples[samples]} viewport={viewport} camera={camera.transform.position} fov={camera.fieldOfView} clear={clear} blockers={string.Join(",",blockers.Select(hit=>hit.collider.name))}");
                if(!clear)hiddenSamples++;
                if(!SystemInfo.graphicsDeviceType.Equals(UnityEngine.Rendering.GraphicsDeviceType.Null)&&new[]{0,4,6,9,13}.Contains(samples))Capture(camera,"View_"+samples+".png");
                samples++;stage=1;next=EditorApplication.timeSinceStartup+.1;
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Warp(Vector3 position)
        {
            capsule.enabled=false;player.transform.position=position;
            var body=player.GetComponent<Rigidbody>();if(body)body.position=position;
            capsule.enabled=true;Physics.SyncTransforms();
        }
        static bool Free(Vector2Int point)
        {
            if(walkable.TryGetValue(point,out bool cached))return cached;
            Vector3 position=new Vector3(point.x*.5f,0,point.y*.5f);
            bool clear=ClimbClearance.IsClear(capsule,position,null)&&ClimbClearance.HasSupport(capsule,position);
            walkable[point]=clear;return clear;
        }
        static bool Reachable(Vector3 from,Vector3 to)
        {
            var start=new Vector2Int(Mathf.RoundToInt(from.x*2),Mathf.RoundToInt(from.z*2));
            var end=new Vector2Int(Mathf.RoundToInt(to.x*2),Mathf.RoundToInt(to.z*2));
            if(!Free(start)||!Free(end))return false;
            var queue=new Queue<Vector2Int>();var visited=new HashSet<Vector2Int>{start};queue.Enqueue(start);
            while(queue.Count>0)
            {
                var current=queue.Dequeue();if(current==end)return true;
                foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                {
                    var next=current+direction;if(next.x< -42||next.x>38||next.y< -42||next.y>136||visited.Contains(next)||!Free(next))continue;
                    Vector3 a=new Vector3(current.x*.5f,0,current.y*.5f),b=new Vector3(next.x*.5f,0,next.y*.5f);
                    if(!ClimbClearance.CanMove(capsule,a,b,null))continue;
                    visited.Add(next);queue.Enqueue(next);
                }
            }
            return false;
        }
        static void Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1280,720,24);
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Output(name),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);}
        }
        static string Output(string name)
        {string directory=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/StorageFacility");Directory.CreateDirectory(directory);return Path.Combine(directory,name);}
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Pending,false);
            if(error!=null)report.AppendLine("FAIL: "+error);
            report.AppendLine($"Camera samples: {samples}, obscured/outside frame: {hiddenSamples}");
            foreach(var failure in failures.Distinct())report.AppendLine("RUNTIME ERROR: "+failure);
            File.WriteAllText(Output("PlayValidation.txt"),report.ToString());
            Debug.Log(report.ToString());EditorApplication.Exit(error==null&&hiddenSamples==0&&failures.Count==0?0:1);
        }
    }
}
