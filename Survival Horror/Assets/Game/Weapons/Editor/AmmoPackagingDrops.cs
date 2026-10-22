using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    /// <summary>Builds independent save-compatible ammunition drop prefabs after packaging models exist.</summary>
    public static class AmmoPackagingDrops
    {
        public const string DatabasePath = "Assets/AssetStore/HorrorEngine/ExampleContent/Databases/SpawnableSavableDB.asset";
        const string SourcePath = "Assets/AssetStore/HorrorEngine/ExampleContent/Pickups/PickupDroppedCase.prefab";
        const string Prefabs = "Assets/Game/Weapons/Prefabs/";
        const string Models = "Assets/Game/Weapons/Models/AmmoPackaging/";
        const string Examples = "Assets/AssetStore/HorrorEngine/ExampleContent/Items/";
        static readonly string[] Names = { "PistolAmmo", "ShotgunAmmo", "RevolverAmmo", "HuntingRifleAmmo", "FlareAmmo", "SMGAmmo" };
        static readonly string[] DataPaths = {
            Examples+"GunAmmo/GunAmmo.asset", Examples+"ShotgunAmmo/ShotgunAmmo.asset",
            "Assets/Game/Weapons/Data/RevolverAmmo.asset", "Assets/Game/Weapons/Data/HuntingRifleAmmo.asset", "Assets/Game/Weapons/Data/FlareAmmo.asset", "Assets/Game/Weapons/Data/SMGAmmo.asset"
        };
        [Serializable] sealed class DropRecord
        {
            public string Ammo, DataPath, DropPath, PrefabId;
            public Vector3 VisualSize;
        }
        [Serializable] sealed class DropReport { public List<DropRecord> Entries = new List<DropRecord>(); }

        [MenuItem("Horror Game/Weapons/Build Ammo Packaging Drop Prefabs")]
        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var database = AssetDatabase.LoadAssetAtPath<SpawnableSavableDatabase>(DatabasePath);
            if (!source || !database) throw new Exception("Missing generic drop template or SpawnableSavable database");
            if (!source.GetComponent<SpawnableSavable>() || !source.GetComponent<ObjectUniqueId>() || !source.GetComponent<PickupItem>())
                throw new Exception("Generic drop template lacks required gameplay/save components");
            // Preflight every input before creating any drop registration or changing ammo data.
            for (int i = 0; i < Names.Length; ++i)
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(Models+Names[i]+"_Model.prefab")) throw new Exception("Build packaging model first: "+Names[i]);
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+Names[i]+"_Pickup.prefab")) throw new Exception("Missing normal ammo pickup: "+Names[i]);
                if (!AssetDatabase.LoadAssetAtPath<AmmoData>(DataPaths[i])) throw new Exception("Missing ammo data: "+DataPaths[i]);
            }
            if (database.Entries == null) database.Entries = new List<SpawnableSavable>();
            var previousRegistrations = database.Entries.Where(s=>s).ToDictionary(s=>s,s=>s.GetComponent<ObjectUniqueId>().Id);
            var usedIds = ValidateDatabase(database);
            var report = new DropReport();
            for (int i = 0; i < Names.Length; ++i)
            {
                string name=Names[i], path=Prefabs+name+"_Drop.prefab";
                var ammo=AssetDatabase.LoadAssetAtPath<AmmoData>(DataPaths[i]);
                var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    Unpack(root);
                    root.name=name+"_Drop";
                    root.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
                    root.transform.localScale=Vector3.one;
                    root.SetActive(true);
                    var id=root.GetComponent<ObjectUniqueId>();
                    if (!id) throw new Exception("Missing drop ObjectUniqueId: "+name);
                    if (!existing)
                    {
                        // The generic template's prefab ID must never be copied into a new registered type.
                        do { id.RegenerateId(); } while (usedIds.Contains(id.Id));
                    }
                    else if (string.IsNullOrEmpty(id.Id)) throw new Exception("Existing registered drop has an empty ID: "+path);
                    id.IsUniqueInstance=false; id.IntentionallyDuplicated=false;
                    string stableId=id.Id;
                    usedIds.Add(stableId);
                    var pickup=root.GetComponent<PickupItem>();
                    if (!pickup) throw new Exception("Missing drop PickupItem: "+name);
                    // Actual dropped counts/status are copied by PickupDropper.Drop before use.
                    pickup.Entry=new InventoryEntry { Item=ammo, Count=1, SecondaryCount=0, Status=0 };
                    pickup.ShowItemSplash=true;
                    ReplaceRenderedChildren(root,name);
                    Bounds visualBounds=MeshBounds(root);
                    // Keep the generic drop physics/interaction policy unchanged.
                    var spawnable=root.GetComponent<SpawnableSavable>();
                    if (!spawnable || !root.GetComponent<SavableObjectState>() || !root.GetComponent<PooledGameObject>())
                        throw new Exception("Drop template lost its pooling/save components: "+name);
                    if (stableId!=id.Id) throw new Exception("Drop prefab ID changed while replacing geometry: "+name);
                    ValidateVisuals(root,name);
                    var saved=PrefabUtility.SaveAsPrefabAsset(root,path);
                    if (!saved) throw new Exception("Could not save ammo drop prefab: "+path);
                    var savedSpawnable=saved.GetComponent<SpawnableSavable>();
                    if (!database.Entries.Contains(savedSpawnable)) database.Entries.Add(savedSpawnable);
                    ammo.DropPrefab=savedSpawnable;
                    string icon=name=="FlareAmmo"?"Assets/Game/Weapons/Models/FlareAmmo/FlareAmmo_Icon.png":"Assets/Game/Weapons/Models/AmmoSides/"+name+"_PickupIcon.png";
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(icon);
                    if (!sprite) throw new Exception("Missing shared inventory/pickup icon: "+icon);
                    ammo.Image=sprite;
                    EditorUtility.SetDirty(ammo);
                    report.Entries.Add(new DropRecord { Ammo=name, DataPath=DataPaths[i], DropPath=path, PrefabId=stableId, VisualSize=visualBounds.size });
                    Debug.Log("AMMO_PACKAGING_DROP_OK "+name+" prefabId="+stableId+" size="+visualBounds.size);
                }
                finally { if(existing)PrefabUtility.UnloadPrefabContents(root);else Object.DestroyImmediate(root); }
            }
            foreach (var registration in previousRegistrations)
                if (!database.Entries.Contains(registration.Key) || registration.Key.GetComponent<ObjectUniqueId>().Id!=registration.Value)
                    throw new Exception("Existing spawnable registration/ID was changed: "+registration.Key.name);
            ValidateDatabase(database);
            database.HashRegisters();
            for (int i=0;i<Names.Length;++i)
            {
                var ammo=AssetDatabase.LoadAssetAtPath<AmmoData>(DataPaths[i]);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+Names[i]+"_Drop.prefab");
                if (ammo.DropPrefab!=prefab.GetComponent<SpawnableSavable>()) throw new Exception("Ammo points to the wrong drop prefab: "+Names[i]);
                string id=prefab.GetComponent<ObjectUniqueId>().Id;
                if (database.GetPrefab(id)!=prefab) throw new Exception("Spawnable database cannot resolve ammo drop: "+Names[i]);
                ValidateSpawnIdentity(prefab);
            }
            EditorUtility.SetDirty(database); AssetDatabase.SaveAssets();
            string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"ArtSource/Weapons/AmmoPackaging/Preview");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"DropPresentationReport.json"),JsonUtility.ToJson(report,true));
            Debug.Log("AMMO_PACKAGING_DROPS_OK six distinct stable prefab IDs, shared icons, preserved existing registrations and runtime spawn identity checks");
        }

        static HashSet<string> ValidateDatabase(SpawnableSavableDatabase database)
        {
            var ids=new HashSet<string>();
            foreach(var entry in database.Entries)
            {
                if(!entry)continue;
                var id=entry.GetComponent<ObjectUniqueId>();
                if(!id||string.IsNullOrEmpty(id.Id)||!ids.Add(id.Id))throw new Exception("Invalid/duplicate SpawnableSavable registration: "+entry.name);
            }
            return ids;
        }
        static void Unpack(GameObject root)
        {
            var instances=root.GetComponentsInChildren<Transform>(true).Select(t=>PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject)).Where(g=>g).Distinct().ToArray();
            foreach(var instance in instances)if(instance&&PrefabUtility.IsPartOfPrefabInstance(instance))PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        }
        static void ReplaceRenderedChildren(GameObject root,string name)
        {
            // Keep the generic interaction child and its persistent Take callback.
            // Entire obsolete visual/shadow branches are removed so their scripts
            // cannot continue referencing the old drop-case renderer.
            foreach(var child in root.transform.Cast<Transform>().ToArray())
            {
                if(child.GetComponentInChildren<Renderer>(true)&&!child.GetComponentInChildren<Interactive>(true)&&!child.GetComponentInChildren<PickupItem>(true))
                    Object.DestroyImmediate(child.gameObject);
            }
            foreach(var renderer in root.GetComponents<Renderer>())Object.DestroyImmediate(renderer);
            foreach(var filter in root.GetComponents<MeshFilter>())Object.DestroyImmediate(filter);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Models+name+"_Model.prefab");
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);visual.name="AmmoPackagingVisual";
            Bounds modelBounds=MeshBounds(visual);
            var normal=PrefabUtility.LoadPrefabContents(Prefabs+name+"_Pickup.prefab");
            Bounds normalBounds;
            try{normalBounds=MeshBounds(normal);}finally{PrefabUtility.UnloadPrefabContents(normal);}
            float normalSize=Mathf.Max(normalBounds.size.x,normalBounds.size.y,normalBounds.size.z);
            float modelSize=Mathf.Max(modelBounds.size.x,modelBounds.size.y,modelBounds.size.z);
            if(normalSize<.001f||modelSize<.001f)throw new Exception("Invalid drop/normal ammo bounds: "+name);
            float scale=normalSize/modelSize;visual.transform.localScale=Vector3.one*scale;
            visual.transform.localPosition=new Vector3(-modelBounds.center.x*scale,-modelBounds.min.y*scale,-modelBounds.center.z*scale);
            foreach(var child in visual.GetComponentsInChildren<Transform>(true))child.gameObject.layer=root.layer;
        }
        static Bounds MeshBounds(GameObject root)
        {
            bool first=true;var result=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!filter.sharedMesh||!renderer||!renderer.enabled)continue;
                Bounds bounds=filter.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    Vector3 point=root.transform.InverseTransformPoint(filter.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
            }
            if(first)throw new Exception("No drop model mesh bounds: "+root.name);return result;
        }
        static void ValidateVisuals(GameObject root,string name)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0)throw new Exception("No visible ammo drop model: "+name);
            foreach(var renderer in renderers)
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(!filter||!filter.sharedMesh||renderer.sharedMaterials.Any(m=>!m||!m.shader||m.shader.name=="Hidden/InternalErrorShader"))throw new Exception("Missing drop mesh/material: "+name);
            }
            if(!root.GetComponentInChildren<Interactive>(true)||!root.GetComponentsInChildren<Collider>(true).Any(c=>c.isTrigger))throw new Exception("Missing drop interaction trigger: "+name);
            var interactive=root.GetComponentInChildren<Interactive>(true);
            var pickup=root.GetComponent<PickupItem>();bool takesThisPickup=false;
            if(interactive.OnInteract!=null)for(int i=0;i<interactive.OnInteract.GetPersistentEventCount();++i)
                takesThisPickup |= interactive.OnInteract.GetPersistentTarget(i)==pickup && interactive.OnInteract.GetPersistentMethodName(i)=="Take";
            if(!takesThisPickup)throw new Exception("Drop interaction callback lost its PickupItem.Take target: "+name);
            var bounds=MeshBounds(root);if(bounds.size.sqrMagnitude<.00001f)throw new Exception("Drop visual too small: "+name);
        }
        static void ValidateSpawnIdentity(GameObject prefab)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                string prefabId=prefab.GetComponent<ObjectUniqueId>().Id;
                var id=instance.GetComponent<ObjectUniqueId>();id.RegenerateId();
                if(string.IsNullOrEmpty(id.Id)||id.Id==prefabId)throw new Exception("Spawned drop instance reused its registered prefab ID: "+prefab.name);
                var spawnable=instance.GetComponent<SpawnableSavable>();spawnable.SetPrefab(prefab);
                if(spawnable.PrefabId!=prefabId)throw new Exception("Spawned drop lost its persistent prefab lookup: "+prefab.name);
            }
            finally{Object.DestroyImmediate(instance);}
        }
        public static void RunBatch()
        {
            try{Apply();EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}