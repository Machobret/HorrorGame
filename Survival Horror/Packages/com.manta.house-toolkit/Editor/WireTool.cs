using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [EditorTool("Wire")]
    public sealed class WireTool : EditorTool
    {
        // Unity's editor mesh picker works on imported meshes even when Read/Write is disabled.
        delegate bool Intersect(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
        static readonly Intersect intersect = typeof(HandleUtility).GetMethod("IntersectRayMesh",
            BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static, null,
            new[]{typeof(Ray),typeof(Mesh),typeof(Matrix4x4),typeof(RaycastHit).MakeByRefType()},null)?.CreateDelegate(typeof(Intersect)) as Intersect;
        static WireTool current;
        ProceduralWire wire;
        Vector3? first, cursor;
        int node=-1, segment=-1, anchor=-1, snapped=-1, dragControl;
        bool drawing;
        SceneView rightPressView;
        Vector2 rightPressPosition;
        readonly PointPlacement<Vector3> press = new();
        Mesh baked;
        GameObject[] ignore;
        const float DefaultDiameter=.018f;

        internal static bool Active => current;
        internal static bool Drawing => current && current.drawing;
        internal static ProceduralWire Selected => Selection.activeGameObject && Selection.activeGameObject.scene.IsValid()
            ? Selection.activeGameObject.GetComponent<ProceduralWire>() : null;

        internal static void SetDrawing(bool value)
        {
            if(!current || !current.wire || current.drawing==value)return;
            current.rightPressView=null;current.EndDrag();current.press.Cancel();
            current.drawing=value;current.anchor=current.node;current.segment=-1;current.cursor=null;
            HouseProperties.RepaintOpen();SceneView.RepaintAll();
        }

        [MenuItem("GameObject/Environment/Wire",false,19)]
        internal static void NewWire() => Begin(null);
        internal static void Begin(ProceduralWire target)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(HouseScene.Floor)HouseScene.Leave();
            if(RoadScene.Active)RoadScene.Leave();
            ToolManager.SetActiveTool<WireTool>();
            var tool=current;
            if(!tool)return;
            tool.Bind(target);
            if(target)Selection.activeGameObject=target.gameObject;
            HouseProperties.Open();
            SceneView.lastActiveSceneView?.Focus();SceneView.RepaintAll();
        }
        internal static void Leave()
        {
            if(current)ToolManager.RestorePreviousTool();
        }
        void Bind(ProceduralWire target)
        {
            press.Cancel();EndDrag();
            rightPressView=null;wire=target;first=cursor=null;node=segment=anchor=snapped=-1;drawing=!target;
            ignore=target?new[]{target.gameObject}:null;
            if(target)target.EnsureTopology();
        }
        public override void OnActivated()
        {
            current=this;Bind(Selected);
            SceneView.beforeSceneGui+=ObserveRightClick;
            Undo.undoRedoPerformed+=AfterUndo;
            Selection.selectionChanged+=SelectionChanged;
        }
        void SelectionChanged()
        {
            if(!wire || Selection.activeGameObject==wire.gameObject)return;
            if(Selected){Bind(Selected);HouseProperties.RepaintOpen();SceneView.RepaintAll();}
            else Leave();
        }
        void AfterUndo()
        {
            node=segment=anchor=snapped=-1;first=cursor=null;
            if(wire)wire.Rebuild();
            HouseProperties.RepaintOpen();SceneView.RepaintAll();
        }
        public override void OnWillBeDeactivated()
        {
            SceneView.beforeSceneGui-=ObserveRightClick;rightPressView=null;
            Selection.selectionChanged-=SelectionChanged;Undo.undoRedoPerformed-=AfterUndo;press.Cancel();EndDrag();wire=null;first=null;cursor=null;current=null;
            if(baked)DestroyImmediate(baked);
            HouseProperties.RepaintOpen();SceneView.RepaintAll();
        }
        // Observe before the Scene camera handles RMB. Leave its mouse capture and release intact.
        void ObserveRightClick(SceneView view) => TrackRightClick(view, Event.current);

        internal void TrackRightClick(SceneView view, Event e)
        {
            if(!drawing || e==null || !view)return;
            var type=e.rawType;
            if(type==EventType.MouseDown && e.button==1){
                rightPressView=e.alt?null:view;
                rightPressPosition=e.mousePosition;
                return;
            }
            if(rightPressView!=view)return;
            if(type==EventType.KeyDown || type==EventType.ScrollWheel || type==EventType.MouseLeaveWindow ||
                type==EventType.Ignore || e.alt || (e.isMouse && (e.mousePosition-rightPressPosition).sqrMagnitude>=9f)){
                rightPressView=null;
                return;
            }
            if(type!=EventType.MouseUp || e.button!=1)return;
            rightPressView=null;
            if(anchor<0 && !first.HasValue)return;
            press.Cancel();
            first=cursor=null;
            anchor=node=segment=snapped=-1;
            HouseProperties.RepaintOpen();view.Repaint();
        }
        void EndDrag()
        {
            if(dragControl!=0 && GUIUtility.hotControl==dragControl)GUIUtility.hotControl=0;
            dragControl=0;
        }
        internal static bool Properties(GameObject selected)
        {
            var tool=current;
            var w=tool?tool.wire:selected?selected.GetComponent<ProceduralWire>():null;
            if(!tool && !w)return false;
            EditorGUILayout.LabelField("Wire",EditorStyles.boldLabel);
            if(w)
            {
                DrawWireSettings(w);
                if(tool && !tool.drawing && tool.segment>=0 && tool.segment<w.segments.Count){
                    var edge=w.segments[tool.segment];
                    EditorGUI.BeginChangeCheck();
                    float sag=EditorGUILayout.Slider("Segment sag (%)",edge.sag*100,0,50);
                    if(EditorGUI.EndChangeCheck()){
                        Undo.RecordObject(w,"Set segment sag");edge.sag=sag*.01f;Changed(w);
                    }
                }
            }
            if(!tool){if(GUILayout.Button("Edit Wire"))Begin(w);return true;}
            int next=GUILayout.Toolbar(tool.drawing?1:0,new[]{"Edit nodes","Draw"});
            SetDrawing(next==1);
            EditorGUILayout.LabelField(tool.drawing?(w && tool.anchor<0?"Click an existing node to start a branch.":"Click to connect. Right-click to end the run and draw from another node. Enter switches to Select."):
                "Select a node and choose Draw to branch. Select a segment to adjust sag, or double-click to split.",EditorStyles.wordWrappedMiniLabel);
            if(GUILayout.Button("Done"))ToolManager.RestorePreviousTool();
            return true;
        }
        internal static void DrawWireSettings(ProceduralWire w)
        {
            float mm=w.diameter*1000f;
            using(new EditorGUILayout.HorizontalScope()){
                EditorGUILayout.PrefixLabel(new GUIContent("Thickness (mm)","Outside diameter of each strand."));
                EditorGUI.BeginChangeCheck();
                float slider=GUILayout.HorizontalSlider(Mathf.Clamp(mm,1,100),1,100,GUILayout.ExpandWidth(true));
                if(EditorGUI.EndChangeCheck())mm=Mathf.Round(slider);
                mm=EditorGUILayout.FloatField(mm,GUILayout.Width(48));
            }
            if(float.IsFinite(mm) && !Mathf.Approximately(Mathf.Max(1,mm),w.diameter*1000f)){
                Undo.RecordObject(w,"Set wire thickness");w.diameter=Mathf.Max(1,mm)*.001f;Changed(w);
            }
            int count=EditorGUILayout.IntSlider("Number of wires",w.numberOfWires,1,8);
            if(count!=w.numberOfWires){
                Undo.RecordObject(w,"Set wire bundle size");w.numberOfWires=count;Changed(w);
            }
        }
        static void Changed(ProceduralWire w)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(w);
            w.Rebuild();HouseProperties.RepaintOpen();SceneView.RepaintAll();
        }
        internal static bool MeshHit(Ray ray,Mesh mesh,Transform space,out RaycastHit hit)
        {
            hit=default;
            return intersect!=null && mesh && intersect(ray,mesh,space.localToWorldMatrix,out hit);
        }
        bool Surface(Vector2 mouse,out Vector3 position)
        {
            var ray=HandleUtility.GUIPointToWorldRay(mouse);
            Vector3 normal;
            bool found=HandleUtility.PlaceObject(mouse,out position,out normal);
            var target=HandleUtility.PickGameObject(mouse,false,ignore);
            if(target && (!wire || target!=wire.gameObject))
            {
                Mesh mesh=null;
                if(target.TryGetComponent<MeshFilter>(out var mf))mesh=mf.sharedMesh;
                else if(target.TryGetComponent<SkinnedMeshRenderer>(out var skin)){
                    if(!baked)baked=new Mesh{hideFlags=HideFlags.HideAndDontSave};skin.BakeMesh(baked);mesh=baked;
                }
                if(MeshHit(ray,mesh,target.transform,out var hit) &&
                    (!found || hit.distance <= Vector3.Distance(position,ray.origin)+.002f))
                {position=hit.point;normal=hit.normal;found=true;}
            }
            if(!found)return false;
            if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;
            position+=normal.normalized*(wire?wire.diameter:DefaultDiameter)*.5f;
            return true;
        }
        internal void Place(Vector3 position,bool unused)
        {
            if(!wire)
            {
                if(!first.HasValue){first=position;SceneView.RepaintAll();return;}
                if(Vector3.Distance(first.Value,position)<.001f)return;
                var go=new GameObject("Wire");
                go.transform.position=first.Value;
                wire=go.AddComponent<ProceduralWire>();
                wire.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("HouseToolkitWire");
                wire.nodes.Add(Vector3.zero);
                wire.nodes.Add(go.transform.InverseTransformPoint(position));
                Undo.RegisterCreatedObjectUndo(go,"Draw wire");
                EnvironmentHierarchy.Group(go);Selection.activeGameObject=go;ignore=new[]{go};
                first=null;
                node=anchor=1;
            }
            else
            {
                var local=wire.transform.InverseTransformPoint(position);
                if(anchor<0 || anchor>=wire.nodes.Count){
                    if(snapped>=0){node=anchor=snapped;SceneView.RepaintAll();HouseProperties.RepaintOpen();}
                    return;
                }
                if(Vector3.Distance(wire.nodes[anchor],local)<.001f)return;
                Undo.RecordObject(wire,"Draw wire segment");
                wire.EnsureTopology();
                int end=snapped;
                if(end<0){end=wire.nodes.Count;wire.nodes.Add(local);}
                if(!wire.Connect(anchor,end))return;
                node=anchor=end;
            }
            Changed(wire);
        }
        void SetNode(Vector3 world)
        {
            Undo.RecordObject(wire,"Move wire node");wire.nodes[node]=wire.transform.InverseTransformPoint(world);Changed(wire);
        }
        internal void DeleteNode()
        {
            if(!wire || node<0 || node>=wire.nodes.Count || wire.nodes.Count<=2)return;
            var incident=wire.segments.FindAll(edge=>edge.a==node || edge.b==node);
            if(incident.Count>2)return;
            Undo.RecordObject(wire,"Delete wire node");
            foreach(var edge in incident)wire.segments.Remove(edge);
            if(incident.Count==2)
                wire.Connect(incident[0].Other(node),incident[1].Other(node),Mathf.Min(.5f,incident[0].sag+incident[1].sag));
            wire.nodes.RemoveAt(node);
            foreach(var edge in wire.segments){if(edge.a>node)edge.a--;if(edge.b>node)edge.b--;}
            node=anchor=segment=-1;Changed(wire);
        }
        internal void Split(int index,float t)
        {
            var edge=wire.segments[index];
            if(t<=.001f || t>=.999f)return;
            Undo.RecordObject(wire,"Insert wire node");
            node=wire.nodes.Count;wire.nodes.Add(wire.Point(edge,t));
            wire.segments[index]=new ProceduralWire.Segment(edge.a,node,edge.sag*t);
            wire.segments.Add(new ProceduralWire.Segment(node,edge.b,edge.sag*(1-t)));
            segment=-1;Changed(wire);
        }
        int PickNode(Vector2 mouse)
        {
            float best=11;int picked=-1;
            if(wire)for(int i=0;i<wire.nodes.Count;i++){
                float d=Vector2.Distance(HandleUtility.WorldToGUIPoint(wire.transform.TransformPoint(wire.nodes[i])),mouse);
                if(d<best){best=d;picked=i;}
            }
            return picked;
        }
        internal static void DrawNetwork(ProceduralWire target,int selectedNode,int selectedSegment)
        {
            using(new Handles.DrawingScope(Color.cyan,target.transform.localToWorldMatrix)){
                for(int i=0;i<target.segments.Count;i++){
                    var edge=target.segments[i];
                    int steps=target.Steps(edge);
                    var points=new Vector3[steps+1];
                    for(int k=0;k<=steps;k++)points[k]=target.Point(edge,(float)k/steps);
                    Handles.color=i==selectedSegment?Color.yellow:Color.cyan;
                    Handles.DrawAAPolyLine(i==selectedSegment?4f:2f,points);
                }
            }
            for(int i=0;i<target.nodes.Count;i++){
                var p=target.transform.TransformPoint(target.nodes[i]);
                using(new Handles.DrawingScope(i==selectedNode?Color.yellow:Color.cyan))
                    Handles.DotHandleCap(0,p,Quaternion.identity,HandleUtility.GetHandleSize(p)*.065f,EventType.Repaint);
            }
        }
        public override void OnToolGUI(EditorWindow window)
        {
            if(!(window is SceneView view))return;
            var e=Event.current;
            if(e.type==EventType.KeyDown && (e.keyCode==KeyCode.Escape || e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter)){
                e.Use();if(drawing && wire){drawing=false;press.Cancel();HouseProperties.RepaintOpen();view.Repaint();}else ToolManager.RestorePreviousTool();return;
            }
            if (e.type==EventType.Ignore || e.type==EventType.MouseLeaveWindow) EndDrag();
            if(press.Track(view,e,Place))return;
            if(e.rawType==EventType.MouseUp && dragControl!=0 && e.button==0){EndDrag();e.Use();return;}
            if(e.type==EventType.Repaint && wire)DrawNetwork(wire,node,segment);
            if(e.alt || Tools.viewToolActive || (e.isMouse && e.button==1))return;
            int id=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout){
                HandleUtility.AddDefaultControl(id);
                if(wire && !drawing){
                    // Give visible paths and nodes priority over ordinary Scene object selection.
                    if(PickNode(e.mousePosition)>=0)HandleUtility.AddControl(id,3f);
                    else if(PickSegment(out _)>=0)HandleUtility.AddControl(id,4f);
                }
            }
            if(!drawing && wire && node>=0 && node<wire.nodes.Count && dragControl==0)
            {
                if(e.type==EventType.KeyDown && (e.keyCode==KeyCode.Delete || e.keyCode==KeyCode.Backspace))
                { DeleteNode(); e.Use();return;}
                EditorGUI.BeginChangeCheck();
                var p=Handles.PositionHandle(wire.transform.TransformPoint(wire.nodes[node]),Tools.pivotRotation==PivotRotation.Local?wire.transform.rotation:Quaternion.identity);
                if(EditorGUI.EndChangeCheck())SetNode(p);
            }
            bool input=e.type==EventType.MouseMove || e.type==EventType.MouseDown || e.type==EventType.MouseDrag;
            if(input && (drawing || dragControl!=0)){snapped=drawing?PickNode(e.mousePosition):-1;
                cursor=snapped>=0?wire.transform.TransformPoint(wire.nodes[snapped]):Surface(e.mousePosition,out var pt)?pt:(Vector3?)null;view.Repaint();}
            if(dragControl!=0 && e.type==EventType.MouseDrag && e.button==0){if(cursor.HasValue)SetNode(cursor.Value);e.Use();return;}
            if(e.type==EventType.Repaint)
            {
                if(drawing && cursor.HasValue)
                {
                    var p=cursor.Value;var from=wire && anchor>=0 && anchor<wire.nodes.Count?wire.transform.TransformPoint(wire.nodes[anchor]):first;
                    using(new Handles.DrawingScope(Color.green)){if(from.HasValue)Handles.DrawAAPolyLine(2,from.Value,p);Handles.DotHandleCap(0,p,Quaternion.identity,HandleUtility.GetHandleSize(p)*.05f,EventType.Repaint);}
                }
            }
            if(e.type!=EventType.MouseDown || e.button!=0 || GUIUtility.hotControl!=0 || HandleUtility.nearestControl!=id)return;
            if(drawing){if(cursor.HasValue)press.Press(view,e,id,cursor.Value,false,Place);return;}
            if(!wire)return;
            int picked=PickNode(e.mousePosition);
            if(picked>=0){node=picked;segment=-1;dragControl=GUIUtility.hotControl=id;e.Use();HouseProperties.RepaintOpen();view.Repaint();return;}
            int selected=PickSegment(out float splitT);
            node=-1;segment=selected;
            if(selected>=0 && e.clickCount==2)Split(selected,splitT);
            HouseProperties.RepaintOpen();view.Repaint();e.Use();
        }
        int PickSegment(out float splitT)
        {
            float best=10;splitT=0;int selected=-1;
            for(int i=0;i<wire.segments.Count;i++){
                var edge=wire.segments[i];int steps=wire.Steps(edge);
                var a=wire.transform.TransformPoint(wire.nodes[edge.a]);
                for(int k=1;k<=steps;k++){
                    var b=wire.transform.TransformPoint(wire.Point(edge,(float)k/steps));
                    float d=HandleUtility.DistanceToLine(a,b);
                    if(d<best){
                        best=d;selected=i;
                        var delta=b-a;
                        float fraction=Vector3.Dot(HandleUtility.ClosestPointToPolyLine(a,b)-a,delta)/Mathf.Max(1e-12f,delta.sqrMagnitude);
                        splitT=(k-1+Mathf.Clamp01(fraction))/steps;
                    }
                    a=b;
                }
            }
            return selected;
        }
    }

    [CustomEditor(typeof(ProceduralWire))]
    internal sealed class WireInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            WireTool.DrawWireSettings((ProceduralWire)target);
            if(!WireTool.Active && GUILayout.Button("Edit Wire"))WireTool.Begin((ProceduralWire)target);
        }

        void OnSceneGUI()
        {
            if(!WireTool.Active && Event.current.type==EventType.Repaint)
                WireTool.DrawNetwork((ProceduralWire)target,-1,-1);
        }
    }

}
