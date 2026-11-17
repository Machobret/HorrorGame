using System.Collections;
using System.Linq;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ShelfEditingTests
{
    ProceduralShelf shelf;

    [SetUp]
    public void Create()
    {
        shelf = new GameObject("Test rack").AddComponent<ProceduralShelf>();
        Assert.That(shelf.family, Is.Not.Null, "Shelf creation must supply a working default family.");
        shelf.fill = 1;
        shelf.transform.SetPositionAndRotation(new Vector3(3, 2, -7), Quaternion.Euler(0, 37, 0));
    }

    [TearDown]
    public void Close()
    {
        if (RoadScene.Active == shelf) RoadScene.Leave();
        if (shelf) Object.DestroyImmediate(shelf.gameObject);
    }

    bool Line(Vector3 a, Vector3 b, out string issue) =>
        RoadEditing.Change(shelf, "Draw shelf", graph =>
        {
            var start = RoadEditing.Point(graph, a);
            var end = RoadEditing.Point(graph, b);
            RoadEditing.Extend(graph, start.id, end.id, 0, null);
        }, out issue, true);

    (Vector3, int)[] Stock()
    {
        var block = new MaterialPropertyBlock();
        return shelf.GetComponentsInChildren<MeshRenderer>()
            .Where(r => r.sharedMaterial == shelf.family.stock.GetComponent<MeshRenderer>().sharedMaterial)
            .Select(r =>
            {
                r.GetPropertyBlock(block);
                return (r.transform.position, (int)block.GetFloat("_AtlasTile"));
            }).ToArray();
    }

    [Test]
    public void InsertingACollinearNodePreservesStockThroughUndoAndRedo()
    {
        Assert.That(Line(Vector3.zero, Vector3.right * 4.04f, out var issue), Is.True, issue);
        var original = Stock();
        Assert.That(original.Length, Is.EqualTo(32));
        Assert.That(original.Select(p => p.Item2).Distinct().Count(), Is.GreaterThan(1));
        Undo.IncrementCurrentGroup();
        Assert.That(RoadEditing.Change(shelf, "Split shelf", graph =>
            RoadEditing.Split(graph, graph.spans[0], RoadEditing.Point(graph, Vector3.right * 1.7f)), out issue, true), Is.True, issue);
        CollectionAssert.AreEqual(original, Stock());
        Undo.PerformUndo();
        Assert.That(shelf.paths.spans.Count, Is.EqualTo(1));
        CollectionAssert.AreEqual(original, Stock());
        Undo.PerformRedo();
        Assert.That(shelf.paths.spans.Count, Is.EqualTo(2));
        CollectionAssert.AreEqual(original, Stock());
    }

    [Test]
    public void SquareReturnClearsIncomingRackAndSharesMeshes()
    {
        var corner = Vector3.right * 4.04f;
        Assert.That(Line(Vector3.zero, corner, out var issue), Is.True, issue);
        Assert.That(Line(corner, corner + Vector3.forward * 3.34f, out issue), Is.True, issue);
        var meshes = shelf.GetComponentsInChildren<MeshFilter>();
        Assert.That(meshes.Count(m => m.sharedMesh == shelf.family.frame), Is.EqualTo(9));
        Assert.That(meshes.Count(m => m.sharedMesh == shelf.family.deck), Is.EqualTo(28));
        Assert.That(Stock().Length, Is.EqualTo(56));
        var returningFrames = meshes.Where(m => m.sharedMesh == shelf.family.frame &&
            Mathf.Abs(m.transform.localPosition.x-corner.x) < .001f).ToArray();
        Assert.That(returningFrames.Length, Is.EqualTo(4));
        float nearestEdge = returningFrames.Min(f => f.transform.localPosition.z) - shelf.family.frame.bounds.extents.x;
        Assert.That(nearestEdge, Is.EqualTo(shelf.PathWidth * .5f).Within(.001f));
        Assert.That(Line(corner, corner + Vector3.back * 2, out _), Is.False);
        Assert.That(shelf.paths.spans.Count, Is.EqualTo(2));
    }

    [TestCase(.9f, .3f, 7)]
    [TestCase(3.2f, 1.15f, 5)]
    [TestCase(.25f, .15f, 12)]
    [TestCase(1f, .5f, 1)]
    public void LocalDimensionsKeepDecksAndStockInsideTheirBays(float height, float depth, int decks)
    {
        shelf.height = height; shelf.depth = depth; shelf.decks = decks;
        Assert.That(Line(Vector3.zero, Vector3.right * 1.04f, out var issue), Is.True, issue);
        var colliders = shelf.GetComponentsInChildren<BoxCollider>();
        var surfaces = colliders.Where(c => c.GetComponent<MeshFilter>().sharedMesh == shelf.family.deck)
            .OrderBy(c => c.transform.localPosition.y).ToArray();
        Assert.That(surfaces.Length, Is.EqualTo(Mathf.Min(decks, shelf.DeckLimit)));
        var frame = colliders.First(c => c.GetComponent<MeshFilter>().sharedMesh == shelf.family.frame);
        var frameSize = Vector3.Scale(frame.size, frame.transform.localScale);
        Assert.That(frameSize.y, Is.EqualTo(height).Within(.001f));
        Assert.That(frameSize.z, Is.EqualTo(depth).Within(.001f));
        var boxes = shelf.GetComponentsInChildren<MeshRenderer>().Where(r => r.GetComponent<ISeededStock>() != null).ToArray();
        Assert.IsNotEmpty(boxes);
        foreach (var r in boxes)
        {
            var p = r.transform.localPosition;
            var size = r.transform.localScale;
            int level = System.Array.FindIndex(surfaces, c => Mathf.Abs(c.transform.localPosition.y - p.y) < .0001f);
            Assert.GreaterOrEqual(level, 0);
            var deck = surfaces[level];
            var deckSize = Vector3.Scale(deck.size, deck.transform.localScale);
            Assert.LessOrEqual(size.z + 2 * Mathf.Abs(p.z), deckSize.z + .0001f);
            Assert.LessOrEqual(size.x + 2 * Mathf.Abs(p.x - deck.transform.localPosition.x), deckSize.x + .0001f);
            if (level + 1 < surfaces.Length)
                Assert.LessOrEqual(p.y + size.y, surfaces[level+1].transform.localPosition.y - deckSize.y - .024f);
        }
        Assert.That(shelf.family.Height, Is.EqualTo(2).Within(.001f));
        Assert.That(shelf.family.levels, Is.EqualTo(4));
    }

    [Test]
    public void FillClosesGapsWithoutMovingExistingBoxes()
    {
        Assert.That(Line(Vector3.zero, Vector3.right * 4.04f, out var issue), Is.True, issue);
        var full = Stock();
        shelf.fill = .35f; shelf.Rebuild();
        var sparse = Stock();
        Assert.That(sparse.Length, Is.EqualTo(Mathf.RoundToInt(.35f * full.Length)));
        CollectionAssert.IsSubsetOf(sparse, full);
        shelf.fill = 0; shelf.Rebuild();
        Assert.IsEmpty(Stock());
    }

    [UnityTest]
    public IEnumerator FinishedShelfCanBePickedAndReopened()
    {
        var view = ScriptableObject.CreateInstance<SceneView>();
        var previous = Selection.activeObject;
        try
        {
            RoadScene.Enter(shelf);
            Assert.That(Line(Vector3.zero, Vector3.right * 4.04f, out var issue), Is.True, issue);
            RoadScene.Leave();
            Selection.activeObject = null;
            view.Show();
            view.position = new Rect(80, 80, 850, 680);
            view.orthographic = true;
            var t = shelf.transform;
            var box = shelf.GetComponentsInChildren<MeshRenderer>()
                .First(r => r.GetComponent<ISeededStock>() != null);
            var target = box.bounds.center;
            view.LookAtDirect(target, Quaternion.LookRotation(t.forward, t.up), 2);
            view.Focus(); view.Repaint();
            yield return null; yield return null;
            PavementEditingTests.ClickScene(view, target);
            yield return null;
            Assert.AreEqual(shelf.gameObject, Selection.activeGameObject, "Clicking a stocked box should select its shelf.");
            PavementEditingTests.ClickScene(view, target);
            // Native selection is delivered on the editor update after SceneView picking.
            double deadline = EditorApplication.timeSinceStartup + 2;
            do { yield return null; } while (Selection.activeGameObject != shelf.gameObject && EditorApplication.timeSinceStartup < deadline);
            Assert.AreEqual(shelf.gameObject, Selection.activeGameObject, "Clicking again should keep the shelf editable.");
            RoadScene.Enter(Selection.activeGameObject.GetComponent<ProceduralShelf>());
            Assert.AreEqual(shelf, RoadScene.Active);
            Assert.That(Line(Vector3.right * 4.04f, Vector3.right * 6, out issue), Is.True, issue);
            RoadScene.Leave();
            shelf.fill = 0;
            shelf.Rebuild();
            Selection.activeObject = null;
            var deck = shelf.GetComponentsInChildren<MeshRenderer>().First(r => r.sharedMaterial == shelf.family.timber);
            target = deck.bounds.center;
            view.LookAtDirect(target, Quaternion.LookRotation(-t.up, t.forward), 2);
            view.Repaint();
            yield return null; yield return null;
            PavementEditingTests.ClickScene(view, target);
            yield return null;
            Assert.AreEqual(shelf.gameObject, Selection.activeGameObject, "An empty rack should also select its shelf.");
        }
        finally
        {
            view.Close();
            Selection.activeObject = previous;
        }
    }

    [Test]
    public void SavingAndReopeningRegeneratesShelfPartsOnce()
    {
        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/ShelfPickingTest.unity");
        try
        {
            Assert.That(Line(Vector3.zero, Vector3.right * 4.04f, out var issue), Is.True, issue);
            int parts = shelf.GetComponentsInChildren<MeshRenderer>().Length;
            var scene = shelf.gameObject.scene;
            EnvironmentHierarchy.Group(shelf.gameObject);
            int sceneRoots = scene.rootCount;
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, path, true));
            var reopened = EditorSceneManager.OpenScene(path);
            Assert.AreEqual(sceneRoots, reopened.rootCount, "Generated parts should never reload as orphaned scene roots.");
            var root = reopened.GetRootGameObjects().Single(go => go.name == "Shelves").transform.Find("Test rack").gameObject;
            Assert.AreEqual(1, root.transform.childCount);
            Assert.AreEqual(parts, root.GetComponentsInChildren<MeshRenderer>().Length);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(path);
        }
    }

    [UnityTest]
    public IEnumerator DrawAndSplitShelfInRotatedSceneView()
    {
        var view = ScriptableObject.CreateInstance<SceneView>();
        var oldTool = Tools.current;
        float oldGrid = PlanSnapping.Grid;
        try
        {
            RoadScene.Enter(shelf);
            RoadScene.SetBrush(RoadScene.Brush.Sidewalk);
            view.Show();
            view.position = new Rect(80, 80, 850, 680);
            view.orthographic = true;
            var t = shelf.transform;
            view.LookAtDirect(t.TransformPoint(new Vector3(2, 0, 1)), Quaternion.LookRotation(-t.up, t.forward), 6);
            Tools.current = Tool.Move;
            PlanSnapping.Grid = .25f;
            view.Focus(); view.Repaint();
            yield return null; yield return null;
            void Click(float x, float z, bool shift = false, int clicks = 1) =>
                PavementEditingTests.ClickScene(view, t.TransformPoint(new Vector3(x, 0, z)), shift, false, clicks);
            Click(0, 0); Click(4, .15f, true); Click(4, 3);
            Assert.That(shelf.paths.spans.Count, Is.EqualTo(2), RoadScene.Message);
            Assert.That(shelf.paths.nodes[1].position.z, Is.EqualTo(0).Within(.001f));
            RoadScene.SetBrush(RoadScene.Brush.Select);
            Click(2, .1f, clicks: 2);
            Assert.That(RoadScene.Selected.part, Is.EqualTo(RoadPart.PathPoint));
            Assert.That(shelf.paths.spans.Count, Is.EqualTo(3));
            RoadScene.Delete();
            Assert.That(shelf.paths.spans.Count, Is.EqualTo(2));
        }
        finally
        {
            view.Close(); Tools.current = oldTool; PlanSnapping.Grid = oldGrid;
        }
    }
}

