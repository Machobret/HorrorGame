using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    // Copy into the isolated project's Editor folder to run. This exercises the
    // actual player's dropper and pickups; it never invokes disk save/load APIs.
    [InitializeOnLoad]
    public static class AmmoPackagingRuntimeValidation
    {
        const string SessionKey = "HorrorGame.AmmoPackagingRuntimeValidation";
        const string Root = "Assets/Game/Weapons/";
        static readonly string[] Names = { "PistolAmmo", "ShotgunAmmo", "RevolverAmmo", "HuntingRifleAmmo", "FlareAmmo", "SMGAmmo" };
        static readonly string[] DataPaths = {
            "Assets/AssetStore/HorrorEngine/ExampleContent/Items/GunAmmo/GunAmmo.asset",
            "Assets/AssetStore/HorrorEngine/ExampleContent/Items/ShotgunAmmo/ShotgunAmmo.asset",
            Root+"Data/RevolverAmmo.asset", Root+"Data/HuntingRifleAmmo.asset", Root+"Data/FlareAmmo.asset", Root+"Data/SMGAmmo.asset"
        };
        [Serializable] sealed class CaseReport
        {
            public string Ammo, ItemId, PrefabId, RuntimeId, IconPath, ExaminationPath, SnapshotJson;
            public float InventoryBefore, InventoryAfter, InventoryAfterCleanup;
            public bool NewCarton, SharedIcon, NewExaminationCarton, DatabaseLookup, SpawnTracked, SnapshotRestored, Deactivated;
        }
        [Serializable] sealed class Report
        {
            public string Status = "running", Error = "";
            public bool UsesDiskSaveApis = false;
            public int Assertions;
            public List<CaseReport> Cases = new List<CaseReport>();
        }
        static int stage, index, spawnFrame, pickupFrame;
        static double started;
        static PickupDropper dropper;
        static SpawnableSavableDatabase database;
        static AmmoData ammo;
        static PickupItem spawnedPickup;
        static SpawnableSavable spawned;
        static HashSet<string> idsBefore, runtimeIds;
        static Dictionary<InventoryEntry,float> countsBefore;
        static CaseReport current;
        static Report report;
        static string runtimeError;
        static Inventory Inv => GameManager.Instance.Inventory;
        static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName,"ArtSource/Weapons/AmmoPackaging/Preview/RuntimeDropPickupReport.json");
        static AmmoPackagingRuntimeValidation() { EditorApplication.playModeStateChanged += OnPlay; }

        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");
                SessionState.SetBool(SessionKey,true);
                EditorApplication.isPlaying=true;
            }
            catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(SessionKey,false))return;
            started=EditorApplication.timeSinceStartup;stage=index=0;runtimeError=null;
            report=new Report();runtimeIds=new HashSet<string>();
            Application.runInBackground=true;
            Application.logMessageReceived+=OnLog;
            EditorApplication.update+=Tick;
        }
        static void OnLog(string condition,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
                runtimeError=condition+"\n"+stack;
        }
        static void Check(bool condition,string message)
        {
            ++report.Assertions;
            if(!condition)throw new Exception((index<Names.Length?Names[index]+": ":"")+message);
        }
        static List<InventoryEntry> Stacks(AmmoData item)
        {
            var entries=new List<InventoryEntry>();Inv.GetAll(item,entries);return entries;
        }
        static float Count(AmmoData item) => Stacks(item).Sum(entry=>entry.Count);
        static bool HasNewCarton(GameObject model,string name)
        {
            if(!model)return false;
            string expected=Root+"Models/AmmoPackaging/"+name+"_Carton.asset";
            return model.GetComponentsInChildren<MeshFilter>(true).Any(filter=>filter.sharedMesh&&AssetDatabase.GetAssetPath(filter.sharedMesh)==expected);
        }
        static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>100)throw new Exception("Ammo drop/pickup runtime timeout; stage="+stage+" index="+index);
                if(!string.IsNullOrEmpty(runtimeError))throw new Exception("Runtime error during ammo validation: "+runtimeError);
                if(stage==0)
                {
                    if(!GameManager.Exists||!GameManager.Instance.Player||Time.timeSinceLevelLoad<3)return;
                    dropper=GameManager.Instance.Player.GetComponentInChildren<PickupDropper>(true);
                    database=GameManager.Instance.GetDatabase<SpawnableSavableDatabase>();
                    Check(dropper,"Player has no PickupDropper");Check(database,"GameManager has no SpawnableSavable database");
                    database.HashRegisters();stage=1;
                }
                else if(stage==1)
                {
                    string name=Names[index];ammo=AssetDatabase.LoadAssetAtPath<AmmoData>(DataPaths[index]);
                    Check(ammo,"Ammo data missing");Check(ammo.DropPrefab,"Ammo DropPrefab missing");
                    string expectedPrefab=Root+"Prefabs/"+name+"_Drop.prefab";
                    Check(AssetDatabase.GetAssetPath(ammo.DropPrefab)==expectedPrefab,"DropPrefab points to the wrong asset");
                    var prefabId=ammo.DropPrefab.GetComponent<ObjectUniqueId>();Check(prefabId&&!string.IsNullOrEmpty(prefabId.Id),"Registered drop prefab ID is empty");
                    current=new CaseReport{Ammo=name,ItemId=ammo.UniqueId,PrefabId=prefabId.Id};report.Cases.Add(current);
                    string expectedIcon=name=="FlareAmmo"?Root+"Models/FlareAmmo/FlareAmmo_Icon.png":Root+"Models/AmmoSides/"+name+"_PickupIcon.png";
                    current.IconPath=AssetDatabase.GetAssetPath(ammo.Image);
                    current.SharedIcon=ammo.Image&&current.IconPath==expectedIcon;
                    Check(current.SharedIcon,"Ammo data does not use its shared updated pickup/inventory sprite");
                    current.ExaminationPath=AssetDatabase.GetAssetPath(ammo.ExamineModel);
                    current.NewExaminationCarton=HasNewCarton(ammo.ExamineModel,name);
                    Check(current.NewExaminationCarton,"Examination does not use the new carton geometry");
                    current.DatabaseLookup=database.GetPrefab(prefabId.Id)==ammo.DropPrefab.gameObject;
                    Check(current.DatabaseLookup,"Runtime database cannot resolve the registered drop prefab");
                    Check(Inv.CanAdd(ammo,3),"Test inventory has insufficient capacity for three rounds");
                    current.InventoryBefore=Count(ammo);
                    countsBefore=Stacks(ammo).ToDictionary(entry=>entry,entry=>entry.Count);
                    idsBefore=new HashSet<string>(Object.FindObjectsByType<SpawnableSavable>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                        .Select(value=>value.GetComponent<ObjectUniqueId>()).Where(id=>id).Select(id=>id.Id));
                    // Use the actual player drop path. Runtime IDs/registration are
                    // generated by PickupDropper, not assigned by this test.
                    dropper.Drop(new InventoryEntry{Item=ammo,Count=3,SecondaryCount=0,Status=0});
                    spawnFrame=Time.frameCount;stage=2;
                }
                else if(stage==2&&Time.frameCount>spawnFrame+1)
                {
                    var matches=Object.FindObjectsByType<SpawnableSavable>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                        .Where(value=>value.gameObject.activeInHierarchy&&value.PrefabId==current.PrefabId)
                        .Where(value=>{var id=value.GetComponent<ObjectUniqueId>();return id&&!idsBefore.Contains(id.Id);}).ToArray();
                    Check(matches.Length==1,"Actual player drop did not create exactly one new matching active spawned carton");
                    spawned=matches[0];spawnedPickup=spawned.GetComponent<PickupItem>();
                    Check(spawnedPickup,"Spawned carton lacks PickupItem");
                    current.RuntimeId=spawned.GetComponent<ObjectUniqueId>().Id;
                    Check(!string.IsNullOrEmpty(current.RuntimeId)&&current.RuntimeId!=current.PrefabId&&runtimeIds.Add(current.RuntimeId),"Spawn instance reused a prefab/previous runtime ID");
                    current.NewCarton=HasNewCarton(spawned.gameObject,Names[index]);Check(current.NewCarton,"Runtime dropped object does not render the revised carton");
                    foreach(var renderer in spawned.GetComponentsInChildren<MeshRenderer>(true))
                        Check(renderer.GetComponent<MeshFilter>()&&renderer.GetComponent<MeshFilter>().sharedMesh&&renderer.sharedMaterials.All(material=>material&&material.shader&&material.shader.name!="Hidden/InternalErrorShader"),"Runtime dropped carton has a missing mesh/material");
                    current.SpawnTracked=ObjectStateManager.Instance.HasSpawnedObject(current.RuntimeId);Check(current.SpawnTracked,"Dropped object was not registered with ObjectStateManager");
                    Check(spawnedPickup.Entry.Item==ammo&&Mathf.Approximately(spawnedPickup.Entry.Count,3),"Dropper failed to copy ammo/count into actual pickup");
                    current.SnapshotJson=spawnedPickup.GetSavableData();
                    var alternate=AssetDatabase.LoadAssetAtPath<AmmoData>(DataPaths[(index+1)%Names.Length]);
                    spawnedPickup.Entry.Item=alternate;spawnedPickup.Entry.Count=17;spawnedPickup.Entry.SecondaryCount=11;spawnedPickup.Entry.Status=.5f;
                    spawnedPickup.SetFromSavedData(current.SnapshotJson);
                    current.SnapshotRestored=spawnedPickup.Entry.Item==ammo&&Mathf.Approximately(spawnedPickup.Entry.Count,3)&&Mathf.Approximately(spawnedPickup.Entry.SecondaryCount,0)&&Mathf.Approximately(spawnedPickup.Entry.Status,0);
                    Check(current.SnapshotRestored,"Pickup in-memory save-data roundtrip did not restore item/count/status");
                    spawnedPickup.ShowItemSplash=false;
                    spawnedPickup.Take();pickupFrame=Time.frameCount;stage=3;
                }
                else if(stage==3&&Time.frameCount>pickupFrame)
                {
                    current.InventoryAfter=Count(ammo);current.Deactivated=!spawnedPickup.gameObject.activeSelf;
                    Check(Mathf.Approximately(current.InventoryAfter,current.InventoryBefore+3),"Taking the actual dropped carton did not add exactly three rounds");
                    Check(current.Deactivated,"Taken dropped carton remained active");
                    float remove=3;
                    foreach(var entry in Stacks(ammo))
                    {
                        float before=countsBefore.TryGetValue(entry,out float existing)?existing:0;
                        float added=Mathf.Max(0,entry.Count-before),amount=Mathf.Min(remove,added);
                        if(amount>0){Inv.Remove(entry,amount);remove-=amount;}
                    }
                    current.InventoryAfterCleanup=Count(ammo);
                    Check(Mathf.Approximately(remove,0)&&Mathf.Approximately(current.InventoryAfterCleanup,current.InventoryBefore),"Test cleanup did not remove only the three newly picked-up rounds");
                    Debug.Log("AMMO_PACKAGING_RUNTIME_CASE_OK "+Names[index]+" real drop/pickup, unique runtime ID, DB lookup, tracked spawn and in-memory save-data roundtrip");
                    if(++index==Names.Length){report.Status="passed";Debug.Log("ALL_AMMO_PACKAGING_RUNTIME_CHECKS_OK assertions="+report.Assertions);Finish(0);}
                    else stage=1;
                }
            }
            catch(Exception error)
            {
                report.Status="failed";report.Error=error.ToString();Debug.LogException(error);Finish(1);
            }
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            SessionState.SetBool(SessionKey,false);
            try{Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllText(Output,JsonUtility.ToJson(report,true));}
            catch(Exception error){Debug.LogException(error);code=1;}
            EditorApplication.Exit(code);
        }
    }
}