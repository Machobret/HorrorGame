using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    public static class SmallRoomCollisionTools
    {
        [MenuItem("Horror Game/Environment/Audit Nearby Prop Collisions")]
        public static void Audit()
        {
            if (!Selection.activeGameObject) throw new InvalidOperationException("Select a prop in the room first.");
            Vector3 origin=Selection.activeGameObject.transform.position;
            var report=new StringBuilder(); report.AppendLine("Center: "+origin);
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f=>f.gameObject.scene==Selection.activeGameObject.scene && Vector3.Distance(f.transform.position,origin)<7)
                .OrderBy(f=>Vector3.Distance(f.transform.position,origin)))
            {
                var renderer=filter.GetComponent<Renderer>();if(!renderer)continue;
                string prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject);
                var colliders=filter.GetComponentsInParent<Collider>().Concat(filter.GetComponentsInChildren<Collider>()).Distinct().ToArray();
                report.AppendLine($"{PathOf(filter.transform)} | center={renderer.bounds.center} size={renderer.bounds.size} | solid={colliders.Count(c=>c.enabled&&!c.isTrigger)} trigger={colliders.Count(c=>c.isTrigger)} | prefab={prefab}");
            }
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/Collision");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"NearbyProps.txt"),report.ToString());
            Debug.Log("PROP_COLLISION_AUDIT_OK: "+folder);
        }

        [MenuItem("Horror Game/Environment/Repair Nearby Prop Collisions")]
        public static void Repair()
        {
            if(EditorApplication.isPlaying || !Selection.activeGameObject)
                throw new InvalidOperationException("Exit Play mode and select a prop in the room first.");
            var selected=Selection.activeGameObject;
            var scene=selected.scene;
            Vector3 origin=selected.transform.position;
            bool Nearby(MeshFilter f)=>f.gameObject.scene==scene && Vector3.Distance(f.transform.position,origin)<7;
            bool Owned(string p)=>p.StartsWith("Assets/Game/Environment/",StringComparison.Ordinal)||p.StartsWith("Assets/Game/Items/",StringComparison.Ordinal);
            var missingPaths=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f=>Nearby(f)&&f.sharedMesh&&f.GetComponent<Renderer>()&&!HasSolid(f))
                .Select(f=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(f.gameObject))
                .Where(p=>!string.IsNullOrEmpty(p)&&Owned(p)).Distinct().ToArray();
            // These boxes have both directly placed FBXs and separately authored reusable visual prefabs.
            var visualPaths=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(Nearby)
                .Select(f=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(f.gameObject))
                .Where(p=>Owned(p)&&p.EndsWith("_Model.fbx",StringComparison.OrdinalIgnoreCase))
                .Select(p=>p.Substring(0,p.Length-"_Model.fbx".Length)+"_Visual.prefab")
                .Where(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p));
            var pickupPaths=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(Nearby)
                .Select(f=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(f.gameObject))
                .Where(p=>p.EndsWith("/CigarettePack_Pickup.prefab",StringComparison.Ordinal));
            var paths=missingPaths.Concat(visualPaths).Concat(pickupPaths).Distinct().ToArray();
            int assets=0,added=0;
            foreach(string path in paths)
            {
                if(path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))
                {
                    var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                    if(importer && !importer.addCollider) {importer.addCollider=true;importer.SaveAndReimport();assets++;}
                }
                else if(path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                {
                    var contents=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int count=0;
                        foreach(var filter in contents.GetComponentsInChildren<MeshFilter>(true))
                            if(path.EndsWith("/CigarettePack_Pickup.prefab",StringComparison.Ordinal)&&filter.sharedMesh&&filter.GetComponent<MeshCollider>())
                            {
                                // The tiny imported local mesh can be discarded by PhysX triangle cleaning;
                                // a solid box uses its exact local bounds without mesh cooking.
                                Object.DestroyImmediate(filter.GetComponent<MeshCollider>());
                                var box=filter.gameObject.AddComponent<BoxCollider>();
                                box.center=filter.sharedMesh.bounds.center;box.size=filter.sharedMesh.bounds.size;count++;
                            }
                            else if(filter.sharedMesh&&filter.GetComponent<Renderer>()&&!HasSolid(filter))
                            { Configure(filter.gameObject.AddComponent<MeshCollider>(),filter); count++; }
                        if(count>0){PrefabUtility.SaveAsPrefabAsset(contents,path);assets++;added+=count;}
                    }
                    finally{PrefabUtility.UnloadPrefabContents(contents);}
                }
            }
            // Refresh live instances after imports; repair any instance whose inherited collider was removed.
            int instances=0;
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(Nearby))
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject);
                if(!filter.sharedMesh||!filter.GetComponent<Renderer>()||!Owned(path)||HasSolid(filter))continue;
                var collider=Undo.AddComponent<MeshCollider>(filter.gameObject);Configure(collider,filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);instances++;
            }
            if(instances>0)EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();Physics.SyncTransforms();
            Selection.activeGameObject=selected;Audit();
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/Collision");
            File.WriteAllText(Path.Combine(folder,"RepairSummary.txt"),$"Updated {assets} prefab/model assets; added {added} prefab colliders and {instances} scene instance colliders.\nAssets:\n"+string.Join("\n",paths));
            Debug.Log($"PROP_COLLISION_REPAIR_OK: {assets} prefab/model assets; {instances} extra instance colliders. Open scene not saved.");
        }
        [MenuItem("Horror Game/Environment/Validate Nearby Prop Collisions")]
        public static void Validate()
        {
            if(!Selection.activeGameObject)throw new InvalidOperationException("Select a room prop first.");
            var selected=Selection.activeGameObject;Vector3 origin=selected.transform.position;
            var report=new StringBuilder();int checkedCount=0,failed=0;
            Physics.SyncTransforms();
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f=>f.gameObject.scene==selected.scene&&Vector3.Distance(f.transform.position,origin)<7))
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject);
                if(!(path.StartsWith("Assets/Game/Environment/",StringComparison.Ordinal)||path.StartsWith("Assets/Game/Items/",StringComparison.Ordinal))||!filter.GetComponent<Renderer>())continue;
                if(!HasSolid(filter)){failed++;report.AppendLine("FAIL missing solid: "+PathOf(filter.transform));continue;}
                var meshCollider=filter.GetComponents<Collider>().FirstOrDefault(c=>c.enabled&&!c.isTrigger);
                if(!meshCollider)continue;
                Bounds b=meshCollider.bounds;bool hit=false;
                float distance=b.extents.magnitude+1;
                foreach(var axis in new[] {Vector3.right,Vector3.up,Vector3.forward})
                    foreach(float u in new[] {-.9f,0,.9f})
                        foreach(float v in new[] {-.9f,0,.9f})
                        {
                            Vector3 offset=axis==Vector3.right?new Vector3(0,u*b.extents.y,v*b.extents.z)
                                :axis==Vector3.up?new Vector3(u*b.extents.x,0,v*b.extents.z)
                                :new Vector3(u*b.extents.x,v*b.extents.y,0);
                            foreach(float sign in new[] {-1f,1f})
                                if(meshCollider.Raycast(new Ray(b.center+offset+axis*distance*sign,-axis*sign),out _,distance*2))hit=true;
                        }
                checkedCount++;
                if(!hit)failed++;
                report.AppendLine((hit?"PASS raycast: ":"FAIL raycast: ")+PathOf(filter.transform));
            }
            report.AppendLine($"RESULT: {checkedCount} prop colliders raycast-tested, {failed} failures.");
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/Collision");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"PhysicsValidation.txt"),report.ToString());
            if(failed>0)throw new InvalidOperationException(report.ToString());
            Debug.Log($"PROP_COLLISION_VALIDATION_OK: {checkedCount} prop colliders hit; no nearby owned prop missing solid collision.");
        }
        static bool HasSolid(MeshFilter filter)=>filter.GetComponentsInParent<Collider>()
            .Any(c=>c.enabled&&!c.isTrigger) || filter.GetComponents<Collider>().Any(c=>c.enabled&&!c.isTrigger);
        static void Configure(MeshCollider collider,MeshFilter filter)
        {
            collider.sharedMesh=filter.sharedMesh;collider.isTrigger=false;collider.enabled=true;
            var body=filter.GetComponentInParent<Rigidbody>();collider.convex=body&&!body.isKinematic;
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    }
}
