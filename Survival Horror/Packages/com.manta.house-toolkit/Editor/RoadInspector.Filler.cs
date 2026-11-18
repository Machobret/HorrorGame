using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal sealed partial class RoadInspector
    {
        static readonly string[] FillerTabs={"Building","Selection","Floors"};

        static FillerBuildingType[] types;
        static FillerBuildingType[] BuildingTypes => types??=AssetDatabase.FindAssets("t:FillerBuildingType")
            .Select(g=>AssetDatabase.LoadAssetAtPath<FillerBuildingType>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t=>t && t.facade).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
        static RoadInspector() { EditorApplication.projectChanged+=()=>types=null; }

        internal static FillerBuildingType StartingType(int seed)
        {
            var choices=BuildingTypes;
            return choices.Length==0 ? null : choices[(int)((uint)seed%(uint)choices.Length)];
        }

        void TypePicker(FillerBuilding building)
        {
            var choices=BuildingTypes;
            string[] names=new[]{"Custom"}.Concat(choices.Select(t=>t.name)).ToArray();
            int index=Array.IndexOf(choices,building.buildingType)+1;
            EditorGUI.BeginChangeCheck();
            int next=ProjectAssetNavigation.Popup("Building type",building.buildingType,index,names);
            if(EditorGUI.EndChangeCheck())
                EditFiller(building,"Change building type",()=>building.SetType(next==0 ? null : choices[next-1]));
        }

        static void Storeys(SerializedProperty property)
        {
            var label=new GUIContent("Storeys","Slide from 1 to 10 or enter a number up to "+FillerBuilding.MaxStoreys+".");
            var rect=EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect,label,property);
            rect=EditorGUI.PrefixLabel(rect,label);
            var number=new Rect(rect.xMax-44,rect.y,44,rect.height);
            rect.xMax=number.x-6;
            EditorGUI.BeginChangeCheck();
            int slid=Mathf.RoundToInt(GUI.HorizontalSlider(rect,Mathf.Min(property.intValue,10),1,10));
            if(EditorGUI.EndChangeCheck())property.intValue=slid;
            EditorGUI.BeginChangeCheck();
            int typed=EditorGUI.DelayedIntField(number,property.intValue);
            if(EditorGUI.EndChangeCheck())property.intValue=Mathf.Clamp(typed,1,FillerBuilding.MaxStoreys);
            EditorGUI.EndProperty();
        }

        void DrawFiller(FillerBuilding building,bool editing)
        {
            if(editing)tab=(Tab)GUILayout.Toolbar((int)tab,FillerTabs);
            if(!editing || tab==Tab.Road)
            {
                TypePicker(building);
                serializedObject.Update();
                void Field(string key,string label)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(key),new GUIContent(label));
                if(!building.buildingType || building.facade)Field("facade","Default façade");
                Field("roof","Roof material");
                Storeys(serializedObject.FindProperty("floors"));Field("clearHeight","Clear height (m)");Field("bayWidth","Preferred bay width (m)");
                if(serializedObject.ApplyModifiedProperties())building.Rebuild();
                if(GUILayout.Button("Reroll appearance"))
                    EditFiller(building,"Reroll façade",()=>building.seed=Guid.NewGuid().GetHashCode() & 0x7fffff);
                if(editing && RoadScene.Drawing)
                {
                    PlanSnapping.Grid=EditorGUILayout.DelayedFloatField("Grid spacing (m)",PlanSnapping.Grid);
                    EditorGUILayout.HelpBox("Click around the footprint, then click the first point to close it.",MessageType.Info);
                }
                return;
            }

            string FloorName(int level)=>level==0 ? "Ground floor" : "Floor "+(level+1);
            if(tab==Tab.Tool)
            {
                for(int level=0;level<building.floors;level++)
                {
                    var current=building.Override(0,level);
                    var value=Asset(FloorName(level),current);
                    if(current!=value)EditFiller(building,"Set floor façade",()=>building.SetFinish(0,level,value));
                }
                return;
            }

            if(RoadScene.Picks.DrawFilter(RoadScene.SyncPicks))return;
            var walls=RoadScene.Picks.Items.Where(p=>p.part==RoadPart.Boundary).Select(p=>p.id).ToArray();
            if(walls.Length==0)
            {
                EditorGUILayout.HelpBox("Select a façade wall. Shift-click to edit several walls together.",MessageType.Info);
                return;
            }
            var scopes=new List<string>{"All floors"};
            for(int i=0;i<building.floors;i++)scopes.Add(FloorName(i));
            fillerScope=EditorGUILayout.Popup("Applies to",Mathf.Clamp(fillerScope,0,building.floors),scopes.ToArray());
            int floor=fillerScope-1;
            if(floor!=0)
                MultiField.Draw(walls,id=>building.Override(id,floor),v=>Asset("Façade override",v),
                    v=>EditFiller(building,"Set wall façade",()=>{foreach(int id in walls)building.SetFinish(id,floor,v);}));
            if(floor>0)
            {
                var effective=walls.Select(id=>building.MaterialFor(id,floor)).Distinct().ToArray();
                EditorGUILayout.LabelField("Effective façade",effective.Length>1 ? "Mixed" : effective[0] ? effective[0].name : "Default material");
            }
            if(floor<=0)
            {
                GroundStylePicker(building,walls);
                var entranceWalls=walls.Where(id=>building.FinishAt(id,0)?.groundStyle!=FillerBuilding.GroundStyle.Solid).ToArray();
                MultiField.Draw(entranceWalls,id=>building.EntranceBay(id)+1,
                    v=>EditorGUILayout.DelayedIntField(new GUIContent("Entrance bay","Ground floor, numbered from 1 along the outside face. 0 removes the entrance."),v),
                    v=>EditFiller(building,"Place façade entrance",()=>{
                        foreach(int id in entranceWalls)
                        {
                            var edge=building.footprint.edges.Find(e=>e.id==id);
                            building.SetEntrance(id,Mathf.Clamp(v,0,building.Bays(edge))-1);
                        }
                    }));
            }
        }

        void GroundStylePicker(FillerBuilding building,int[] walls)
        {
            var shops=BuildingTypes.SelectMany(t=>t.storefronts ?? Array.Empty<Material>()).Where(m=>m).Distinct()
                .OrderBy(m=>m.name,StringComparer.Ordinal).ToList();
            var labels=new List<string>{"Inherit","Normal","Solid wall"};
            labels.AddRange(shops.Select(m=>"Storefront/"+ObjectNames.NicifyVariableName(m.name.Replace("_Storefront",""))));
            int custom=labels.Count;
            labels.Add("Custom material");
            int Choice(int id)
            {
                var finish=building.FinishAt(id,0);
                if(finish==null)return 0;
                if(finish.groundStyle==FillerBuilding.GroundStyle.Normal)return 1;
                if(finish.groundStyle==FillerBuilding.GroundStyle.Solid)return 2;
                int preset=shops.IndexOf(finish.material);
                return preset>=0 ? preset+3 : custom;
            }
            void Set(FillerBuilding.GroundStyle style,Material material=null)
            {
                SetGroundStyle(building,walls,style,material);
            }
            MultiField.Draw(walls,Choice,index=>ProjectAssetNavigation.Popup("Ground floor",index >= 3 && index < custom ? shops[index-3] : null,index,labels.ToArray()),index=>{
                if(index==0)Set(FillerBuilding.GroundStyle.Inherit);
                else if(index==1)Set(FillerBuilding.GroundStyle.Normal);
                else if(index==2)Set(FillerBuilding.GroundStyle.Solid);
                else Set(FillerBuilding.GroundStyle.Material,index==custom ? null : shops[index-3]);
            });
            if(walls.All(id=>Choice(id)==custom))
                MultiField.Draw(walls,id=>building.Override(id,0),v=>Asset("Ground-floor material",v),
                    v=>Set(FillerBuilding.GroundStyle.Material,v));
        }

        internal static void SetGroundStyle(FillerBuilding building,int[] walls,FillerBuilding.GroundStyle style,Material material=null)
        {
            EditFiller(building,"Set ground-floor wall",()=>{
                foreach(int id in walls)building.SetGroundStyle(id,style,material);
            });
        }

        static void EditFiller(FillerBuilding building,string label,Action change)
        {
            Undo.RecordObject(building,label);
            change();
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            EditorUtility.SetDirty(building);
            building.Rebuild();
            SceneView.RepaintAll();
        }
    }
}
