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
        var house = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<House>()).Single();
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
        var bounds=leaf.sharedMesh.bounds;
        Assert.That(Mathf.Abs(leaf.transform.localPosition.x),Is.InRange(.40f,.46f),"The visual origin belongs on the hinge rail.");
        Assert.That(leaf.transform.localPosition.x+bounds.center.x,Is.EqualTo(0).Within(.001f),"Closed leaf must stay centred in the doorway.");
        var body=leaf.GetComponent<BoxCollider>();
        Assert.That(body && body.enabled);
        var hinge=leaf.transform.position;
        var closedCentre=body.transform.TransformPoint(body.center);
        leaf.transform.localRotation=Quaternion.Euler(0,90,0);
        Assert.That(Vector3.Distance(hinge,leaf.transform.position),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(closedCentre,body.transform.TransformPoint(body.center)),Is.GreaterThan(.5f));
        leaf.transform.localRotation=Quaternion.identity;
        Assert.IsTrue(host.GetComponentsInChildren<AuthoredPanel>().Any(p=>p.source==family.FindSurround(gate).mesh));
        Assert.IsTrue(walls.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>()).Count(f=>f.sharedMesh==family.panel.mesh)>1);
        Assert.IsFalse(WallBuilder.SetHeight(floor,2f,out issue));
        Assert.AreEqual(HouseFloor.DefaultHeight,floor.height);
        Assert.IsTrue(WallBuilder.SetHeight(floor,3f,out issue),issue);
        Assert.IsEmpty(floor.GetComponentsInChildren<HouseSlab>());
    }

    [Test]
    public void LargeGateReservesItsAuthoredBayAndUsesClippedWire()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var family=Asset<WallFamily>(Families+"MetalFence/Metal Fence.asset");
        var house=HouseEditing.Create(new Vector3(8,1,3),family,true);
        house.transform.rotation=Quaternion.Euler(0,31,0);
        var floor=HouseEditing.Floors(house).Single();
        var plan=new FloorEdit(floor);
        plan.Draw(Vector2.zero,Vector2.right*9.3f);
        Assert.IsTrue(plan.Commit("Draw fence",out var issue),issue);
        var host=floor.GetComponentInChildren<HouseWall>();
        var closed=Asset<HouseInsert>(Families+"MetalFence/Large Gate Closed.asset");
        var sliding=Asset<HouseInsert>(Families+"MetalFence/Large Gate Sliding.asset");
        var panel=family.FindSurround(closed);
        Assert.AreSame(panel,family.FindSurround(sliding));
        var palette=OpeningPalette.Choices(host,OpeningKind.Panel);
        Assert.IsFalse(OpeningPalette.Choices(host,OpeningKind.Door).Contains(closed));
        Assert.That(palette.IndexOf(closed),Is.GreaterThanOrEqualTo(0));
        Assert.That(palette.IndexOf(sliding),Is.GreaterThan(palette.IndexOf(closed)));
        Assert.IsFalse(WallBuilder.Place(host,closed,1,0,out issue),"A gate must fit at its full width.");
        Assert.IsTrue(WallBuilder.Place(host,closed,4,0,out issue),issue);
        var assembly=host.GetComponentsInChildren<AuthoredPanel>().Single(p=>p.source==panel.mesh);
        Assert.That(panel.size.x,Is.EqualTo(5.36f).Within(.01f));
        Assert.That(assembly.transform.lossyScale.x,Is.EqualTo(1f).Within(.0001f));
        var leaf=host.GetComponentInChildren<InsertAttachment>().GetComponentInChildren<MeshFilter>();
        Assert.AreSame(leaf.sharedMesh,sliding.prefab.GetComponentInChildren<MeshFilter>().sharedMesh);
        Assert.That(leaf.sharedMesh.bounds.size.x,Is.EqualTo(5.33f).Within(.01f));
        foreach(var renderer in host.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<AuthoredPanel>() || r==leaf.GetComponent<MeshRenderer>()))
            CollectionAssert.AreEqual(new[]{family.surface,family.glass},renderer.sharedMaterials);
        Assert.That(family.glass.IsKeywordEnabled("_ALPHATEST_ON"));
        Assert.That(family.glass.GetFloat("_Cull"),Is.Zero);
        Assert.That(family.glass.GetTexture("_BaseMap"),Is.Not.Null);
        Assert.That(family.surface.GetFloat("_AlphaClip"),Is.Zero);
        Physics.SyncTransforms();
        bool Blocked()=>Physics.Raycast(host.transform.TransformPoint(new Vector3(4,1,-1)),host.transform.forward,2,~0,QueryTriggerInteraction.Ignore);
        Assert.That(Blocked(),"The closed leaf should block the passage.");
        leaf.gameObject.SetActive(false);Physics.SyncTransforms();
        Assert.IsFalse(Blocked(),"The frame or a repeating fence panel blocks the opening.");
    }

    [TestCase(false,OpeningKind.Door)]
    [TestCase(false,OpeningKind.Window)]
    [TestCase(true,OpeningKind.Door)]
    [TestCase(true,OpeningKind.Window)]
    public void EmptyOpeningsRemainClearSelectableAndRefillable(bool complex,OpeningKind kind)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var storefront=Asset<WallFamily>(Families+"Storefront/Storefront.asset");
        var insert=storefront.defaultInserts.Single(i=>i.kind==kind);
        var house=HouseEditing.Create(Vector3.zero,complex?storefront:Asset<WallFamily>(Families+"Plain/Plain.asset"));
        house.transform.rotation=Quaternion.Euler(0,28,0);
        var floor=HouseEditing.Floors(house).Single();
        var plan=new FloorEdit(floor);
        plan.Draw(Vector2.zero,Vector2.right*8);
        Assert.IsTrue(plan.Commit("Draw wall",out var issue),issue);
        var wall=floor.GetComponentInChildren<HouseWall>();
        float sill=kind==OpeningKind.Window?.8f:0;
        Assert.IsTrue(WallBuilder.Place(wall,insert,3,sill,out issue,false,empty:true),issue);
        var opening=wall.openings.Single();
        Assert.IsTrue(opening.Copy().empty);
        var rect=HouseSelection.OpeningBounds(wall,opening,false);
        var ray=new Ray(wall.transform.TransformPoint(rect.center-Vector3.forward),wall.transform.forward);
        Physics.SyncTransforms();
        Assert.IsFalse(Physics.Raycast(ray,2,~0,QueryTriggerInteraction.Ignore));
        Assert.IsEmpty(wall.GetComponentsInChildren<InsertAttachment>());
        float maxDepth=.5f;
        Assert.IsFalse(HouseScenePicker.PickEmptyOpening(wall,ray,ref maxDepth,out _),"A closer surface should win.");
        maxDepth=2;
        Assert.IsTrue(HouseScenePicker.PickEmptyOpening(wall,ray,ref maxDepth,out var hit));
        Assert.AreEqual(opening.id,hit.OpeningId);
        if(complex)
        {
            var frame=storefront.FindSurround(insert);
            Assert.IsTrue(wall.GetComponentsInChildren<AuthoredPanel>().Any(p=>p.source==frame.mesh));
        }
        Assert.IsFalse(WallBuilder.Place(wall,insert,3,sill,out issue),"The empty opening still reserves space.");
        Assert.IsTrue(HouseProperties.EditOpenings(new[]{hit},o=>o.empty=false),HouseScene.Message);
        Assert.AreEqual(opening.id,wall.GetComponentInChildren<InsertAttachment>().openingId);
        Assert.IsTrue(HouseProperties.EditOpenings(new[]{hit},o=>o.empty=true),HouseScene.Message);
        Assert.IsEmpty(wall.GetComponentsInChildren<InsertAttachment>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FamilyPanelFrameCanBePlacedPickedAndSwapped(bool standalone)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var fence=Asset<WallFamily>(Families+"MetalFence/Metal Fence.asset");
        var frame=Asset<HouseInsert>(Families+"MetalFence/Large Gate Frame.asset");
        var sliding=Asset<HouseInsert>(Families+"MetalFence/Large Gate Sliding.asset");
        var house=HouseEditing.Create(Vector3.zero,fence,standalone);
        house.transform.rotation=Quaternion.Euler(0,31,0);
        var floor=HouseEditing.Floors(house).Single();
        var plan=new FloorEdit(floor);
        plan.Draw(Vector2.zero,Vector2.right*10);
        Assert.IsTrue(plan.Commit("Draw test wall",out var issue),issue);
        var wall=floor.GetComponentInChildren<HouseWall>();
        Assert.AreSame(frame,OpeningPalette.Choices(wall,OpeningKind.Panel).First());
        Assert.IsTrue(WallBuilder.Place(wall,frame,5,0,out issue),issue);
        var placed=wall.openings.Single();
        var panel=wall.GetComponentsInChildren<AuthoredPanel>().Single(p=>p.source==frame.panelSegment.mesh);
        Assert.AreEqual(placed.id,HouseScenePicker.Surround(wall,panel.GetComponent<Renderer>(),panel.GetComponentInParent<WallOutput>()));
        Assert.IsEmpty(wall.GetComponentsInChildren<InsertAttachment>());
        Assert.IsFalse(WallBuilder.Place(wall,sliding,5.5f,0,out issue));
        Assert.IsTrue(HouseScene.EditOpening(wall,placed,sliding,6,0,true,out issue),issue);
        Assert.AreEqual(placed.id,wall.GetComponentInChildren<InsertAttachment>().openingId);
        Assert.IsTrue(HouseScene.EditOpening(wall,wall.openings.Single(),frame,5,0,true,out issue),issue);
        Assert.IsEmpty(wall.GetComponentsInChildren<InsertAttachment>());
        var plain=Asset<WallFamily>(Families+"Plain/Plain.asset");
        wall.familyOverride=plain;
        Assert.IsEmpty(OpeningPalette.Choices(wall,OpeningKind.Panel));
        Assert.IsFalse(WallBuilder.Rebuild(wall,out issue));
        StringAssert.Contains("does not offer",issue);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PrefabPanelsFitOnlyWhenOfferedAndLeaveWallAbove(bool complex)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var family=Object.Instantiate(Asset<WallFamily>(Families+(complex?"Storefront/Storefront.asset":"Plain/Plain.asset")));
        var insert=Object.Instantiate(Asset<HouseInsert>(Doors+"Door_MetalGate.asset"));
        try
        {
            insert.kind=OpeningKind.Panel;
            var house=HouseEditing.Create(Vector3.zero,family);
            var floor=HouseEditing.Floors(house).Single();
            var plan=new FloorEdit(floor);
            plan.Draw(Vector2.zero,Vector2.right*7);
            Assert.IsTrue(plan.Commit("Draw wall",out var issue),issue);
            var wall=floor.GetComponentInChildren<HouseWall>();
            Assert.IsFalse(WallBuilder.Place(wall,insert,3,0,out issue));
            family.defaultInserts=new[]{insert};
            Assert.Contains(insert,OpeningPalette.Choices(wall,OpeningKind.Panel));
            Assert.IsTrue(WallBuilder.Place(wall,insert,3,0,out issue),issue);
            Assert.IsNotNull(wall.GetComponentInChildren<InsertAttachment>());
            var output=wall.GetComponentInChildren<WallOutput>();
            var above=wall.transform.TransformPoint(new Vector3(3,2.5f,0));
            Assert.IsTrue(output.GetComponentsInChildren<Renderer>().Any(r=>r.bounds.Contains(above)));
        }
        finally { Object.DestroyImmediate(family);Object.DestroyImmediate(insert); }
    }

    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator SlidingGateOpensAndClosesThroughItsGameInteraction()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new UnityEngine.TestTools.EnterPlayMode();
        var prefab=Asset<HouseInsert>(Families+"MetalFence/Large Gate Sliding.asset").prefab;
        var gate=Object.Instantiate(prefab,new Vector3(12,2,-7),Quaternion.Euler(0,37,0));
        var leaf=gate.GetComponentInChildren<MeshFilter>().transform;
        var trigger=gate.GetComponentsInChildren<BoxCollider>().Single(c=>c.isTrigger);
        var interactive=trigger.GetComponent("Interactive");
        Assert.IsNotNull(interactive);
        var position=leaf.localPosition;
        var triggerPosition=trigger.transform.position;
        var origin=gate.transform.TransformPoint(new Vector3(0,1,-1));
        bool Blocked()
        {
            Physics.SyncTransforms();
            return Physics.Raycast(origin,gate.transform.forward,2,~0,QueryTriggerInteraction.Ignore);
        }
        Assert.IsTrue(Blocked());
        void Use()=>interactive.GetType().GetMethod("Interact").Invoke(interactive,new object[]{null});
        Use();
        float until=Time.realtimeSinceStartup+7;
        while(leaf.localPosition.x<position.x+5.39f && Time.realtimeSinceStartup<until)yield return null;
        Assert.That(leaf.localPosition.x-position.x,Is.EqualTo(5.4f).Within(.015f));
        Assert.IsFalse(Blocked());
        Assert.AreEqual(triggerPosition,trigger.transform.position);
        Use();
        until=Time.realtimeSinceStartup+7;
        while(leaf.localPosition!=position && Time.realtimeSinceStartup<until)yield return null;
        Assert.AreEqual(position,leaf.localPosition);
        Assert.IsTrue(Blocked());
        yield return new UnityEngine.TestTools.ExitPlayMode();
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
