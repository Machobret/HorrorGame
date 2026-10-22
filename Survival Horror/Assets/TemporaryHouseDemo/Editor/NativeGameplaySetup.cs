using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TemporaryHouseDemo.Editor
{
    // Editor-only assembly: all gameplay is supplied by the project's existing prefabs.
    public static class NativeGameplaySetup
    {
        private const string Prefabs = "Assets/AssetStore/HorrorEngine/Prefabs/";
        public static Type NativeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
            ?? throw new Exception("Native project type unavailable: " + name);

        private static GameObject Instance(string path, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new Exception("Native prefab missing: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            return instance;
        }

        public static void Add(GameObject house, Func<float,float,float,Vector3> map)
        {
            BakeNavigation(house);
            var spawn = Instance(Prefabs + "PlayerSpawnPoint.prefab", "House - Native Player Spawn");
            spawn.transform.SetPositionAndRotation(map(5,2,.04f), Quaternion.LookRotation(map(4,2,0)-map(5,2,0),Vector3.up));
            var point = spawn.GetComponent(NativeType("HorrorEngine.PlayerSpawnPoint"));
            var serialized = new SerializedObject(point);
            serialized.FindProperty("IsDefault").boolValue = true;
            serialized.FindProperty("SnapToNavMesh").boolValue = true;
            serialized.FindProperty("SpawnId").stringValue = "temporary-house-demo";
            serialized.FindProperty("Character").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/AssetStore/HorrorEngine/ExampleContent/Character/CharacterPlayer.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Instance(Prefabs + "HECore.prefab", "HECore - Native Game Services");
            CameraVolume("Kitchen",0,4,0,4,new Vector3(.45f,3.55f,3.4f),new Vector3(2f,1.7f,.7f),map);
            CameraVolume("Bedroom",0,4,4,8,new Vector3(.4f,4.35f,3.3f),new Vector3(2.1f,6.25f,.7f),map);
            CameraVolume("Corridor South",4,6,0,4,new Vector3(5.75f,.3f,3.6f),new Vector3(4.9f,3.1f,.7f),map);
            CameraVolume("Corridor North",4,6,4,8,new Vector3(5.75f,4.1f,3.6f),new Vector3(4.9f,7.0f,.7f),map);
            var enemy = Instance("Assets/Game/Enemies/Monster_Runner.prefab", "House - Native Runner Enemy");
            var requested = map(5,6.8f,.05f);
            if (!NavMesh.SamplePosition(requested,out var hit,.6f,NavMesh.AllAreas))
                throw new Exception("Enemy spawn is off the demo NavMesh");
            enemy.transform.SetPositionAndRotation(hit.position,Quaternion.LookRotation(map(5,2,0)-map(5,6.8f,0),Vector3.up));
        }

        private static void CameraVolume(string name,float minX,float maxX,float minY,float maxY,
            Vector3 position,Vector3 target,Func<float,float,float,Vector3> map)
        {
            var camera = Instance(Prefabs+"FixedCameraWithShape.prefab","House Camera - "+name);
            camera.transform.SetPositionAndRotation(map((minX+maxX)/2,(minY+maxY)/2,-.05f),Quaternion.identity);
            var legacy = camera.GetComponentsInChildren<Component>(true).SingleOrDefault(c => c && c.GetType().Name == "CinemachineVirtualCamera");
            GameObject cameraObject = legacy ? legacy.gameObject : null;
            // The source prefab is CM2; override only this scene instance's obsolete component.
            // Keep the prefab connection, native POV, trigger, follow settings and shared asset intact.
            if (legacy) Object.DestroyImmediate(legacy);
            var virtualCamera = camera.GetComponentsInChildren<Component>(true).SingleOrDefault(c => c && c.GetType().Name == "CinemachineCamera");
            if (!virtualCamera) virtualCamera = cameraObject.AddComponent(NativeType("Unity.Cinemachine.CinemachineCamera"));
            virtualCamera.transform.position = map(position.x,position.y,position.z);
            virtualCamera.transform.rotation = Quaternion.LookRotation(map(target.x,target.y,target.z)-virtualCamera.transform.position,Vector3.up);
            var lens = new SerializedObject(virtualCamera);
            lens.FindProperty("Lens.FieldOfView").floatValue = name.StartsWith("Corridor") ? 62 : 68;
            lens.FindProperty("Lens.NearClipPlane").floatValue = .05f;
            lens.ApplyModifiedPropertiesWithoutUndo();
            var povSettings = new SerializedObject(camera.GetComponent(NativeType("HorrorEngine.CameraPOV")));
            povSettings.FindProperty("PositionFollow.Enabled").boolValue = false;
            povSettings.FindProperty("RotationFollow.Enabled").boolValue = false;
            povSettings.ApplyModifiedPropertiesWithoutUndo();
            var shape = camera.GetComponentInChildren(NativeType("HorrorEngine.Shape"),true);
            // Author the native shape volume as a room rectangle. Keep its trigger layer and camera settings.
            shape.transform.localPosition = Vector3.zero;
            shape.transform.localRotation = Quaternion.identity;
            shape.transform.localScale = Vector3.one;
            var shapeData = new SerializedObject(shape);
            var points = shapeData.FindProperty("Points");
            points.arraySize = 4;
            var corners = new[] { map(minX,minY,-.05f),map(maxX,minY,-.05f),map(maxX,maxY,-.05f),map(minX,maxY,-.05f) };
            for (int i=0;i<4;i++) points.GetArrayElementAtIndex(i).vector3Value = shape.transform.InverseTransformPoint(corners[i]);
            shapeData.ApplyModifiedPropertiesWithoutUndo();
            var shapeCollider = shape.GetComponent(NativeType("HorrorEngine.ShapeCollider"));
            var colliderData = new SerializedObject(shapeCollider);
            colliderData.FindProperty("m_Height").floatValue = 3.9f;
            colliderData.ApplyModifiedPropertiesWithoutUndo();
            shapeCollider.GetType().GetMethod("UpdateCollider").Invoke(shapeCollider,null);
            var mesh = shape.GetComponent<MeshCollider>().sharedMesh;
            string meshPath = DemoSceneBuilder.Root+"/Navigation/CameraVolume_"+name.Replace(" ","")+".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing) { EditorUtility.CopySerialized(mesh,existing); shape.GetComponent<MeshCollider>().sharedMesh=existing; Object.DestroyImmediate(mesh); }
            else AssetDatabase.CreateAsset(mesh,meshPath);
            PrefabUtility.RecordPrefabInstancePropertyModifications(shape.GetComponent<MeshCollider>());
        }

        private static void BakeNavigation(GameObject house)
        {
            System.IO.Directory.CreateDirectory(DemoSceneBuilder.Root+"/Navigation");
            AssetDatabase.Refresh();
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(house.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .3f;
            settings.agentHeight = 1.9f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .05f;
            var bounds = new Bounds(new Vector3(-3,2,-4),new Vector3(9,7,11));
            var data = NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if (!data || data.sourceBounds.size == Vector3.zero) throw new Exception("Demo NavMesh bake failed");
            string path = DemoSceneBuilder.Root+"/Navigation/HouseNavMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing) { AssetDatabase.DeleteAsset(path); }
            AssetDatabase.CreateAsset(data,path);
            var surfaceType = NativeType("Unity.AI.Navigation.NavMeshSurface");
            // The FBX root has a -90 degree import rotation; world-space bake data needs an identity transform.
            var surface = new GameObject("House Navigation").AddComponent(surfaceType);
            var serialized = new SerializedObject(surface);
            serialized.FindProperty("m_NavMeshData").objectReferenceValue = data;
            serialized.FindProperty("m_AgentTypeID").intValue = 0;
            serialized.FindProperty("m_CollectObjects").enumValueIndex = 2;
            serialized.FindProperty("m_UseGeometry").enumValueIndex = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            NavMesh.AddNavMeshData(data);
            if (!NavMesh.SamplePosition(new Vector3(-5,.04f,-2),out _,.5f,NavMesh.AllAreas))
                throw new Exception("Native player spawn has no walkable NavMesh");
        }
    }
}
