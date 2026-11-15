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
        Assert.IsTrue(library.materials.All(m => m && m.shader.name == "House Toolkit/Projected Finish" && m.mainTexture && (m.GetTexture("_SurfaceMap") || m.GetFloat("_NormalStrength")==0)));
        var storefront = Asset<WallFamily>(Families+"Storefront/Storefront.asset");
        Assert.AreEqual(PanelLayout.EvenSpacing, storefront.defaultPanelLayout);
        CollectionAssert.AreEqual(new[]{2f,1f,.5f},storefront.panels.Select(p => Mathf.Round(p.mesh.bounds.size.x*10)/10));
    }

    [TestCase(false, "Door_Placeholder")][TestCase(true, "Door_Placeholder")]
    [TestCase(false, "Door_Glass")][TestCase(true, "Door_Glass")]
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

    [TearDown]
    public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
}
