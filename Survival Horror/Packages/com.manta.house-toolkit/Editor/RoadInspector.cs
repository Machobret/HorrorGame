using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [CustomEditor(typeof(ProceduralSurface),true)]
    internal sealed partial class RoadInspector : UnityEditor.Editor
    {
        enum Tab { Road, Selection, Tool }
        static readonly string[] RoadTabs = { "Road", "Selection", "Tool Properties" };
        static readonly string[] SidewalkTabs = { "Sidewalk", "Selection", "Tool Properties" };
        static readonly string[] GroundTabs = { "Ground Area", "Selection", "Tool Properties" };
        static Tab tab;
        UnityEditor.Editor familyEditor;
        int fillerScope;
        Bounds bounds;
        bool HasFrameBounds()=>RoadScene.Active==target && RoadScene.Frame(out bounds);
        Bounds OnGetFrameBounds()=>bounds;
        internal static void ShowRoad() => tab = Tab.Road;
        internal static void ShowTool(bool tool) => tab = tool ? Tab.Tool : Tab.Selection;
        void OnDisable()
        {
            if (familyEditor) DestroyImmediate(familyEditor);
        }
        static T Asset<T>(string label,T value) where T:UnityEngine.Object =>
            (T)EditorGUILayout.ObjectField(label,value,typeof(T),false);

        public override void OnInspectorGUI()
        {
            var road = (ProceduralSurface)target;
            if (GUILayout.Button(HouseProperties.WindowTitle))
            {
                Selection.activeGameObject = road.gameObject;
                HouseProperties.Open();
            }
            DrawProperties();
        }

        internal void DrawProperties()
        {
            var road = (ProceduralSurface)target;
            bool editing = RoadScene.Active == road;
            if (!editing && GUILayout.Button(RoadScene.EditLabel(road))) RoadScene.Enter(road);
            if(road is FillerBuilding building){DrawFiller(building,editing);return;}
            if (editing) tab = (Tab)GUILayout.Toolbar((int)tab, road is RoadNetwork ? RoadTabs : road is ProceduralSidewalk ? SidewalkTabs : GroundTabs);
            if (!editing || tab == Tab.Road)
            {
                if (!(road is RoadNetwork network))
                {
                    PavementDefaults(road,road is ProceduralSidewalk);
                    if (editing && road is GroundArea) DrawConnectedAreaMaterial(road);
                    return;
                }
                serializedObject.Update();
                var family = serializedObject.FindProperty("family");
                EditorGUILayout.PropertyField(family, new GUIContent("Network family"));
                if (serializedObject.ApplyModifiedProperties()) road.Rebuild();
                if (network.family)
                {
                    DrawFamilySettings(network.family);
                }
                else if (GUILayout.Button("Create road family"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Create road family", "Road Family", "asset", "Choose where to save the family.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var created = CreateInstance<RoadFamily>();
                        AssetDatabase.CreateAsset(created, path);
                        family.objectReferenceValue = created;
                        serializedObject.ApplyModifiedProperties();
                        road.Rebuild();
                    }
                }
                if ((network.paths.spans.Count>0 || network.ground.edges.Count>0) && GUILayout.Button("Separate existing pavement"))
                    RoadEditing.SeparatePavement(network);
                if (editing && network.ground.faces.Count>0) DrawConnectedAreaMaterial(road);
                return;
            }
            if (tab == Tab.Tool)
            {
                PlanSnapping.Grid = EditorGUILayout.DelayedFloatField("Grid spacing (m)", PlanSnapping.Grid);
                if (RoadScene.Mode == RoadScene.Brush.Ground || RoadScene.Mode == RoadScene.Brush.Sidewalk)
                {
                    PavementDefaults(road, RoadScene.Mode == RoadScene.Brush.Sidewalk);
                    if (RoadScene.Mode == RoadScene.Brush.Ground)
                        EditorGUILayout.HelpBox("Click to draw edges. Close a perimeter to fill it, or connect two boundary points to divide it.",MessageType.Info);
                }
                if (RoadScene.Mode == RoadScene.Brush.Road)
                {
                    RoadScene.DrawFamily = Asset("Draw family override", RoadScene.DrawFamily);
                    if (RoadScene.DrawFamily) DrawFamilySettings(RoadScene.DrawFamily);
                }
                return;
            }
            if (RoadScene.Picks.DrawFilter(RoadScene.SyncPicks)) return;
            if (RoadScene.Picks.Items.Count>0) DrawRoadSelection(road);
            else EditorGUILayout.LabelField(road is GroundArea ? "Select a face or boundary." : road is ProceduralSidewalk ? "Select a sidewalk segment or point." : "Select a road, sidewalk or point.");
        }

        static void DrawConnectedAreaMaterial(ProceduralSurface road)
        {
            var selectedFaces=RoadScene.Picks.Items.Where(hit=>hit.part==RoadPart.Ground).Select(hit=>hit.id).ToHashSet();
            if (selectedFaces.Count==0) return;
            var areas=road.Areas.faces.Where(face=>selectedFaces.Contains(face.id)).GroupBy(face=>face.areaId)
                .Select(group=>group.First()).ToArray();
            MultiField.Draw(areas,face=>face.areaMaterial,value=>Asset("Connected area material",value),
                value=>RoadScene.ChangeGround("Change area materials",plan=>{
                    var selectedAreas=areas.Select(face=>face.areaId).ToHashSet();
                    foreach (var face in plan.faces)
                        if (selectedAreas.Contains(face.areaId)) face.areaMaterial=value;
                }));
        }

        void PavementDefaults(ProceduralSurface road, bool width)
        {
            serializedObject.Update();
            if (!(road is RoadNetwork network))
            {
                if (width) EditorGUILayout.PropertyField(serializedObject.FindProperty("width"),new GUIContent("Sidewalk width (m)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("material"),new GUIContent("Default material"));
                if (serializedObject.ApplyModifiedProperties()) road.Rebuild();
                return;
            }
            EditorGUILayout.LabelField("Network pavement defaults",EditorStyles.boldLabel);
            if (width)
            {
                var property=serializedObject.FindProperty("pavementWidth");
                float fallback=(network.family ? network.family : RoadFamily.Default).sidewalkWidth;
                property.floatValue=Width("Sidewalk width (m)",property.floatValue,fallback);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pavementMaterial"),
                new GUIContent("Pavement material","Empty inherits the road family's sidewalk material."));
            if (serializedObject.ApplyModifiedProperties()) road.Rebuild();
        }

        static float Width(string label, float value, float fallback,float minimum=.1f)
        {
            float edited=Inherited(label,value>0 ? value : -1,fallback);
            return edited<0 ? 0 : Mathf.Max(minimum,edited);
        }

        void DrawFamilySettings(RoadFamily family)
        {
            EditorGUILayout.LabelField("Shared family settings", EditorStyles.boldLabel);
            CreateCachedEditor(family, typeof(RoadFamilyInspector), ref familyEditor);
            familyEditor.OnInspectorGUI();
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
        bool surfaces, sidewalks, markings;

        static void Dimensions(SerializedObject settings)
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
            Dimensions(serializedObject);
            sidewalks = EditorGUILayout.Foldout(sidewalks, "Sidewalks and curbs", true);
            if (sidewalks)
            {
                Field("leftSidewalk","Left sidewalk");Field("rightSidewalk","Right sidewalk");
                Field("sidewalkHeight","Sidewalk height (m)");Field("sidewalk","Sidewalk material");
                Field("curbs","Curbs");
                if (serializedObject.FindProperty("curbs").boolValue)
                {
                    Field("curbWidth","Curb width (m)");
                    var height = serializedObject.FindProperty("curbHeight");
                    height.floatValue = RoadInspector.Inherited("Curb height (m)",height.floatValue,serializedObject.FindProperty("sidewalkHeight").floatValue);
                    Field("curbMaterial","Curb material");
                }
            }
            surfaces = EditorGUILayout.Foldout(surfaces, "Road surface and junctions", true);
            if (surfaces)
            {
                Field("road","Road material");Field("bareAsphalt","Unmarked asphalt");
                Field("tJunction","T-junction");Field("xJunction","X-junction");
                Field("repeatLength","Texture repeat (m)");Field("wheelPathTint","Wheel path tint");
            }
            markings = EditorGUILayout.Foldout(markings, "Markings", true);
            if (markings)
            {
                Field("markingSet","Marking set");Field("crossingPaint","Crosswalk / stop line paint");
                Field("paintWear","Paint wear");Field("centerLine","Center line");
                var line = (RoadLines)serializedObject.FindProperty("centerLine").enumValueIndex;
                if (line != RoadLines.None)
                {
                    Field("paint","Center line paint");Field("lineWidth","Line width (m)");
                }
                if (line == RoadLines.Dashed) { Field("dash","Dash (m)");Field("gap","Gap (m)"); }
            }
            if(serializedObject.ApplyModifiedProperties())RoadEditing.RebuildLoaded((RoadFamily)target);
        }
    }
}
