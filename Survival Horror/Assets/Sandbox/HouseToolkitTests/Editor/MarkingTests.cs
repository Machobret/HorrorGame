using System.Collections;
using System.Linq;
using HouseToolkit;
using HouseToolkit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MarkingTests
{
    ProceduralMarking marking;
    GameObject support;
    [SetUp] public void SetUp()
    {
        marking=new GameObject("Test marking").AddComponent<ProceduralMarking>();
        marking.transform.SetPositionAndRotation(new Vector3(3,2,-7),Quaternion.Euler(0,37,0));
    }
    [TearDown] public void TearDown()
    {
        RoadScene.Leave();
        if(marking)Object.DestroyImmediate(marking.gameObject);
        if(support)Object.DestroyImmediate(support);
    }
    void Line(Vector3 a,Vector3 b)
    {
        Assert.IsTrue(RoadEditing.Change(marking,"Draw marking",g=>g.Connect(RoadEditing.Point(g,a).id,RoadEditing.Point(g,b).id,0,null),out var issue,true),issue);
    }
    static Vector3[] Corners(MeshRenderer renderer)
    {
        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        return Enumerable.Range(0,4).Select(i=>(Vector3)block.GetVector("_Corner"+i)).ToArray();
    }
    [Test]
    public void StripsShareMeshAndMaterialHaveCorrectBoundsAndNoColliders()
    {
        Line(Vector3.zero,Vector3.forward*4);Line(Vector3.forward*4,new Vector3(3,0,4));
        var renderers=marking.GetComponentsInChildren<MeshRenderer>();
        Assert.AreEqual(2,renderers.Length);
        Assert.AreEqual(1,renderers.Select(r=>r.sharedMaterial).Distinct().Count());
        Assert.IsTrue(renderers[0].sharedMaterial.enableInstancing);
        Assert.AreEqual(1,marking.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Distinct().Count());
        Assert.IsEmpty(marking.GetComponentsInChildren<Collider>());
        foreach(var renderer in renderers)
            foreach(var point in Corners(renderer)) Assert.IsTrue(renderer.bounds.Contains(marking.transform.TransformPoint(point)));
        marking.Rebuild();Assert.AreEqual(2,marking.GetComponentsInChildren<MeshRenderer>().Length);
    }
    [TestCase(90)] [TestCase(45)] [TestCase(5)] [TestCase(175)]
    public void CornersShareTheirBoundaryWithoutSpikes(float angle)
    {
        var node=Vector3.forward*4;
        Line(Vector3.zero,node);
        Line(node,node+Quaternion.Euler(0,angle,0)*Vector3.back*4);
        var parts=marking.GetComponentsInChildren<MeshRenderer>();
        var a=Corners(parts[0]);var b=Corners(parts[1]);
        Assert.That(Vector3.Distance(a[2],b[1]),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(a[3],b[0]),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(a[2],node+Vector3.up*.003f),Is.LessThanOrEqualTo(marking.width+.0001f));
    }
    [Test]
    public void WearSeedAndTintAreInstanceDataAndRebuildIsStable()
    {
        Line(Vector3.zero,Vector3.forward*4);
        var block=new MaterialPropertyBlock();
        marking.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
        var original=block.GetVector("_PaintWear");var material=marking.Pavement;
        marking.Rebuild();marking.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
        Assert.AreEqual(original,block.GetVector("_PaintWear"));
        marking.wear=.8f;marking.color=Color.red;marking.Rebuild();
        marking.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
        Assert.AreSame(material,marking.Pavement);
        Assert.AreEqual(.8f,block.GetVector("_PaintWear").x);
        Assert.AreEqual(original.y,block.GetVector("_PaintWear").y);
        Assert.AreEqual(Color.red,block.GetColor("_PaintTint"));
        marking.wear=1.7f;marking.wearScale=3;marking.Rebuild();
        marking.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
        var extended=block.GetVector("_PaintWear");
        Assert.AreEqual(1.7f,extended.x,"Wear beyond the old maximum must reach the shader.");
        Assert.AreEqual(3,extended.w);
        Assert.AreEqual(1,original.w,"Existing markings keep the original scale.");
        Assert.AreEqual(original.y,extended.y);Assert.AreEqual(original.z,extended.z);
        Assert.AreSame(material,marking.Pavement);
    }
    void Ramp()
    {
        support=GameObject.CreatePrimitive(PrimitiveType.Cube);
        support.name="Support ramp";
        var rotation=marking.transform.rotation*Quaternion.Euler(-Mathf.Atan(.2f)*Mathf.Rad2Deg,0,0);
        support.transform.rotation=rotation;
        support.transform.localScale=new Vector3(3,.2f,6.5f);
        support.transform.position=marking.transform.TransformPoint(new Vector3(0,.6f,3))-rotation*Vector3.up*.1f;
        Physics.SyncTransforms();
    }
    [Test]
    public void RotatedRampFitsDrawingAndRefitWithoutRuntimeColliders()
    {
        Ramp();
        Assert.IsTrue(MarkingPlacement.Surface(marking,new Vector3(0,.1f,1),out var a));
        Assert.IsTrue(MarkingPlacement.Surface(marking,new Vector3(0,.9f,5),out var b));
        Assert.That(a.y,Is.EqualTo(.2f).Within(.001));Assert.That(b.y,Is.EqualTo(1).Within(.001));
        Assert.AreEqual(1,MarkingPlacement.Stretches(marking,a,b).Count);
        Line(a,b);
        Assert.IsTrue(MarkingPlacement.Pick(marking,new Ray(marking.transform.TransformPoint(new Vector3(0,4,3)),Vector3.down),out _,out var point,out _));
        Assert.That(point.y,Is.EqualTo(.603f).Within(.002));
        foreach(var n in marking.paths.nodes)n.position+=Vector3.up*.2f;
        MarkingPlacement.Refit(marking);
        Assert.That(marking.paths.nodes[0].position.y,Is.EqualTo(.2f).Within(.001));
    }
    [Test]
    public void RefitKeepsTheSupportedPartInsteadOfRejectingTheWholeSpan()
    {
        Ramp();Line(new Vector3(0,.2f,1),new Vector3(0,1.8f,9));
        Undo.IncrementCurrentGroup();MarkingPlacement.Refit(marking);
        Assert.AreEqual(1,marking.paths.spans.Count);
        Assert.That(marking.paths.nodes.Max(n=>n.position.z),Is.LessThan(6.3f));
        Assert.That(marking.paths.nodes.Min(n=>n.position.z),Is.EqualTo(1).Within(.001));
        Undo.PerformUndo();Assert.That(marking.paths.nodes.Max(n=>n.position.z),Is.EqualTo(9));
    }

    [TestCase(true)] [TestCase(false)]
    public void PaintContinuesBeneathDoorsAndWallsWithoutClimbingTheirCaps(bool door)
    {
        support=GameObject.CreatePrimitive(PrimitiveType.Cube);
        support.transform.SetPositionAndRotation(marking.transform.TransformPoint(Vector3.down*.1f),marking.transform.rotation);
        support.transform.localScale=new Vector3(8,.2f,8);
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            obstacle.transform.SetPositionAndRotation(marking.transform.TransformPoint(new Vector3(0,.25f,0)),marking.transform.rotation);
            obstacle.transform.localScale=new Vector3(3,.5f,.3f);
            if(door)obstacle.AddComponent<InsertAttachment>();else obstacle.AddComponent<HouseWall>();
            Physics.SyncTransforms();
            Assert.IsTrue(MarkingPlacement.Surface(marking,Vector3.zero,out var fitted));
            Assert.That(fitted.y,Is.EqualTo(0).Within(.001));
            Assert.IsTrue(MarkingPlacement.Cursor(marking,new Ray(marking.transform.TransformPoint(Vector3.up*4),Vector3.down),out var cursor));
            Assert.That(cursor.y,Is.EqualTo(0).Within(.001));
            var stretches=MarkingPlacement.Stretches(marking,Vector3.back*3,Vector3.forward*3);
            Assert.AreEqual(1,stretches.Count);
            Assert.That(stretches[0].b.z,Is.EqualTo(3).Within(.001));
            Line(Vector3.back*3,Vector3.forward*3);
            Assert.AreEqual(1,marking.GetComponentsInChildren<MeshRenderer>().Length);
        }
        finally{Object.DestroyImmediate(obstacle);}
    }

    void SplitFloor()
    {
        support=new GameObject("Floor with a gap");support.transform.SetPositionAndRotation(marking.transform.position,marking.transform.rotation);
        foreach(float z in new[]{-1.75f,1.75f})
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(support.transform,false);
            cube.transform.localPosition=new Vector3(0,-.1f,z);cube.transform.localScale=new Vector3(3,.2f,2.5f);
        }
        Physics.SyncTransforms();
    }

    [Test]
    public void OnlyMissingFloorInsideArchitectureIsBridged()
    {
        SplitFloor();
        Assert.AreEqual(2,MarkingPlacement.Stretches(marking,Vector3.back*2.5f,Vector3.forward*2.5f).Count);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.SetPositionAndRotation(marking.transform.TransformPoint(Vector3.up),marking.transform.rotation);
            wall.transform.localScale=new Vector3(3,2,1.02f);wall.AddComponent<HouseWall>();Physics.SyncTransforms();
            Assert.AreEqual(1,MarkingPlacement.Stretches(marking,Vector3.back*2.5f,Vector3.forward*2.5f).Count);
        }
        finally{Object.DestroyImmediate(wall);}
    }

    [UnityTest]
    public IEnumerator DrawingAcrossGapKeepsBothSidesInOneUndoAndContinuesChain()
    {
        SplitFloor();var view=ScriptableObject.CreateInstance<SceneView>();var tool=Tools.current;float grid=PlanSnapping.Grid;
        try
        {
            RoadScene.Enter(marking);RoadScene.SetBrush(RoadScene.Brush.Sidewalk);
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            view.LookAtDirect(marking.transform.position,Quaternion.LookRotation(-marking.transform.up,marking.transform.forward),6);
            Tools.current=Tool.Move;PlanSnapping.Grid=.25f;view.Focus();view.Repaint();yield return null;yield return null;
            void Click(float z)=>PavementEditingTests.ClickScene(view,marking.transform.TransformPoint(Vector3.forward*z));
            Click(-2.5f);Undo.IncrementCurrentGroup();Click(2.5f);
            Assert.AreEqual(2,marking.paths.spans.Count,RoadScene.Message);
            float length=marking.paths.spans.Sum(s=>Vector3.Distance(marking.paths.Node(s.start.node).position,marking.paths.Node(s.end.node).position));
            Assert.That(length,Is.EqualTo(4).Within(.01));
            Undo.IncrementCurrentGroup();Click(2.75f);
            Assert.AreEqual(3,marking.paths.spans.Count,"The drawing chain must continue after the clipped span.");
            Undo.PerformUndo();Assert.AreEqual(2,marking.paths.spans.Count);
            Undo.PerformUndo();Assert.AreEqual(0,marking.paths.spans.Count);
            Undo.PerformRedo();Assert.AreEqual(2,marking.paths.spans.Count);
        }
        finally{view.Close();Tools.current=tool;PlanSnapping.Grid=grid;}
    }
    [Test]
    public void VerticalMoveAndDissolveKeepUndoAndRedo()
    {
        Line(new Vector3(.13f,0,0),new Vector3(.13f,1,4));
        var n=marking.paths.nodes[1];int id=n.id;
        Undo.IncrementCurrentGroup();
        Assert.IsTrue(RoadEditing.Change(marking,"Raise point",g=>RoadEditing.Move(g,id,new Vector3(.13f,2,4)),out _,true));
        Assert.AreEqual(.13f,marking.paths.Node(id).position.x);
        Undo.PerformUndo();Assert.AreEqual(1,marking.paths.Node(id).position.y);
        Undo.PerformRedo();Assert.AreEqual(2,marking.paths.Node(id).position.y);
        RoadScene.Enter(marking);RoadScene.Pick(new RoadHit(RoadPart.PathPoint,id));
        Assert.IsTrue(RoadScene.Frame(out var bounds));
        Assert.That(Vector3.Distance(bounds.center,marking.transform.TransformPoint(marking.paths.Node(id).position)),Is.LessThan(.001));
    }
    [UnityTest]
    public IEnumerator DrawAndSplitRampInSceneViewPreservesElevation()
    {
        Ramp();var view=ScriptableObject.CreateInstance<SceneView>();var tool=Tools.current;float grid=PlanSnapping.Grid;
        try
        {
            RoadScene.Enter(marking);RoadScene.SetBrush(RoadScene.Brush.Sidewalk);
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            view.LookAtDirect(marking.transform.TransformPoint(new Vector3(0,0,3)),Quaternion.LookRotation(-marking.transform.up,marking.transform.forward),6);
            Tools.current=Tool.Move;PlanSnapping.Grid=.25f;view.Focus();view.Repaint();yield return null;yield return null;
            void Click(Vector3 point,int count=1)=>PavementEditingTests.ClickScene(view,marking.transform.TransformPoint(point),false,false,count);
            Click(new Vector3(0,.2f,1));Click(new Vector3(0,1,5));
            Assert.AreEqual(1,marking.paths.spans.Count,RoadScene.Message);
            Assert.That(marking.paths.nodes[1].position.y,Is.EqualTo(1).Within(.01));
            RoadScene.SetBrush(RoadScene.Brush.Select);
            Click(new Vector3(0,.6f,3),2);
            Assert.AreEqual(RoadPart.PathPoint,RoadScene.Selected.part);
            Assert.AreEqual(2,marking.paths.spans.Count);
            Assert.That(marking.paths.Node(RoadScene.Selected.id).position.y,Is.EqualTo(.6f).Within(.01));
            Undo.IncrementCurrentGroup();RoadScene.Delete();Assert.AreEqual(1,marking.paths.spans.Count);
            Undo.PerformUndo();Assert.AreEqual(2,marking.paths.spans.Count);
        }
        finally{view.Close();Tools.current=tool;PlanSnapping.Grid=grid;}
    }

    [UnityTest]
    public IEnumerator VerticalGizmoDoesNotSnapAnOffGridPointSideways()
    {
        Line(new Vector3(.13f,.2f,1),new Vector3(.13f,.8f,4));
        var view=ScriptableObject.CreateInstance<SceneView>();var tool=Tools.current;float grid=PlanSnapping.Grid;
        try
        {
            RoadScene.Enter(marking);var node=marking.paths.nodes[1];int id=node.id;
            RoadScene.Pick(new RoadHit(RoadPart.PathPoint,id));
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            var pivot=marking.transform.TransformPoint(node.position);
            view.LookAtDirect(pivot,marking.transform.rotation*Quaternion.Euler(25,25,0),5);
            Tools.current=Tool.Move;PlanSnapping.Grid=.25f;view.Focus();view.Repaint();yield return null;yield return null;
            Vector2 start=default,end=default;
            void Locate(SceneView v)
            {
                if(v!=view || Event.current.commandName!="LocateMarkingHandle")return;
                var at=pivot+marking.transform.up*(HandleUtility.GetHandleSize(pivot)*.5f);
                start=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(at))-view.position.position;
                end=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(at+marking.transform.up*.7f))-view.position.position;
            }
            SceneView.duringSceneGui+=Locate;
            try{view.SendEvent(EditorGUIUtility.CommandEvent("LocateMarkingHandle"));}
            finally{SceneView.duringSceneGui-=Locate;}
            Undo.IncrementCurrentGroup();
            foreach(var type in new[]{EventType.MouseMove,EventType.Layout,EventType.MouseDown})
                view.SendEvent(new Event{type=type,button=0,clickCount=1,mousePosition=start});
            for(int i=1;i<=4;i++)view.SendEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=Vector2.Lerp(start,end,i/4f),delta=(end-start)/4});
            view.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=end});
            Assert.That(marking.paths.Node(id).position.y,Is.GreaterThan(1.2f),"The Y gizmo must actually move the selected point.");
            Assert.That(marking.paths.Node(id).position.x,Is.EqualTo(.13f).Within(.001));
            Assert.That(marking.paths.Node(id).position.z,Is.EqualTo(4).Within(.001));
            Undo.PerformUndo();Assert.That(marking.paths.Node(id).position.y,Is.EqualTo(.8f).Within(.001));
        }
        finally{view.Close();Tools.current=tool;PlanSnapping.Grid=grid;}
    }

    [Test]
    public void SavingAndReopeningRebuildsOnlyOneGeneratedContainer()
    {
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Sandbox/MarkingSaveTest.unity");
        try
        {
            Line(Vector3.zero,new Vector3(0,1,4));
            EnvironmentHierarchy.Group(marking.gameObject);
            Assert.AreEqual("Markings",marking.transform.parent.name);
            Assert.IsTrue(EditorSceneManager.SaveScene(marking.gameObject.scene,path,true));
            EditorSceneManager.OpenScene(path);
            marking=Object.FindFirstObjectByType<ProceduralMarking>();
            Assert.IsNotNull(marking);Assert.AreEqual(1,marking.transform.childCount);
            Assert.AreEqual(1,marking.GetComponentsInChildren<MeshRenderer>().Length);
            Assert.AreEqual(1,marking.paths.nodes[1].position.y);
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(path);}
    }

    [UnityTest]
    public IEnumerator ClickingDeformedPaintSelectsItsRootAndReentersEditing()
    {
        Line(new Vector3(4,0,4),new Vector3(4,0,8));
        var view=ScriptableObject.CreateInstance<SceneView>();var tool=Tools.current;
        try
        {
            RoadScene.Leave();Selection.activeObject=null;
            view.Show();view.position=new Rect(80,80,850,680);view.orthographic=true;
            var point=marking.transform.TransformPoint(new Vector3(4,0,6));
            view.LookAtDirect(point,Quaternion.LookRotation(-marking.transform.up,marking.transform.forward),4);
            Tools.current=Tool.Move;view.Focus();view.Repaint();yield return null;yield return null;
            PavementEditingTests.ClickScene(view,point);
            Assert.AreSame(marking.gameObject,Selection.activeGameObject);
            PavementEditingTests.ClickScene(view,point,clicks:2);
            Assert.AreSame(marking,RoadScene.Active);
        }
        finally{view.Close();Tools.current=tool;}
    }
}
