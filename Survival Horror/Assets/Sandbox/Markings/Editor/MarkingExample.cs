using System.IO;
using HouseToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MarkingExample
{
    public const string ScenePath="Assets/Sandbox/Markings/WarehouseMarkings.unity";
    [MenuItem("Tools/Procedural Environment/Create Marking Example")]
    public static void Create()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var concrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Environment/ProjectedFinishes/Concrete/Mottled Grey Concrete.mat");
        GameObject Block(string name,Vector3 position,Vector3 size,Quaternion rotation)
        {
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name=name;
            block.transform.SetPositionAndRotation(position,rotation);block.transform.localScale=size;
            block.GetComponent<MeshRenderer>().sharedMaterial=concrete;return block;
        }
        Block("Warehouse concrete",new Vector3(0,-.15f,2),new Vector3(15,.3f,12),Quaternion.identity);
        var rotation=Quaternion.Euler(-Mathf.Atan(.2f)*Mathf.Rad2Deg,0,0);
        Block("Ramp - 1.2m rise",new Vector3(4,.6f,3)-rotation*Vector3.up*.1f,new Vector3(2,.2f,Mathf.Sqrt(36+1.44f)),rotation);
        Block("Upper landing",new Vector3(4,1.05f,6.9f),new Vector3(2,.3f,1.8f),Quaternion.identity);
        ProceduralMarking Path(string name,float wear,params Vector3[] points)
        {
            var path=new GameObject(name).AddComponent<ProceduralMarking>();path.width=.14f;path.wear=wear;path.seed=17;
            int last=0;
            foreach(var point in points)
            {
                var node=path.paths.AddNode(point);
                if(last!=0)path.paths.Connect(last,node.id,0,null);
                last=node.id;
            }
            path.Rebuild();return path;
        }
        Path("Wear 0 - fresh",0,new Vector3(-6,0,-2),new Vector3(-2,0,-2));
        Path("Wear 35 - default",.35f,new Vector3(-6,0,-1),new Vector3(-2,0,-1));
        Path("Wear 75 - heavily chipped",.75f,new Vector3(-6,0,0),new Vector3(-2,0,0));
        Path("Right angle route",.35f,new Vector3(-6,0,2),new Vector3(-6,0,5),new Vector3(-2,0,5));
        Path("Angled route",.35f,new Vector3(-1,0,0),new Vector3(-1,0,2),new Vector3(1,0,4),new Vector3(1,0,6));
        Path("Ramp and landing",.35f,new Vector3(4,0,-2),new Vector3(4,0,0),new Vector3(4,1.2f,6),new Vector3(4,1.2f,7.5f));
        var light=new GameObject("Warehouse review light").AddComponent<Light>();light.type=LightType.Directional;
        light.intensity=.9f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(55,-30,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.37f,.4f);
        var camera=new GameObject("Marking review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=7.3f;
        camera.transform.position=new Vector3(11,14,-13);camera.transform.LookAt(new Vector3(0,0,2));
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.08f,.1f);camera.allowHDR=false;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));AssetDatabase.Refresh();
        EditorSceneManager.SaveScene(scene,ScenePath);
        Selection.activeGameObject=GameObject.Find("Ramp and landing");
    }
}
