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

    [Test] public void RebuildPreservesLooseDecorationsAndOnlyExplicitAssignmentMovesThemIntoRooms()
    {
        var wall = floor.GetComponentInChildren<HouseWall>();
        var output = wall.GetComponentInChildren<WallOutput>();
        var group = new GameObject("Grocery display");
        group.transform.SetParent(output.surfaces.First().transform, true);
        group.transform.position = floor.World(new Vector2(2, 2)) + Vector3.up;
        var product = GameObject.CreatePrimitive(PrimitiveType.Cube);
        product.transform.SetParent(group.transform, false);
        var excluded = new GameObject("Excluded decorations");
        excluded.transform.SetParent(wall.transform, false);
        excluded.AddComponent<ExcludeFromRoomAssignment>();
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.transform.SetParent(excluded.transform, false);
        sign.transform.position = group.transform.position;
        var position = group.transform.position;
        Assert.IsTrue(WallBuilder.Rebuild(wall, out var issue), issue);
        Assert.AreSame(wall.transform, group.transform.parent);
        Assert.Less(Vector3.Distance(position, group.transform.position), .001f);
        Assert.AreSame(group.transform, product.transform.parent);
        RoomAssignment.Assign(house);
        var contents = group.transform.parent;
        Assert.IsNotNull(contents.GetComponent<RoomContents>());
        Assert.AreSame(excluded.transform, sign.transform.parent);
        Assert.AreSame(wall.transform, excluded.transform.parent);
        group.transform.position += Vector3.right * 20;
        var plan = new FloorEdit(floor);
        Assert.IsTrue(plan.Commit("Rebuild rooms", out issue), issue);
        Assert.AreSame(contents, group.transform.parent);
    }

    [UnityTest] public IEnumerator NestedGroceryBoxCanBePickedMovedAndUndoneAndFollowsFloorCutaway()
    {
        var upper = HouseEditing.AddFloor(house, true);
        var wall = upper.GetComponentInChildren<HouseWall>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/StorageBoxes/StorageBox_NoCollision.prefab");
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wall.transform);
        prop.transform.position = upper.World(new Vector2(3,2)) + Vector3.up * .6f;
        var visual = prop.GetComponentInChildren<Renderer>();
        var view = ScriptableObject.CreateInstance<SceneView>();
        var oldTool = Tools.current;
        var oldPivot = Tools.pivotMode;
        var oldRotation = Tools.pivotRotation;
        try
        {
            view.Show();
            view.position = new Rect(80,80,850,680);
            view.orthographic = true;
            Tools.current = Tool.Move;
            Tools.pivotMode = PivotMode.Pivot;
            Tools.pivotRotation = PivotRotation.Global;
            HouseScene.Enter(floor);
            HouseScene.SwitchFloor(floor);
            Assert.IsTrue(SceneVisibilityManager.instance.IsHidden(prop));
            HouseScene.SwitchFloor(upper);
            Assert.IsFalse(SceneVisibilityManager.instance.IsHidden(prop));
            var point = visual.bounds.center;
            view.LookAtDirect(point, Quaternion.LookRotation(Vector3.down,Vector3.forward), 3);
            view.Focus(); view.Repaint(); yield return null; yield return null;
            PavementEditingTests.ClickScene(view,point);
            Assert.AreSame(prop, Selection.activeGameObject, "House Select should pick the box through its visible mesh.");
            Assert.IsFalse(Tools.hidden);
            var original = prop.transform.position;
            float size = 0;
            void Measure(SceneView v)
            {
                if (v == view && Event.current.commandName == "MeasurePropHandle") size = HandleUtility.GetHandleSize(original);
            }
            SceneView.duringSceneGui += Measure;
            try { view.SendEvent(EditorGUIUtility.CommandEvent("MeasurePropHandle")); }
            finally { SceneView.duringSceneGui -= Measure; }
            PavementEditingTests.Drag(view, original + Vector3.right * size * .76f, original + Vector3.right * (size * .76f + .6f), shiftOnDrag:false);
            Assert.Greater(prop.transform.position.x-original.x,.3f,"Drag the native Move gizmo.");
            Assert.AreSame(wall.transform,prop.transform.parent);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.Less(Vector3.Distance(original,prop.transform.position),.001f);
            HouseScene.Leave();
            // Keep the roof and walls out of the camera ray for the native pick.
            var roof = upper.GetComponentsInChildren<HouseSlab>().First(slab => slab.overhead);
            SceneVisibilityManager.instance.Hide(roof.gameObject, true);
            Selection.activeObject = null;
            view.Repaint(); yield return null; yield return null;
            PavementEditingTests.ClickScene(view,visual.bounds.center);
            yield return null; yield return null;
            Assert.AreSame(prop, Selection.activeGameObject, "Normal Scene selection should pick the same prefab.");
        }
        finally
        {
            HouseScene.Leave(); view.Close(); Tools.current = oldTool;
            Tools.pivotMode = oldPivot; Tools.pivotRotation = oldRotation;
        }
    }

    [UnityTest] public IEnumerator NativePickOfBoxInPrefabBuildingSelectsTheBox()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/StorageBoxes/StorageBox_NoCollision.prefab");
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, floor.transform);
        // Outside the roof footprint so that only the box is visible at this pixel.
        prop.transform.position = floor.World(new Vector2(9, 3));
        var position = prop.GetComponentInChildren<Renderer>().bounds.center;
        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/GrocerySelectionTest.prefab");
        var view = ScriptableObject.CreateInstance<SceneView>();
        try
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(house.gameObject,path,InteractionMode.AutomatedAction);
            Selection.activeObject = null;
            view.Show(); view.position = new Rect(80,80,850,680); view.orthographic = true;
            view.LookAtDirect(position, Quaternion.LookRotation(Vector3.down,Vector3.forward), 3);
            view.Focus(); view.Repaint(); yield return null; yield return null;
            PavementEditingTests.ClickScene(view,position);
            yield return null; yield return null;
            Assert.AreSame(prop, Selection.activeGameObject);
        }
        finally { view.Close(); AssetDatabase.DeleteAsset(path); }
    }

    [Test] public void ExtraDecorationDoesNotBecomePartOfAWindowOrWallFixture()
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First();
        var window = AssetDatabase.LoadAssetAtPath<HouseInsert>("Assets/Game/Environment/Windows/SquareWindow/SquareWindow_Insert.asset");
        Assert.IsTrue(WallBuilder.Place(wall, window, 2, .7f, out var issue), issue);
        var attachment = wall.GetComponentInChildren<InsertAttachment>();
        var model = attachment.transform.GetChild(0);
        Assert.AreSame(model.gameObject, HouseOwnership.InsertModel(model.GetComponentInChildren<Renderer>().gameObject, attachment));
        var priceTag = GameObject.CreatePrimitive(PrimitiveType.Quad);
        priceTag.transform.SetParent(model, false);
        Assert.IsNull(HouseOwnership.InsertModel(priceTag, attachment));
        Assert.AreSame(priceTag, HouseOwnership.PropRoot(priceTag));

        var fixture = FixtureTool.Place(palette.fixtures.First(c => c.mount == FixtureMount.Wall),wall.transform,wall.transform.position,1);
        Assert.IsTrue(HouseOwnership.IsFixturePart(fixture.GetComponentInChildren<Renderer>().gameObject, fixture));
        wall.openings.Clear();
        Assert.IsTrue(WallBuilder.Rebuild(wall, out issue), issue);
        Assert.AreSame(wall.transform, priceTag.transform.parent, "Removing the generated window must preserve its added decoration.");
        priceTag.transform.SetParent(fixture.transform, true);
        Assert.IsFalse(HouseOwnership.IsFixturePart(priceTag, fixture));
    }

    [UnityTest] public IEnumerator RoomWarningTracksAPropAcrossTheWallAndUndo()
    {
        var split = new FloorEdit(floor);
        split.Draw(new Vector2(3,0), new Vector2(3,4));
        Assert.IsTrue(split.Commit("Divide shop", out var issue), issue);
        var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prop.transform.rotation = floor.transform.rotation;
        prop.transform.position = floor.World(new Vector2(1,2)) + floor.transform.up;
        RoomAssignment.Assign(house);
        var room = prop.GetComponentInParent<HouseRoom>();
        Assert.IsNotNull(room);
        var parent = prop.transform.parent;
        var material = prop.GetComponent<Renderer>().sharedMaterial;
        yield return null;
        Assert.IsEmpty(RoomContentsWarnings.ForRoom(room));

        Undo.IncrementCurrentGroup();
        Undo.RecordObject(prop.transform, "Move display across aisle");
        prop.transform.position = floor.World(new Vector2(2.8f,2)) + floor.transform.up;
        Undo.FlushUndoRecordObjects();
        for (int i = 0; i < 10 && RoomContentsWarnings.ForRoom(room).Count == 0; i++) yield return null;
        var warning = RoomContentsWarnings.ForRoom(room).Single();
        Assert.AreSame(prop, warning.item, "The pivot is inside but part of the display crosses the wall.");
        Assert.That(Vector3.Distance(warning.bounds.center, prop.transform.position), Is.LessThan(.001f));
        Assert.AreSame(parent, prop.transform.parent);
        Assert.AreSame(material, prop.GetComponent<Renderer>().sharedMaterial);
        Undo.PerformUndo();
        Assert.IsEmpty(RoomContentsWarnings.ForRoom(room));
        Undo.PerformRedo();
        Assert.AreSame(prop, RoomContentsWarnings.ForRoom(room).Single().item);
        prop.transform.position = floor.World(new Vector2(5,2)) + floor.transform.up;
        RoomAssignment.Assign(house);
        yield return null; yield return null;
        var next = prop.GetComponentInParent<HouseRoom>();
        Assert.IsNotNull(next);
        Assert.AreNotSame(room, next);
        Assert.IsEmpty(RoomContentsWarnings.ForRoom(room));
        Assert.IsEmpty(RoomContentsWarnings.ForRoom(next));
    }

    [Test] public void RoomWarningUsesStoreyHeightTriggerExtentAndEmitterPosition()
    {
        var room = floor.GetComponentInChildren<HouseRoom>();
        var contents = new GameObject("Contents").AddComponent<RoomContents>();
        contents.transform.SetParent(room.transform, false);
        Transform Prop(string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(contents.transform, false);
            t.position = floor.transform.TransformPoint(position);
            return t;
        }
        var trigger = Prop("Till zone", new Vector3(5.8f,1,2)).gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        var lamp = Prop("Upstairs light", new Vector3(2,floor.height + .1f,2)).gameObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        var sound = Prop("Indoor emitter", new Vector3(2,1,2)).gameObject.AddComponent<AudioSource>();
        sound.spatialBlend = 1; sound.maxDistance = 100;
        var exempt = Prop("Excluded group", new Vector3(9,1,2));
        exempt.gameObject.AddComponent<ExcludeFromRoomAssignment>();
        GameObject.CreatePrimitive(PrimitiveType.Cube).transform.SetParent(exempt, false);
        var shared = new GameObject("Shared contents").AddComponent<RoomContents>();
        shared.transform.SetParent(floor.transform, false);
        GameObject.CreatePrimitive(PrimitiveType.Cube).transform.SetParent(shared.transform, false);
        var warnings = RoomContentsWarnings.ForRoom(room);
        CollectionAssert.AreEquivalent(new[]{trigger.gameObject, lamp.gameObject}, warnings.Select(w => w.item));
        Assert.That(Vector3.Distance(warnings.Single(w => w.item == lamp.gameObject).bounds.center,
            lamp.transform.position), Is.LessThan(.001f));
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
                var sourceLight = choice.prefab.GetComponentInChildren<Light>(true);
                Assert.IsNotNull(sourceLight,choice.label);
                Assert.IsTrue(renderers.Any(r=>r.sharedMaterials.Contains(choice.diffuserMaterial)),choice.label);
                Assert.IsTrue(choice.diffuserMaterial.IsKeywordEnabled("_EMISSION"),choice.label);
                Assert.AreEqual(sourceLight ? sourceLight.shadows : LightShadows.None, light.shadows, choice.label);
                if (choice.label == "Bar light" || choice.label == "Wall light")
                {
                    Assert.AreEqual(LightShadows.Hard, light.shadows);
                    foreach (var housing in renderers)
                        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, housing.shadowCastingMode, choice.label);
                }
            }
        }
    }

    [Test] public void MineLampRecessesHalfItsCageOnEitherWallFace()
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First(w => w.length > 5);
        var choice = palette.fixtures.Single(c => c.label == "Mine lamp");
        Assert.IsTrue(choice.Supports(FixtureMount.Floor));
        Assert.IsTrue(choice.Supports(FixtureMount.Wall));
        Physics.SyncTransforms();
        foreach (int side in new[] { 1, -1 })
        {
            var normal = wall.transform.forward * side;
            var point = wall.transform.TransformPoint(new Vector3(.35f, 1.6f, side * wall.Thickness * .5f));
            var fixture = FixtureTool.PlaceFromRay(choice, new Ray(point + normal * .25f, -normal), point, null);
            Assert.AreSame(wall.transform, fixture.transform.parent);
            Assert.AreEqual(FixtureMount.Wall, fixture.mount);
            var depths = fixture.GetComponentsInChildren<MeshFilter>().SelectMany(filter =>
                filter.sharedMesh.vertices.Select(vertex => Vector3.Dot(filter.transform.TransformPoint(vertex) - point, normal))).ToArray();
            Assert.That(depths.Max(), Is.InRange(.08f, .12f));
            Assert.AreEqual(-depths.Min(), depths.Max(), .003f);
            Assert.That(Vector3.Dot(fixture.emitter.transform.position - point, normal), Is.InRange(.025f, depths.Max()));
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

    [Test] public void MountedPointLightUpgradesAnUntouchedLegacyEmitterOnce()
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First();
        var choice = palette.fixtures.First(c => c.mount == FixtureMount.Wall && c.lightRange > 0);
        foreach (int side in new[] { -1, 1 })
        {
            var fixture = FixtureTool.Place(choice, wall.transform, wall.transform.position, side);
            var face = fixture.emitter;
            Assert.AreEqual(LightType.Point, face.type);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(fixture.transform, false);
            fixture.emitter = light;
            var model = fixture.transform.GetChild(0);
            var mesh = model.GetComponentInChildren<MeshFilter>();
            var bounds = mesh.sharedMesh.bounds;
            float front = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                front = Mathf.Max(front,fixture.transform.InverseTransformPoint(mesh.transform.TransformPoint(corner)).z);
            }
            fixture.emitterFitted = false;
            light.type = LightType.Point;
            light.transform.localPosition = Vector3.forward * (front + .04f);
            light.intensity = 1.23f;
            Assert.IsTrue(FixtureTool.UpgradeEmitter(fixture));
            Assert.AreEqual(1.23f, light.intensity, .001f);
            Assert.Less(Vector3.Distance(face.transform.position, light.transform.position), .0001f);
            Assert.IsFalse(face.enabled);
            Assert.IsFalse(FixtureTool.UpgradeEmitter(fixture));
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

    [TestCase("Wall light", 72f, 40f)]
    [TestCase("Ceiling light", 90f, 60f)]
    [TestCase("Bollard light", 9f, 3f)]
    public void SharedSpreadSupportsOverridesRefitNewPlacementsAndUndo(string label, float shared, float local)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/FixtureSpreadTest.asset");
        Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(palette), path));
        try
        {
            var copy = AssetDatabase.LoadAssetAtPath<FixturePalette>(path);
            var preset = copy.fixtures.Single(c => c.label == label);
            // Exercise a custom model that requests a generated directional beam.
            if (preset.mount != FixtureMount.Floor) preset.prefab = copy.fixtures.Single(c => c.label == "Vent cover").prefab;
            var host = preset.mount == FixtureMount.Wall ? floor.GetComponentInChildren<HouseWall>().transform : floor.transform;
            FixtureAttachment Place() => FixtureTool.Place(preset, host, host.position, 1);
            float Spread(FixtureAttachment f) => preset.mount == FixtureMount.Floor ? f.emitter.range : f.emitter.spotAngle;
            var a = Place();
            var b = Place();
            var c = Place();
            var unrelated = FixtureTool.Place(palette.fixtures.Single(p => p.label == label), host, host.position, 1);
            float unrelatedSpread = Spread(unrelated);
            float original = Spread(a);
            FixtureTool.SetSpread(b, true, local);
            c.gameObject.SetActive(false);
            Undo.IncrementCurrentGroup();
            FixtureTool.SetSpread(a, false, shared);
            Assert.AreEqual(shared, Spread(a), .001f);
            Assert.AreEqual(local, Spread(b), .001f);
            Assert.AreEqual(shared, Spread(c), .001f);
            Assert.AreEqual(unrelatedSpread, Spread(unrelated), .001f);
            Assert.AreEqual(preset.lightIntensity, a.emitter.intensity);
            if (preset.mount != FixtureMount.Floor)
            {
                Assert.AreEqual(shared * 100f / 155f, a.emitter.innerSpotAngle, .001f);
                Assert.AreEqual(preset.lightRange, a.emitter.range);
            }
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.AreEqual(original, Spread(a), .001f);
            Assert.AreEqual(local, Spread(b), .001f);
            Undo.PerformRedo();
            Assert.AreEqual(shared, Spread(a), .001f);
            Assert.AreEqual(shared, Spread(Place()), .001f);
            Assert.IsTrue(FixtureTool.Refit(b));
            Assert.AreEqual(local, Spread(b), .001f);
            FixtureTool.SetSpread(b, false, local);
            Assert.AreEqual(shared, Spread(b), .001f);
            Assert.AreEqual(shared, preset.mount == FixtureMount.Floor ? a.LightPreset.lightRange : a.LightPreset.beamAngle, .001f);
        }
        finally { AssetDatabase.DeleteAsset(path); }
    }

    [Test] public void ReopeningASceneUsesTheLatestSharedSpreadAndKeepsItsOverride()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/FixtureSpreadReload.asset");
        string scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/FixtureSpreadReload.unity");
        Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(palette), path));
        try
        {
            var choice = AssetDatabase.LoadAssetAtPath<FixturePalette>(path).fixtures.Single(c => c.label == "Wall light");
            choice.prefab = palette.fixtures.Single(c => c.label == "Vent cover").prefab;
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<FixturePalette>(path));
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<FixturePalette>(path));
            FixtureTool.SelectBrush(choice);
            var host = floor.GetComponentInChildren<HouseWall>().transform;
            FixtureTool.Place(choice, host, host.position, 1);
            FixtureTool.SetSpread(FixtureTool.Place(choice, host, host.position, 1), true, 67);
            Assert.IsTrue(EditorSceneManager.SaveScene(house.gameObject.scene, scenePath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            host = new GameObject("Fixture host").transform;
            choice = FixtureTool.Brush;
            Assert.IsNotNull(choice, "The selected fixture swatch should survive switching scenes.");
            var editing = FixtureTool.Place(choice, host, host.position, 1);
            Assert.AreSame(choice, editing.LightPreset, "Placement should bind the selected palette entry.");
            FixtureTool.SetSpread(editing, false, 110);
            Assert.AreEqual(110, editing.emitter.spotAngle, .001f, "Shared edit should reach the active light.");
            var scene = EditorSceneManager.OpenScene(scenePath);
            var fixtures = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FixtureAttachment>()).ToArray();
            var inherited = fixtures.Single(f => !f.overrideSpread);
            Assert.AreEqual(path, AssetDatabase.GetAssetPath(inherited.lightPalette), "Reopen should retain the palette.");
            Assert.AreEqual(110, inherited.LightPreset.beamAngle, .001f, "Reopen should retain the edited preset.");
            Assert.AreEqual(110, inherited.emitter.spotAngle, .001f, "Reopen should apply the current preset.");
            Assert.AreEqual(67, fixtures.Single(f => f.overrideSpread).emitter.spotAngle, .001f);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(scenePath);
            AssetDatabase.DeleteAsset(path);
        }
    }

    [Test] public void ExistingFixturesAcquirePresetOnSceneProcessingAndPreserveACustomBeam()
    {
        var choice = palette.fixtures.Single(c => c.label == "Wall light");
        var host = floor.GetComponentInChildren<HouseWall>().transform;
        var fixture = FixtureTool.Place(choice, host, host.position, 1);
        var legacy = new GameObject("Light").AddComponent<Light>();
        legacy.transform.SetParent(fixture.transform, false);
        legacy.type = LightType.Spot;
        legacy.spotAngle = 88;
        fixture.emitter = null;
        fixture.lightPalette = null;
        fixture.lightPrefab = null;
        new BuildMaterials().OnProcessScene(house.gameObject.scene, null);
        Assert.AreSame(choice, fixture.LightPreset);
        Assert.IsTrue(fixture.overrideSpread);
        Assert.AreSame(legacy, fixture.emitter);
        Assert.AreEqual(LightType.Point, fixture.emitter.type);
        Assert.IsFalse(FixtureTool.SyncLighting(fixture));
        var socket = FixtureTool.Place(palette.fixtures.Single(c => c.label == "Power socket"), host, host.position, 1);
        Assert.IsNull(socket.emitter);
        Assert.IsFalse(FixtureTool.SyncLighting(socket));
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

    [Test] public void BarDiffuserRendersAboveWhiteInAnUnlitRoom()
    {
        var choice = palette.fixtures.Single(c => c.label == "Bar light");
        var fixture = FixtureTool.Place(choice, null, Vector3.right * 100, 1);
        fixture.emitter.cullingMask = 0;
        var camera = new GameObject("Diffuser review camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(fixture.transform.position + Vector3.forward, Quaternion.LookRotation(Vector3.back));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = .12f;
        camera.allowHDR = true;
        var target = RenderTexture.GetTemporary(256, 64, 24, RenderTextureFormat.ARGBHalf);
        var previous = RenderTexture.active;
        var pixel = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        try
        {
            camera.targetTexture = target;
            float ReadCenter()
            {
                camera.Render();
                RenderTexture.active = target;
                pixel.ReadPixels(new Rect(128, 32, 1, 1), 0, 0);
                return pixel.GetPixel(0, 0).r;
            }
            float on = ReadCenter();
            FixtureAttachment.SetPower(fixture.emitter, false);
            float off = ReadCenter();
            Assert.That(on - off, Is.GreaterThan(1f), $"Center of bar, powered {on:F2}, unpowered {off:F2}");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(pixel);
            Object.DestroyImmediate(camera.gameObject);
        }
    }

    [TestCase("Pancake light")]
    [TestCase("Bar light")]
    public void FlatFixturesRotateFlushAndMoveBetweenWallAndCeiling(string label)
    {
        var choice = palette.fixtures.Single(c => c.label == label);
        var wall = floor.GetComponentsInChildren<HouseWall>().First(w => w.length > 5);
        var pos = wall.transform.TransformPoint(new Vector3(3, 1, 0));
        var ray = new Ray(pos - wall.transform.forward, wall.transform.forward);
        Assert.IsTrue(FixtureTool.TargetSurface(floor, choice, choice.mount, ray, out var host, out var point, out var side));
        Assert.AreSame(wall.transform, host);
        var f = FixtureTool.Place(choice, host, point, side);
        var model = f.GetComponentInChildren<MeshFilter>();
        var renderer = model.GetComponent<Renderer>();
        Assert.IsTrue(renderer.sharedMaterial.enableInstancing);
        Assert.IsTrue(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"), "The diffuser must remain emissive after URP imports it.");
        float emission = renderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent;
        if (label == "Bar light")
            Assert.That(emission, Is.InRange(1.1f, 1.6f), "The fluorescent face should peak slightly above white.");
        else
            Assert.That(emission, Is.InRange(.1f, .9f));
        Assert.IsEmpty(f.GetComponentsInChildren<Collider>());
        void CheckFace(Vector3 normal)
        {
            var distances = model.sharedMesh.vertices.Select(v => Vector3.Dot(model.transform.TransformPoint(v) - f.transform.position, normal)).ToArray();
            Assert.AreEqual(0, distances.Min(), .001f);
            Assert.That(distances.Max(), Is.InRange(.03f, .05f));
            Assert.AreEqual(LightType.Point, f.emitter.type);
            Assert.Less(Vector3.Distance(f.emitter.transform.position, renderer.bounds.center + normal * f.lightOffset), .001f);
            Assert.Greater(f.emitter.intensity, 0);
            Assert.IsTrue(f.emitter.enabled);
            Assert.AreSame(choice, f.LightPreset);
            Assert.AreEqual(1, f.GetComponentsInChildren<Light>().Length);
        }
        FixtureTool.SetRotation(f, 90);
        Assert.Greater(Mathf.Abs(Vector3.Dot(f.transform.right, wall.transform.up)), .999f);
        CheckFace(wall.transform.forward * side);
        FixtureTool.SetLightOffset(f, .25f);
        CheckFace(wall.transform.forward * side);
        ray = new Ray(floor.World(new Vector2(3,2)) + floor.transform.up, floor.transform.up);
        Assert.IsTrue(FixtureTool.TargetSurface(floor, choice, choice.mount, ray, out host, out point, out side));
        Assert.AreSame(floor.transform, host);
        FixtureTool.Mount(f, host, point, side);
        Assert.AreEqual(FixtureMount.Ceiling, f.mount);
        CheckFace(-floor.transform.up);
        Assert.IsTrue(FixtureTool.Refit(f));
        Assert.AreEqual(90, f.surfaceRotation, .001f);
        CheckFace(-floor.transform.up);
        var second = FixtureTool.Place(choice, host, point, side);
        Assert.AreEqual(0, second.lightOffset);
        FixtureTool.SetLightOffset(f, 0);
        CheckFace(-floor.transform.up);
        Assert.AreSame(model.sharedMesh, second.GetComponentInChildren<MeshFilter>().sharedMesh);
        Assert.AreSame(renderer.sharedMaterial, second.GetComponentInChildren<Renderer>().sharedMaterial);
    }

    [Test] public void WallAndCeilingShareRangeAndIntensityWithIndependentLocalOverrides()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/FixtureLightingTest.asset");
        Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(palette), path));
        try
        {
            var choice = AssetDatabase.LoadAssetAtPath<FixturePalette>(path).fixtures.Single(c => c.label == "Bar light");
            var host = floor.GetComponentInChildren<HouseWall>().transform;
            var a = FixtureTool.Place(choice, host, host.position, 1);
            var b = FixtureTool.Place(choice, floor.transform, floor.transform.position, 1);
            FixtureTool.SetLightField(a, FixtureTool.LightField.Range, true, 4);
            FixtureTool.SetLightField(b, FixtureTool.LightField.Range, false, 10);
            Undo.IncrementCurrentGroup();
            float original = choice.lightIntensity;
            FixtureTool.SetLightField(b, FixtureTool.LightField.Intensity, false, 2.5f);
            Assert.AreEqual(2.5f, a.emitter.intensity);
            Assert.AreEqual(4, a.emitter.range);
            Assert.AreEqual(10, b.emitter.range);
            Assert.AreEqual(LightType.Point, a.emitter.type);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.AreEqual(original, a.emitter.intensity);
            Undo.PerformRedo();
            FixtureTool.SetLightField(a, FixtureTool.LightField.Intensity, true, 0);
            FixtureTool.SetLightField(a, FixtureTool.LightField.Range, false, 4);
            FixtureTool.Refit(a);
            Assert.AreEqual(10, a.emitter.range);
            Assert.AreEqual(0, a.emitter.intensity);
            Assert.AreEqual(2.5f, b.emitter.intensity);
            var legacy = FixtureTool.Place(b.LightPreset, host, host.position, 1);
            legacy.lightingInitialized = false;
            legacy.emitter.range = 7;
            legacy.emitter.intensity = .4f;
            FixtureTool.SyncLighting(legacy);
            Assert.AreEqual(10, legacy.emitter.range);
            Assert.AreEqual(.4f, legacy.emitter.intensity);
            Assert.IsTrue(legacy.intensity.enabled);
        }
        finally { AssetDatabase.DeleteAsset(path); }
    }

    [Test] public void SlidingFixtureKeepsItsFaceOffsetAndUndoesOnARotatedWall()
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First(w => w.length > 5);
        var point = new Vector3(1, 1, -wall.Thickness * .5f - .025f);
        var fixture = FixtureTool.Place(palette.fixtures.Single(c => c.label == "Bar light"),
            wall.transform, wall.transform.TransformPoint(point), -1);
        FixtureTool.SetRotation(fixture, 70);
        Undo.IncrementCurrentGroup();
        FixtureTool.Slide(fixture, wall.transform.TransformPoint(new Vector3(3, 2, 4)));
        Assert.Less(Vector3.Distance(fixture.transform.localPosition, new Vector3(3, 2, point.z)), .001f);
        Assert.AreSame(wall.transform, fixture.transform.parent);
        Assert.AreEqual(70, fixture.surfaceRotation, .001f);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.Less(Vector3.Distance(fixture.transform.localPosition, point), .001f);
    }

    [UnityTest] public IEnumerator WallFixtureMoveGizmoSlidesOnItsRotatedHost()
    {
        var wall = floor.GetComponentsInChildren<HouseWall>().First(w => w.length > 5);
        var start = new Vector3(2, 1, wall.Thickness * .5f);
        var fixture = FixtureTool.Place(palette.fixtures.Single(c => c.label == "Bar light"),
            wall.transform, wall.transform.TransformPoint(start), 1);
        var view = ScriptableObject.CreateInstance<SceneView>();
        var previous = Tools.current;
        try
        {
            view.Show(); view.position = new Rect(80,80,850,680); view.orthographic = true;
            view.LookAtDirect(fixture.transform.position, Quaternion.LookRotation(-wall.transform.forward, wall.transform.up), 3);
            Tools.current = Tool.Move;
            Selection.activeGameObject = fixture.gameObject;
            view.Focus(); view.Repaint(); yield return null; yield return null;
            var world = fixture.transform.position;
            PavementEditingTests.Drag(view, world, world + wall.transform.right * .6f + wall.transform.up * .4f, shiftOnDrag:false);
            var delta = fixture.transform.localPosition - start;
            Assert.That(delta.x, Is.EqualTo(.6f).Within(.04f));
            Assert.That(delta.y, Is.EqualTo(.4f).Within(.04f));
            Assert.AreEqual(0, delta.z, .001f);
            Assert.AreSame(wall.transform, fixture.transform.parent);
        }
        finally { Selection.activeObject = null; view.Close(); Tools.current = previous; }
    }

    [Test] public void LegacyBarKeepsItsLightReferenceAndHasOnlyOneActivePointAtTheDiffuser()
    {
        var choice = palette.fixtures.Single(c => c.label == "Bar light");
        var host = floor.GetComponentInChildren<HouseWall>().transform;
        var f = FixtureTool.Place(choice, host, host.position, 1);
        var face = f.emitter;
        var old = new GameObject("Light").AddComponent<Light>();
        old.transform.SetParent(f.transform, false);
        old.transform.localPosition = Vector3.forward * .4f;
        old.type = LightType.Spot;
        f.emitter = old;
        FixtureTool.SetLightField(f, FixtureTool.LightField.Range, true, 4);
        Assert.AreSame(old, f.emitter);
        Assert.AreEqual(LightType.Point, old.type);
        Assert.AreEqual(4f, old.range);
        Assert.AreEqual(LightShadows.Hard, old.shadows);
        Assert.IsFalse(face.enabled);
        Assert.Less(Vector3.Distance(face.transform.position, old.transform.position), .001f);
        FixtureTool.SetLightOffset(f, .3f);
        Assert.Less(Vector3.Distance(face.transform.position + f.transform.forward * .3f, old.transform.position), .001f);
        Assert.IsFalse(FixtureTool.SyncLighting(f));
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

    [Test] public void FreeLightsKeepIndependentEmissionAndRotateTheirOffsetAxis()
    {
        var choice=palette.fixtures.Single(c=>c.label=="Table lamp");
        var a=FixtureTool.PlaceFromRay(choice,new Ray(Vector3.up*80,Vector3.up),Vector3.zero,null);
        var b=FixtureTool.Place(choice,null,Vector3.right,1);
        Assert.IsTrue(a.freePlacement);
        var diffuser=a.GetComponentsInChildren<Renderer>().Single(r=>r.sharedMaterial==choice.diffuserMaterial);
        var other=b.GetComponentsInChildren<Renderer>().Single(r=>r.sharedMaterial==choice.diffuserMaterial);
        Assert.AreSame(diffuser.sharedMaterial,other.sharedMaterial);
        Color Glow(Renderer r){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,0);return block.GetColor("_EmissionColor");}
        var inherited=Glow(other);
        FixtureTool.SetAppearance(a,()=>{a.overrideColor=true;a.localColor=Color.blue;});
        Assert.AreEqual(Color.blue,a.emitter.color);
        Assert.Greater(Glow(diffuser).b,0);
        Assert.AreEqual(0,Glow(diffuser).r);
        Assert.AreEqual(inherited,Glow(other));
        FixtureTool.SetAppearance(a,()=>{a.overridePower=true;a.localOn=false;});
        Assert.IsFalse(a.emitter.enabled);
        Assert.AreEqual(0,Glow(diffuser).maxColorComponent);
        a.transform.rotation=Quaternion.Euler(0,0,90);
        var source=a.emitter.transform.position;
        var cover=diffuser.transform.position;
        FixtureTool.SetLightOffset(a,.4f);
        Assert.Less(Vector3.Distance(source+a.transform.up*.4f,a.emitter.transform.position),.001f);
        Assert.AreEqual(cover,diffuser.transform.position);
        FixtureTool.SetAppearance(a,()=>{a.overridePower=a.overrideColor=a.overrideOffset=false;});
        Assert.IsTrue(a.emitter.enabled);
        Assert.AreEqual(inherited,Glow(diffuser));
        Assert.Less(Vector3.Distance(source,a.emitter.transform.position),.001f);
        FixtureAttachment.SetPower(a.emitter,false);
        Assert.IsFalse(a.emitter.enabled);
        Assert.AreEqual(0,Glow(diffuser).maxColorComponent);
        FixtureAttachment.SetPower(a.emitter,true);
        Assert.AreEqual(inherited,Glow(diffuser));
    }

    [UnityTest] public IEnumerator SceneClickMountsMineLampOnSelectedFillerBuilding()
    {
        var building = new GameObject("Rotated filler").AddComponent<FillerBuilding>();
        building.transform.SetPositionAndRotation(Vector3.right * 20, Quaternion.Euler(0, 32, 0));
        var points = SlabOpening.Rectangle(new Rect(0, 0, 6, 3));
        Assert.IsTrue(GroundEditing.Change(building, "Footprint", plan => {
            for (int i = 0; i < points.Length; i++)
                GroundEditing.Line(plan, points[i], points[(i + 1) % points.Length]);
        }, out var issue), issue);
        building.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        var view = ScriptableObject.CreateInstance<SceneView>();
        try
        {
            var point = building.transform.TransformPoint(new Vector3(3, building.Elevation(1) + .5f, 0));
            view.Show();
            view.position = new Rect(80, 80, 850, 680);
            view.orthographic = true;
            view.LookAtDirect(point, building.transform.rotation, 3);
            FixtureTool.SelectBrush(palette.fixtures.Single(c => c.label == "Table lamp"));
            FixtureTool.BeginFacadeBrush(building);
            Assert.IsTrue(FixtureTool.Brush.Supports(FixtureMount.Wall));
            FixtureTool.SelectBrush(palette.fixtures.Single(c => c.label == "Mine lamp"));
            Physics.SyncTransforms();
            view.Focus();
            view.Repaint();
            yield return null;
            yield return null;
            PavementEditingTests.ClickScene(view, point);
            var lamp = building.GetComponentInChildren<FixtureAttachment>();
            Assert.IsNotNull(lamp, "Scene click should attach the selected swatch to this building.");
            Assert.AreEqual("Mine lamp", lamp.name);
            Assert.AreEqual(1, lamp.storey);
            Assert.AreEqual(FixtureMount.Wall, lamp.mount);
            Assert.That(Vector3.Distance(point, lamp.transform.position), Is.LessThan(.08f));
            Assert.AreSame(building, RoadScene.Active);
            Assert.AreSame(building.gameObject, Selection.activeGameObject);
            Assert.AreSame(building, FixtureTool.facadeBrush);
            Assert.IsFalse(lamp.overrideOffset);
            Assert.AreEqual(FixtureTool.Brush.emitterOffset, lamp.lightOffset, .001f);
            PavementEditingTests.ClickScene(view, point + building.transform.right);
            var lamps = building.GetComponentsInChildren<FixtureAttachment>();
            Assert.AreEqual(2, lamps.Length);
            Assert.AreSame(lamp.LightPreset, lamps.Single(other => other != lamp).LightPreset);
            view.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            Assert.IsNull(FixtureTool.facadeBrush);
            Assert.AreSame(building, RoadScene.Active);
        }
        finally { RoadScene.Leave(); view.Close(); }
    }

    [Test] public void FacadeLightFollowsStoreySplitMoveDeleteAndUndo()
    {
        var building=new GameObject("Facade").AddComponent<FillerBuilding>();
        building.transform.SetPositionAndRotation(Vector3.right*20,Quaternion.Euler(0,32,0));
        var a=Vector2.zero;var b=Vector2.right*6;var c=new Vector2(6,3);var d=new Vector2(0,3);
        Assert.IsTrue(GroundEditing.Change(building,"Footprint",p=>{
            GroundEditing.Line(p,a,b);GroundEditing.Line(p,b,c);GroundEditing.Line(p,c,d);GroundEditing.Line(p,d,a);
        },out var issue),issue);
        var edge=building.footprint.edges[0];
        var p=building.transform.TransformPoint(new Vector3(4,building.Elevation(1)+.5f,0));
        var n=-building.transform.forward;
        Physics.SyncTransforms();
        var lamp=FixtureTool.PlaceFromRay(palette.fixtures.Single(f=>f.label=="Bar light"),new Ray(p+n,-n),Vector3.zero,null);
        Assert.AreEqual(edge.id,lamp.facadeEdge);
        Assert.AreEqual(1,lamp.storey);
        Assert.IsTrue(GroundEditing.InsertPoint(building,edge.id,Vector2.right*2,out _,out issue),issue);
        Assert.AreNotEqual(edge.id,lamp.facadeEdge);
        Assert.Less(Vector3.Distance(p,lamp.transform.position),.001f);
        building.clearHeight+=1;
        building.Rebuild();
        Assert.Less(Vector3.Distance(p+building.transform.up,lamp.transform.position),.001f);
        var translated=Vector2.up*.7f;
        Assert.IsTrue(GroundEditing.Change(building,"Move fixture wall",plan=>GroundEditing.Move(plan,
            new System.Collections.Generic.HashSet<Vector2>{Vector2.right*2,b},translated),out issue),issue);
        Assert.Less(Vector3.Distance(p+building.transform.up+building.transform.forward*.7f,lamp.transform.position),.001f);
        var id=lamp.facadeEdge;
        Undo.IncrementCurrentGroup();
        Assert.IsTrue(GroundEditing.Change(building,"Delete fixture wall",plan=>plan.edges.RemoveAll(e=>e.id==id),out issue),issue);
        Assert.IsFalse(lamp);
        Undo.FlushUndoRecordObjects();Undo.PerformUndo();
        Assert.AreEqual(id,building.GetComponentInChildren<FixtureAttachment>().facadeEdge);
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
