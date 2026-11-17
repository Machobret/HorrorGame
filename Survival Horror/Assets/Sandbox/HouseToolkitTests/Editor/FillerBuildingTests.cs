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

public class FillerBuildingTests
{
    const string Root="Assets/Game/Environment/FillerBuildings/";
    [SetUp] public void Open()=>EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    [TearDown] public void Close()
    {
        RoadScene.Leave();HouseScene.Leave();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
    static FillerBuildingType[] AvailableTypes()=>Enumerable.Range(0,AssetDatabase.FindAssets("t:FillerBuildingType").Length)
        .Select(RoadInspector.StartingType).Distinct().ToArray();
    static Material Finish(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");

    [Test]
    public void ThicknessResolvesThroughTheChainAndValidatesInsertsBeforeRebuild()
    {
        var plain=AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/Plain/Plain.asset");
        var complex=AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/Storefront/Storefront.asset");
        var house=HouseEditing.Create(Vector3.zero,plain);
        var floor=HouseEditing.Floors(house).Single();
        var edit=new FloorEdit(floor);
        var outline=SlabOpening.Rectangle(new Rect(0,0,8,4));
        for(int i=0;i<4;i++)edit.Draw(outline[i],outline[(i+1)%4]);
        edit.Draw(new Vector2(4,0),new Vector2(4,4));
        Assert.IsTrue(edit.Commit("Two rooms",out var error),error);
        var walls=floor.GetComponentsInChildren<HouseWall>();
        var inside=walls.Single(w=>w.interior);var outside=walls.First(w=>!w.interior);
        Assert.AreEqual(plain.thickness,inside.Thickness);
        house.exteriorThickness=.42f;house.interiorThickness=.18f;
        Assert.AreEqual(.18f,inside.Thickness);Assert.AreEqual(.42f,outside.Thickness);
        floor.interiorThickness=.21f;outside.thicknessOverride=.35f;
        Assert.AreEqual(.21f,inside.Thickness);Assert.AreEqual(.35f,outside.Thickness);
        Assert.IsTrue(HouseEditing.Refresh(house,out error),error);
        var box=outside.GetComponentInChildren<BoxCollider>();
        Assert.That(Mathf.Abs(outside.transform.InverseTransformVector(box.transform.TransformVector(Vector3.forward*box.size.z)).z),
            Is.EqualTo(.35f).Within(.001));
        var insert=ScriptableObject.CreateInstance<HouseInsert>();
        try
        {
            insert.prefab=new GameObject("Door");insert.opening=Vector2.one;insert.maximumThickness=.3f;
            var candidate=new PlacedOpening{insert=insert,distance=2};
            Assert.IsFalse(outside.TryLayout(floor.height,new List<FittedOpening>(),out error,candidate));
            outside.thicknessOverride=.25f;
            Assert.IsTrue(outside.TryLayout(floor.height,new List<FittedOpening>(),out error,candidate),error);
            outside.openings.Add(candidate);
            outside.thicknessOverride=0;
            Assert.IsFalse(new FloorEdit(floor).Prepare(out error));StringAssert.Contains("thickness",error);
            outside.openings.Clear();outside.familyOverride=complex;
            Assert.AreEqual(complex.thickness,outside.Thickness);
            outside.thicknessOverride=.8f;Assert.AreEqual(complex.thickness,outside.Thickness);
        }
        finally {Object.DestroyImmediate(insert);}
    }

    [Test]
    public void ShaderSamplesTheAssignedFamilyAndGroundFloorEntrance()
    {
        var families=new[]{"Brick","OchrePlaster","Concrete","IndustrialBrick","SageClapboard"};
        var preview=new PreviewRenderUtility();
        var palette=new Texture2D(384,384,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var shopPalette=new Texture2D(192,192,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var temporary=new List<Material>();
        Texture2D image=null;
        bool previous=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            // Six binary stripes identify the cell. The last two calibrate black and white
            // in the same bay so the check also works in gamma projects and under exposure.
            for(int y=0;y<384;y++)for(int x=0;x<384;x++)
            {
                int tile=(5-y/64)*6+x/64, stripe=x%64/8;
                bool lit=stripe==7 || stripe<6 && (tile & (1<<stripe))!=0;
                palette.SetPixel(x,y,lit ? Color.white : Color.black);
            }
            palette.Apply();
            for(int y=0;y<192;y++)for(int x=0;x<192;x++)
            {
                int tile=36+(2-y/64)*3+x/64, stripe=x%64/8;
                bool lit=stripe==7 || stripe<6 && (tile & (1<<stripe))!=0;
                shopPalette.SetPixel(x,y,lit ? Color.white : Color.black);
            }
            shopPalette.Apply();
            var buildings=new List<FillerBuilding>();
            var materials=new List<Material>();
            // The four regular cells each appear on the middle floor with these seeds.
            int[] reviewSeeds={30,37,92,115,150};
            Vector3 Origin(int i)=>new Vector3((i%3)*13+(i>=3 ? 6.5f : 0),i<3 ? 10 : 0,0);
            for(int i=0;i<families.Length;i++)
            {
                var name=families[i];
                var type=AvailableTypes().Single(t=>AssetDatabase.GetAssetPath(t).StartsWith(Root+name+"/"));
                var mat=type.facade;
                var shop=Finish(name+"/"+name+"_Storefront");
                var blank=Finish(name+"/"+name+"_Blank");
                Assert.IsTrue(mat && shop && blank);
                var go=new GameObject(name);
                var building=go.AddComponent<FillerBuilding>();
                building.SetType(type);building.seed=reviewSeeds[i];building.floors=3;
                var loop=SlabOpening.Rectangle(new Rect(0,0,12,5));
                Assert.IsTrue(GroundEditing.Change(building,"Footprint",p=>{
                    for(int j=0;j<4;j++)GroundEditing.Line(p,loop[j],loop[(j+1)%4]);
                },out var issue),issue);
                var wall=building.footprint.edges.Single(e=>e.a.y==0 && e.b.y==0);
                building.SetFinish(0,0,shop);building.SetEntrance(wall.id,1);
                go.transform.position=Origin(i);
                building.Rebuild();buildings.Add(building);preview.AddSingleGO(go);
                materials.AddRange(new[]{mat,shop,blank});
            }
            var cam=preview.camera;
            cam.orthographic=true;cam.orthographicSize=10.3f;
            cam.transform.position=new Vector3(19,9.47f,-25);cam.transform.rotation=Quaternion.identity;
            cam.nearClipPlane=.1f;cam.farClipPlane=100;cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=new Color(.16f,.17f,.18f);cam.allowHDR=false;
            preview.ambientColor=Color.gray;
            preview.lights[0].color=Color.white;preview.lights[0].intensity=1;
            preview.lights[0].transform.rotation=Quaternion.Euler(20,-15,0);preview.lights[1].intensity=0;
            Texture2D Render()
            {
                preview.BeginStaticPreview(new Rect(0,0,1680,852));
                cam.cameraType=CameraType.Game;preview.Render(true);
                return preview.EndStaticPreview();
            }
            image=Render();
            Assert.IsFalse(ShaderUtil.ShaderHasError(materials[0].shader));
            string capture=System.Environment.GetEnvironmentVariable("FILLER_FACADE_PREVIEW");
            if(!string.IsNullOrEmpty(capture))System.IO.File.WriteAllBytes(capture,image.EncodeToPNG());
            Object.DestroyImmediate(image);
            for(int i=0;i<buildings.Count;i++)
            {
                var building=buildings[i];
                int front=building.footprint.edges.Single(e=>e.a.y==0 && e.b.y==0).id;
                if(i==0)building.SetGroundStyle(front,FillerBuilding.GroundStyle.Solid);
                if(i==1)building.SetGroundStyle(front,FillerBuilding.GroundStyle.Normal);
                if(i==2)building.SetGroundStyle(front,FillerBuilding.GroundStyle.Material,buildings[4].buildingType.storefronts[0]);
                if(i==0)building.SetFinish(0,2,materials[i*3+2]);
                building.Rebuild();
                var renderer=building.GetComponent<MeshRenderer>();
                CollectionAssert.AreEquivalent(new[]{building.roof,building.buildingType.facade},renderer.sharedMaterials);
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
                    if(!materials.Contains(m))return m;
                    var copy=new Material(m);temporary.Add(copy);copy.SetTexture("_BaseMap",palette);copy.SetTexture("_DisplayMap",shopPalette);return copy;
                }).ToArray();
            }
            image=Render();
            var regularCells=new HashSet<int>();
            var familiesWithTopWindows=new HashSet<int>();
            for(int i=0;i<5;i++)
                for(int floor=0;floor<4;floor++)
                    for(int bay=0;bay<4;bay++)
                    {
                        float Sample(int stripe)
                        {
                            float offset=(stripe+.5f)/8;
                            var point=cam.WorldToViewportPoint(Origin(i)+new Vector3((bay+offset)*3,(floor==3 ? 3*2.98f-.16f : (floor+.5f)*2.98f),0));
                            return image.GetPixel(Mathf.RoundToInt(point.x*(image.width-1)),Mathf.RoundToInt(point.y*(image.height-1))).grayscale;
                        }
                        float black=Sample(6),white=Sample(7);
                        Assert.That(white-black,Is.GreaterThan(.2f),"The diagnostic strips must render on "+families[i]);
                        int tile=0;
                        for(int bit=0;bit<6;bit++)if(Sample(bit)>(black+white)*.5f)tile|=1<<bit;
                        if(floor==0 && i==0)Assert.AreEqual(0,tile,"Solid includes the reserved entrance bay");
                        else if(floor==0 && i==2)Assert.AreEqual(bay==1 ? 4*6+5 : 36+4,tile,"Chosen storefront family");
                        else if(floor==0 && bay==1)Assert.AreEqual(i*6+5,tile,"Door cell for "+families[i]);
                        else if(floor==0 && i==1)Assert.That(tile,Is.InRange(i*6,i*6+4),"Normal overrides the floor storefront");
                        else if(floor==0)Assert.AreEqual(36+i,tile,"Display window");
                        else if(floor==3)Assert.AreEqual(30+i,tile,"Roof coping");
                        else if(floor==2)
                        {
                            if(i==0)Assert.AreEqual(0,tile,"Blank override below coping");
                            else
                            {
                                Assert.That(tile,Is.InRange(i*6,i*6+4));
                                if(tile>i*6)familiesWithTopWindows.Add(i);
                            }
                        }
                        else {Assert.That(tile,Is.InRange(i*6+1,i*6+4));regularCells.Add(tile);}
                    }
            Assert.AreEqual(4,familiesWithTopWindows.Count,"Windowed top floors keep their windows below the coping.");
            Assert.AreEqual(20,regularCells.Count,"The review must show every regular window in all five families.");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation=previous;if(image)Object.DestroyImmediate(image);
            preview.Cleanup();foreach(var mat in temporary)Object.DestroyImmediate(mat);Object.DestroyImmediate(palette);Object.DestroyImmediate(shopPalette);
        }
    }

    [Test]
    public void TownSharesOneMaterialAndDepotUsesSquareAtlasAndTallerLoadingBays()
    {
        var choices=AvailableTypes().Where(t=>AssetDatabase.GetAssetPath(t).StartsWith(Root)).ToArray();
        Assert.That(choices.Length,Is.GreaterThanOrEqualTo(6));
        var town=Finish("TownFacades_Shared");
        CollectionAssert.IsSubsetOf(new[]{"Red Brick","Ochre Plaster","Concrete","Industrial Brick","Sage Clapboard"},choices.Where(t=>t.facade==town).Select(t=>t.name).ToArray());
        var depot=choices.Single(t=>t.name=="Industrial Depot");
        foreach(var texture in choices.Select(t=>t.facade.mainTexture).Concat(new[]{town.GetTexture("_DisplayMap")}))
            Assert.AreEqual(texture.width,texture.height,texture.name);
        var b=new GameObject("Depot").AddComponent<FillerBuilding>();
        b.SetType(depot);b.floors=16;
        var loop=SlabOpening.Rectangle(new Rect(0,0,18,12));
        Assert.IsTrue(GroundEditing.Change(b,"Draw depot",p=>{
            for(int i=0;i<4;i++)GroundEditing.Line(p,loop[i],loop[(i+1)%4]);
        },out var issue),issue);
        Assert.AreEqual(6,b.bayWidth);
        var garage=b.entrances.Single();b.SetEntrance(garage.wall,-1);b.Rebuild();
        Assert.AreEqual(-1,b.EntranceBay(garage.wall),"Removed loading doors stay removed.");
        Assert.That(b.Elevation(1),Is.EqualTo(5).Within(.001));
        Assert.That(b.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,Is.EqualTo(5+15*2.98f).Within(.001));
        Assert.AreSame(depot.facade,b.DefaultFacade);
        var shop=choices.Single(t=>t.name=="Industrial Machine Shop");
        b.SetType(shop);
        Assert.AreSame(depot.facade,b.MaterialFor(1,0));
        Assert.AreSame(town,b.MaterialFor(1,1));
        Assert.That(b.Elevation(1),Is.EqualTo(4.5f).Within(.001f));
        b.SetFinish(1,1,depot.facade);
        Assert.AreSame(depot.facade,b.MaterialFor(1,1));
    }
    [TestCase("Red Brick")]
    [TestCase("Industrial Depot")]
    public void GroundStyleFollowsWallAcrossSplitsAndRestoresItsEntrance(string typeName)
    {
        var types=AvailableTypes();
        var type=types.Single(t=>t.name==typeName);
        var plaster=types.Single(t=>t.name=="Ochre Plaster");
        var shop=plaster.storefronts.Single();
        var building=new GameObject("Ground-floor choices").AddComponent<FillerBuilding>();
        building.SetType(type);
        building.floors=2;
        var loop=SlabOpening.Rectangle(new Rect(0,0,18,12));
        Assert.IsTrue(GroundEditing.Change(building,"Draw footprint",plan=>{
            for(int i=0;i<4;i++)GroundEditing.Line(plan,loop[i],loop[(i+1)%4]);
        },out var issue),issue);
        var wall=building.footprint.edges[0];
        int other=building.footprint.edges[1].id;
        building.SetFinish(0,0,shop);
        building.SetEntrance(wall.id,1);
        void Check(FillerBuilding.GroundStyle style,Material ground,int mode,bool entrance)
        {
            building.SetGroundStyle(wall.id,style,style==FillerBuilding.GroundStyle.Material ? shop : null);
            building.Rebuild();
            Assert.AreSame(ground,building.MaterialFor(wall.id,0));
            Assert.AreSame(type.facade,building.MaterialFor(wall.id,1));
            Assert.AreSame(shop,building.MaterialFor(other,0));
            var mesh=building.GetComponent<MeshFilter>().sharedMesh;
            var uv=new List<Vector4>();mesh.GetUVs(5,uv);
            var colors=mesh.colors32;
            var selected=Enumerable.Range(0,uv.Count).Where(i=>uv[i].x==wall.id && uv[i].y==0).ToArray();
            Assert.IsNotEmpty(selected);
            Assert.AreEqual(entrance,selected.Any(i=>uv[i].z<0));
            Assert.IsTrue(selected.All(i=>(colors[i].r==255 ? ground.GetFloat("_BayType") : colors[i].g)==mode));
            if(type.facade==plaster.facade)
                CollectionAssert.DoesNotContain(building.GetComponent<MeshRenderer>().sharedMaterials,shop);
        }
        Check(FillerBuilding.GroundStyle.Solid,type.facade,1,false);
        Check(FillerBuilding.GroundStyle.Normal,type.facade,0,true);
        Check(FillerBuilding.GroundStyle.Material,shop,2,true);
        Check(FillerBuilding.GroundStyle.Inherit,shop,2,true);
        Undo.IncrementCurrentGroup();
        RoadInspector.SetGroundStyle(building,new[]{wall.id,other},FillerBuilding.GroundStyle.Solid);
        Undo.FlushUndoRecordObjects();
        Assert.AreSame(type.facade,building.MaterialFor(other,0));
        Undo.PerformUndo();
        Assert.AreSame(shop,building.MaterialFor(wall.id,0));
        Assert.AreSame(shop,building.MaterialFor(other,0));
        Undo.PerformRedo();
        Assert.AreEqual(FillerBuilding.GroundStyle.Solid,building.FinishAt(other,0).groundStyle);
        int child=building.footprint.nextId;
        Undo.IncrementCurrentGroup();
        Assert.IsTrue(GroundEditing.InsertPoint(building,wall.id,(wall.a+wall.b)*.5f,out _,out issue),issue);
        Assert.AreEqual(FillerBuilding.GroundStyle.Solid,building.FinishAt(child,0).groundStyle);
        Undo.PerformUndo();
        Assert.IsNull(building.FinishAt(child,0));
        Assert.AreEqual(FillerBuilding.GroundStyle.Solid,building.FinishAt(wall.id,0).groundStyle);
        Undo.PerformRedo();
        Assert.AreEqual(FillerBuilding.GroundStyle.Solid,building.FinishAt(child,0).groundStyle);
    }

    [UnityTest]
    public IEnumerator DrawFootprintPickWholeWallsAndOverrideOneFloor()
    {
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;
        try
        {
            view.Show();view.position=new Rect(80,80,950,710);view.orthographic=true;
            view.LookAtDirect(new Vector3(5,0,3),Quaternion.LookRotation(Vector3.down,Vector3.forward),11);
            Tools.current=Tool.Move;view.Focus();view.Repaint();
            yield return null;yield return null;
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/Environment/Filler Building"));
            void Click(float x,float z)=>PavementEditingTests.ClickScene(view,new Vector3(x,0,z));
            Click(0,0);Click(12,0);Click(12,6);Click(6,6);Click(6,9);Click(0,9);Click(0,0);
            var building=Selection.activeGameObject.GetComponent<FillerBuilding>();
            Assert.IsNotNull(building);Assert.AreEqual(1,building.footprint.faces.Count);
            Assert.IsTrue(building.DefaultFacade && building.roof);
            Assert.IsFalse(RoadScene.Drawing);
            var front=building.footprint.edges.First(e=>e.a.y==0 && e.b.y==0);
            Assert.AreSame(RoadInspector.StartingType(building.seed),building.buildingType);
            var placedType=building.buildingType;
            var initialEntrances=building.entrances.Count;
            Assert.AreEqual(Mathf.RoundToInt(12/building.bayWidth),building.Bays(front));
            var glass=Finish("OchrePlaster/OchrePlaster_Storefront");
            var timber=Finish("SageClapboard/SageClapboard_Facade");
            var solid=Finish("IndustrialBrick/IndustrialBrick_Blank");
            Assert.IsTrue(glass && timber && solid);
            building.transform.rotation=Quaternion.Euler(0,29,0);
            var t=building.transform;
            view.LookAtDirect(t.TransformPoint(new Vector3(6,4,1)),Quaternion.LookRotation(t.forward,Vector3.up),9);
            view.Repaint();yield return null;yield return null;
            PavementEditingTests.ClickScene(view,t.TransformPoint(new Vector3(3,5,0)));
            Assert.AreEqual(RoadPart.Boundary,RoadScene.Selected.part);Assert.AreEqual(front.id,RoadScene.Selected.id);
            Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(building,"Edit façades");
            building.SetFinish(front.id,-1,solid);building.SetFinish(0,0,timber);building.SetFinish(front.id,0,glass);
            building.SetEntrance(front.id,1);building.Rebuild();
            Assert.AreSame(glass,building.MaterialFor(front.id,0));
            Assert.AreSame(timber,building.MaterialFor(building.footprint.edges.First(e=>e.id!=front.id).id,0));
            Assert.AreSame(solid,building.MaterialFor(front.id,1));
            Assert.AreEqual(1,building.GetComponentsInChildren<MeshRenderer>().Length);
            var mesh=building.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size.y,Is.EqualTo(building.Elevation(3)).Within(.001f));
            var bays=new List<Vector4>();mesh.GetUVs(5,bays);
            Assert.AreEqual(4,bays.Count(v=>v.x==front.id && v.y==0 && v.z==-2));
            Assert.IsTrue(building.GetComponent<MeshRenderer>().sharedMaterials.Contains(building.DefaultFacade));
            Undo.PerformUndo();Assert.IsEmpty(building.finishes);Assert.AreEqual(initialEntrances,building.entrances.Count);
            Undo.PerformRedo();Assert.AreSame(glass,building.MaterialFor(front.id,0));
            var path=AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/FillerRestoreTest.unity");
            try
            {
                EditorSceneManager.SaveScene(building.gameObject.scene,path);
                var restored=EditorSceneManager.OpenScene(path).GetRootGameObjects().Select(g=>g.GetComponent<FillerBuilding>()).Single(b=>b);
                Assert.AreSame(placedType,restored.buildingType);
                Assert.AreSame(glass,restored.MaterialFor(front.id,0));Assert.AreEqual(1,restored.EntranceBay(front.id));
                var data=new List<Vector4>();restored.GetComponent<MeshFilter>().sharedMesh.GetUVs(5,data);
                CollectionAssert.AreEqual(bays,data);
            }
            finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(path);}
        }
        finally {view.Close();Tools.current=oldTool;}
    }
}
