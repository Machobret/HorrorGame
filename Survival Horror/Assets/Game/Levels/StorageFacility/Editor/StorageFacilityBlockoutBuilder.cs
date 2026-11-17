using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace HorrorGame.StorageFacility.Editor
{
    public static class StorageFacilityBlockoutBuilder
    {
        public const string Root="Assets/Game/Levels/StorageFacility/";
        public const string ScenePath=Root+"Scenes/StorageFacility_Blockout.unity";
        public struct Area
        {
            public string Name;public Rect Bounds;public float Height;
            public Area(string name,float x,float z,float width,float depth,float height=3.6f){Name=name;Bounds=new Rect(x,z,width,depth);Height=height;}
        }
        public static readonly Area[] Areas={
            new Area("Arrival yard",-16,-20,32,20,0),new Area("Reception lobby",-3,0,8,9),
            new Area("Office",5,0,8,7),new Area("Electrical closet",-7,5,4,4),
            new Area("Entrance dogleg",-3,9,15,4),new Area("Storage corridor",9,13,3,24),
            new Area("Unit A102 - maintenance",3,20,6,4),new Area("Unit B102 - feeder",12,20,6,4),
            new Area("Service bay",7,37,9,9,4),new Area("Warehouse vestibule",7,46,9,5,4),
            new Area("High-bay warehouse",-20,44,27,23,7.5f)
        };
        static Dictionary<string,Material> materials;
        static Transform architecture,fixtures,lighting,cameras,gameplay;
        static Material concrete,wall,steel,teal,blue,orange,carton,white,black,yellow;

        [MenuItem("Horror Game/Storage Facility/Create Blockout Scene")]
        public static void Create()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);return;}
            Generate();
        }
        public static void BuildBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use the Create menu in the editor.");
            try{Generate();Debug.Log("STORAGE_FACILITY_BUILD_OK");EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BuildForValidation()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Validation generation requires a separate batch editor.");
            Generate();
        }
        static void Generate()
        {
            foreach(var directory in new[]{"Scenes","Assets"})Directory.CreateDirectory(Root+directory);
            AssetDatabase.Refresh();materials=new Dictionary<string,Material>();
            concrete=Mat("Concrete",new Color(.43f,.46f,.47f));wall=Mat("CorporateWhite",new Color(.76f,.78f,.77f));
            steel=Mat("Steel",new Color(.23f,.28f,.3f));teal=Mat("CorporateTeal",new Color(.08f,.35f,.38f));
            blue=Mat("UnitBlue",new Color(.19f,.36f,.51f));orange=Mat("SafetyOrange",new Color(.8f,.38f,.12f));
            carton=Mat("Cartons",new Color(.48f,.43f,.34f));white=Mat("Fluorescent",new Color(.83f,.91f,.89f),true);
            black=Mat("Screen",new Color(.035f,.06f,.07f));yellow=Mat("SafetyYellow",new Color(.85f,.72f,.24f));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            architecture=Group("01 Architecture");fixtures=Group("02 Units and furnishings");lighting=Group("03 Lighting");
            cameras=Group("04 Template camera volumes");gameplay=Group("05 Interactions and progression");
            BuildShell();BuildUnits();BuildYard();BuildOffice();BuildWarehouse();BuildLighting();BuildCameras();
            BuildProgression();BuildMap();
            var spawn=new GameObject("Arrival_PlayerSpawn").AddComponent<PlayerSpawnPoint>();
            spawn.transform.position=new Vector3(-2,0,-17);spawn.IsDefault=true;spawn.SnapToNavMesh=false;spawn.SpawnId="storage-arrival";
            spawn.Character=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/AssetStore/HorrorEngine/ExampleContent/Character/CharacterPlayer.asset");
            // The template core creates its normal managers and registered character. Never add separate copies.
            var core=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/HECore.prefab"));
            var coreData=new SerializedObject(core.GetComponent<HECore>());coreData.FindProperty("m_CorePrefabs").objectReferenceValue=CameraCoreProfile();coreData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(core.GetComponent<HECore>());
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.52f,.56f,.6f);
            RenderSettings.fog=false;
            var sun=new GameObject("Overcast daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.8f;
            sun.transform.rotation=Quaternion.Euler(50,-30,0);sun.shadows=LightShadows.Soft;
            foreach(var id in Object.FindObjectsByType<ObjectUniqueId>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!id.IsUniqueInstance&&!id.IntentionallyDuplicated)id.RegenerateId();
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
        }
        static Transform Group(string name,Transform parent=null)
        {var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);return go.transform;}
        static HECorePrefabs CameraCoreProfile()
        {
            // Native prototype inheritance overrides only this level's blend settings.
            var camera=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/CameraSystem.prefab"));
            GameObject variant;
            try
            {
                var brain=camera.GetComponentInChildren<CinemachineBrain>(true);
                brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);
                brain.CustomBlends=null;PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                variant=PrefabUtility.SaveAsPrefabAsset(camera,Root+"Assets/StorageFacility_CameraSystem.prefab");
            }
            finally{Object.DestroyImmediate(camera);}
            var profile=AssetDatabase.LoadAssetAtPath<HECorePrefabs>(Root+"Assets/StorageFacility_CoreProfile.asset");
            if(!profile){profile=ScriptableObject.CreateInstance<HECorePrefabs>();AssetDatabase.CreateAsset(profile,Root+"Assets/StorageFacility_CoreProfile.asset");}
            var data=new SerializedObject(profile);
            data.FindProperty("m_Prototype").objectReferenceValue=AssetDatabase.LoadAssetAtPath<HECorePrefabs>("Assets/AssetStore/HorrorEngine/Prefabs/HECorePrefabs.asset");
            data.FindProperty("m_CameraSystem").objectReferenceValue=variant;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(profile);
            return profile;
        }
        static Material Mat(string name,Color color,bool unlit=false)
        {
            if(materials.TryGetValue(name,out var existing))return existing;
            string path=Root+"Assets/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!value){value=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(value,path);}
            value.SetColor("_BaseColor",color);if(value.HasProperty("_Smoothness"))value.SetFloat("_Smoothness",.08f);
            EditorUtility.SetDirty(value);materials.Add(name,value);return value;
        }
        static GameObject Box(string name,Vector3 position,Vector3 size,Material material,Transform parent,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;return go;
        }
        static TMP_Text Sign(string text,Vector3 position,Vector3 normal,float size= .26f,Transform parent=null)
        {
            var go=new GameObject("Sign - "+text.Replace('\n',' '),typeof(TextMeshPro));go.transform.SetParent(parent?parent:fixtures,false);
            go.transform.position=position;go.transform.rotation=Quaternion.LookRotation(-normal,Vector3.up);
            var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.alignment=TextAlignmentOptions.Center;
            label.color=new Color(.89f,.94f,.93f);label.rectTransform.sizeDelta=new Vector2(8,2);
            var lines=text.Split('\n');float width=Mathf.Clamp(lines.Max(line=>line.Length)*size*.63f+.25f,.7f,7.9f);
            var plate=Box("Sign backing",position-normal*.035f,new Vector3(width,lines.Length*size*1.4f+.12f,.045f),teal,go.transform,false);
            plate.transform.rotation=go.transform.rotation;return label;
        }
        static void WallX(string name,float x,float z0,float z1,float height=3.6f)
        {Box(name,new Vector3(x,height*.5f,(z0+z1)*.5f),new Vector3(.2f,height,z1-z0),wall,architecture);}
        static void WallZ(string name,float z,float x0,float x1,float height=3.6f)
        {Box(name,new Vector3((x0+x1)*.5f,height*.5f,z),new Vector3(x1-x0,height,.2f),wall,architecture);}
        static void BuildShell()
        {
            var cells=new Dictionary<Vector2Int,Area>();
            foreach(var area in Areas)
            {
                var rect=area.Bounds;
                Box(area.Name+" floor",new Vector3(rect.center.x,-.1f,rect.center.y),new Vector3(rect.width,.2f,rect.height),concrete,architecture);
                if(area.Height>0)Box(area.Name+" ceiling",new Vector3(rect.center.x,area.Height+.08f,rect.center.y),new Vector3(rect.width,.16f,rect.height),wall,architecture);
                for(int x=(int)rect.xMin;x<rect.xMax;x++)for(int z=(int)rect.yMin;z<rect.yMax;z++)cells[new Vector2Int(x,z)]=area;
            }
            // Merge the perimeter of the public floor plan into straight wall runs.
            var edges=new Dictionary<(bool x,int coordinate,float height),List<int>>();
            foreach(var cell in cells)
            foreach(var dir in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
            {
                if(cells.ContainsKey(cell.Key+dir))continue;
                bool axis=dir.x!=0;int coordinate=axis?cell.Key.x+(dir.x>0?1:0):cell.Key.y+(dir.y>0?1:0);
                int along=axis?cell.Key.y:cell.Key.x;float height=cell.Value.Height;
                var key=(axis,coordinate,height);if(!edges.ContainsKey(key))edges[key]=new List<int>();edges[key].Add(along);
            }
            foreach(var pair in edges)
            {
                var values=pair.Value.Distinct().OrderBy(v=>v).ToArray();int first=values[0],last=first;
                for(int i=1;i<=values.Length;i++)
                {
                    if(i<values.Length&&values[i]==last+1){last=values[i];continue;}
                    if(pair.Key.height>0)
                    {if(pair.Key.x)WallX("Perimeter",pair.Key.coordinate,first,last+1,pair.Key.height);else WallZ("Perimeter",pair.Key.coordinate,first,last+1,pair.Key.height);}
                    else Fence(pair.Key.x,pair.Key.coordinate,first,last+1);
                    if(i<values.Length)first=last=values[i];
                }
            }
            // Controlled openings between adjacent floor rectangles.
            WallZ("Entrance facade",0,-3,-1.3f);WallZ("Entrance facade",0,1.3f,13);
            Box("Entrance lintel",new Vector3(0,3.2f,0),new Vector3(2.6f,.8f,.22f),wall,architecture);
            WallX("Office partition",5,0,2.2f);WallX("Office partition",5,4.2f,7);
            WallX("Electrical closet partition",-3,5,6);WallX("Electrical closet partition",-3,8,9);
            foreach(float x in new[]{9f,12f}){WallX("Open unit return",x,20,21);WallX("Open unit return",x,23,24);}
            WallZ("Service shutter return",45.5f,7,9);WallZ("Service shutter return",45.5f,12,16);
            Box("Service lintel",new Vector3(10.5f,3.65f,45.5f),new Vector3(3,.7f,.2f),wall,architecture);
            WallX("Warehouse fire wall",7,44,47,7.5f);WallX("Warehouse fire wall",7,50,67,7.5f);
            Box("Warehouse portal header",new Vector3(7,5.5f,48.5f),new Vector3(.2f,4,3),wall,architecture);
            Sign("NORTHLINE\nSELF STORAGE",new Vector3(0,3.05f,-.14f),Vector3.back,.32f);
            Sign("RECEPTION  >",new Vector3(2,2.1f,.2f),Vector3.back,.2f);
            Sign("STORAGE A / B   >",new Vector3(1,2.7f,12.82f),Vector3.back,.22f);
        }
        static void Fence(bool axis,float coordinate,float start,float end)
        {
            float length=end-start;Vector3 center=axis?new Vector3(coordinate,1.1f,(start+end)*.5f):new Vector3((start+end)*.5f,1.1f,coordinate);
            Box("Yard perimeter fence",center,axis?new Vector3(.1f,2.2f,length):new Vector3(length,2.2f,.1f),steel,architecture);
            // A solid gray placeholder denotes future mesh fencing; it has deliberate collision now.
        }
        static void BuildUnits()
        {
            for(int row=0;row<4;row++)foreach(bool left in new[]{true,false})
            {
                float z=16+row*6,x=left?6:15,face=left?9:12;
                var parent=Group((left?"A":"B")+(101+row),fixtures);
                if(row!=1)
                {
                    Box("Closed storage unit mass",new Vector3(x,1.8f,z),new Vector3(6,3.6f,4),wall,parent);
                    Box("Roll-up door",new Vector3(face+(left?.18f:-.18f),1.35f,z),new Vector3(.12f,2.7f,3),row%2==0?blue:teal,parent);
                    for(int rib=0;rib<11;rib++)Box("Door rib",new Vector3(face+(left?.245f:-.245f),.15f+rib*.24f,z),new Vector3(.025f,.025f,2.9f),steel,parent,false);
                }
                Sign((left?"A":"B")+(101+row),new Vector3(face+(left?.13f:-.13f),3,z),left?Vector3.right:Vector3.left,.24f,parent);
            }
            Cartons(new Vector3(4,.0f,22),3,fixtures);Box("Abandoned dolly",new Vector3(7,.6f,23),new Vector3(.65f,1.2f,.45f),steel,fixtures);
            Sign("MAINTENANCE\nNO PUBLIC ACCESS",new Vector3(8.85f,2,22),Vector3.right,.16f);
            Sign("FEEDER B\nSERVICE ACCESS",new Vector3(12.15f,2,22),Vector3.left,.17f);
        }
        static void BuildYard()
        {
            for(int i=0;i<3;i++)
            {
                float x=-13+i*4;
                Box("Exterior storage mass",new Vector3(x,1.75f,-1.8f),new Vector3(3.8f,3.5f,3.6f),wall,fixtures);
                Box("Orange exterior shutter",new Vector3(x,1.4f,-3.65f),new Vector3(3,2.8f,.12f),orange,fixtures);
                Sign("EXT "+(i+1),new Vector3(x,3.1f,-3.75f),Vector3.back,.18f);
            }
            // Painted vehicle bays and a delivery van silhouette establish scale without final art.
            for(int i=0;i<3;i++)Box("Parking stripe",new Vector3(6+i*3,.008f,-11),new Vector3(.07f,.012f,5),yellow,fixtures,false);
            Box("Abandoned delivery van",new Vector3(10,1.25f,-9),new Vector3(2.2f,2.5f,5),wall,fixtures);
            Box("Van windshield",new Vector3(10,1.8f,-11.53f),new Vector3(1.9f,.65f,.04f),black,fixtures,false);
            Box("Utility cabinet",new Vector3(-7.2f,1.05f,-7),new Vector3(.7f,2.1f,1.2f),steel,fixtures);
            Box("Tablet pedestal",new Vector3(-13.5f,.65f,-10),new Vector3(.6f,1.3f,.6f),teal,fixtures);
            Box("Tablet screen",new Vector3(-13.5f,1.34f,-10),new Vector3(.4f,.06f,.3f),black,fixtures);
            for(int i=0;i<3;i++)Box("Entrance bollard",new Vector3(-2.5f+i*2.5f,.5f,-2.3f),new Vector3(.2f,1,.2f),yellow,fixtures);
            Cartons(new Vector3(-10,0,-15),2,fixtures);
            Sign("UTILITY / CHECK-IN  <",new Vector3(-5,1.7f,-7),Vector3.back,.24f);
            Box("Utility wayfinding post",new Vector3(-5,.8f,-7),new Vector3(.12f,1.6f,.12f),steel,fixtures);
            Box("Utility bay curb",new Vector3(-11,.18f,-5.2f),new Vector3(10,.36f,.25f),steel,fixtures);
        }
        static void BuildOffice()
        {
            Box("Reception desk",new Vector3(9,.5f,5.2f),new Vector3(3,1,.8f),teal,fixtures);
            Box("Monitor",new Vector3(9,1.4f,5.35f),new Vector3(.65f,.45f,.12f),black,fixtures);
            Box("Desk phone",new Vector3(8,1.08f,5.2f),new Vector3(.3f,.12f,.22f),steel,fixtures);
            Box("Key cabinet",new Vector3(12.75f,1.5f,4.7f),new Vector3(.3f,1,1.1f),steel,fixtures);
            Box("Future save point plinth",new Vector3(6,.65f,6),new Vector3(.65f,1.3f,.65f),wall,fixtures);
            Sign("RECEPTION / SECURITY",new Vector3(9,2.8f,.15f),Vector3.forward,.22f);
            Sign("STAFF REST POINT",new Vector3(7,2.4f,6.85f),Vector3.back,.18f);
        }
        static void Cartons(Vector3 origin,int count,Transform parent)
        {
            Box("Pallet",origin+new Vector3(.5f,.08f,.5f),new Vector3(1.2f,.16f,1),steel,parent);
            for(int i=0;i<count;i++)Box("Stored carton",origin+new Vector3(.3f+(i%2)*.55f,.4f+(i/2)*.6f,.5f),new Vector3(.5f,.58f,.7f),carton,parent);
        }
        static void BuildWarehouse()
        {
            foreach(float z in new[]{51f,56f,61f})
            {
                var rack=Group("High rack - aisle "+z,fixtures);
                foreach(float x in new[]{-17f,-11f,-5f,1f})
                {
                    foreach(float dz in new[]{-.55f,.55f})Box("Upright",new Vector3(x,2.9f,z+dz),new Vector3(.15f,5.8f,.15f),teal,rack);
                }
                foreach(float y in new[]{.25f,2.1f,4.1f})
                {
                    Box("Rack shelf",new Vector3(-8,y,z),new Vector3(18,.13f,1.2f),orange,rack);
                    for(int bay=0;bay<3;bay++)for(int pack=0;pack<3;pack++)
                        Box("Warehouse pallet load",new Vector3(-15+bay*6+pack*1.5f,y+.6f,z),new Vector3(1.25f,1.05f,.95f),carton,rack);
                }
            }
            Cartons(new Vector3(-18,0,46),4,fixtures);
            Box("Forklift blockout body",new Vector3(-16,.65f,64),new Vector3(1.2f,1.3f,2),yellow,fixtures);
            Box("Forklift mast",new Vector3(-16,1.8f,62.9f),new Vector3(1.1f,3.6f,.2f),steel,fixtures);
            Sign("NORTHLINE LOGISTICS\nHIGH-BAY STORAGE",new Vector3(-7,6.4f,66.85f),Vector3.back,.52f);
            Sign("DISPATCH  >",new Vector3(5.9f,3.5f,64),Vector3.left,.3f);
            Box("Future dispatch exit",new Vector3(3,1.5f,66.85f),new Vector3(3,3,.15f),teal,fixtures);
            Box("Maintenance pipe",new Vector3(7.7f,1.2f,41),new Vector3(.3f,2.4f,.3f),steel,fixtures);
        }
        static void Fixture(Vector3 position,float width=1.5f)
        {
            Box("Fluorescent housing",position,new Vector3(width,.12f,.4f),steel,lighting,false);
            Box("Fluorescent diffuser",position-Vector3.up*.07f,new Vector3(width-.1f,.035f,.25f),white,lighting,false);
            var go=new GameObject("Fluorescent pool");go.transform.SetParent(lighting,false);go.transform.position=position-Vector3.up*.3f;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.82f,.92f,.94f);light.intensity=2;light.range=7;light.shadows=LightShadows.None;
        }
        static void BuildLighting()
        {
            foreach(float z in new[]{3f,10.8f})Fixture(new Vector3(1,3.35f,z));
            Fixture(new Vector3(9,3.35f,4));Fixture(new Vector3(-5,3.35f,7));
            foreach(float z in new[]{15f,21f,27f,33f})Fixture(new Vector3(10.5f,3.35f,z),2);
            foreach(float z in new[]{39f,43f,48f})Fixture(new Vector3(11,3.75f,z));
            foreach(float z in new[]{47f,54f,59f,65f})foreach(float x in new[]{-13f,-3f,4f})Fixture(new Vector3(x,7.2f,z),2.5f);
        }
        static CameraPOV Camera(string name,Rect bounds,Vector3 position,Vector3 target,int priority=10,bool thirdPerson=false)
        {
            var root=new GameObject(name);root.transform.SetParent(cameras,false);
            var trigger=root.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(bounds.center.x,2,bounds.center.y);trigger.size=new Vector3(bounds.width,6,bounds.height);
            root.AddComponent<OnDisableNotifier>();var pov=root.AddComponent<CameraPOV>();pov.Priority=priority;
            pov.PositionFollow.Enabled=!thirdPerson;pov.PositionFollow.FollowMode=CameraPOV.PlayerFollowMode.KeepPlayerCentered;pov.PositionFollow.CenterFollowDistance=.6f;
            pov.RotationFollow.Enabled=false;pov.ThirdPersonFollow.Enabled=thirdPerson;pov.ThirdPersonFollow.Distance=3.4f;pov.ThirdPersonFollow.Pitch=18;
            var camera=new GameObject("View");camera.transform.SetParent(root.transform,false);camera.transform.position=position;camera.transform.LookAt(target);
            var virtualCamera=camera.AddComponent<CinemachineCamera>();var lens=virtualCamera.Lens;lens.FieldOfView=thirdPerson?70:65;lens.NearClipPlane=.08f;virtualCamera.Lens=lens;
            return pov;
        }
        static void BuildCameras()
        {
            Camera("01 Facility tracking",new Rect(-21,-21,40,89),new Vector3(0,3,-8),new Vector3(0,1,0),0,true);
            var south=Camera("07 Storage south",new Rect(9,13,3,12),new Vector3(10.5f,3.1f,11),new Vector3(10.5f,1,22));south.PositionFollow.FollowHorizontal=false;
            var north=Camera("09 Storage north",new Rect(9,25,3,12),new Vector3(10.5f,3.1f,23),new Vector3(10.5f,1,34));north.PositionFollow.FollowHorizontal=false;
        }
        static DialogData Dialog(string text)
        {
            var data=new DialogData{PauseGame=true,CanBeDismissed=true};
            if(!string.IsNullOrEmpty(text))data.SetLines(new[]{new DialogLine{LineText=new LocalizableText{Unlocalized=text}}});
            return data;
        }
        static InteractionData Interaction(string key,string prompt)
        {
            string path=Root+"Assets/Interaction_"+key+".asset";var data=AssetDatabase.LoadAssetAtPath<InteractionData>(path);
            if(!data){data=ScriptableObject.CreateInstance<InteractionData>();AssetDatabase.CreateAsset(data,path);}
            data.Prompt=new LocalizableText{Unlocalized=prompt};data.RotateToInteractive=false;data.InteractionDuration=0;data.InteractionDelay=0;
            data.Icon=AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/AssetStore/HorrorEngine/ExampleContent/Interaction/Interaction_Use.asset").Icon;
            EditorUtility.SetDirty(data);return data;
        }
        static StorageAccessPanel Panel(string name,Vector3 position,Vector3 facing,string prompt,string info,string missing,params FlaggedState[] required)
        {
            var root=new GameObject(name);root.transform.SetParent(gameplay,false);root.transform.position=position;root.layer=LayerMask.NameToLayer("InteractionSystem");
            var trigger=root.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(1.15f,1.25f,1.15f);
            var interactive=root.AddComponent<Interactive>();interactive.Data=Interaction(name,prompt);interactive.OnInteract=new OnInteractionEvent();
            interactive.OnFinishInteract=new UnityEvent();interactive.OnDataChanged=new UnityEvent();
            var panel=root.AddComponent<StorageAccessPanel>();panel.Prerequisites=required;panel.MissingRequirement=Dialog(missing);panel.Information=Dialog(info);
            var state=root.GetComponent<FlaggedState>();state.OnFlag=new UnityEvent();state.OnLoadedFlagged=new UnityEvent();
            Box("Control face",position,new Vector3(.35f,.5f,.14f),teal,root.transform,false);
            panel.Status=Sign(prompt.ToUpperInvariant(),position+Vector3.up*.45f+facing*.13f,facing,.14f,root.transform);
            return panel;
        }
        static GameObject Gate(string name,Vector3 center,Vector3 size)
        {
            var root=Group(name,gameplay);Box("Closed shutter",center,size,steel,root);
            for(int i=0;i<10;i++)Box("Shutter rib",new Vector3(center.x,.18f+i*.29f,center.z),new Vector3(size.x+.015f,.02f,size.z+.015f),wall,root,false);
            return root.gameObject;
        }
        static void BuildProgression()
        {
            var tablet=Panel("Tablet_CheckIn",new Vector3(-13.5f,1.45f,-10),Vector3.back,"Register service tablet",
                "NORTHLINE SERVICE TERMINAL\nLocal tablet paired. Restore the utility disconnect beside the cabinet, then enter reception. The office holds the facility map and service module.","",Array.Empty<FlaggedState>());
            tablet.CompletedText="TABLET PAIRED";
            var power=Panel("PowerGrid1",new Vector3(-7.65f,1.25f,-7),Vector3.left,"Restore facility supply",
                "UTILITY SUPPLY RESTORED\nEntrance access is live. Interior security shutters still require their local controls.","Register the service tablet at the yard terminal first.",tablet.GetComponent<FlaggedState>());
            power.CompletedText="SUPPLY ONLINE";power.ReleasedBarriers=new[]{Gate("Entrance_Shutter",new Vector3(0,1.5f,0),new Vector3(2.6f,3,.18f))};
            var upgrade=Panel("Office_ServiceModule",new Vector3(7.9f,1.12f,4.9f),Vector3.back,"Collect tablet service module",
                "SERVICE MODULE INSTALLED\nYour tablet can now authorize feeder and distribution controls. Collect the facility map before leaving reception.","",Array.Empty<FlaggedState>());
            upgrade.CompletedText="SERVICE MODULE INSTALLED";
            var p1=Panel("Puzzle1_LocalAccess",new Vector3(1.8f,1.25f,12.7f),Vector3.back,"Enable corridor access",
                "CORRIDOR ACCESS ENABLED\nThe local shutter has opened. Feeder B is inside unit B102.","Restore the utility supply outside first.",power.GetComponent<FlaggedState>());
            p1.CompletedText="CORRIDOR ONLINE";p1.ReleasedBarriers=new[]{Gate("Puzzle1_Shutter",new Vector3(5,1.5f,11),new Vector3(.18f,3,4))};
            var p2=Panel("Puzzle2_FeederB",new Vector3(16.9f,1.25f,22),Vector3.left,"Authorize feeder B",
                "FEEDER B AUTHORIZED\nThe north corridor shutter has opened. Proceed to the distribution bay.","Utility power and the office service module are required.",power.GetComponent<FlaggedState>(),upgrade.GetComponent<FlaggedState>(),p1.GetComponent<FlaggedState>());
            p2.CompletedText="FEEDER B ONLINE";p2.ReleasedBarriers=new[]{Gate("Puzzle2_Shutter",new Vector3(10.5f,1.5f,25),new Vector3(3,3,.18f))};
            var p3=Panel("Puzzle3_Distribution",new Vector3(15.5f,1.25f,42),Vector3.left,"Release warehouse shutter",
                "DISTRIBUTION READY\nWarehouse access is released. This is the end of the blockout progression; dispatch is reserved for a later connection.","Restore feeder B and install the office service module first.",power.GetComponent<FlaggedState>(),upgrade.GetComponent<FlaggedState>(),p2.GetComponent<FlaggedState>());
            p3.CompletedText="WAREHOUSE ACCESS OPEN";p3.ReleasedBarriers=new[]{Gate("Puzzle3_Shutter",new Vector3(10.5f,1.5f,45.5f),new Vector3(3,3,.18f))};
        }
        static void BuildMap()
        {
            string path=Root+"Assets/StorageFacility_Map.asset";
            var data=AssetDatabase.LoadAssetAtPath<MapData>(path);
            if(!data){data=ScriptableObject.CreateInstance<MapData>();AssetDatabase.CreateAsset(data,path);}
            data.Name=new LocalizableText{Unlocalized="Northline Storage Facility"};data.Abbreviation=new LocalizableText{Unlocalized="NSF"};data.GenerateId();
            data.GlobalScale=8;data.Size=new Vector2Int(80,150);data.CellSize=8;
            var root=Group("06 Native facility map");root.gameObject.AddComponent<SavableObjectState>();var map=root.gameObject.AddComponent<MapController>();map.Data=data;
            foreach(var area in Areas)
            {
                var go=new GameObject(area.Name);go.transform.SetParent(root,false);
                var room=go.AddComponent<MapRoom>();go.AddComponent<SavableObjectState>();room.Name=area.Name;
                room.LinkedElements=new List<MapElement>();room.CompletionSteps=Array.Empty<MapRoomCompletionStep>();
                var r=area.Bounds;go.GetComponent<Shape>().Points=new List<Vector3>{new Vector3(r.xMin,0,r.yMin),new Vector3(r.xMin,0,r.yMax),new Vector3(r.xMax,0,r.yMax),new Vector3(r.xMax,0,r.yMin)};
            }
            var pickup=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/PickupMap.prefab"));
            pickup.name="FacilityMap_Pickup";pickup.transform.position=new Vector3(10,1.08f,4.9f);pickup.transform.SetParent(gameplay,true);
            var serialized=new SerializedObject(pickup.GetComponent<PickupMap>());serialized.FindProperty("m_Data").objectReferenceValue=data;
            serialized.FindProperty("m_GiveEntireSet").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
            Box("Map paper",pickup.transform.position,new Vector3(.4f,.015f,.3f),wall,pickup.transform,false);
            Sign("FACILITY MAP",pickup.transform.position+Vector3.up*.3f,Vector3.back,.13f,pickup.transform);
            var database=AssetDatabase.LoadAssetAtPath<MapDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/MapDB.asset");
            if(!database.Registers.Contains(data))database.Registers.Add(data);EditorUtility.SetDirty(database);
            MapSerializer.SerializeAndSaveMap(map);
        }
    }
}