public sealed class ShelfExampleTests
{
    const string Example = "Assets/Sandbox/Shelving/StockedShelves.unity";

    static string[] Designs()
    {
        var racks = Object.FindObjectsByType<ProceduralShelf>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
        Assert.AreEqual(2, racks.Length);
        var block = new MaterialPropertyBlock();
        return racks.Select(shelf =>
        {
            var material = shelf.family.stock.GetComponent<MeshRenderer>().sharedMaterial;
            var boxes = shelf.GetComponentsInChildren<MeshRenderer>().Where(r => r.sharedMaterial == material);
            return string.Join(",", boxes.Select(r =>
            {
                r.GetPropertyBlock(block);
                return (int)block.GetFloat("_AtlasTile");
            }));
        }).ToArray();
    }

    [UnityTest]
    public IEnumerator SavedExampleKeepsItsBoxDesignsInPlayMode()
    {
        const string key = "ShelfExampleTests.ExpectedDesigns";
        EditorSceneManager.OpenScene(Example);
        var editor = Designs();
        Assert.That(editor.All(rack => rack.Split(',').Length >= 10));
        // EnterPlayMode reloads the test assembly and clears iterator locals.
        SessionState.SetString(key, string.Join("|", editor));
        yield return new EnterPlayMode();
        Assert.AreEqual(SessionState.GetString(key, ""), string.Join("|", Designs()));
        yield return new ExitPlayMode();
        Assert.AreEqual(SessionState.GetString(key, ""), string.Join("|", Designs()));
        SessionState.EraseString(key);
    }
}
