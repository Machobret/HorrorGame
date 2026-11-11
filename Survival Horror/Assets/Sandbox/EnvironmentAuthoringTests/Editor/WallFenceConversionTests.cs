using System.Collections.Generic;
using System.Linq;
using EnvironmentAuthoring;
using EnvironmentAuthoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WallFenceConversionTests
{
    private HousePlan plan;
    private FenceFamily fence;
    private TownModule panel;
    private Mesh mesh;

    [SetUp]
    public void SetUp()
    {
        // The Edit Mode runner supplies an isolated empty scene.
        plan = HouseStructureCommands.CreateHouse(SceneManager.GetActiveScene());
        Assert.That(plan.Data.TryCreateRoom(Vector2.zero, new Vector2(4, 4), out var reason), Is.True, reason);
        foreach (var boundary in HouseBuilding.For(plan).Boundaries)
            HouseSurfaceLayout.Seed(boundary, string.IsNullOrEmpty(boundary.LowerId) ? null : plan.Data,
                string.IsNullOrEmpty(boundary.UpperId) ? null : plan.Data);
        var kit = plan.GetComponent<HouseWallBuilder>().Data;
        kit.DefaultFamily = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.CompleteFamilyPath);
        kit.ExteriorFamily = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.ExteriorFamilyPath);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(cube);
        panel = ScriptableObject.CreateInstance<TownModule>();
        panel.Kind = TownModule.ModuleKind.WallPanel;
        panel.Mesh = mesh; panel.Length = 2; panel.Height = 1; panel.Width = .1f;
        fence = ScriptableObject.CreateInstance<FenceFamily>();
        fence.Panels = new[] { panel };
        fence.Posts = new[] { new FenceFamily.Post { Mesh = mesh } };
    }

    [TearDown]
    public void TearDown()
    {
        if (plan != null) Object.DestroyImmediate(HouseBuilding.For(plan).gameObject);
        Object.DestroyImmediate(fence); Object.DestroyImmediate(panel); Object.DestroyImmediate(mesh);
        GeneratedMeshResources.Prune();
    }

    [Test]
    public void ConversionPreservesRoomAndIdentityAndCanResetWithUndo()
    {
        var wall = plan.Data.Walls[0]; string id = wall.Id; string room = plan.Data.CellId;
        var result = HouseEditCommands.SetWallFamily(plan, new[] { id }, fence);
        Assert.That(result.Applied, Is.True, result.Message);
        Assert.That(plan.Data.CellId, Is.EqualTo(room));
        Assert.That(plan.Data.Walls.Single(w => w.Id == id).Fence, Is.SameAs(fence));
        var output = plan.GetComponent<HouseWallBuilder>().Output;
        Assert.That(output.Problem, Is.Empty);
        var group = output.Groups.Single(g => g.Key == "wall:" + id);
        Assert.That(group.Root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(5));
        Assert.That(HouseWallOutputBuilder.IsCurrent(plan.GetComponent<HouseWallBuilder>(), HouseGenerationData.Create(plan)), Is.True);
        Assert.That(HouseSceneQueries.WallHeight(plan, plan.Data.Walls[0]).Max, Is.EqualTo(1));
        Assert.That(HouseCollisionBuilder.Output(plan).Groups.Single(g => g.Key == "wall:" + id)
            .Root.GetComponentsInChildren<MeshCollider>().Length, Is.EqualTo(5));
        result = HouseEditCommands.SetWallFamily(plan, new[] { id }, null);
        Assert.That(result.Applied, Is.True, result.Message);
        Assert.That(plan.Data.Walls[0].IsFence, Is.False);
        Undo.PerformUndo();
        Assert.That(plan.Data.Walls[0].Fence, Is.SameAs(fence));
        Undo.PerformRedo();
        Assert.That(plan.Data.Walls[0].IsFence, Is.False);
    }

    [Test]
    public void EveryWallCanConvertTogetherAndReturnToAnotherWallFamily()
    {
        var ids = plan.Data.Walls.Select(w => w.Id).ToArray();
        var result = HouseEditCommands.SetWallFamily(plan, ids, fence);
        Assert.That(result.Applied, Is.True, result.Message);
        Assert.That(plan.Data.Walls.All(w => w.IsFence), Is.True);
        var kit = plan.GetComponent<HouseWallBuilder>();
        Assert.That(kit.Output.Problem, Is.Empty);
        Assert.That(kit.Output.Groups.Count(g => g.Key.StartsWith("wall:")), Is.EqualTo(ids.Length));
        var family = kit.Data.DefaultFamily;
        result = HouseEditCommands.SetWallFamily(plan, ids, family);
        Assert.That(result.Applied, Is.True, result.Message);
        Assert.That(plan.Data.Walls.All(w => w.Family == family && !w.IsFence), Is.True);
        var restored = JsonUtility.FromJson<FloorPlan>(JsonUtility.ToJson(plan.Data));
        Assert.That(restored.Walls.All(w => w.Family == family), Is.True);
    }

    [Test]
    public void SplitCopyAndSerializationPreserveFenceAndDifferentFamiliesCannotDissolve()
    {
        plan.Data.Walls[0].SetFamily(fence);
        Assert.That(plan.Data.TryInsertCorner(0, .5f, out var reason), Is.True, reason);
        Assert.That(plan.Data.Walls.Count(w => w.IsFence), Is.EqualTo(2));
        Assert.That(plan.Data.Copy().Walls.Count(w => w.Fence == fence), Is.EqualTo(2));
        var restored = JsonUtility.FromJson<FloorPlan>(JsonUtility.ToJson(plan.Data));
        Assert.That(restored.Walls.Count(w => w.Fence == fence), Is.EqualTo(2));
        var split = plan.Data.Walls.First(w => w.IsFence);
        var adjacent = plan.Data.Walls.First(w => w.IsFence && w.Id != split.Id);
        adjacent.SetFamily(null);
        var node = new[] { split.StartId, split.EndId }.Intersect(new[] { adjacent.StartId, adjacent.EndId }).Single();
        Assert.That(plan.Data.TryDissolveCorner(node, out _), Is.False);
    }

    [Test]
    public void ConvertingAnOpeningWallRemovesOpeningsAndFenceRejectsNewOpenings()
    {
        var wall = plan.Data.Walls[0];
        Assert.That(plan.Data.TryAddDoorway(wall.Id, 2, 1, out var reason), Is.True, reason);
        var result = HouseEditCommands.SetWallFamily(plan, new[] { wall.Id }, fence);
        Assert.That(result.Applied, Is.True, result.Message);
        Assert.That(plan.Data.Walls[0].Openings, Is.Empty);
        Assert.That(plan.Data.TryAddDoorway(wall.Id, 2, 1, out _), Is.False);
    }

    [Test]
    public void InvalidFamilyAndMixedInvalidSelectionLeaveSourceUntouched()
    {
        string before = JsonUtility.ToJson(plan.Data);
        Assert.That(HouseEditCommands.SetWallFamily(plan, new[] { plan.Data.Walls[0].Id }, panel).Applied, Is.False);
        fence.Panels = new TownModule[0];
        Assert.That(HouseEditCommands.SetWallFamily(plan, new[] { plan.Data.Walls[0].Id }, fence).Applied, Is.False);
        fence.Panels = new[] { panel };
        Assert.That(HouseEditCommands.SetWallFamily(plan, new[] { plan.Data.Walls[0].Id, "missing" }, fence).Applied, Is.False);
        Assert.That(JsonUtility.ToJson(plan.Data), Is.EqualTo(before));
    }

    [Test]
    public void RailingRetainsItsNativeHeightRustAndCollisionWhenStoreyHeightChanges()
    {
        var railing = AssetDatabase.LoadAssetAtPath<WallFamily>("Assets/Game/Environment/Railings/Sources/Wall_Railing.asset");
        var rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Metal/M_Metal_Rusty_04.mat");
        Assert.That(railing, Is.Not.Null);
        Assert.That(rust, Is.Not.Null);
        var result = HouseEditCommands.SetWallFamily(plan, plan.Data.Walls.Select(w => w.Id), railing);
        Assert.That(result.Applied, Is.True, result.Message);
        result = HouseEditCommands.SetLevelHeight(plan, 4);
        Assert.That(result.Applied, Is.True, result.Message);
        var builder = plan.GetComponent<HouseWallBuilder>();
        Assert.That(builder.Output.Problem, Is.Empty);
        foreach (var wall in plan.Data.Walls)
        {
            Assert.That(HouseSceneQueries.WallHeight(plan, wall).Max, Is.EqualTo(1.198f).Within(.001f));
            var group = builder.Output.Groups.Single(g => g.Key == "wall:" + wall.Id);
            float top = group.Root.GetComponentsInChildren<MeshRenderer>()
                .Max(renderer => plan.transform.InverseTransformPoint(renderer.bounds.max).y);
            Assert.That(top, Is.EqualTo(1.198f).Within(.003f));
            Assert.That(group.Root.GetComponentsInChildren<MeshRenderer>()
                .All(renderer => renderer.sharedMaterials.All(material => material == rust)), Is.True);
            var collision = HouseCollisionBuilder.Output(plan).Groups.Single(g => g.Key == "wall:" + wall.Id);
            Assert.That(collision.Root.GetComponentsInChildren<Collider>()
                .Max(collider => plan.transform.InverseTransformPoint(collider.bounds.max).y), Is.EqualTo(1.198f).Within(.001f));
        }
        result = HouseEditCommands.SetWallFamily(plan, plan.Data.Walls.Select(w => w.Id), builder.Data.DefaultFamily);
        Assert.That(result.Applied, Is.True, result.Message);
        Assert.That(builder.Output.GetComponentsInChildren<MeshRenderer>()
            .Max(renderer => plan.transform.InverseTransformPoint(renderer.bounds.max).y), Is.GreaterThan(3.9f));
    }

    [Test]
    public void NativePanelsCropRemainderAndSourceEditsInvalidateOutput()
    {
        panel.AllowStretch = false;
        var parts = new List<TownRebuild.Part>();
        TownWallBuilder.BuildFence("rail", fence, Vector3.zero, Vector3.right * 3, null, null, true, parts);
        Assert.That(parts.Count(p => p.Key.Contains("/panel-")), Is.EqualTo(2));
        Assert.That(parts.Single(p => p.Key == "rail/panel-0").Mesh, Is.SameAs(mesh));
        Assert.That(parts.Single(p => p.Key == "rail/panel-1").Mesh, Is.Not.SameAs(mesh));
        var wall = plan.Data.Walls[0]; wall.SetFamily(fence);
        string original = HouseWallOutputBuilder.FenceInputs(wall);
        panel.Height = 1.2f;
        Assert.That(HouseWallOutputBuilder.FenceInputs(wall), Is.Not.EqualTo(original));
    }
}
