using System.Linq;
using System.Reflection;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GameHouseToolkitTests
{
    const string Families = "Assets/Game/Environment/WallFamilies/";
    const string Doors = "Assets/Game/Doors/Inserts/";
    static T Asset<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.IsNotNull(asset, path);
        return asset;
    }

    [Test]
    public void StartingLevelHasOneEditableFloorAndIndependentProjectedFinishes()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/Levels/Chapter0/Scenes/StartingLevel.unity");
        var house = scene.GetRootGameObjects().Select(go => go.GetComponent<House>()).Single(h => h);
        Assert.AreEqual(1, HouseEditing.Floors(house).Length);
        Assert.AreNotSame(house.interiorFinish, house.exteriorFinish);
        var library = Asset<FinishLibrary>("Assets/Game/Environment/ProjectedFinishes/WallFinishes.asset");
        Assert.Contains(house.interiorFinish, library.materials);
        Assert.Contains(house.exteriorFinish, library.materials);
        var root = "Assets/Game/Environment/ProjectedFinishes/";
        foreach (var material in library.materials)
        {
            Assert.IsNotNull(material);
            StringAssert.StartsWith(root, AssetDatabase.GetAssetPath(material));
            Assert.IsTrue(material.HasProperty("_HouseRight") && material.HasProperty("_HouseUp") &&
                          material.HasProperty("_HouseForward"), material.name);
            Assert.IsNotNull(material.mainTexture, material.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), material.shader.name);
            if (material.GetFloat("_NormalStrength") > 0)
                Assert.IsTrue(material.GetTexture("_SurfaceMap") ||
                              (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap")), material.name);
        }

        var storefront = Asset<WallFamily>(Families+"Storefront/Storefront.asset");
        Assert.AreEqual(PanelLayout.EvenSpacing, storefront.defaultPanelLayout);
        CollectionAssert.AreEqual(new[]{2f,1f,.5f},storefront.panels.Select(p => Mathf.Round(p.mesh.bounds.size.x*10)/10));
    }

    [TestCase(false, "Door_Placeholder")][TestCase(true, "Door_Placeholder")]
    [TestCase(false, "Door_Glass")][TestCase(true, "Door_Glass")]
    [TestCase(false, "Door_MetalGate")][TestCase(true, "Door_MetalGate")]
    public void PlacedGameDoorKeepsInteractionAndCanBeSelected(bool storefront, string name)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var family = Asset<WallFamily>(Families + (storefront ? "Storefront/Storefront.asset" : "Plain/Plain.asset"));
        var house = HouseEditing.Create(Vector3.zero, family);
        house.transform.rotation=Quaternion.Euler(0,31,0);
        var floor = HouseEditing.Floors(house).Single();
        var plan = new FloorEdit(floor);
        plan.Draw(Vector2.zero,Vector2.right*7);
        Assert.IsTrue(plan.Commit("Draw wall",out var issue),issue);
        var wall = floor.GetComponentInChildren<HouseWall>();
        var insert = Asset<HouseInsert>(Doors+name+".asset");
        Assert.Contains(insert, OpeningPalette.Choices(wall,OpeningKind.Door));
        Assert.IsTrue(WallBuilder.Place(wall,insert,3,0,out issue),issue);
        var binding = wall.GetComponentInChildren<InsertAttachment>();
        var door = binding.transform.GetChild(0).gameObject;
        Assert.AreEqual(insert.prefab, PrefabUtility.GetCorrespondingObjectFromSource(door));
        var interaction = new SerializedObject(door.GetComponent("Interactive"));
        var call = interaction.FindProperty("OnInteract.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0);
        Assert.AreEqual(door.GetComponent("TwoSidedDoor"), call.FindPropertyRelative("m_Target").objectReferenceValue);
        Assert.AreEqual("Use", call.FindPropertyRelative("m_MethodName").stringValue);
        Assert.IsTrue(door.GetComponent<BoxCollider>().isTrigger);
        Assert.IsFalse(door.transform.Find("BodyCollider").GetComponent<BoxCollider>().isTrigger);
        EditorWindow.GetWindow<SceneView>().Focus();
        Selection.activeGameObject = door.transform.Find("Visual").gameObject;
        typeof(HouseEditing).Assembly.GetType("HouseToolkit.Editor.HouseScene")
            .GetMethod("SelectHouseRoots",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        Assert.AreEqual(door, Selection.activeGameObject);
        if (name == "Door_Placeholder")
        {
            var renderer=door.GetComponentInChildren<MeshRenderer>();
            var material=renderer.sharedMaterial;
            Assert.IsTrue(material.enableInstancing);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            Assert.IsTrue(WallBuilder.Place(wall,insert,5,0,out issue),issue);
            var other=wall.GetComponentsInChildren<InsertAttachment>().Single(b=>b!=binding).GetComponentInChildren<MeshRenderer>();
            var block=new MaterialPropertyBlock();
            foreach(var r in new[]{renderer,other})
            {
                var atlas=r.GetComponentInParent<InsertAttachment>().transform.GetChild(0).GetComponent("DoorAtlas");
                var settings=new SerializedObject(atlas);
                settings.FindProperty("design").intValue=r==renderer?1:7;
                settings.ApplyModifiedProperties();
                atlas.SendMessage("Apply");
            }
            Assert.AreSame(material,other.sharedMaterial);
            renderer.GetPropertyBlock(block);Assert.AreEqual(1,block.GetFloat("_AtlasTile"));
            other.GetPropertyBlock(block);Assert.AreEqual(7,block.GetFloat("_AtlasTile"));
        }
        Assert.IsTrue(WallBuilder.Rebuild(wall,out issue),issue);
        Assert.IsTrue(door && door.transform.parent==binding.transform);
    }

    [Test]
    public void ClosedStandaloneFenceKeepsOpenGroundAndUsesSharedGate()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var family = Asset<WallFamily>(Families+"MetalFence/Metal Fence.asset");
        var gate = Asset<HouseInsert>(Doors+"Door_MetalGate.asset");
        var house = HouseEditing.Create(new Vector3(7,0,3),family,true);
        house.transform.rotation = Quaternion.Euler(0,27,0);
        var floor = HouseEditing.Floors(house).Single();
        Assert.AreSame(floor,HouseEditing.AddFloor(house));
        var plan = new FloorEdit(floor);
        var corners = SlabOpening.Rectangle(new Rect(0,0,8,6));
        for (int i=0;i<3;i++) plan.Draw(corners[i],corners[i+1]);
        Assert.IsTrue(plan.Prepare(out var issue),issue);
        plan.Draw(corners[3],corners[0]);
        Assert.IsTrue(plan.Commit("Close fence",out issue),issue);
        var walls = floor.GetComponentsInChildren<HouseWall>();
        Assert.AreEqual(4,walls.Length);
        Assert.IsFalse(walls.Any(w => w.interior));
        Assert.IsEmpty(floor.GetComponentsInChildren<HouseRoom>());
        Assert.IsEmpty(floor.GetComponentsInChildren<HouseSlab>());
        var host = walls.First(w=>w.length>7);
        Assert.AreSame(gate,OpeningPalette.Choices(host,OpeningKind.Door).First());
        Assert.IsTrue(WallBuilder.Place(host,gate,4,0,out issue),issue);
        var leaf = host.GetComponentInChildren<InsertAttachment>().GetComponentInChildren<MeshFilter>();
        Assert.That(leaf.sharedMesh.bounds.size.x,Is.EqualTo(.9f).Within(.01f));
        Assert.That(leaf.sharedMesh.bounds.size.y,Is.EqualTo(2.1f).Within(.01f));
        Assert.That(leaf.transform.localPosition.x,Is.EqualTo(0).Within(.001f));
        Assert.IsTrue(host.GetComponentsInChildren<AuthoredPanel>().Any(p=>p.source==family.FindSurround(gate).mesh));
        Assert.IsTrue(walls.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>()).Count(f=>f.sharedMesh==family.panel.mesh)>1);
        Assert.IsFalse(WallBuilder.SetHeight(floor,2f,out issue));
        Assert.AreEqual(HouseFloor.DefaultHeight,floor.height);
        Assert.IsTrue(WallBuilder.SetHeight(floor,3f,out issue),issue);
        Assert.IsEmpty(floor.GetComponentsInChildren<HouseSlab>());
    }

    [Test]
    public void ProceduralEnvPropertiesFollowsRoadFamilyAndReturnsToHouse()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.IsTrue(EditorApplication.ExecuteMenuItem("Window/House Toolkit/Procedural Env Properties"));
        var window = Resources.FindObjectsOfTypeAll<HouseProperties>().Single();
        var editorType = typeof(HouseEditing).Assembly;
        var roadScene = editorType.GetType("HouseToolkit.Editor.RoadScene");
        var houseScene = editorType.GetType("HouseToolkit.Editor.HouseScene");
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags shared = BindingFlags.Static | BindingFlags.NonPublic;
        var roadEditor = typeof(HouseProperties).GetField("roadEditor", instance);

        void Layout() => window.SendEvent(new Event { type = EventType.Layout });
        void AssertRoad(RoadNetwork expected, RoadFamily drawOverride = null)
        {
            Layout();
            var editor = (UnityEditor.Editor)roadEditor.GetValue(window);
            Assert.AreEqual(expected, editor.target);
            var familyEditor = (UnityEditor.Editor)editor.GetType().GetField("familyEditor", instance).GetValue(editor);
            Assert.AreEqual(drawOverride ? drawOverride : expected.family, familyEditor.target);
        }

        try
        {
            Assert.AreEqual("Procedural Env Properties", window.titleContent.text);
            var road = new GameObject("Road").AddComponent<RoadNetwork>();
            road.family = Asset<RoadFamily>("Assets/Game/Environment/Roads/TownStreet/Town Street.asset");
            Selection.activeGameObject = road.gameObject;
            AssertRoad(road);

            var other = new GameObject("Side road").AddComponent<RoadNetwork>();
            other.family = Asset<RoadFamily>("Assets/Game/Environment/Roads/TownStreet/Side Street.asset");
            roadScene.GetMethod("Enter", shared).Invoke(null, new object[] { other });
            AssertRoad(other);
            roadScene.GetMethod("SetDrawing", shared).Invoke(null, new object[] { true });
            roadScene.GetField("DrawFamily", shared).SetValue(null, road.family);
            AssertRoad(other, road.family);
            roadScene.GetMethod("SetDrawing", shared).Invoke(null, new object[] { false });
            Layout();

            var house = HouseEditing.Create(Vector3.zero, Asset<WallFamily>(Families + "Plain/Plain.asset"));
            houseScene.GetMethod("Enter", shared).Invoke(null, new object[] { HouseEditing.Floors(house).Single() });
            HouseProperties.OpenFloors();
            Layout();
            Assert.IsFalse((UnityEditor.Editor)roadEditor.GetValue(window));
            Assert.AreSame(window, Resources.FindObjectsOfTypeAll<HouseProperties>().Single());
        }
        finally
        {
            houseScene.GetMethod("Leave", shared).Invoke(null, null);
            roadScene.GetMethod("Leave", shared).Invoke(null, null);
            window.Close();
        }
    }

    [TearDown]
    public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
}
