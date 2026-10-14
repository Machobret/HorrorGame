using System;
using System.Collections.Generic;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    // A separate empty scene keeps projectile regression checks away from the user's level.
    public static class WeaponPlayValidation
    {
        const string Root = "Assets/Game/Weapons/";
        static double started, pauseAt;
        static int stage;
        static SignalFlare burn, blocked, harmless;
        static SignalFlare airborne;
        static Health target, behindWall, noDamageTarget, shooterHealth;
        static Health rifleTarget, firedFlareTarget;
        static Vector3 pausedPosition;
        static List<string> errors = new List<string>();
        const string SessionKey = "HorrorGame.WeaponValidation";
        static bool running;
        static bool captured;
        [InitializeOnLoadMethod]
        static void ResumeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlay;
            Application.logMessageReceived += OnLog;
            if (EditorApplication.isPlaying) EditorApplication.delayCall += () => OnPlay(PlayModeStateChange.EnteredPlayMode);
        }
        public static void RunBatch()
        {
            errors.Clear();
            WeaponAssetBuilder.Validate();
            CheckReload();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.playModeStateChanged += OnPlay;
            Application.logMessageReceived += OnLog;
            EditorApplication.delayCall += EditorApplication.EnterPlaymode;
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void CheckReload()
        {
            foreach (string name in new[] { "HuntingRifle", "FlareGun", "Revolver" })
            {
                var data = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(Root + "Data/" + name + ".asset");
                var inventory = new Inventory { Items = new List<InventoryEntry>() };
                var weapon = new InventoryEntry { Item = data, Count = 1, SecondaryCount = 0 };
                var ammo = new InventoryEntry { Item = data.AmmoItem, Count = 9 };
                inventory.Items.Add(weapon); inventory.Items.Add(ammo);
                inventory.ReloadWeapon(weapon, ammo);
                Check(weapon.SecondaryCount == data.MaxAmmo && ammo.Count == 9 - data.MaxAmmo, name + " reload count mismatch");
                var save = inventory.GetSavableData();
                Check(save.Items[0].ItemId == data.UniqueId && save.Items[0].SecondaryCount == data.MaxAmmo, name + " save data mismatch");
            }
            Debug.Log("WEAPONS_RELOAD_SAVE_CHECK_OK");
        }
        static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || running) return;
            running = true;
            try
            {
                new GameObject("Validation audio listener").AddComponent<AudioListener>();
                new GameObject("GameObjectPool").AddComponent<GameObjectPool>();
                var source = Target("Shooter", new Vector3(0,0,0), out shooterHealth);
                var owner = source.GetComponent<Combatant>();
                Target("Burn target", new Vector3(0,0,3), out target);
                Target("Behind wall", new Vector3(3,0,4), out behindWall);
                Target("Damage disabled", new Vector3(6,0,3), out noDamageTarget);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name="Blocking wall";wall.transform.position=new Vector3(3,0,2);wall.transform.localScale=new Vector3(2,3,.2f);
                Physics.SyncTransforms();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Effects/SignalFlare.prefab").GetComponent<SignalFlare>();
                airborne = Object.Instantiate(prefab,new Vector3(-4,3,0),Quaternion.identity);
                airborne.Launch(Vector3.forward*11,owner);
                Check(airborne.GetComponentsInChildren<ParticleSystem>().Length >= 2,"Flare halo/sparks missing");
                burn = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity); burn.Gravity=0;burn.Launch(Vector3.forward*18,owner);
                Check(burn.Owner==owner,"Projectile lost shooter attribution");
                blocked = Object.Instantiate(prefab,new Vector3(3,0,0),Quaternion.identity);blocked.Gravity=0;blocked.Launch(Vector3.forward*18,owner);
                harmless = Object.Instantiate(prefab,new Vector3(6,0,0),Quaternion.identity);harmless.Gravity=0;harmless.EnableDamage=false;harmless.Launch(Vector3.forward*18,owner);
                CheckWeaponFire("HuntingRifle",10,out rifleTarget);
                CheckWeaponFire("FlareGun",13,out firedFlareTarget);
                CheckWeaponFire("Revolver",16,out var revolverTarget);
                Check(Mathf.Abs(revolverTarget.Value-6)<.001f,"Revolver damage incorrect");
                var charge = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Revolver.prefab").GetComponent<AttackCharge>();
                Check(!charge,"Revolver should fire on a tap without a charge component");
                Check(prefab.Gravity > 9 && prefab.GetComponentInChildren<Light>().intensity >= 12,"Flare gravity/light configuration incorrect");
                stage=0;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
            }
            catch(Exception exception) { Finish(exception); }
        }
        static void CheckWeaponFire(string name,float x,out Health health)
        {
            var source=new GameObject(name+" owner");source.transform.position=new Vector3(x,0,0);source.AddComponent<Combatant>();
            Target(name+" target",new Vector3(x,0,3),out health);
            var data=AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(Root+"Data/"+name+".asset");
            var weapon=Object.Instantiate(data.EquipPrefab,source.transform);weapon.transform.localPosition=Vector3.zero;weapon.transform.localScale=Vector3.one;
            var muzzle=Array.Find(weapon.GetComponentsInChildren<Socket>(),s=>s.Handle.name.Contains("Muzzle"));
            if(!muzzle)throw new Exception("No muzzle socket on "+name);
            weapon.transform.rotation=Quaternion.FromToRotation(muzzle.transform.forward,Vector3.forward)*weapon.transform.rotation;
            weapon.transform.position+=new Vector3(0,-muzzle.transform.position.y,0);
            Physics.SyncTransforms();
            var attack=weapon.GetComponent<AttackBase>();attack.StartAttack();
            if(name=="HuntingRifle")Check(Mathf.Abs(health.Value-8)<.001f,"Rifle shot damage incorrect: "+health.Value);
        }
        static GameObject Target(string name,Vector3 position,out Health health)
        {
            var root=new GameObject(name);root.transform.position=position;root.layer=9;root.AddComponent<Combatant>();
            health=root.AddComponent<Health>();health.Max=health.Value=10;
            var collider=root.AddComponent<SphereCollider>();collider.radius=.5f;
            var damageable=root.AddComponent<Damageable>();damageable.Type=AssetDatabase.LoadAssetAtPath<DamageableType>("Assets/AssetStore/HorrorEngine/ExampleContent/Combat/Damageable_EnemyGeneric.asset");
            return root;
        }
        static void Tick()
        {
            try
            {
                if (!captured && Time.timeSinceLevelLoad > .25f && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    captured = true;
                    var cameraObject = new GameObject("Flare visual verification camera");
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.transform.position = new Vector3(-7,3,-1);
                    camera.transform.LookAt(airborne.transform.position);
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.028f,.03f);
                    var texture = new RenderTexture(512,512,24); camera.targetTexture = texture; camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = texture;
                    var image = new Texture2D(512,512,TextureFormat.RGBA32,false); image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
                    string path = "ArtSource/Weapons/Revolver/Flare_Flight_Unity.png";
                    File.WriteAllBytes(path,image.EncodeToPNG());
                    RenderTexture.active = previous; camera.targetTexture = null;
                    Object.Destroy(image); Object.Destroy(texture); Object.Destroy(cameraObject);
                    Debug.Log("FLARE_FLIGHT_CAPTURE_OK: " + path);
                }
                if(EditorApplication.timeSinceStartup-started>40)throw new Exception("Play validation timed out");
                if(stage==0&&Time.timeSinceLevelLoad>.8f)
                {
                    Check(airborne.transform.position.z > 1 && airborne.transform.position.y < 2,"Flare did not travel in a gravity arc");
                    Check(airborne.GetComponent<TrailRenderer>().positionCount > 1,"Flying flare has no visible trail");
                    Check(target.Value<10,"Flare did not hit target");Check(behindWall.Value==10,"Flare went through a wall");Check(shooterHealth.Value==10,"Flare hit its shooter");Check(noDamageTarget.Value==10,"Damage toggle ignored");
                    Check(firedFlareTarget.Value<10,"Flare gun failed to launch a damaging projectile");
                    Check(burn.transform.parent&&blocked.transform.parent,"Flares failed to stick on impact");
                    pausedPosition=burn.transform.position;pauseAt=EditorApplication.timeSinceStartup;Time.timeScale=0;stage=1;
                }
                else if(stage==1&&EditorApplication.timeSinceStartup-pauseAt>.5f)
                {Check(burn&&burn.transform.position==pausedPosition,"Flare moved/expired during pause");Time.timeScale=1;stage=2;}
                else if(stage==2&&Time.timeSinceLevelLoad>7)
                {Check(target.IsDead,"Flare did not finish burning target: "+target.Value);Check(noDamageTarget.Value==10,"Disabled flare applied burn damage");stage=3;}
                else if(stage==3&&Time.timeSinceLevelLoad>12.8f)
                {Check(!burn&&!blocked&&!harmless,"Expired flares remain alive");Check(errors.Count==0,string.Join("\n",errors));Debug.Log("WEAPONS_PLAY_VALIDATION_OK: swept collision, wall blocking, shooter exclusion, burn amount/expiry, damage toggle, pause and lifetime");Finish(null);}
            }
            catch(Exception exception){Finish(exception);}
        }
        static void Finish(Exception exception)
        {
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;Application.logMessageReceived-=OnLog;Time.timeScale=1;
            SessionState.SetBool(SessionKey, false);
            if(exception!=null)Debug.LogException(exception);
            EditorApplication.Exit(exception==null?0:1);
        }
    }
}
