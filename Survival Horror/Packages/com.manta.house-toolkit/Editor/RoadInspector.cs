using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseToolkit.Editor
{
    [Overlay(typeof(SceneView),"house-toolkit-roads","Road Tools",true,
        defaultDockZone=DockZone.LeftColumn,defaultDockPosition=DockPosition.Top)]
    public sealed class RoadOverlay : Overlay,ITransientOverlay
    {
        public bool visible => !EditorApplication.isPlayingOrWillChangePlaymode &&
            (RoadScene.Active || Selection.activeGameObject && Selection.activeGameObject.GetComponent<RoadNetwork>());
        public override VisualElement CreatePanelContent()
        {
            var root=new VisualElement();root.style.width=168;
            Button Add(string label,Action action)
            {
                var button=new Button(action){text=label};button.style.height=26;root.Add(button);return button;
            }
            var enter=Add("Edit Roads",()=>RoadScene.Enter(Selection.activeGameObject.GetComponent<RoadNetwork>()));
            var select=SceneToolButtons.Add(root,"Select","Select a road, point, approach or sidewalk.",()=>RoadScene.SetDrawing(false));
            var draw=SceneToolButtons.Add(root,"Draw Road","Draw roads. Shift constrains direction and Alt bypasses snapping.",()=>RoadScene.SetDrawing(true));
            var done=Add("Done",RoadScene.Leave);
            var message=new Label();message.style.whiteSpace=WhiteSpace.Normal;root.Add(message);
            void Refresh()
            {
                bool editing=RoadScene.Active;
                enter.style.display=editing?DisplayStyle.None:DisplayStyle.Flex;
                select.style.display=draw.style.display=done.style.display=editing?DisplayStyle.Flex:DisplayStyle.None;
                SceneToolButtons.Highlight(select,editing&&!RoadScene.Drawing);
                SceneToolButtons.Highlight(draw,editing&&RoadScene.Drawing);
                message.text=RoadScene.Message;
            }
            Refresh();root.schedule.Execute(Refresh).Every(120);return root;
        }
    }

    [CustomEditor(typeof(RoadNetwork))]
    internal sealed class RoadInspector : UnityEditor.Editor
    {
        static int tab;
        static bool showFamilyDimensions;
        Bounds bounds;
        bool HasFrameBounds()=>RoadScene.Active==target && RoadScene.Frame(out bounds);
        Bounds OnGetFrameBounds()=>bounds;
        internal static void ShowTool(bool tool)=>tab=tool?0:1;
        static T Asset<T>(string label,T value) where T:UnityEngine.Object =>
            (T)EditorGUILayout.ObjectField(label,value,typeof(T),false);

        public override void OnInspectorGUI()
        {
            var road=(RoadNetwork)target;
            if(RoadScene.Active!=road)
            {
                serializedObject.Update();EditorGUILayout.PropertyField(serializedObject.FindProperty("family"),new GUIContent("Road family"));
                if(serializedObject.ApplyModifiedProperties())road.Rebuild();
                if(GUILayout.Button("Edit Roads"))RoadScene.Enter(road);
                return;
            }
            tab=GUILayout.Toolbar(tab,new[]{"Tool Properties","Selection"});
            if(tab==0)
            {
                serializedObject.Update();EditorGUILayout.PropertyField(serializedObject.FindProperty("family"),new GUIContent("Network family"));
                if(serializedObject.ApplyModifiedProperties())road.Rebuild();
                if(RoadScene.Drawing)RoadScene.DrawFamily=Asset("Draw family override",RoadScene.DrawFamily);
                var current=RoadScene.Drawing && RoadScene.DrawFamily ? RoadScene.DrawFamily : road.family;
                if(current)
                {
                    showFamilyDimensions=EditorGUILayout.Foldout(showFamilyDimensions,"Family dimensions",true);
                    if(showFamilyDimensions)
                    {
                        var settings=new SerializedObject(current);
                        RoadFamilyInspector.Dimensions(settings);
                        if(settings.ApplyModifiedProperties())RoadEditing.RebuildLoaded(current);
                    }
                }
                else if(GUILayout.Button("Create road family?"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Create road family","Road Family","asset","Choose where to save the family.");
                    if(!string.IsNullOrEmpty(path))
                    {
                        var family=CreateInstance<RoadFamily>();
                        AssetDatabase.CreateAsset(family,path);
                        Undo.RecordObject(road,"Assign road family");
                        road.family=family;EditorUtility.SetDirty(road);road.Rebuild();showFamilyDimensions=true;
                    }
                }
                PlanSnapping.Grid=EditorGUILayout.DelayedFloatField("Grid spacing (m)",PlanSnapping.Grid);
                return;
            }
            var h=RoadScene.Selected;
            var s=road.graph.Span(h.id);
            var n=road.graph.Node(h.id);
            if(h.part==RoadPart.Point && n!=null)
            {
                EditorGUI.BeginChangeCheck();
                var position=EditorGUILayout.Vector3Field("Location (m)",n.position);
                if(EditorGUI.EndChangeCheck())RoadScene.Change("Move road point",g=>RoadEditing.Move(g,n.id,position));
                var arms=RoadGeometry.Arms(road.graph,n,road.family);
                if(arms.Count>=3)
                {
                    var mat=Asset("Junction override",n.junctionMaterial);
                    if(mat!=n.junctionMaterial)RoadScene.Change("Change junction material",g=>g.Node(n.id).junctionMaterial=mat);
                    int first=RoadGeometry.Primary(arms);
                    if(GUILayout.Button(arms.Count==3?"Cycle T branch":"Rotate X texture 90°"))
                        RoadScene.Change("Orient junction",g=>g.Node(n.id).firstArm=arms[(first+1)%arms.Count].end.port);
                }
                else if(GUILayout.Button(arms.Count==2?"Dissolve point":"Delete endpoint"))RoadScene.Delete();
            }
            else if(h.part==RoadPart.Road && s!=null)
            {
                var ids=RoadScene.SelectedRoads;
                var selected=road.graph.spans.Where(x=>ids.Contains(x.id)).ToList();
                EditorGUILayout.LabelField(selected.Count==1?"Road":selected.Count+" roads",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue=selected.Any(x=>x.family!=s.family);
                var family=Asset("Family override",s.family);
                EditorGUI.showMixedValue=false;
                if(EditorGUI.EndChangeCheck())RoadScene.Change("Change road family",g=>{
                    foreach(var span in g.spans.Where(x=>ids.Contains(x.id)))span.family=family;
                });
                foreach(bool left in new[]{true,false})
                {
                    RoadSide Side(RoadSpan span)=>left?span.left:span.right;
                    EditorGUI.showMixedValue=selected.Any(x=>Side(x).sidewalk!=Side(s).sidewalk);
                    EditorGUI.BeginChangeCheck();
                    var walk=(RoadOverride)EditorGUILayout.EnumPopup(left?"Left sidewalk":"Right sidewalk",Side(s).sidewalk);
                    EditorGUI.showMixedValue=false;
                    if(EditorGUI.EndChangeCheck())RoadScene.Change("Change road sidewalk",g=>{
                        foreach(var span in g.spans.Where(x=>ids.Contains(x.id)))Side(span).sidewalk=walk;
                    });
                }
            }
            else if(h.part==RoadPart.Approach && s!=null)
            {
                RoadEnd End(RoadSpan span)=>span.start.port==h.arm?span.start:span.end;
                var e=End(s);EditorGUILayout.LabelField("Approach",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                bool crossing=EditorGUILayout.Toggle("Crosswalk",e.crosswalk);
                bool stop=EditorGUILayout.Toggle("Stop line",e.stopLine);
                var set = s.Style(road.family).markingSet;
                string symbol=e.symbol, pattern=e.crosswalkStyle;
                if (set && set.atlas)
                {
                    if (crossing) pattern=MarkingPicker("Crosswalk style",set,pattern,true);
                    symbol=MarkingPicker("Lane marking",set,symbol,false);
                }
                if(EditorGUI.EndChangeCheck())RoadScene.Change("Change approach markings",g=>{
                    var end=End(g.Span(s.id));end.crosswalk=crossing;end.stopLine=stop;
                    end.symbol=symbol;end.crosswalkStyle=pattern;
                });
                if(GUILayout.Button("Select road"))RoadScene.Pick(new RoadHit(RoadPart.Road,s.id));
            }
            else if(h.part==RoadPart.Sidewalk && s!=null)
            {
                var side=h.left?s.left:s.right;
                var copy=JsonUtility.FromJson<RoadSide>(JsonUtility.ToJson(side));
                EditorGUILayout.LabelField(h.left?"Left sidewalk":"Right sidewalk",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                copy.material=Asset("Sidewalk material",copy.material);
                Curb(copy.curb,s.Style(road.family));
                if(EditorGUI.EndChangeCheck())RoadScene.Change("Change sidewalk finish",g=>{
                    if(h.left)g.Span(s.id).left=copy;else g.Span(s.id).right=copy;
                });
            }
            else if(h.part==RoadPart.Corner && n!=null)
            {
                var arms=RoadGeometry.Arms(road.graph,n,road.family);
                var a=arms.Find(x=>x.end.port==h.arm);var b=arms.Find(x=>x.end.port==h.next);
                if(a==null || b==null)return;
                var stored=RoadGeometry.Corner(n,a,b);
                var copy=stored==null?new RoadCorner{arm=h.arm,nextArm=h.next}:JsonUtility.FromJson<RoadCorner>(JsonUtility.ToJson(stored));
                EditorGUILayout.LabelField("Sidewalk corner",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                copy.sidewalk=EditorGUILayout.Toggle("Sidewalk",copy.sidewalk);
                if(copy.sidewalk)
                {
                    copy.material=Asset("Sidewalk material",copy.material);
                    Curb(copy.curb,a.family,a.Side(true).curb);
                }
                if(EditorGUI.EndChangeCheck())SetCorner(h,copy);
            }
            else EditorGUILayout.LabelField("Select a point, road, approach or sidewalk.");
            if(!string.IsNullOrEmpty(RoadScene.Message))EditorGUILayout.HelpBox(RoadScene.Message,MessageType.Info);
        }

        static string MarkingPicker(string title,RoadMarkingSet set,string current,bool crossing)
        {
            var selected=set.Find(current,crossing);
            string fallback=crossing?"Standard":"None";
            var entries=(set.markings??Array.Empty<RoadMarkingSet.Marking>())
                .Where(m=>m!=null && m.Valid && m.crosswalk==crossing).ToArray();
            if(entries.Length==0)return current;
            string[] names=new[]{fallback}.Concat(entries.Select(m=>m.name)).ToArray();
            int index=EditorGUILayout.Popup(title,Array.IndexOf(entries,selected)+1,names);
            if(index>0 && set.atlas.mainTexture)
            {
                var box=EditorGUILayout.GetControlRect(false,36);
                var preview=new Rect(box.x+EditorGUIUtility.labelWidth,box.y,36,36);
                GUI.DrawTextureWithTexCoords(preview,set.atlas.mainTexture,entries[index-1].uv,true);
            }
            return index==0?null:entries[index-1].name;
        }

        internal static void SetCorner(RoadHit hit,RoadCorner copy)=>RoadScene.Change("Edit sidewalk corner",g=>{
            var corners=g.Node(hit.id).corners;
            corners.RemoveAll(c=>c.arm==hit.arm && c.nextArm==hit.next);corners.Add(copy);
        });

        static void Curb(RoadCurb curb,RoadFamily family,RoadCurb parent=null)
        {
            curb.enabled=(RoadOverride)EditorGUILayout.EnumPopup("Curbs",curb.enabled);
            bool inherited=parent!=null && parent.enabled!=RoadOverride.Inherit ? parent.enabled==RoadOverride.On : family.curbs;
            if(curb.enabled==RoadOverride.Off || curb.enabled==RoadOverride.Inherit && !inherited)return;
            float width=Inherited("Curb width (m)",curb.width>0?curb.width:-1,parent!=null && parent.width>0?parent.width:family.curbWidth);
            curb.width=width<0?0:Mathf.Max(.01f,width);
            curb.height=Inherited("Curb height (m)",curb.height,parent!=null && parent.height>=0?parent.height:family.curbHeight>=0?family.curbHeight:family.sidewalkHeight);
            curb.material=Asset("Curb material",curb.material);
        }
        internal static float Inherited(string label,float value,float fallback)
        {
            EditorGUILayout.BeginHorizontal();EditorGUI.BeginChangeCheck();
            float input=EditorGUILayout.DelayedFloatField(new GUIContent(label,value<0?"Inherited":"Local override"),value<0?fallback:value);
            if(EditorGUI.EndChangeCheck())value=Mathf.Max(0,input);
            using(new EditorGUI.DisabledScope(value<0))
                if(GUILayout.Button(new GUIContent("↺","Restore inheritance"),GUILayout.Width(25))){value=-1;GUI.changed=true;}
            EditorGUILayout.EndHorizontal();return value;
        }
    }

    [CustomEditor(typeof(RoadMarkingSet))]
    internal sealed class RoadMarkingSetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if(DrawDefaultInspector()) RoadEditing.RebuildLoaded();
        }
    }

    [CustomEditor(typeof(RoadFamily))]
    internal sealed class RoadFamilyInspector : UnityEditor.Editor
    {
        internal static void Dimensions(SerializedObject settings)
        {
            void Field(string key,string label)=>EditorGUILayout.PropertyField(settings.FindProperty(key),new GUIContent(label));
            Field("width","Road width (m)");
            Field("forwardLanes","Forward lanes");Field("reverseLanes","Reverse lanes");
            Field("sidewalkWidth","Sidewalk width (m)");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            void Field(string name,string title)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(title));
            Dimensions(serializedObject);Field("repeatLength","Texture repeat (m)");
            Field("road","Road material");Field("bareAsphalt","Unmarked asphalt");
            Field("tJunction","T-junction");Field("xJunction","X-junction");
            Field("wheelPathTint","Wheel path tint");Field("paintWear","Paint wear");
            EditorGUILayout.Space();Field("leftSidewalk","Left sidewalk");Field("rightSidewalk","Right sidewalk");
            Field("sidewalkHeight","Sidewalk height (m)");
            Field("sidewalk","Sidewalk material");
            Field("curbs","Curbs");
            if(serializedObject.FindProperty("curbs").boolValue)
            {
                Field("curbWidth","Curb width (m)");
                var height=serializedObject.FindProperty("curbHeight");
                height.floatValue=RoadInspector.Inherited("Curb height (m)",height.floatValue,serializedObject.FindProperty("sidewalkHeight").floatValue);
                Field("curbMaterial","Curb material");
            }
            EditorGUILayout.Space();Field("markingSet","Marking set");Field("crossingPaint","Crosswalk / stop line paint");Field("centerLine","Center line");
            var line=(RoadLines)serializedObject.FindProperty("centerLine").enumValueIndex;
            if(line!=RoadLines.None)
            {
                Field("paint","Center line paint");Field("lineWidth","Line width (m)");
            }
            if(line==RoadLines.Dashed){Field("dash","Dash (m)");Field("gap","Gap (m)");}
            if(serializedObject.ApplyModifiedProperties())RoadEditing.RebuildLoaded((RoadFamily)target);
        }
    }
}
