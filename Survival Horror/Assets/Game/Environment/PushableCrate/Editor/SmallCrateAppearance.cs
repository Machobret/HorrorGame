using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Environment.Editor
{
    /// <summary>Visual mesh only; the native one-metre cube collider and traversal rig stay intact.</summary>
    public static class SmallCrateAppearance
    {
        const string Folder = "Assets/Game/Environment/PushableCrate/";
        static readonly List<Vector3> vertices = new List<Vector3>();
        static readonly List<Vector2> uvs = new List<Vector2>();
        static readonly List<int> woodTriangles = new List<int>();
        static readonly List<int> labelTriangles = new List<int>();
        static int plank;

        [MenuItem("Horror Game/Environment/Update Small Crate Appearance")]
        public static void UpdatePrefab()
        {
            string path = Folder + "PushableCrate1.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            CapturePreview();
            Debug.Log("SMALL_CRATE_APPEARANCE_OK: one-metre wood frame, four X-braced sides and four pushing/handling labels; gameplay components retained.");
        }

        [MenuItem("Horror Game/Environment/Capture Small Crate Appearance")]
        public static void CapturePreview()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/PushableCrate");
            Directory.CreateDirectory(output);
            var previousRT=RenderTexture.active;
            RenderTexture rt=null;
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PushableCrate1.prefab");
                var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var cameraObject=new GameObject("Crate review camera");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.095f,.105f,.12f,1);
                camera.orthographic=true;camera.orthographicSize=.86f;camera.nearClipPlane=.01f;camera.farClipPlane=20;
                camera.scene=scene;
                rt=new RenderTexture(900,900,24);rt.Create();camera.targetTexture=rt;
                foreach(var spec in new[] {new Vector3(45,-35,0),new Vector3(35,140,0)})
                {
                    var lightObject=new GameObject("Crate review light");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,scene);
                    var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;
                    light.intensity=spec.y<0?2.2f:.8f;lightObject.transform.rotation=Quaternion.Euler(spec);
                }
                for(int view=0;view<2;view++)
                {
                    camera.transform.position=new Vector3(view==0?1.6f:-1.6f,1.55f,view==0?1.9f:-1.9f);
                    camera.transform.LookAt(new Vector3(0,.49f,0));camera.Render();RenderTexture.active=rt;
                    var image=new Texture2D(900,900,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,900,900),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(output,view==0?"PushableCrate1_Front.png":"PushableCrate1_Back.png"),image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
            }
            finally
            {
                RenderTexture.active=previousRT;
                if(rt) {rt.Release();Object.DestroyImmediate(rt);}
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static void Apply(GameObject root)
        {
            string texturePath = Folder + "PushableCrate1_HandlingLabel.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Handling label texture is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 512;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            var oldWood = AssetDatabase.LoadAssetAtPath<Material>(Folder + "PushableCrate_Wood.mat");
            var wood = Material("PushableCrate1_FramedWood", oldWood.GetTexture("_MainTex"));
            var label = Material("PushableCrate1_HandlingLabel", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            vertices.Clear(); uvs.Clear(); woodTriangles.Clear(); labelTriangles.Clear(); plank = 0;

            // Recessed closed body; full-size rails keep the silhouette exactly one metre.
            Box(Vector3.zero, new Vector3(.90f,.90f,.90f), Quaternion.identity);
            foreach (float x in new[] { -.45f, .45f })
                foreach (float z in new[] { -.45f, .45f })
                    Box(new Vector3(x,0,z), new Vector3(.10f,.80f,.10f), Quaternion.identity);
            foreach (float y in new[] { -.45f,.45f })
            {
                foreach (float z in new[] { -.46f,.46f })
                    Beam(new Vector3(-.5f,y,z),new Vector3(.5f,y,z),.10f,.08f,Quaternion.identity);
                foreach (float x in new[] { -.46f,.46f })
                    Beam(new Vector3(x,y,-.42f),new Vector3(x,y,.42f),.10f,.08f,Quaternion.Euler(0,90,0));
            }
            for(int side=0;side<4;side++)
            {
                Quaternion rotation = Quaternion.Euler(0,side*90,0);
                for(int i=0;i<6;i++)
                    FaceBox(new Vector3(-.35f+i*.14f,0,.445f),new Vector3(.137f,.80f,.03f),Quaternion.identity,rotation);
                // Grain follows each diagonal. A small depth offset prevents crossing faces from flickering.
                FaceBeam(new Vector3(-.35f,-.35f,.472f),new Vector3(.35f,.35f,.472f),.09f,.028f,rotation);
                FaceBeam(new Vector3(-.35f,.35f,.484f),new Vector3(.35f,-.35f,.484f),.09f,.028f,rotation);
                // Large handling placard spans the braces; its thin wood backing supports the paper.
                FaceBox(new Vector3(0,0,.494f),new Vector3(.82f,.52f,.01f),Quaternion.identity,rotation);
                Quad(rotation*new Vector3(-.40f,-.25f,.4995f),rotation*new Vector3(.40f,-.25f,.4995f),
                    rotation*new Vector3(.40f,.25f,.4995f),rotation*new Vector3(-.40f,.25f,.4995f),
                    new Rect(0,0,1,1),labelTriangles);
            }
            // Solid, flat top planks remain just below the existing climb surface.
            for(int i=0;i<5;i++)
                Box(new Vector3(-.35f+i*.175f,.4675f,0),new Vector3(.172f,.80f,.055f),Quaternion.Euler(90,0,0));

            string meshPath=Folder+"PushableCrate1_FramedMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (!mesh) { mesh=new Mesh(); AssetDatabase.CreateAsset(mesh,meshPath); }
            mesh.Clear();mesh.name="PushableCrate1_FramedMesh";
            mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.subMeshCount=2;
            mesh.SetTriangles(woodTriangles,0);mesh.SetTriangles(labelTriangles,1);
            mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var visual=root.transform.Find("Crate");
            visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials=new[] {wood,label};
            if ((mesh.bounds.size-Vector3.one).sqrMagnitude>.00001f || labelTriangles.Count!=24)
                throw new InvalidOperationException("Crate visual bounds or four side labels are incorrect.");
        }

        static Material Material(string name, Texture texture)
        {
            string path=Folder+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Smoothness",.04f);material.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(material);return material;
        }
        static void FaceBox(Vector3 center,Vector3 size,Quaternion local,Quaternion side)
            =>Box(side*center,size,side*local);
        static void FaceBeam(Vector3 a,Vector3 b,float width,float depth,Quaternion side)
            =>Beam(side*a,side*b,width,depth,side);
        static void Beam(Vector3 a,Vector3 b,float width,float depth,Quaternion side)
        {
            Vector3 up=(b-a).normalized;
            Quaternion rotation=Quaternion.LookRotation(side*Vector3.forward,up);
            Box((a+b)*.5f,new Vector3(width,(b-a).magnitude,depth),rotation);
        }
        static void Box(Vector3 center,Vector3 size,Quaternion rotation)
        {
            Vector3 p(Vector3 v)=>center+rotation*Vector3.Scale(v,size*.5f);
            // Sample only the plain wood interior of the old atlas, avoiding its metal border.
            Rect uv=new Rect(.19f+(plank++%6)*.095f,.12f,.075f,.76f);
            Quad(p(new Vector3(-1,-1,1)),p(new Vector3(1,-1,1)),p(new Vector3(1,1,1)),p(new Vector3(-1,1,1)),uv,woodTriangles);
            Quad(p(new Vector3(1,-1,-1)),p(new Vector3(-1,-1,-1)),p(new Vector3(-1,1,-1)),p(new Vector3(1,1,-1)),uv,woodTriangles);
            Quad(p(new Vector3(1,-1,1)),p(new Vector3(1,-1,-1)),p(new Vector3(1,1,-1)),p(new Vector3(1,1,1)),uv,woodTriangles);
            Quad(p(new Vector3(-1,-1,-1)),p(new Vector3(-1,-1,1)),p(new Vector3(-1,1,1)),p(new Vector3(-1,1,-1)),uv,woodTriangles);
            Quad(p(new Vector3(-1,1,1)),p(new Vector3(1,1,1)),p(new Vector3(1,1,-1)),p(new Vector3(-1,1,-1)),uv,woodTriangles);
            Quad(p(new Vector3(-1,-1,-1)),p(new Vector3(1,-1,-1)),p(new Vector3(1,-1,1)),p(new Vector3(-1,-1,1)),uv,woodTriangles);
        }
        static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect uv,List<int> triangles)
        {
            int n=vertices.Count;vertices.AddRange(new[] {a,b,c,d});
            float left=ReferenceEquals(triangles,labelTriangles)?uv.xMax:uv.xMin;
            float right=ReferenceEquals(triangles,labelTriangles)?uv.xMin:uv.xMax;
            uvs.AddRange(new[] {new Vector2(left,uv.yMin),new Vector2(right,uv.yMin),new Vector2(right,uv.yMax),new Vector2(left,uv.yMax)});
            triangles.AddRange(new[] {n,n+1,n+2,n,n+2,n+3});
        }
    }
}
