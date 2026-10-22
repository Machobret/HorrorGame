using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    /// <summary>Six-face ammunition packaging. RunBatch is intended for the isolated validation project.</summary>
    public static class AmmoPackagingBuilder
    {
        public const string Folder = "Assets/Game/Weapons/Models/AmmoPackaging/";
        const string Prefabs = "Assets/Game/Weapons/Prefabs/";
        const string Examples = "Assets/AssetStore/HorrorEngine/ExampleContent/Items/";
        const string VisualName = "AmmoPackagingVisual";
        sealed class Definition
        {
            public string Name, Data, Exam, Base, Icon;
            public Vector3 Size;
            public Definition(string name, string data, string exam, string original, string icon, Vector3 size)
            { Name = name; Data = data; Exam = exam; Base = original; Icon = icon; Size = size; }
        }
        static readonly Definition[] Definitions = {
            new Definition("PistolAmmo", Examples+"GunAmmo/GunAmmo.asset", Examples+"GunAmmo/GunAmmoExamination.prefab", Examples+"GunAmmo/GunAmmo.prefab", "Assets/Game/Weapons/Models/AmmoSides/PistolAmmo_PickupIcon.png", new Vector3(.13f,.065f,.075f)),
            new Definition("ShotgunAmmo", Examples+"ShotgunAmmo/ShotgunAmmo.asset", Examples+"ShotgunAmmo/ShotgunAmmoExamination.prefab", Examples+"ShotgunAmmo/ShotgunAmmo.prefab", "Assets/Game/Weapons/Models/AmmoSides/ShotgunAmmo_PickupIcon.png", new Vector3(.12f,.09f,.09f)),
            new Definition("RevolverAmmo", "Assets/Game/Weapons/Data/RevolverAmmo.asset", Prefabs+"RevolverAmmo_Examination.prefab", null, "Assets/Game/Weapons/Models/AmmoSides/RevolverAmmo_PickupIcon.png", new Vector3(.125f,.055f,.075f)),
            new Definition("HuntingRifleAmmo", "Assets/Game/Weapons/Data/HuntingRifleAmmo.asset", Prefabs+"HuntingRifleAmmo_Examination.prefab", null, "Assets/Game/Weapons/Models/AmmoSides/HuntingRifleAmmo_PickupIcon.png", new Vector3(.16f,.07f,.09f)),
            new Definition("FlareAmmo", "Assets/Game/Weapons/Data/FlareAmmo.asset", Prefabs+"FlareAmmo_Examination.prefab", null, "Assets/Game/Weapons/Models/FlareAmmo/FlareAmmo_Icon.png", new Vector3(.105f,.095f,.075f)),
            // SMG ammunition is physically 9mm, but deliberately remains a separate
            // inventory ammo type with its own blue carton for gameplay readability.
            new Definition("SMGAmmo", "Assets/Game/Weapons/Data/SMGAmmo.asset", Prefabs+"SMGAmmo_Examination.prefab", null, "Assets/Game/Weapons/Models/AmmoSides/SMGAmmo_PickupIcon.png", new Vector3(.13f,.065f,.075f))
        };
        static readonly Dictionary<ScriptableRendererFeature, bool> FeatureStates = new Dictionary<ScriptableRendererFeature, bool>();
        static Camera camera;
        static RenderTexture target;
        static int warmup;
        static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ArtSource/Weapons/AmmoPackaging/Preview");

        [MenuItem("Horror Game/Weapons/Apply Researched Ammo Packaging")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var definition in Definitions)
            {
                string atlasPath = Folder + definition.Name + "_Atlas.png";
                var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
                if (!importer) throw new Exception("Missing generated six-panel atlas: " + atlasPath);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
                Material paper = MaterialAsset(definition.Name+"_Packaging", Color.white, 0, .08f);
                paper.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));
                paper.SetTextureScale("_BaseMap", Vector2.one); paper.SetTextureOffset("_BaseMap", Vector2.zero);
                EditorUtility.SetDirty(paper);
                Mesh mesh = SaveMesh(Carton(definition.Size, definition.Name), Folder + definition.Name + "_Carton.asset");
                ValidateCartonMesh(mesh, definition.Name);
                BuildModelPrefab(definition, mesh, paper);
                UpdatePrefab(Prefabs + definition.Name + "_Pickup.prefab", definition, false);
                UpdatePrefab(definition.Exam, definition, true);
                if (definition.Base != null) UpdatePrefab(definition.Base, definition, false);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("AMMO_PACKAGING_ASSETS_OK six cartons and pickup/examination assets; six textured faces per carton");
        }

        static Material MaterialAsset(string name, Color color, float metallic, float smoothness)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("URP Lit shader unavailable");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader;
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_Cull",2); material.SetFloat("_Surface",0); material.SetFloat("_AlphaClip",0);
            EditorUtility.SetDirty(material); return material;
        }

        static Mesh SaveMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing) { AssetDatabase.CreateAsset(generated,path); return generated; }
            EditorUtility.CopySerialized(generated,existing); Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); return existing;
        }

        // The atlas contains 3 columns x 2 rows with measured panel boundaries. Top: FRONT BACK LEFT;
        // bottom: RIGHT TOP BOTTOM. Distinct vertices prevent UV or normal sharing.
        static Mesh Carton(Vector3 size, string name)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector3 h=size*.5f;
            AddFace(vertices,normals,uv,triangles,new Vector3(0,0,-h.z),Vector3.right*h.x,Vector3.up*h.y,Vector3.back,0,name);
            AddFace(vertices,normals,uv,triangles,new Vector3(0,0,h.z),Vector3.left*h.x,Vector3.up*h.y,Vector3.forward,1,name);
            AddFace(vertices,normals,uv,triangles,new Vector3(-h.x,0,0),Vector3.back*h.z,Vector3.up*h.y,Vector3.left,2,name);
            AddFace(vertices,normals,uv,triangles,new Vector3(h.x,0,0),Vector3.forward*h.z,Vector3.up*h.y,Vector3.right,3,name);
            AddFace(vertices,normals,uv,triangles,new Vector3(0,h.y,0),Vector3.right*h.x,Vector3.forward*h.z,Vector3.up,4,name);
            AddFace(vertices,normals,uv,triangles,new Vector3(0,-h.y,0),Vector3.right*h.x,Vector3.back*h.z,Vector3.down,5,name);
            var mesh = new Mesh { name="SixFaceAmmoCarton" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
        static void AddFace(List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> triangles,Vector3 center,Vector3 right,Vector3 up,Vector3 normal,int tile,string name)
        {
            int start=vertices.Count; int column=tile%3; int row=tile/3;
            Rect panel = AtlasPanel(name, row, column);
            float u0=panel.xMin,u1=panel.xMax,v0=panel.yMin,v1=panel.yMax;
            vertices.Add(center-right-up); vertices.Add(center+right-up); vertices.Add(center+right+up); vertices.Add(center-right+up);
            for(int i=0;i<4;++i)normals.Add(normal);
            uv.Add(new Vector2(u0,v0));uv.Add(new Vector2(u1,v0));uv.Add(new Vector2(u1,v1));uv.Add(new Vector2(u0,v1));
            triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
        }

        // Source panels have deliberately measured boundaries rather than equal
        // thirds. Pixel Y is top-down in the artwork; Unity UV Y is bottom-up.
        static Rect AtlasPanel(string name,int row,int column)
        {
            float[] top, bottom; float split=512;
            switch(name)
            {
                case "SMGAmmo": top=new float[]{0,514,1104,1536};bottom=new float[]{0,474,1056,1536};break;
                case "PistolAmmo": top=new float[]{0,514,1104,1536};bottom=new float[]{0,474,1056,1536};break;
                case "ShotgunAmmo": top=new float[]{0,516,1104,1536};bottom=new float[]{0,474,1049,1536};break;
                case "RevolverAmmo": top=new float[]{0,512,1026,1536};bottom=top;break;
                case "HuntingRifleAmmo": top=new float[]{0,520,1210,1536};bottom=new float[]{0,330,1020,1536};break;
                case "FlareAmmo": top=new float[]{0,534,1132,1536};bottom=new float[]{0,404,1062,1536};split=534;break;
                default: throw new Exception("No measured packaging panel layout for "+name);
            }
            float[] columns=row==0?top:bottom;
            float x0=columns[column],x1=columns[column+1];
            float y0=row==0?0:split,y1=row==0?split:1024;
            float paddingX=(x1-x0)*.02f,paddingY=(y1-y0)*.02f;
            return Rect.MinMaxRect((x0+paddingX)/1536f,1-(y1-paddingY)/1024f,
                (x1-paddingX)/1536f,1-(y0+paddingY)/1024f);
        }
        static void BuildModelPrefab(Definition definition, Mesh carton, Material paper)
        {
            var root = new GameObject(VisualName);
            try
            {
                var box=new GameObject("SixFaceCarton"); box.transform.SetParent(root.transform,false);box.transform.localPosition=Vector3.up*definition.Size.y*.5f;
                box.AddComponent<MeshFilter>().sharedMesh=carton; box.AddComponent<MeshRenderer>().sharedMaterial=paper;
                if(definition.Name=="FlareAmmo") AddFlareBundle(root.transform,definition.Size);
                else AddLooseCartridges(root.transform,definition);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+definition.Name+"_Model.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void AddFlareBundle(Transform parent, Vector3 size)
        {
            // Independent closed lathed shells replace the imported bundle whose
            // band geometry stretches when its source mesh is substituted/rebuilt.
            var brass=MaterialAsset("Cartridge_Brass",new Color(.48f,.35f,.16f),.65f,.26f);
            var red=MaterialAsset("FlareCartridge_Red",new Color(.46f,.065f,.045f),0,.12f);
            var label=MaterialAsset("FlareCartridge_Label",new Color(.78f,.74f,.61f),0,.10f);
            // The carton carries RED SIGNAL text. Keep this tiny physical band
            // plain ivory rather than stretching an unrelated atlas panel around it.
            label.SetTexture("_BaseMap",null);EditorUtility.SetDirty(label);
            var cap=MaterialAsset("FlareCartridge_Cap",new Color(.105f,.115f,.12f),0,.10f);
            Vector2[] profile={
                new Vector2(.011f,0),new Vector2(.011f,.002f),
                new Vector2(.010f,.002f),new Vector2(.010f,.012f),
                new Vector2(.0098f,.012f),new Vector2(.0098f,.026f),
                new Vector2(.0099f,.026f),new Vector2(.0099f,.048f),
                new Vector2(.0098f,.048f),new Vector2(.0098f,.068f),
                new Vector2(.0093f,.070f),new Vector2(0,.070f)
            };
            int[] materials={0,0,0,0,1,2,2,2,1,3,3};
            Mesh mesh=SaveMesh(Lathe(profile,materials),Folder+"FlareAmmo_LooseRound.asset");
            for(int i=0;i<3;++i)
            {
                var round=new GameObject("SignalCartridge"+(i+1));round.transform.SetParent(parent,false);
                round.transform.localPosition=new Vector3(-.035f,.011f,-size.z*.5f-.015f-i*.023f);
                round.transform.localRotation=Quaternion.Euler(0,0,-90);
                round.AddComponent<MeshFilter>().sharedMesh=mesh;
                round.AddComponent<MeshRenderer>().sharedMaterials=new[]{brass,red,label,cap};
            }
        }
        static void AddLooseCartridges(Transform parent, Definition definition)
        {
            var brass=MaterialAsset("Cartridge_Brass",new Color(.48f,.35f,.16f),.65f,.26f);
            var copper=MaterialAsset("Cartridge_Copper",new Color(.50f,.25f,.12f),.45f,.24f);
            var lead=MaterialAsset("Cartridge_Lead",new Color(.17f,.18f,.19f),.3f,.12f);
            var red=MaterialAsset("ShotShell_Red",new Color(.37f,.055f,.045f),0,.12f);
            Vector2[] profile; int[] material;
            switch(definition.Name)
            {
                case "ShotgunAmmo": profile=new[]{new Vector2(.010f,0),new Vector2(.010f,.002f),new Vector2(.0092f,.002f),new Vector2(.0092f,.014f),new Vector2(.0091f,.014f),new Vector2(.0091f,.059f),new Vector2(.0078f,.061f),new Vector2(0,.061f)};material=new[]{0,0,0,1,1,1,1};break;
                case "HuntingRifleAmmo": profile=new[]{new Vector2(.0062f,0),new Vector2(.0062f,.002f),new Vector2(.0059f,.002f),new Vector2(.0056f,.046f),new Vector2(.0043f,.055f),new Vector2(.0039f,.063f),new Vector2(.0039f,.068f),new Vector2(.0028f,.079f),new Vector2(0,.086f)};material=new[]{0,0,0,0,0,1,1,1};break;
                case "RevolverAmmo": profile=new[]{new Vector2(.0056f,0),new Vector2(.0056f,.0015f),new Vector2(.0048f,.0015f),new Vector2(.0048f,.032f),new Vector2(.0045f,.032f),new Vector2(.0045f,.038f),new Vector2(.0036f,.043f),new Vector2(0,.043f)};material=new[]{0,0,0,1,1,1,1};break;
                default: profile=new[]{new Vector2(.0051f,0),new Vector2(.0051f,.0015f),new Vector2(.0048f,.002f),new Vector2(.0048f,.019f),new Vector2(.0045f,.019f),new Vector2(.0045f,.024f),new Vector2(.0028f,.030f),new Vector2(0,.031f)};material=new[]{0,0,0,1,1,1,1};break;
            }
            Mesh mesh=SaveMesh(Lathe(profile,material),Folder+definition.Name+"_LooseRound.asset");
            for(int i=0;i<2;++i)
            {
                var round=new GameObject("LooseRound"+(i+1));round.transform.SetParent(parent,false);
                round.transform.localPosition=new Vector3(-profile[profile.Length-1].y*.5f+i*.005f,.011f,-definition.Size.z*.5f-.016f-i*.022f);
                round.transform.localRotation=Quaternion.Euler(0,i==0?-8:8,-90);
                round.AddComponent<MeshFilter>().sharedMesh=mesh;
                round.AddComponent<MeshRenderer>().sharedMaterials=new[]{brass,definition.Name=="ShotgunAmmo"?red:definition.Name=="RevolverAmmo"?lead:copper};
            }
        }
        static Mesh Lathe(Vector2[] profile,int[] material)
        {
            if(profile==null||material==null||profile.Length<2||material.Length!=profile.Length-1||material.Any(slot=>slot<0))throw new Exception("Invalid cartridge lathe profile/material slots");
            int slots=material.Max()+1;const int sides=12;
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();
            var triangles=new List<int>[slots];for(int slot=0;slot<slots;++slot)triangles[slot]=new List<int>();
            for(int ring=0;ring<profile.Length-1;++ring)for(int side=0;side<sides;++side)
            {
                float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides;int first=v.Count;
                Vector2 low=profile[ring],high=profile[ring+1];
                v.Add(new Vector3(Mathf.Cos(a)*low.x,low.y,Mathf.Sin(a)*low.x));v.Add(new Vector3(Mathf.Cos(b)*low.x,low.y,Mathf.Sin(b)*low.x));
                v.Add(new Vector3(Mathf.Cos(b)*high.x,high.y,Mathf.Sin(b)*high.x));v.Add(new Vector3(Mathf.Cos(a)*high.x,high.y,Mathf.Sin(a)*high.x));
                Vector3 normal=Vector3.Cross(v[first+2]-v[first],v[first+1]-v[first]).normalized;
                for(int k=0;k<4;++k){n.Add(normal);uv.Add(Vector2.zero);}
                triangles[material[ring]].AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
            }
            // Close the primer end; radial side/end profiles remain explicitly low poly.
            int center=v.Count;v.Add(Vector3.zero);n.Add(Vector3.down);uv.Add(Vector2.zero);
            for(int side=0;side<sides;++side){int a=v.Count;float angle=side*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(angle)*profile[0].x,0,Mathf.Sin(angle)*profile[0].x));n.Add(Vector3.down);uv.Add(Vector2.zero);float next=(side+1)*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(next)*profile[0].x,0,Mathf.Sin(next)*profile[0].x));n.Add(Vector3.down);uv.Add(Vector2.zero);triangles[0].AddRange(new[]{center,a,a+1});}
            var mesh=new Mesh{name="LowPolyCartridge"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.subMeshCount=slots;for(int i=0;i<slots;++i)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();return mesh;
        }

        static Bounds LocalBounds(Transform root) => BoundsIn(root,root.GetComponentsInChildren<Renderer>(true));
        static Bounds BoundsIn(Transform frame,Renderer[] renderers)
        {
            bool first=true;var result=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled)continue;
                Bounds b=renderer.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {Vector3 p=frame.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}
            }
            return result;
        }
        static string GameplaySignature(GameObject root)
        {
            // Capture the serialized components before any geometry replacement.
            var records=new List<string>();
            foreach(var component in root.GetComponentsInChildren<Component>(true))
            {
                if(component is Collider || component is ObjectUniqueId || component is PickupItem)
                    records.Add(component.GetType().FullName+":"+EditorJsonUtility.ToJson(component));
            }
            return string.Join("\n",records.OrderBy(s=>s));
        }
        static void UnpackVisualInstances(GameObject root)
        {
            var roots=root.GetComponentsInChildren<Transform>(true).Select(t=>PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject)).Where(g=>g).Distinct().ToArray();
            foreach(var instance in roots)if(instance&&PrefabUtility.IsPartOfPrefabInstance(instance))PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        }
        static void ReplaceVisuals(GameObject root,Definition definition,bool examination)
        {
            Bounds before=LocalBounds(root.transform);
            float previousSize=Mathf.Max(before.size.x,before.size.y,before.size.z);
            UnpackVisualInstances(root);
            var old=root.transform.Find(VisualName);
            if(old)Object.DestroyImmediate(old.gameObject);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))Object.DestroyImmediate(renderer);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(filter);
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))Object.DestroyImmediate(renderer);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+definition.Name+"_Model.prefab");
            if(!model)throw new Exception("Missing packaging model "+definition.Name);
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);visual.name=VisualName;
            Bounds bounds=LocalBounds(visual.transform);
            float modelSize=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            float scale=previousSize>.00001f?previousSize/modelSize:1;
            visual.transform.localScale=Vector3.one*scale;
            visual.transform.localPosition=examination?-bounds.center*scale:new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
            foreach(var transform in visual.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=root.layer;
            var utility=root.GetComponent<UIExamineItemUtility>();if(utility)utility.Visuals=visual;
        }
        static void UpdatePrefab(string path,Definition definition,bool examination)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before=GameplaySignature(root);
                ReplaceVisuals(root,definition,examination);
                if(before!=GameplaySignature(root))throw new Exception("Gameplay components changed during packaging revision: "+path);
                ValidateVisuals(root,definition.Name);
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log("AMMO_PACKAGING_PREFAB_OK "+path+" gameplay IDs/quantities/colliders retained");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>WeaponAssetBuilder can prefer this to the old side-only correction after instantiating its model.</summary>
        public static bool ApplyToModel(GameObject model,string modelName)
        {
            string name=modelName=="RifleAmmo"?"HuntingRifleAmmo":modelName;
            var definition=Definitions.FirstOrDefault(d=>d.Name==name);
            if(definition==null||!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+"_Model.prefab"))return false;
            ReplaceVisuals(model,definition,false);return true;
        }
        static void ValidateCartonMesh(Mesh mesh,string name)
        {
            if(!mesh||mesh.vertexCount!=24||mesh.triangles.Length!=36)throw new Exception("Invalid carton geometry "+name);
            var uv=mesh.uv;var triangles=mesh.triangles;
            for(int face=0;face<6;++face)
            {
                int a=triangles[face*6],b=triangles[face*6+1],c=triangles[face*6+2];
                float area=Mathf.Abs((uv[b].x-uv[a].x)*(uv[c].y-uv[a].y)-(uv[b].y-uv[a].y)*(uv[c].x-uv[a].x));
                if(area<.01f)throw new Exception("Degenerate panel UV "+name+" face="+face);
            }
        }
        static void ValidateVisuals(GameObject root,string name)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length<1)throw new Exception("No visible packaging "+name);
            foreach(var renderer in renderers)
            {
                var mesh=renderer.GetComponent<MeshFilter>();
                if(!mesh||!mesh.sharedMesh)throw new Exception("Missing mesh "+name);
                foreach(var material in renderer.sharedMaterials)if(!material||!material.shader||material.shader.name=="Hidden/InternalErrorShader")throw new Exception("Missing material "+name);
            }
            var carton=root.transform.Find(VisualName+"/SixFaceCarton");
            if(!carton)throw new Exception("Missing six-face carton "+name);
            ValidateCartonMesh(carton.GetComponent<MeshFilter>().sharedMesh,name);
        }

        public static void RunBatch()
        {
            try
            {
                Apply();Directory.CreateDirectory(Output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(Application.isBatchMode)foreach(var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
                {
                    var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach(var feature in renderer.rendererFeatures)if(feature&&feature.GetType().Name.Contains("FullScreen")&&!FeatureStates.ContainsKey(feature)){FeatureStates.Add(feature,feature.isActive);feature.SetActive(false);}
                }
                RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.33f,.35f);
                var key=new GameObject("Warm carton key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2.2f;key.color=new Color(1,.86f,.69f);key.transform.rotation=Quaternion.Euler(38,-38,0);
                var fill=new GameObject("Cool carton fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.9f;fill.color=new Color(.65f,.78f,1);fill.transform.rotation=Quaternion.Euler(30,145,0);
                camera=new GameObject("Packaging inspection camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=25;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                target=new RenderTexture(1024,1024,24);camera.targetTexture=target;warmup=0;EditorApplication.update+=CaptureWarm;
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static void CaptureWarm()
        {
            if(++warmup<8)return;
            EditorApplication.update-=CaptureWarm;
            try
            {
                foreach(var definition in Definitions)
                {
                    var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+definition.Name+"_Pickup.prefab"));
                    try
                    {
                        ValidateVisuals(root,definition.Name);
                        Bounds bounds=BoundsIn(root.transform,root.GetComponentsInChildren<Renderer>(true));
                        // Root is at the origin with unit scale in the capture scene.
                        bounds=new Bounds(root.transform.TransformPoint(bounds.center),Vector3.Scale(bounds.size,root.transform.lossyScale));
                        for(int view=0;view<2;++view)
                        {
                            Vector3 direction=view==0?new Vector3(.8f,.85f,-1.6f):new Vector3(-.8f,.85f,1.6f);
                            camera.transform.position=bounds.center+direction*bounds.size.magnitude*3;camera.transform.LookAt(bounds.center);camera.orthographicSize=Fit(bounds)*1.12f;
                            Save(Output+"/"+definition.Name+(view==0?"_Front.png":"_Back.png"));
                            if(view==0)
                            {
                                Save(definition.Icon);AssetDatabase.ImportAsset(definition.Icon,ImportAssetOptions.ForceSynchronousImport);
                                var importer=(TextureImporter)AssetImporter.GetAtPath(definition.Icon);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                                var data=AssetDatabase.LoadAssetAtPath<AmmoData>(definition.Data);if(!data)throw new Exception("Missing ammo data "+definition.Data);data.Image=AssetDatabase.LoadAssetAtPath<Sprite>(definition.Icon);EditorUtility.SetDirty(data);
                            }
                        }
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                foreach(var state in FeatureStates)if(state.Key)state.Key.SetActive(state.Value);FeatureStates.Clear();
                AmmoPackagingDrops.Apply();
                AssetDatabase.SaveAssets();Debug.Log("AMMO_PACKAGING_PRESENTATION_OK six refreshed inventory/pickup icons and twelve front/back previews");Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static float Fit(Bounds bounds)
        {
            float size=0;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){Vector3 point=camera.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));size=Mathf.Max(size,Mathf.Abs(point.x),Mathf.Abs(point.y));}return Mathf.Max(.001f,size);
        }
        static void Save(string path)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);}
        }
        static void Finish(int code)
        {
            EditorApplication.update-=CaptureWarm;foreach(var state in FeatureStates)if(state.Key)state.Key.SetActive(state.Value);FeatureStates.Clear();
            if(camera)Object.DestroyImmediate(camera.gameObject);if(target){target.Release();Object.DestroyImmediate(target);}EditorApplication.Exit(code);
        }
    }
}