using System.Collections;
using System.Linq;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BuildingFixtureTests
{
    FixturePalette palette;
    House house;
    HouseFloor floor;

    [SetUp]
    public void Create()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        palette = AssetDatabase.LoadAssetAtPath<FixturePalette>("Assets/Game/Environment/Lighting/Building Fixtures.asset");
        var family = AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/Plain/Plain.asset");
        house = HouseEditing.Create(new Vector3(7,1,-3), family);
        house.transform.rotation = Quaternion.Euler(0,37,0);
        floor = HouseEditing.Floors(house).Single();
        var edit = new FloorEdit(floor);
        var a = Vector2.zero; var b = new Vector2(6,0); var c = new Vector2(6,4); var d = new Vector2(0,4);
        edit.Draw(a,b); edit.Draw(b,c); edit.Draw(c,d); edit.Draw(d,a);
        Assert.IsTrue(edit.Commit("Draw room",out var issue),issue);
        Physics.SyncTransforms();
    }

    [TearDown] public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test] public void SceneGroupsAreLocalAndPreserveAuthoredParents()
    {
        Assert.AreEqual("Buildings",house.transform.parent.name);
        var scene = house.gameObject.scene;
        var other = HouseEditing.Create(Vector3.zero,house.exteriorFamily);
        Assert.AreSame(house.transform.parent, other.transform.parent);
        var keep = new GameObject("Authored").transform;
        other.transform.SetParent(keep);
        var rack = new GameObject("Existing shelf").AddComponent<ProceduralShelf>();
        var pos = rack.transform.position = Vector3.one;
        EnvironmentHierarchy.Organize(scene);
        EnvironmentHierarchy.Organize(scene);
        Assert.AreEqual(1,scene.GetRootGameObjects().Count(r=>r.name=="Shelves"));
        Assert.AreEqual(pos,rack.transform.position);
        Assert.AreSame(keep,other.transform.parent);
    }

    [Test] public void LooseSceneryKeepsItsPrefabAndCanStillBeAssignedToARoom()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/StorageBoxes/StorageBox_NoCollision.prefab");
        var inside = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var outside = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var pos = inside.transform.position = floor.World(new Vector2(2, 2)) + floor.transform.up * .2f;
        outside.transform.position = Vector3.one * 100;
        EnvironmentHierarchy.Organize(house.gameObject.scene);
        Assert.AreEqual("Props", inside.transform.parent.name);
        Assert.AreSame(inside.transform.parent, outside.transform.parent);
        Assert.That(Vector3.Distance(pos, inside.transform.position), Is.LessThan(.0001f));
        Assert.AreSame(prefab, PrefabUtility.GetCorrespondingObjectFromSource(inside));
        RoomAssignment.Assign(house);
        Assert.IsNotNull(inside.GetComponentInParent<HouseRoom>());
        Assert.AreEqual("Props", outside.transform.parent.name);
        EnvironmentHierarchy.Organize(house.gameObject.scene);
        Assert.IsNotNull(inside.GetComponentInParent<HouseRoom>());
    }

    [Test] public void SwatchesPlaceTheirLinkedModelsFlushWithTheMount()
    {
        var wall=floor.GetComponentsInChildren<HouseWall>().First();
        foreach (var choice in palette.fixtures)
        {
            Assert.IsNotNull(choice.prefab,choice.label);
            bool onWall=choice.mount==FixtureMount.Wall;
            var host=onWall?wall.transform:floor.transform;
            var local=onWall?new Vector3(1,1,wall.Thickness*.5f):
                new Vector3(1,choice.mount==FixtureMount.Ceiling?floor.height:0,1);
            var fixture=FixtureTool.Place(choice,host,host.TransformPoint(local),1);
            var model=fixture.transform.GetChild(0);
            Assert.AreSame(choice.prefab,PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject));
            var renderers=model.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers,choice.label);
            float nearest=float.PositiveInfinity;
            foreach (var r in renderers)
            {
                var b=r.localBounds;
                for (int i=0;i<8;i++)
                {
                    var v=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);
                    var p=fixture.transform.InverseTransformPoint(r.transform.TransformPoint(v));
                    nearest=Mathf.Min(nearest,onWall?p.z:choice.mount==FixtureMount.Ceiling?-p.y:p.y);
                }
            }
            Assert.AreEqual(0,nearest,.001f,choice.label);
            if (choice.lightRange > 0)
            {
                var light = fixture.GetComponentInChildren<Light>();
                Assert.AreEqual(choice.lightIntensity, light.intensity, .001f, choice.label);
                Assert.AreEqual(choice.lightRange, light.range, .001f, choice.label);
                Assert.AreEqual(LightShadows.None, light.shadows, choice.label);
            }
        }
    }

    [TestCase("Wall light", 1, .15f)]
    [TestCase("Power socket", 1, .035f)]
    [TestCase("Vent cover", -1, .068f)]
    public void AuthoredWallFixturesFaceOutwardOnEitherSide(string label, int frontX, float depth)
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First();
        var choice = palette.fixtures.Single(c => c.label == label);
        foreach (var side in new[] { 1, -1 })
        {
            var normal = wall.transform.forward * side;
            var point = wall.transform.TransformPoint(new Vector3(1, 1, side * wall.Thickness * .5f));
            var fixture = FixtureTool.Place(choice, wall.transform, point, side);
            var model = fixture.transform.GetChild(0);
            Assert.That(Vector3.Dot(model.right * frontX, normal), Is.GreaterThan(.999f));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                foreach (var vertex in renderer.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    float distance = Vector3.Dot(renderer.transform.TransformPoint(vertex) - point, normal);
                    Assert.That(distance, Is.InRange(-.001f, depth + .001f), label);
                }
        }
    }

    [Test] public void RefitUpdatesExistingModelAndEmitterInPlaceAndUndoesTogether()
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First();
        var choice = palette.fixtures.Single(c => c.label == "Wall light");
        var fixture = FixtureTool.Place(choice, wall.transform, wall.transform.position, -1);
        var model = fixture.transform.GetChild(0);
        model.localRotation = Quaternion.identity;
        model.localPosition = Vector3.zero;
        var light = fixture.GetComponentInChildren<Light>();
        light.range = 6;
        light.intensity = 1;
        Undo.IncrementCurrentGroup();
        Assert.IsTrue(FixtureTool.Refit(fixture));
        Assert.Less(Quaternion.Angle(model.localRotation, Quaternion.Euler(choice.modelRotation)), .001f);
        Assert.AreEqual(choice.lightIntensity, light.intensity);
        Assert.AreEqual(choice.lightRange, light.range);
        Assert.AreSame(light, fixture.GetComponentsInChildren<Light>().Single());
        Assert.AreSame(choice.prefab, PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.Less(Quaternion.Angle(model.localRotation, Quaternion.identity), .001f);
        Assert.AreEqual(Vector3.zero, model.localPosition);
        Assert.AreEqual(1, light.intensity);
        Assert.AreEqual(6, light.range);
    }

    [Test] public void MountsFollowWallSplitMoveAndStoreyHeight()
    {
        Assert.IsTrue(palette.fixtures.All(c=>c.prefab));
        var wall = floor.GetComponentsInChildren<HouseWall>().First(w=>w.length>5);
        var pos = wall.transform.TransformPoint(new Vector3(4.5f,1.6f,0));
        Assert.IsTrue(FixtureTool.Target(floor,FixtureMount.Wall,new Ray(pos+wall.transform.forward, -wall.transform.forward),
            out var host,out var point,out int side));
        Assert.AreSame(wall.transform,host);
        var lamp = FixtureTool.Place(palette.fixtures.First(c=>c.mount==FixtureMount.Wall),host,point,side);
        Assert.IsNotNull(lamp.GetComponentInChildren<Light>());
        var original = lamp.transform.position;
        var split = new FloorEdit(floor);
        split.Split(wall,floor.Local(wall.transform.TransformPoint(Vector3.right*2)));
        Assert.IsTrue(split.Commit("Split",out var issue),issue);
        Assert.Less(Vector3.Distance(original,lamp.transform.position),.001f);
        var next=lamp.transform.parent.GetComponent<HouseWall>();
        Assert.AreNotSame(wall,next);
        next.thicknessOverride=.5f;
        Assert.IsTrue(WallBuilder.Rebuild(next,out issue),issue);
        Assert.AreEqual(.25f,lamp.transform.localPosition.z,.001f);
        foreach(var mount in new[]{FixtureMount.Floor,FixtureMount.Ceiling})
        {
            var ray = new Ray(floor.World(new Vector2(3,2))+floor.transform.up*6,-floor.transform.up);
            Assert.IsTrue(FixtureTool.Target(floor,mount,ray,out host,out point,out side), mount.ToString());
            var item=FixtureTool.Place(palette.fixtures.First(c=>c.mount==mount),host,point,side);
            floor.height+=.5f; SlabBuilder.Rebuild(house); Physics.SyncTransforms();
            Assert.AreEqual(mount==FixtureMount.Floor?0:floor.height,item.transform.localPosition.y,.001f);
        }
        var back = new Vector3(1,1,-wall.Thickness*.5f);
        FixtureTool.Mount(lamp,wall.transform,wall.transform.TransformPoint(back),-1);
        Assert.AreSame(wall.transform,lamp.transform.parent);
        Assert.Less(lamp.transform.localPosition.z,0);
    }

    [Test] public void PlainWallRevealsShareACentreSeam()
    {
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.manta.house-toolkit/Runtime/Wall Faces.asset");
        Assert.AreEqual(2,mesh.subMeshCount);
        for(int slot=0;slot<2;slot++)
        {
            var z=mesh.GetTriangles(slot).Select(i=>mesh.vertices[i].z).ToArray();
            Assert.AreEqual(slot==0?-.5f:0,z.Min());
            Assert.AreEqual(slot==0?0:.5f,z.Max());
            Assert.AreEqual(30,z.Length);
        }
    }

    [UnityTest] public IEnumerator ReattachCeilingLightInCutawayAndUndo()
    {
        var view=ScriptableObject.CreateInstance<SceneView>();
        var oldTool=Tools.current;
        try
        {
            HouseScene.Enter(floor);
            var a=floor.World(new Vector2(2,1))+floor.transform.up*floor.height;
            var b=floor.World(new Vector2(4,3))+floor.transform.up*floor.height;
            var light=FixtureTool.Place(palette.fixtures.First(c=>c.mount==FixtureMount.Ceiling),floor.transform,a,1);
            Undo.IncrementCurrentGroup();
            FixtureTool.BeginMove(light);
            view.Show();
            view.position=new Rect(80,80,850,680);
            view.orthographic=true;
            view.LookAtDirect(b,Quaternion.LookRotation(-floor.transform.up,floor.transform.forward),5);
            Tools.current=Tool.Move;
            view.Focus();view.Repaint();
            yield return null;yield return null;
            Physics.SyncTransforms();
            PavementEditingTests.ClickScene(view,b);
            Assert.AreEqual(light.gameObject,Selection.activeGameObject);
            Assert.Less(Vector3.Distance(b,light.transform.position),.025f);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.Less(Vector3.Distance(a,light.transform.position),.001f);
            Undo.PerformRedo();
            Assert.Less(Vector3.Distance(b,light.transform.position),.025f);
        }
        finally
        {
            HouseScene.Leave();view.Close();Tools.current=oldTool;
        }
    }
}
