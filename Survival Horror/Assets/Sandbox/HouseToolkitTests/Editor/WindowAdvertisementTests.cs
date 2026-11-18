using System.Linq;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class WindowAdvertisementTests
{
    HouseFloor floor;
    HouseWall wall;
    FixtureChoice[] choices;

    [SetUp]
    public void CreateRoom()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        choices = AssetDatabase.LoadAssetAtPath<FixturePalette>(
            "Assets/Game/Environment/NeighborhoodStore/WindowAds/ShopWindowAdvertisements.asset").fixtures;
        var family = AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/WallFamilies/Plain/Plain.asset");
        var house = HouseEditing.Create(Vector3.zero, family);
        floor = HouseEditing.Floors(house).Single();
        var edit = new FloorEdit(floor);
        edit.Draw(Vector2.zero, new Vector2(6, 0));
        edit.Draw(new Vector2(6, 0), new Vector2(6, 4));
        edit.Draw(new Vector2(6, 4), new Vector2(0, 4));
        edit.Draw(new Vector2(0, 4), Vector2.zero);
        Assert.IsTrue(edit.Commit("Create room", out var issue), issue);
        wall = floor.GetComponentsInChildren<HouseWall>().First(w => w.length > 5);
    }

    [TearDown]
    public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SurfaceAdsAcceptInsertFacesWithoutChangingOrdinaryFixtureTargeting()
    {
        var glass = new GameObject("Window insert face");
        glass.transform.SetParent(wall.transform, false);
        glass.transform.localPosition = new Vector3(2, 1, wall.Thickness * .5f + .03f);
        glass.AddComponent<InsertAttachment>();
        glass.AddComponent<BoxCollider>().size = new Vector3(1, 1, .02f);
        Physics.SyncTransforms();
        var ray = new Ray(wall.transform.TransformPoint(new Vector3(2, 1, 2)), -wall.transform.forward);
        Assert.IsFalse(FixtureTool.Target(floor, FixtureMount.Wall, ray, out _, out _, out _));
        Assert.IsTrue(FixtureTool.Target(floor, FixtureMount.Wall, ray, out var host, out var point, out int side, true));
        Assert.AreSame(wall.transform, host);
        var ad = FixtureTool.Place(choices[0], host, point, side);
        var model = ad.transform.GetChild(0);
        Assert.Greater(Vector3.Dot(-model.forward, wall.transform.forward), .999f);
        Assert.AreEqual(choices[0].surfaceClearance, Vector3.Dot(model.position - point, wall.transform.forward), .001f);
    }

    [Test]
    public void ChangingAdvertisementPreservesMountAndScaleAndUndoes()
    {
        var point = wall.transform.TransformPoint(new Vector3(2, 1, wall.Thickness * .5f));
        var ad = FixtureTool.Place(choices[0], wall.transform, point, 1);
        ad.transform.localScale = new Vector3(1.5f, 1.2f, 1);
        var position = ad.transform.position;
        FixtureTool.ReplacePreset(ad, choices[1]);
        Assert.AreEqual(1, ad.transform.childCount);
        Assert.AreSame(wall.transform, ad.transform.parent);
        Assert.AreEqual(position, ad.transform.position);
        Assert.AreEqual(new Vector3(1.5f, 1.2f, 1), ad.transform.localScale);
        Assert.AreSame(choices[1].prefab, PrefabUtility.GetCorrespondingObjectFromSource(ad.transform.GetChild(0).gameObject));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.AreEqual(1, ad.transform.childCount);
        Assert.AreSame(choices[0].prefab, PrefabUtility.GetCorrespondingObjectFromSource(ad.transform.GetChild(0).gameObject));
    }
}
