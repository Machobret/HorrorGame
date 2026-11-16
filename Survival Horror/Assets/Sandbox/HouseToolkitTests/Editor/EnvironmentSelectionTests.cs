using System.Collections;
using System.Linq;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EnvironmentSelectionTests
{
    House house;
    HouseFloor floor;
    static T Asset<T>(string path) where T:Object =>AssetDatabase.LoadAssetAtPath<T>("Assets/Game/Environment/"+path);

    [SetUp]
    public void Open()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        house=HouseEditing.Create(Vector3.zero,Asset<WallFamily>("WallFamilies/Plain/Plain.asset"));
        floor=HouseEditing.Floors(house).Single();
    }
    [TearDown]
    public void Close()
    {
        HouseScene.Leave();RoadScene.Leave();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }

    [Test]
    public void RoofRespectsFloorAndRoomOverridesAndNoTrim()
    {
        var edit=new FloorEdit(floor);
        var a=Vector2.zero;var b=new Vector2(8,0);var c=new Vector2(8,4);var d=new Vector2(0,4);
        edit.Draw(a,b);edit.Draw(b,c);edit.Draw(c,d);edit.Draw(d,a);
        edit.Draw(new Vector2(4,0),new Vector2(4,4));
        Assert.IsTrue(edit.Commit("Draw two rooms",out var error),error);
        var rooms=floor.GetComponentsInChildren<HouseRoom>();
        Assert.AreEqual(2,rooms.Length);
        house.trim=Asset<TrimFamily>("Trims/WoodBaseboard.asset");
        var none=Asset<TrimFamily>("Trims/NoTrim.asset");
        Assert.IsTrue(none && none.omitTrim);
        var wall=floor.GetComponentInChildren<HouseWall>();
        Assert.AreSame(house.trim,wall.Trim);
        floor.trim=none;Assert.IsNull(wall.Trim);
        wall.trimOverride=house.trim;Assert.AreSame(house.trim,wall.Trim);
        wall.trimOverride=none;floor.trim=null;Assert.IsNull(wall.Trim);
        HouseSlab Roof()=>floor.GetComponentsInChildren<HouseSlab>(true).Single(s=>s.overhead);
        floor.openCeiling=true;
        SlabBuilder.Rebuild(house);
        Assert.IsFalse(floor.GetComponentsInChildren<HouseSlab>(true).Any(s=>s.overhead));
        rooms[0].ceilingOverride=RoomCeiling.On;
        SlabBuilder.Rebuild(house);
        Physics.SyncTransforms();
        bool Covered(HouseRoom room)
        {
            var center=room.polygon.Aggregate(Vector2.zero,(sum,p)=>sum+p)/room.polygon.Length;
            var ray=new Ray(floor.World(center)+Vector3.up*10,Vector3.down);
            return Roof().GetComponent<MeshCollider>().Raycast(ray,out _,20);
        }
        Assert.IsTrue(Covered(rooms[0]));Assert.IsFalse(Covered(rooms[1]));
        floor.openCeiling=false;rooms[0].ceilingOverride=RoomCeiling.Off;
        SlabBuilder.Rebuild(house);Physics.SyncTransforms();
        Assert.IsFalse(Covered(rooms[0]));Assert.IsTrue(Covered(rooms[1]));
        var upper=HouseEditing.AddFloor(house,true);
        Assert.IsTrue(upper.GetComponentsInChildren<HouseSlab>(true).Any(s=>!s.overhead));
        Assert.AreEqual(1,upper.GetComponentsInChildren<HouseRoom>().Count(r=>r.ceilingOverride==RoomCeiling.Off));
    }

    [UnityTest]
    public IEnumerator StartingHouseAndStandaloneWallDrawingPreservesTheSceneView()
    {
        var view=ScriptableObject.CreateInstance<SceneView>();
        try
        {
            view.Show();view.Focus();
            yield return null;
            foreach(bool standalone in new[]{false,true})
            {
                view.LookAtDirect(new Vector3(3,8,-2),Quaternion.Euler(40,27,0),9);
                var pivot=view.pivot;
                var rotation=view.rotation;
                float size=view.size;
                HouseScene.CreateAt(new Vector3(2,1,4),true,standalone);
                yield return null;
                HouseScene.RefocusIfMoved();
                Assert.That(Vector3.Distance(view.pivot,pivot),Is.LessThan(.001f),"Starting a drawing moved the camera pivot.");
                Assert.That(Quaternion.Angle(view.rotation,rotation),Is.LessThan(.01f));
                Assert.That(view.size,Is.EqualTo(size).Within(.001f));
                if(!standalone)
                {
                    var upstairs=HouseEditing.AddFloor(HouseScene.House);
                    HouseScene.SwitchFloor(upstairs);
                    Assert.That(view.pivot.y,Is.EqualTo(upstairs.transform.position.y).Within(.001f));
                }
                HouseScene.Leave();
            }
        }
        finally {view.Close();}
    }

    [UnityTest]
    public IEnumerator DraggingWallGizmoKeepsOffGridOffsets()
    {
        house.transform.SetPositionAndRotation(new Vector3(4,1,-3),Quaternion.Euler(0,27,0));
        var edit=new FloorEdit(floor);
        edit.Draw(new Vector2(.2f,.35f),new Vector2(4.2f,.35f));
        edit.Draw(new Vector2(.2f,2.35f),new Vector2(4.2f,2.35f));
        Assert.IsTrue(edit.Commit("Draw off-grid walls",out var error),error);
        var walls=floor.GetComponentsInChildren<HouseWall>();
        var view=ScriptableObject.CreateInstance<SceneView>();
        var tool=Tools.current;
        float grid=HouseScene.Grid;
        try
        {
            view.Show();view.position=new Rect(80,80,960,720);view.Focus();
            HouseScene.Enter(floor);HouseScene.Grid=1;Tools.current=Tool.Move;
            view.orthographic=true;
            view.LookAtDirect(floor.World(new Vector2(2,1)),Quaternion.LookRotation(-floor.transform.up,floor.transform.forward),6);
            yield return null;yield return null;
            HouseScene.SelectWall(walls[0]);
            foreach(bool multiple in new[]{false,true})
            {
                if(multiple)HouseScene.ToggleWall(walls[1]);
                var ids=HouseScene.SelectedWalls.SelectMany(w=>new[]{w.startCorner,w.endCorner}).Distinct().ToArray();
                var before=ids.Select(floor.Position).ToArray();
                var pivot=floor.World(before.Aggregate(Vector2.zero,(a,b)=>a+b)/before.Length);
                Vector2 from=default,to=default;
                void Locate(SceneView v)
                {
                    if(v!=view || Event.current.commandName!="LocateWallHandle")return;
                    var grip=pivot+floor.transform.right*HandleUtility.GetHandleSize(pivot)*.48f;
                    Vector2 Pixel(Vector3 p)=>GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(p))-view.position.position;
                    from=Pixel(grip);to=Pixel(grip+floor.transform.right);
                }
                SceneView.duringSceneGui+=Locate;
                try {view.SendEvent(EditorGUIUtility.CommandEvent("LocateWallHandle"));}
                finally {SceneView.duringSceneGui-=Locate;}
                void Drag(Vector2 end)
                {
                    foreach(var type in new[]{EventType.MouseMove,EventType.Layout,EventType.MouseDown,EventType.MouseDrag,EventType.MouseUp})
                        view.SendEvent(new Event{type=type,button=0,clickCount=1,
                            mousePosition=type==EventType.MouseDrag || type==EventType.MouseUp ? end : from,
                            delta=type==EventType.MouseDrag ? end-from : Vector2.zero});
                }
                Drag(Vector2.Lerp(from,to,.1f));
                for(int i=0;i<ids.Length;i++)
                    Assert.That(Vector2.Distance(before[i],floor.Position(ids[i])),Is.LessThan(.001f),"A sub-step drag moved the wall.");
                Drag(to);
                for(int i=0;i<ids.Length;i++)
                    Assert.That(Vector2.Distance(before[i]+Vector2.right,floor.Position(ids[i])),Is.LessThan(.001f),"X drag must move exactly one local grid step.");
            }
        }
        finally {HouseScene.Grid=grid;Tools.current=tool;HouseScene.Leave();view.Close();}
    }

    [UnityTest]
    public IEnumerator ShiftClickPicksTwoWindowsAndTwoDoorsAndFiltersWithoutChangingWalls()
    {
        var edit=new FloorEdit(floor);
        edit.Draw(Vector2.zero,new Vector2(12,0));
        Assert.IsTrue(edit.Commit("Draw host",out var error),error);
        var wall=floor.GetComponentInChildren<HouseWall>();
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/SelectionTestInsert.prefab");
        var root=new GameObject("Insert");
        var box=GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(root.transform,false);box.transform.localPosition=Vector3.up*.5f;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        var window=ScriptableObject.CreateInstance<HouseInsert>();
        var door=ScriptableObject.CreateInstance<HouseInsert>();
        var replacement=ScriptableObject.CreateInstance<HouseInsert>();
        string alternatePath=AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/OtherSelectionTestInsert.prefab");
        window.kind=OpeningKind.Window;window.prefab=door.prefab=prefab;window.opening=door.opening=Vector2.one;
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;
        try
        {
            for(int i=0;i<4;i++)Assert.IsTrue(WallBuilder.Place(wall,i<2?window:door,2+i*2,0,out error),error);
            view.Show();view.position=new Rect(80,80,960,720);
            HouseScene.Enter(floor);Tools.current=Tool.Move;Physics.SyncTransforms();
            view.Focus();view.orthographic=true;
            view.LookAtDirect(wall.transform.TransformPoint(new Vector3(5,1,0)),Quaternion.LookRotation(wall.transform.forward,Vector3.up),7);
            view.Repaint();yield return null;yield return null;
            void Click(int i,bool shift)
            {
                Vector2 pixel=default;
                void Locate(SceneView v)
                {
                    if(v==view && Event.current.commandName=="LocateInsert")
                        pixel=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(wall.transform.TransformPoint(new Vector3(2+i*2,.5f,0))))-view.position.position;
                }
                SceneView.duringSceneGui+=Locate;
                try {view.SendEvent(EditorGUIUtility.CommandEvent("LocateInsert"));}
                finally {SceneView.duringSceneGui-=Locate;}
                foreach(var type in new[]{EventType.MouseMove,EventType.Layout,EventType.MouseDown,EventType.MouseUp})
                    view.SendEvent(new Event{type=type,button=0,clickCount=1,mousePosition=pixel,shift=shift});
            }
            Click(0,false);Click(1,true);
            var picks=HouseScene.Picks;
            CollectionAssert.AreEqual(new[]{"Window","Window"},picks.Items.Select(p=>p.Kind).ToArray(),"Selected: "+string.Join(",",picks.Items.Select(p=>p.Kind)));
            Assert.IsTrue(HouseProperties.EditOpenings(picks.Items.ToArray(),o=>o.sill=.4f),HouseScene.Message);
            CollectionAssert.AreEqual(new[]{.4f,.4f,0,0},wall.openings.Select(o=>o.sill));
            Undo.PerformUndo();
            Assert.IsTrue(wall.openings.All(o=>o.sill==0));
            Assert.IsFalse(HouseProperties.EditOpenings(picks.Items.ToArray(),o=>o.distance=3));
            CollectionAssert.AreEqual(new[]{2,4,6,8},wall.openings.Select(o=>o.distance));
            Assert.IsTrue(AssetDatabase.CopyAsset(path,alternatePath));
            replacement.kind=OpeningKind.Window;replacement.opening=Vector2.one;
            replacement.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(alternatePath);
            Assert.IsTrue(HouseProperties.EditOpenings(picks.Items.ToArray(),o=>o.insert=replacement),HouseScene.Message);
            foreach(var p in picks.Items)
            {
                var renderers=HouseHighlight.SelectedRenderers(p);
                Assert.IsNotEmpty(renderers);
                Assert.That(renderers.All(r=>r.GetComponentInParent<InsertAttachment>()?.openingId==p.OpeningId));
            }
            Click(2,true);Click(3,true);
            Assert.IsTrue(picks.Mixed);
            picks.Filter("Window",false);HouseScene.SyncPicks();
            CollectionAssert.AreEqual(new[]{"Door","Door"},picks.Items.Select(p=>p.Kind));
            Click(0,true);
            picks.Filter("Door",true);HouseScene.SyncPicks();
            Assert.AreEqual(2,picks.Items.Count);
            HouseScene.DeleteSelection();Assert.AreEqual(2,wall.openings.Count);
            Undo.PerformUndo();Assert.AreEqual(4,wall.openings.Count);
        }
        finally
        {
            Tools.current=oldTool;HouseScene.Leave();view.Close();
            AssetDatabase.DeleteAsset(path);AssetDatabase.DeleteAsset(alternatePath);
            Object.DestroyImmediate(window);Object.DestroyImmediate(door);Object.DestroyImmediate(replacement);
        }
    }

    [Test]
    public void MixedRoadSelectionKeepsOrDiscardsByTypeAndDeletesInOneUndo()
    {
        var road=new GameObject("Road").AddComponent<RoadNetwork>();
        RoadScene.Enter(road);
        Assert.IsTrue(RoadScene.Change("Draw roads",g=>{
            var a=g.AddNode(Vector3.zero);var b=g.AddNode(Vector3.right*8);var c=g.AddNode(Vector3.right*16);
            g.Connect(a.id,b.id,0,null);g.Connect(b.id,c.id,0,null);
        }));
        var spans=road.graph.spans.ToArray();
        RoadScene.Pick(new RoadHit(RoadPart.Road,spans[0].id));
        RoadScene.Pick(new RoadHit(RoadPart.Road,spans[1].id),true);
        RoadScene.Pick(new RoadHit(RoadPart.Sidewalk,spans[1].id,left:true),true);
        Assert.IsTrue(RoadScene.Picks.Mixed);
        RoadScene.Delete();
        Assert.AreEqual(2,road.graph.spans.Count);
        RoadScene.Picks.Filter("Road",true);RoadScene.SyncPicks();
        Assert.AreEqual(2,RoadScene.SelectedRoads.Count);
        Undo.IncrementCurrentGroup();
        RoadScene.Delete();
        Assert.IsEmpty(road.graph.spans);
        Undo.PerformUndo();
        Assert.AreEqual(2,road.graph.spans.Count);
    }
}
