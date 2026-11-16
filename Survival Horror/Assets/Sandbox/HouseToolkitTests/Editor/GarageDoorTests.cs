using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class GarageDoorTests
{
    const string Dir="Assets/Game/Environment/GarageDoors/";
    static GarageDoorInsert Insert(string name)=>AssetDatabase.LoadAssetAtPath<GarageDoorInsert>(Dir+"Garage Door "+name+".asset");
    static WallFamily Family(string name)=>AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/"+name+"/"+name+".asset");

    [Test]
    public void PlainWallFitsEachGarageAndPreservesConfigurationOnRebuildUndoAndCopy()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var house=HouseEditing.Create(Vector3.zero,Family("Plain"));house.transform.rotation=Quaternion.Euler(0,37,0);
        var floor=HouseEditing.Floors(house).Single();
        var edit=new FloorEdit(floor);edit.Draw(Vector2.zero,new Vector2(12,0));Assert.IsTrue(edit.Commit("Draw",out var why),why);
        var wall=floor.GetComponentInChildren<HouseWall>();
        var motor=Insert("Operable");var decor=Insert("Decorative");
        Assert.Contains(motor,OpeningPalette.Choices(wall,OpeningKind.Door));
        Assert.IsTrue(WallBuilder.Place(wall,motor,3,0,out why,false,new Vector2(3.4f,2.3f),13),why);
        Assert.IsTrue(WallBuilder.Place(wall,decor,8,0,out why,false,new Vector2(2.1f,1.8f),2),why);
        Assert.IsFalse(WallBuilder.Place(wall,decor,11,0,out why,false,new Vector2(2,1),0));
        Assert.IsFalse(WallBuilder.Place(wall,decor,6,0,out why,false,new Vector2(1,2.8f),0));
        Assert.IsFalse(WallBuilder.Place(wall,decor,4,0,out why,false,Vector2.one,0));
        Assert.AreEqual(2,wall.openings.Count);
        var door=wall.GetComponentsInChildren<GarageDoor>().Single(v=>v.Operable);
        Assert.That(door.leaf.transform.localScale.x,Is.EqualTo(3.4f).Within(.001f));
        var link=door.GetComponent("GarageDoorCircuitLink");
        var settings=new SerializedObject(link);settings.FindProperty("CircuitId").stringValue="LoadingBay";
        settings.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(link);
        var o=wall.openings[0];
        Undo.IncrementCurrentGroup();
        Assert.IsTrue(HouseScene.EditOpening(wall,o,motor,3,0,true,out why,new Vector2(3,2),7),why);
        Assert.AreSame(door,wall.GetComponentsInChildren<GarageDoor>().Single(v=>v.Operable));
        Assert.AreEqual(7,door.FinishTile);
        Assert.AreEqual("LoadingBay",new SerializedObject(link).FindProperty("CircuitId").stringValue);
        Undo.FlushUndoRecordObjects();Undo.PerformUndo();
        Assert.AreEqual(13,wall.openings[0].finishTile);
        Assert.That(wall.openings[0].clearSize.x,Is.EqualTo(3.4f));
        Assert.AreEqual(2,wall.openings[1].finishTile);
        var upper=HouseEditing.AddFloor(house,true);
        var copy=upper.GetComponentInChildren<HouseWall>();
        CollectionAssert.AreEqual(wall.openings.Select(x=>x.clearSize),copy.openings.Select(x=>x.clearSize));
        CollectionAssert.AreEqual(wall.openings.Select(x=>x.finishTile),copy.openings.Select(x=>x.finishTile));
        wall.familyOverride=Family("Storefront");
        Assert.IsFalse(OpeningPalette.Choices(wall,OpeningKind.Door).Contains(motor));
        Assert.IsFalse(wall.TryLayout(floor.height,new List<FittedOpening>(),out why));
        wall.familyOverride=null;
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/GarageDoorPlacementTest.unity");
        try
        {
            Assert.IsTrue(EditorSceneManager.SaveScene(house.gameObject.scene,path));
            var scene=EditorSceneManager.OpenScene(path);
            var placed=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<GarageDoor>())
                .First(d=>d.Operable && d.GetComponentInParent<HouseFloor>().transform.localPosition.y==0);
            Assert.AreEqual(13,placed.FinishTile);
            Assert.That(placed.leaf.transform.localScale.x,Is.EqualTo(3.4f).Within(.001f));
            Assert.AreEqual("LoadingBay",new SerializedObject(placed.GetComponent("GarageDoorCircuitLink")).FindProperty("CircuitId").stringValue);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(path);
        }
    }

    [UnityTest]
    public IEnumerator LeafOpensReversesAndClearsCollisionWhileDecorativeDoorStaysClosed()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var door=Object.Instantiate(Insert("Operable").prefab).GetComponent<GarageDoor>();
        var decor=Object.Instantiate(Insert("Decorative").prefab,Vector3.right*10,Quaternion.identity).GetComponent<GarageDoor>();
        var header=door.header.localPosition;
        door.Configure(new Vector2(3.2f,2.4f),15,true);
        door.speed=2;door.SetOpen(true);decor.SetOpen(true);
        float timeout=Time.realtimeSinceStartup+5;
        while(door.leaf.transform.localPosition.y<.5f && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.That(door.leaf.transform.localPosition.y,Is.InRange(.5f,2.39f));
        bool Blocked(float y)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(0,y,-1),Vector3.forward,2);
        }
        Assert.IsFalse(Blocked(.2f));Assert.IsTrue(Blocked(2.2f));
        door.SetOpen(false);
        timeout=Time.realtimeSinceStartup+5;
        while(door.leaf.transform.localPosition.y>0 && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.IsTrue(Blocked(.2f));
        door.SetOpen(true);timeout=Time.realtimeSinceStartup+5;
        while(!door.IsOpen && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.IsTrue(door.IsOpen);Assert.IsFalse(Blocked(2.2f));
        Assert.AreEqual(header,door.header.localPosition);
        Assert.AreEqual(Vector3.zero,decor.leaf.transform.localPosition);
        door.SetOpen(false);timeout=Time.realtimeSinceStartup+5;
        while(door.leaf.transform.localPosition.y>0 && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.IsTrue(Blocked(1));Assert.AreEqual(15,door.FinishTile);
        yield return new ExitPlayMode();
    }

    [Test]
    public void ShaderRendersTheSelectedCellOnEachInstance()
    {
        var preview=new PreviewRenderUtility();
        var src=Insert("Decorative");
        var material=new Material(src.prefab.GetComponent<GarageDoor>().leaf.sharedMaterial);
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        Texture2D rendered=null;
        bool asyncShaders=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                int tile=(3-y/64)*4+x/64, stripe=(x%64)*6/64;
                texture.SetPixel(x,y,stripe==5 || stripe<4 && (tile&(1<<stripe))!=0 ? Color.white : Color.black);
            }
            texture.Apply();material.SetTexture("_BaseMap",texture);
            for(int i=0;i<16;i++)
            {
                var obj=Object.Instantiate(src.prefab);
                obj.transform.position=new Vector3(i%4*4, (3-i/4)*4,0);
                var door=obj.GetComponent<GarageDoor>();door.leaf.sharedMaterial=material;door.Configure(new Vector2(3,2),i,false);
                preview.AddSingleGO(obj);
            }
            var cam=preview.camera;cam.cameraType=CameraType.Game;cam.orthographic=true;cam.orthographicSize=8;cam.nearClipPlane=.1f;cam.farClipPlane=50;
            cam.transform.position=new Vector3(6,7,-15);cam.transform.rotation=Quaternion.identity;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.gray;cam.allowHDR=false;
            preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(25,15,0);preview.ambientColor=Color.gray;
            preview.BeginStaticPreview(new Rect(0,0,512,512));preview.Render(true);rendered=preview.EndStaticPreview();
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            for(int i=0;i<16;i++)
            {
                float Sample(int stripe){
                    var wp=new Vector3(i%4*4-1.5f+(stripe+.5f)*.5f,(3-i/4)*4+1,0);
                    var pixel=cam.WorldToViewportPoint(wp);
                    return rendered.GetPixel((int)(pixel.x*512),(int)(pixel.y*512)).grayscale;
                }
                float low=Sample(4),high=Sample(5);Assert.That(high-low,Is.GreaterThan(.12f),"Reference stripes, tile "+i);
                for(int bit=0;bit<4;bit++)Assert.AreEqual((i&(1<<bit))!=0,Sample(bit)>(high+low)*.5f,"Tile "+i+" bit "+bit);
            }
        }
        finally {ShaderUtil.allowAsyncCompilation=asyncShaders;preview.Cleanup();if(rendered)Object.DestroyImmediate(rendered);Object.DestroyImmediate(texture);Object.DestroyImmediate(material);}
    }

    [UnityTest]
    public IEnumerator CircuitAdapterControlsOnlyItsAssignedDoorAndOptsIntoRoomMarker()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var prefab=Insert("Operable").prefab;
        var assembly=prefab.GetComponent("GarageDoorCircuitLink").GetType().Assembly;
        var gridObject=new GameObject("Test grid");gridObject.SetActive(false);
        var grid=gridObject.AddComponent(assembly.GetType("HorrorGame.Chapter1.DistrictPowerGrid",true));
        var circuitType=assembly.GetType("HorrorGame.Chapter1.PowerCircuit",true);
        var circuit=System.Activator.CreateInstance(circuitType);
        circuitType.GetField("Id").SetValue(circuit,"Bay");
        ((IList)grid.GetType().GetField("Circuits").GetValue(grid)).Add(circuit);
        gridObject.SetActive(true);
        var door=Object.Instantiate(prefab).GetComponent<GarageDoor>();
        door.speed=20;
        var link=door.GetComponent("GarageDoorCircuitLink");
        link.GetType().GetField("Grid").SetValue(link,grid);
        link.GetType().GetField("CircuitId").SetValue(link,"Bay");
        yield return null;
        grid.GetType().GetMethod("SetCircuit").Invoke(grid,new object[]{"Bay",true});
        float timeout=Time.realtimeSinceStartup+3;
        while(!door.IsOpen && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.IsTrue(door.IsOpen);
        var roomObject=new GameObject("Map room");roomObject.SetActive(false);
        var room=roomObject.AddComponent(assembly.GetType("HorrorEngine.MapRoom",true));
        var idType=assembly.GetType("HorrorEngine.ObjectUniqueId",true);
        var id=roomObject.GetComponent(idType)??roomObject.AddComponent(idType);
        var roomData=new SerializedObject(id);roomData.FindProperty("m_Id").stringValue="test-bay";roomData.ApplyModifiedPropertiesWithoutUndo();
        link.GetType().GetField("Room").SetValue(link,room);
        var map=ScriptableObject.CreateInstance(assembly.GetType("HorrorEngine.MapData",true));
        var roomRecordType=assembly.GetType("HorrorEngine.MapRoomSerializedData",true);
        var record=System.Activator.CreateInstance(roomRecordType);
        roomRecordType.GetField("UniqueId").SetValue(record,"test-bay");
        var rooms=(IList)map.GetType().GetField("Rooms").GetValue(map);
        var tryMarker=link.GetType().GetMethod("TryMarker",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        object[] args={grid,map,null,null};
        Assert.IsFalse((bool)tryMarker.Invoke(link,args));
        link.GetType().GetField("ShowOnTablet").SetValue(link,true);
        Assert.IsFalse((bool)tryMarker.Invoke(link,args),"This room is absent from the map.");
        rooms.Add(record);
        Assert.IsTrue((bool)tryMarker.Invoke(link,args));Assert.AreEqual("test-bay",args[2]);
        grid.GetType().GetMethod("SetCircuit").Invoke(grid,new object[]{"Bay",false});
        timeout=Time.realtimeSinceStartup+3;
        while(door.leaf.transform.localPosition.y>0 && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.AreEqual(0,door.leaf.transform.localPosition.y);
        Object.Destroy(map);
        yield return new ExitPlayMode();
    }
}
