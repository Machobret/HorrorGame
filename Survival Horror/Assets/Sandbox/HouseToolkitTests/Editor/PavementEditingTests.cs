using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PavementEditingTests
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly Type Scene = typeof(HouseEditing).Assembly.GetType("HouseToolkit.Editor.RoadScene");
    RoadNetwork road;
    static object Call(string name, params object[] args) => Scene.GetMethod(name,Flags).Invoke(null,args);

    [SetUp]
    public void Open()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        road = new GameObject("Pavement").AddComponent<RoadNetwork>();
        road.transform.SetPositionAndRotation(new Vector3(4,3,2),Quaternion.Euler(0,31,0));
        road.family=AssetDatabase.LoadAssetAtPath<RoadFamily>("Assets/Game/Environment/Roads/TownStreet/Town Street.asset");
        Call("Enter",road);
    }

    [TearDown]
    public void Close()
    {
        Call("Leave");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }

    static void Brush(string name) => Call("SetBrush",Enum.Parse(Scene.GetNestedType("Brush",BindingFlags.NonPublic),name));
    static void Place(float x,float z)
    {
        var point = new Vector2(x,z);
        Call("Place",point,(bool)Call("OnPavement",point));
        Assert.IsNull(Scene.GetField("Message",Flags).GetValue(null));
    }

    [Test]
    public void CloseAppendAndDivideGroundWithSharedEdgesAndUndo()
    {
        Brush("Ground");
        Place(0,0);Place(4,0);Place(4,4);Place(0,4);
        Assert.IsEmpty(road.ground.faces);
        Place(0,0);
        var original = road.ground.faces.Single();
        Assert.AreEqual(6,original.triangles.Length);
        original.material=road.family.road;
        original.areaMaterial=road.family.sidewalk;
        Place(4,0); Place(8,0); Place(8,4); Place(4,4);
        Assert.AreEqual(2,road.ground.faces.Count);
        Assert.AreEqual(7,road.ground.edges.Count);
        Assert.That(road.ground.faces.All(f=>f.areaId==original.areaId && f.areaMaterial==road.family.sidewalk));
        Assert.IsNull(road.ground.faces.Single(f=>f.id!=original.id).material);
        Undo.IncrementCurrentGroup();
        Place(2,0); Place(2,4);
        Assert.AreEqual(3,road.ground.faces.Count);
        Assert.AreEqual(2,road.ground.faces.Count(f=>f.material==road.family.road));
        var mesh=road.GetComponent<MeshCollider>().sharedMesh;
        Assert.That(mesh.bounds.size.y,Is.LessThan(.0001f));
        Assert.That(mesh.vertices.All(v=>Mathf.Abs(v.y-road.PavementElevation)<.0001f));
        Assert.AreEqual(3,road.faces.Select(h=>h.id).Distinct().Count());
        Undo.PerformUndo();
        Assert.AreEqual(2,road.ground.faces.Count);
        Undo.PerformRedo();
        Assert.AreEqual(3,road.ground.faces.Count);
    }

    [UnityTest]
    public IEnumerator SceneViewDrawsConstrainedSidewalkAndConcaveGroundOnRotatedNetwork()
    {
        var view=ScriptableObject.CreateInstance<SceneView>();
        float oldGrid=PlanSnapping.Grid;var oldTool=Tools.current;
        try
        {
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            var rotation=Quaternion.LookRotation(-road.transform.up,road.transform.forward);
            view.LookAtDirect(road.transform.TransformPoint(new Vector3(4,0,4)),rotation,9);
            PlanSnapping.Grid=.25f;Tools.current=Tool.Move;view.Focus();view.Repaint();
            yield return null;yield return null;

            void Click(float x,float z,bool shift=false,bool alt=false) =>
                ClickScene(view,road.transform.TransformPoint(new Vector3(x,road.PavementElevation,z)),shift,alt);
            Brush("Sidewalk");
            Click(0,0);Click(4,3.55f,true);
            Assert.AreEqual(1,road.paths.spans.Count);
            var end=road.paths.nodes.Last().position;
            Assert.That(end.x,Is.EqualTo(end.z).Within(.001),"Shift must stay on the rotated network diagonal.");
            Assert.That(end.x*4,Is.EqualTo(Mathf.Round(end.x*4)).Within(.001));
            Click(6.13f,4.17f,alt:true);
            Assert.That(road.paths.nodes.Last().position.x,Is.EqualTo(6.13f).Within(.05));
            Brush("Ground");
            Click(0,6);Click(6,6);Click(6,8);Click(2,8);Click(2,10);Click(0,10);Click(0,6);
            var face=road.ground.faces.Single();
            var mesh=road.GetComponent<MeshCollider>().sharedMesh;
            var indices=mesh.triangles;var vertices=mesh.vertices;
            float area=0;
            for(int i=0;i<road.faces.Length;i++)
                if(road.faces[i].part==RoadPart.Ground && road.faces[i].id==face.id)
                    area+=Vector3.Cross(vertices[indices[i*3+1]]-vertices[indices[i*3]],
                        vertices[indices[i*3+2]]-vertices[indices[i*3]]).magnitude*.5f;
            Assert.That(area,Is.EqualTo(16).Within(.01));
        }
        finally {view.Close();PlanSnapping.Grid=oldGrid;Tools.current=oldTool;}
    }

    [UnityTest]
    public IEnumerator GroundPointsAndSharedEdgesMoveWithShiftInSceneView()
    {
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;
        float oldGrid=PlanSnapping.Grid;
        try
        {
            var area=new GameObject("Ground").AddComponent<GroundArea>();
            area.transform.SetPositionAndRotation(road.transform.position,road.transform.rotation);
            RoadScene.Enter(area);Brush("Ground");
            Place(0,0);Place(4,0);Place(4,4);Place(0,4);Place(0,0);
            Place(4,0);Place(8,0);Place(8,4);Place(4,4);
            var first=area.ground.faces[0];first.material=road.family.sidewalk;
            Brush("Select");
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            view.LookAtDirect(area.transform.TransformPoint(new Vector3(4,0,2)),
                Quaternion.LookRotation(-area.transform.up,area.transform.forward),9);
            Tools.current=Tool.Move;PlanSnapping.Grid=.25f;view.Focus();view.Repaint();
            yield return null;yield return null;
            Vector3 World(float x,float z)=>area.transform.TransformPoint(new Vector3(x,0,z));
            ClickScene(view,World(2,2));
            Assert.AreEqual(RoadPart.Ground,RoadScene.Selected.part);
            ClickScene(view,World(4,0));
            Assert.AreEqual(RoadPart.GroundPoint,RoadScene.Selected.part);
            Assert.IsTrue(RoadScene.Frame(out var bounds));
            Assert.That(Vector3.Distance(bounds.center,World(4,0)),Is.LessThan(.001));
            Drag(view,World(4,0),World(5,.18f));
            var corner=new Vector2(5,0);
            Assert.AreEqual(3,area.ground.edges.Count(e=>e.a==corner || e.b==corner),"All three incident edges follow the constrained point. "+string.Join(", ",area.ground.edges.Select(e=>$"{e.a} to {e.b}")));
            ClickScene(view,World(4.5f,2));
            Assert.AreEqual(RoadPart.Boundary,RoadScene.Selected.part);
            int id=RoadScene.Selected.id;
            Undo.IncrementCurrentGroup();
            Drag(view,World(4.5f,2),World(5.5f,2.1f));
            GroundEdge Shared()=>area.ground.edges.Single(e=>e.id==id);
            CollectionAssert.AreEquivalent(new[]{new Vector2(6,0),new Vector2(5,4)},new[]{Shared().a,Shared().b});
            Assert.AreEqual(2,area.ground.faces.Count);
            Assert.AreSame(first.material,area.ground.faces.Single(f=>f.id==first.id).material);
            Undo.PerformUndo();
            CollectionAssert.AreEquivalent(new[]{corner,new Vector2(4,4)},new[]{Shared().a,Shared().b});
            Undo.PerformRedo();
            Assert.AreEqual(3,area.ground.edges.Count(e=>e.a==new Vector2(6,0) || e.b==new Vector2(6,0)));
        }
        finally {view.Close();Tools.current=oldTool;PlanSnapping.Grid=oldGrid;}
    }

    [UnityTest]
    public IEnumerator ShiftDragConstrainsUnselectedRoadAndSidewalkPoints()
    {
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;float oldGrid=PlanSnapping.Grid;
        try
        {
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            view.LookAtDirect(road.transform.TransformPoint(new Vector3(3,0,2)),
                Quaternion.LookRotation(-road.transform.up,road.transform.forward),9);
            Tools.current=Tool.Move;PlanSnapping.Grid=.25f;view.Focus();view.Repaint();
            yield return null;yield return null;
            foreach(bool path in new[]{false,true})
            {
                float z=path ? 4 : 0;
                Brush(path ? "Sidewalk" : "Road"); Place(0,z);Place(5,z);Brush("Select");
                Vector3 World(float x,float y)=>road.transform.TransformPoint(new Vector3(x,path ? road.PavementElevation : 0,y));
                Drag(view,World(5,z),World(6.1f,z+.95f),true);
                var graph=path?road.paths:road.graph;
                var point=graph.nodes.Last().position;
                Assert.That(point.x-5,Is.EqualTo(point.z-z).Within(.001f));
                Assert.That(point.x,Is.EqualTo(6).Within(.001f));
            }
        }
        finally {view.Close();Tools.current=oldTool;PlanSnapping.Grid=oldGrid;}
    }

    [UnityTest]
    public IEnumerator HouseAndSlabPointsUseTheSameLocalShiftDirections()
    {
        RoadScene.Leave();
        var family=AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/Plain/Plain.asset");
        var house=HouseEditing.Create(road.transform.position,family);
        house.transform.rotation=road.transform.rotation;
        var floor=HouseEditing.Floors(house).Single();
        var edit=new FloorEdit(floor);
        var outline=SlabOpening.Rectangle(new Rect(0,0,8,6));
        for(int i=0;i<4;i++)edit.Draw(outline[i],outline[(i+1)%4]);
        Assert.IsTrue(edit.Commit("Draw room",out var issue),issue);
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;float oldGrid=PlanSnapping.Grid;
        try
        {
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            HouseScene.Enter(floor);Tools.current=Tool.Move;PlanSnapping.Grid=.25f;
            view.LookAtDirect(floor.World(new Vector2(4,3)),Quaternion.LookRotation(-floor.transform.up,floor.transform.forward),9);
            view.Focus();view.Repaint();yield return null;yield return null;
            Drag(view,floor.World(Vector2.zero),floor.World(new Vector2(-1,-.2f)),true);
            Assert.IsTrue(floor.corners.Any(c=>c.point==new Vector2(-1,0)),"Wall endpoint should follow local X.");
            Assert.IsTrue(SlabBuilder.SetOpening(floor,false,0,SlabOpening.Rectangle(new Rect(2,2,3,2)),out issue),issue);
            HouseScene.ToggleOpening(false,false,0);
            Drag(view,floor.World(new Vector2(2,2)),floor.World(new Vector2(1.1f,1)),true);
            Assert.IsTrue(floor.floorOpenings[0].points.Contains(new Vector2(1,1)),"Slab outline point should follow the local diagonal.");
        }
        finally {HouseScene.Leave();view.Close();Tools.current=oldTool;PlanSnapping.Grid=oldGrid;}
    }

    internal static void Drag(SceneView view,Vector3 start,Vector3 end,bool shiftOnPress=false,bool shiftOnDrag=true)
    {
        var a=Pixel(view,start);var b=Pixel(view,end);
        foreach(var type in new[]{EventType.MouseMove,EventType.Layout,EventType.MouseDown})
            view.SendEvent(new Event{type=type,button=0,clickCount=1,mousePosition=a,shift=shiftOnPress});
        for(int i=1;i<=4;i++)
            view.SendEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=Vector2.Lerp(a,b,i/4f),delta=(b-a)/4,shift=shiftOnDrag});
        view.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=b,shift=shiftOnDrag});
    }

    static Vector2 Pixel(SceneView view,Vector3 world)
    {
        Vector2 pixel=default;
        void Locate(SceneView v)
        {
            if(v==view && Event.current.commandName=="LocatePavement")
                pixel=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(world))-view.position.position;
        }
        SceneView.duringSceneGui+=Locate;
        try {view.SendEvent(EditorGUIUtility.CommandEvent("LocatePavement"));}
        finally {SceneView.duringSceneGui-=Locate;}
        return pixel;
    }

    internal static void ClickScene(SceneView view,Vector3 world,bool shift=false,bool alt=false,int clicks=1)
    {
        var pixel=Pixel(view,world);
        foreach(var type in new[]{EventType.MouseMove,EventType.Layout,EventType.MouseDown,EventType.MouseUp})
            view.SendEvent(new Event{type=type,button=0,clickCount=clicks,mousePosition=pixel,shift=shift,alt=alt});
    }

    [UnityTest]
    public IEnumerator DoubleClickInsertsFillerFootprintPointFromOutlineAndFacade()
    {
        var building=new GameObject("Rotated filler").AddComponent<FillerBuilding>();
        building.transform.SetPositionAndRotation(road.transform.position,road.transform.rotation);
        RoadScene.Enter(building);
        var loop=SlabOpening.Rectangle(new Rect(0,0,12,6));
        Assert.IsTrue(RoadScene.ChangeGround("Draw footprint",plan=>{
            for(int i=0;i<4;i++) GroundEditing.Line(plan,loop[i],loop[(i+1)%4]);
        }),RoadScene.Message);
        var host=building.footprint.edges.Single(e=>e.a.y==0 && e.b.y==0);
        Vector2 DoorCenter(GroundEdge edge,int bay)
        {
            var polygon=building.footprint.faces.Single().polygon;
            bool forward=polygon[(Array.IndexOf(polygon,edge.a)+1)%polygon.Length]==edge.b;
            return Vector2.Lerp(forward?edge.a:edge.b,forward?edge.b:edge.a,(bay+.5f)/building.Bays(edge));
        }
        var originalDoor=DoorCenter(host,3);
        building.SetEntrance(host.id,3);
        building.SetFinish(host.id,-1,road.family.sidewalk);
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;
        try
        {
            view.Show();view.position=new Rect(80,80,960,720);view.Focus();view.orthographic=true;Tools.current=Tool.Move;
            foreach(bool facade in new[]{false,true})
            {
                var at=building.transform.TransformPoint(new Vector3(6,facade?2:0,0));
                view.LookAtDirect(at,facade ? building.transform.rotation :
                    Quaternion.LookRotation(-building.transform.up,building.transform.forward),8);
                view.Repaint();yield return null;yield return null;
                ClickScene(view,at);
                Assert.AreEqual(RoadPart.Boundary,RoadScene.Selected.part);
                Undo.IncrementCurrentGroup();
                ClickScene(view,at,clicks:2);
                Assert.AreEqual(RoadPart.GroundPoint,RoadScene.Selected.part,RoadScene.Message);
                Assert.AreEqual(5,building.footprint.edges.Count);
                Assert.AreEqual(1,building.footprint.faces.Count);
                var split=building.footprint.edges.Where(e=>e.a.y==0 && e.b.y==0).ToArray();
                Assert.AreEqual(2,split.Length);
                Assert.That(split.All(e=>Mathf.Abs((e.b-e.a).magnitude-6)<.05f && building.Override(e.id,-1)==road.family.sidewalk));
                Assert.AreEqual(1,building.entrances.Count);
                var door=building.entrances.Single();
                var side=split.Single(e=>e.id==door.wall);
                Assert.That(Vector2.Distance(DoorCenter(side,door.bay),originalDoor),Is.LessThan(.05f));
                Undo.PerformUndo();
                Assert.AreEqual(4,building.footprint.edges.Count);
                Assert.AreEqual(3,building.EntranceBay(host.id));
                Undo.PerformRedo();Assert.AreEqual(5,building.footprint.edges.Count);
                Undo.PerformUndo();RoadScene.Pick(default);
            }
        }
        finally {Tools.current=oldTool;view.Close();}
    }

    [Test]
    public void DrawingGuidesHoldUntilReleaseAndRespectTopologyShiftAndAlt()
    {
        var guides=new DrawingGuides();
        var a=new Vector2(.13f,.17f);var b=a+Vector2.right*40;var c=b+Vector2.up*25;
        var nodes=new[]{a,b,c};var edges=new[]{(a,b),(b,c)};
        Vector2 Snap(Vector2 p,bool shift=false,bool alt=false)=>PlanSnapping.Snap(p,alt,shift?c:(Vector2?)null,.25f,
            nodes,edges,(x,y)=>Vector2.Distance(x,y)*20,guides,c);
        Assert.AreEqual(new Vector2(a.x,c.y),Snap(new Vector2(a.x+.3f,c.y+.25f)));
        Assert.AreEqual(new Vector2(a.x,c.y),Snap(new Vector2(a.x+.6f,c.y+.6f)));
        Assert.AreNotEqual(a.x,Snap(new Vector2(a.x+1,c.y+1)).x);
        Assert.AreEqual(new Vector2(a.x,c.y),Snap(new Vector2(a.x+.22f,c.y+.3f),true));
        var raw=new Vector2(a.x+.2f,c.y+.2f);Assert.AreEqual(raw,Snap(raw,alt:true));
        Assert.AreEqual(a,Snap(a+Vector2.one*.08f));
        // A non-cardinal reference can also supply a parallel direction.
        var d=new Vector2(4,2);var end=c+d*3;
        var snapped=PlanSnapping.Snap(end+new Vector2(.05f,-.06f),false,null,0,nodes,new[]{(a,a+d)},
            (x,y)=>Vector2.Distance(x,y)*20,guides,c);
        Assert.That(PlanSnapping.Cross(snapped-c,d),Is.EqualTo(0).Within(.0001));
    }

    [UnityTest]
    public IEnumerator RotatedHouseClosesOffGridRectangleUsingDistantCornerGuides()
    {
        var family=AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/Plain/Plain.asset");
        var house=HouseEditing.Create(road.transform.position,family);
        var floor=HouseEditing.Floors(house).Single();
        house.transform.rotation=Quaternion.Euler(0,31,0);
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;float oldGrid=PlanSnapping.Grid;
        try
        {
            HouseScene.Enter(floor);HouseScene.SetMode(HouseTool.Wall);
            view.Show();view.position=new Rect(80,80,960,720);view.orthographic=true;
            view.LookAtDirect(floor.World(new Vector2(15,8)),Quaternion.LookRotation(-floor.transform.up,floor.transform.forward),26);
            Tools.current=Tool.Move;PlanSnapping.Grid=.25f;view.Focus();view.Repaint();
            yield return null;yield return null;
            void Click(Vector2 point,bool alt=false)=>ClickScene(view,floor.World(point),alt:alt);
            var a=new Vector2(.13f,.17f);Click(a,true);
            Click(new Vector2(30,a.y+.09f));Click(new Vector2(30.08f,16));Click(new Vector2(a.x+.16f,16.12f));Click(a);
            Assert.AreEqual(4,floor.corners.Count);
            var corners=floor.corners.Select(n=>n.point).ToArray();
            var bottom=corners.OrderBy(n=>n.y).Take(2).ToArray();
            var left=corners.OrderBy(n=>n.x).Take(2).ToArray();
            Assert.That(bottom[0].y,Is.EqualTo(bottom[1].y).Within(.001));
            Assert.That(left[0].x,Is.EqualTo(left[1].x).Within(.001));
            Assert.AreEqual(4,floor.GetComponentsInChildren<HouseWall>().Length);
        }
        finally {HouseScene.Leave();view.Close();Tools.current=oldTool;PlanSnapping.Grid=oldGrid;}
    }

    [TestCase(typeof(GroundArea),"Ground Area")]
    [TestCase(typeof(FillerBuilding),"Draw footprint")]
    public void PerimeterToolShowsOneCaption(Type surfaceType,string caption)
    {
        var surface=(ProceduralSurface)new GameObject("Perimeter").AddComponent(surfaceType);
        RoadScene.Enter(surface);
        var button=new HouseToolsOverlay().CreatePanelContent().Q<Button>("Ground Area");
        Assert.AreEqual(DisplayStyle.Flex,button.style.display.value);
        Assert.IsTrue(string.IsNullOrEmpty(button.text),"The button paints its icon and child label.");
        Assert.AreEqual(caption,button.Query<Label>().ToList().Single().text);
    }

    [UnityTest]
    public IEnumerator CreateStandaloneSidewalkAndGroundFromEmptyScene()
    {
        RoadScene.Leave();
        UnityEngine.Object.DestroyImmediate(road.gameObject);
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/EnvironmentPlacementTest.unity");
        try
        {
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            view.LookAtDirect(new Vector3(3,0,3),Quaternion.LookRotation(Vector3.down,Vector3.forward),9);
            Tools.current=Tool.Move;view.Focus();view.Repaint();
            yield return null;yield return null;
            Assert.IsTrue(view.TryGetOverlay("house-toolkit-tools",out var overlay));
            Assert.AreEqual("Environment Tools",overlay.displayName);
            var palette=overlay.CreatePanelContent();
            foreach(var name in new[]{"House","Wall","Road","Sidewalk","GroundArea"})
                Assert.IsNotNull(palette.Q<Button>("create-"+name));
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/Environment/Sidewalk"));
            view.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
            Assert.IsNull(EnvironmentPlacementTool.Pending);
            Assert.IsEmpty(UnityEngine.Object.FindObjectsByType<ProceduralSurface>(FindObjectsSortMode.None));

            void Click(float x,float z)=>ClickScene(view,new Vector3(x,0,z));
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/Environment/Standalone Wall"));
            Click(1,1);Click(5,1);
            var standalone=HouseScene.House;
            Assert.IsTrue(standalone && standalone.wallsOnly);
            Assert.AreEqual(1,standalone.GetComponentsInChildren<HouseWall>().Length);
            HouseScene.Leave();
            UnityEngine.Object.DestroyImmediate(standalone.gameObject);
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/Environment/Sidewalk"));
            Click(1,1);Click(5,1);
            var walk=Selection.activeGameObject.GetComponent<ProceduralSidewalk>();
            Assert.IsNotNull(walk);
            var defaultPavement=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Environment/Roads/TownStreet/Sidewalk.mat");
            Assert.IsTrue(defaultPavement);Assert.AreSame(defaultPavement,walk.material);
            Assert.That(Vector3.Distance(walk.transform.position,new Vector3(1,0,1)),Is.LessThan(.01));
            Assert.AreEqual(1,walk.paths.spans.Count);
            walk.width=3;walk.Rebuild();
            Assert.That(walk.GetComponent<MeshCollider>().sharedMesh.bounds.size.z,Is.EqualTo(3).Within(.01));

            RoadScene.Leave();
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/Environment/Ground Area"));
            Click(1,4);Click(5,4);Click(5,7);Click(1,7);Click(1,4);
            var area=Selection.activeGameObject.GetComponent<GroundArea>();
            Assert.IsNotNull(area);Assert.AreSame(defaultPavement,area.material);Assert.AreEqual(1,area.ground.faces.Count);
            Assert.AreEqual("Ground Areas",area.transform.parent.name);Assert.AreEqual("Sidewalks",walk.transform.parent.name);
            Assert.IsEmpty(UnityEngine.Object.FindObjectsByType<RoadNetwork>(FindObjectsSortMode.None));
            RoadScene.Leave();
            Assert.IsTrue(EditorSceneManager.SaveScene(area.gameObject.scene,path));
            var saved=EditorSceneManager.OpenScene(path);
            area=saved.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<GroundArea>()).Single();
            walk=saved.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProceduralSidewalk>()).Single();
            Assert.AreEqual(1,area.ground.faces.Count);Assert.AreEqual(3,walk.width);
            RoadScene.Enter(walk); Assert.IsTrue(RoadScene.Active==walk);
            Assert.IsTrue(walk.GetComponent<MeshCollider>().sharedMesh);
        }
        finally
        {
            if(EnvironmentPlacementTool.Pending)EnvironmentPlacementTool.Cancel();
            RoadScene.Leave();view.Close();Tools.current=oldTool;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(path);
        }
    }

    [Test]
    public void SeparatingLegacyPavementRetainsWorldPositionMaterialsAndUndo()
    {
        Brush("Sidewalk");Place(0,0);Place(5,0);
        Brush("Ground");Place(0,1);Place(4,1);Place(4,5);Place(0,5);Place(0,1);
        var world=road.transform.TransformPoint(Vector3.up*road.PavementElevation);
        int id=road.paths.spans.Single().id;
        road.pavementWidth=3;road.pavementMaterial=road.family.sidewalk;
        RoadEditing.SeparatePavement(road);
        var walk=UnityEngine.Object.FindFirstObjectByType<ProceduralSidewalk>();
        var ground=UnityEngine.Object.FindFirstObjectByType<GroundArea>();
        Assert.AreEqual(world,walk.transform.position);Assert.AreEqual(road.transform.rotation,ground.transform.rotation);
        Assert.AreEqual(id,walk.paths.spans.Single().id);Assert.AreEqual(3,walk.width);
        Assert.AreEqual(road.family.sidewalk,walk.material);Assert.AreEqual(1,ground.ground.faces.Count);
        Assert.IsEmpty(road.paths.spans);Assert.IsEmpty(road.ground.faces);
        Undo.PerformUndo();Assert.AreEqual(1,road.paths.spans.Count);Assert.AreEqual(1,road.ground.faces.Count);
        Assert.IsFalse(walk);Assert.IsFalse(ground);
        Undo.PerformRedo();Assert.IsEmpty(road.paths.spans);
        Assert.AreEqual(id,UnityEngine.Object.FindFirstObjectByType<ProceduralSidewalk>().paths.spans.Single().id);
    }

    [Test]
    public void IndependentSidewalkRunSupportsSegmentWidthAndFinishAndPicking()
    {
        Brush("Sidewalk");
        Place(0,0);Place(8,0);Place(16,0);
        Assert.IsEmpty(road.graph.spans);
        Assert.AreEqual(2,road.paths.spans.Count);
        var first=road.paths.spans[0];var last=road.paths.spans[1];
        Assert.AreEqual(first.run,last.run);
        road.paths.runStyles.Add(new SidewalkRunStyle{run=first.run,width=2,material=road.family.road});
        last.pathWidth=4;last.pathMaterial=road.family.sidewalk;
        road.Rebuild();
        var mesh=road.GetComponent<MeshCollider>().sharedMesh;
        Assert.That(mesh.vertices.Where(v=>Mathf.Approximately(v.x,0)).Max(v=>v.z),Is.EqualTo(1).Within(.001));
        Assert.That(mesh.vertices.Where(v=>Mathf.Approximately(v.x,16)).Max(v=>v.z),Is.EqualTo(2).Within(.001));
        var origin=road.transform.TransformPoint(new Vector3(12,3,0));
        Assert.IsTrue(road.GetComponent<MeshCollider>().Raycast(new Ray(origin,-road.transform.up),out var hit,10));
        Assert.AreEqual(RoadPart.Path,road.faces[hit.triangleIndex].part);
        Assert.AreEqual(last.id,road.faces[hit.triangleIndex].id);
        Call("SplitPath",last.id,new Vector2(12,0));
        Assert.AreEqual(3,road.paths.spans.Count);
        Assert.AreEqual(2,road.paths.spans.Count(s=>s.pathWidth==4 && s.pathMaterial==last.pathMaterial));
        Call("Delete");
        Assert.AreEqual(2,road.paths.spans.Count);
    }
}
