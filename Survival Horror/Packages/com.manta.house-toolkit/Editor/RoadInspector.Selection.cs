using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal sealed partial class RoadInspector
    {
        void DrawRoadSelection(ProceduralSurface road)
        {
            var hits=RoadScene.Picks.Items.ToArray();
            var part=hits[0].part;
            string name=part==RoadPart.Ground ? "Ground face" : part==RoadPart.Path ? RoadScene.PathName+" segment" : part==RoadPart.Boundary ? "Ground boundary" :
                part==RoadPart.GroundPoint ? "Ground point" : part==RoadPart.Corner ? "Sidewalk corner" : part==RoadPart.PathPoint ? RoadScene.PathName+" point" : part.ToString();
            EditorGUILayout.LabelField(hits.Length==1 ? name : name+" ("+hits.Length+")",EditorStyles.boldLabel);
            if(part==RoadPart.Ground)
            {
                MultiField.Draw(hits,h=>road.Areas.faces.Find(f=>f.id==h.id).material,v=>Asset("Face material override",v),
                    v=>RoadScene.ChangeGround("Change ground materials",g=>{
                        foreach(var h in hits)g.faces.Find(f=>f.id==h.id).material=v;
                    }));
            }
            bool paths=part==RoadPart.Path || part==RoadPart.PathPoint;
            var graph=paths?road.Paths:road.Streets;
            void Edit(string label,Action<RoadGraph,RoadHit> write,RoadHit[] subset)
            {
                void Apply(RoadGraph g){foreach(var h in subset)write(g,h);}
                if(paths)RoadScene.ChangePaths(label,Apply);else RoadScene.Change(label,Apply);
            }
            void Field<V>(string label,Func<RoadGraph,RoadHit,V> get,Func<V,V> gui,Action<RoadGraph,RoadHit,V> set,RoadHit[] subset=null)
            {
                var targets=subset??hits;
                MultiField.Draw(targets,h=>get(graph,h),gui,v=>Edit(label,(g,h)=>set(g,h,v),targets));
            }
            if(part==RoadPart.Road)
            {
                Field("Change road family",(g,h)=>g.Span(h.id).family,v=>Asset("Family override",v),(g,h,v)=>g.Span(h.id).family=v);
                foreach(bool left in new[]{true,false})
                {
                    RoadSide Side(RoadGraph g,RoadHit h)=>left?g.Span(h.id).left:g.Span(h.id).right;
                    Field("Change road sidewalk",(g,h)=>Side(g,h).sidewalk,
                        v=>(RoadOverride)EditorGUILayout.EnumPopup(left?"Left sidewalk":"Right sidewalk",v),(g,h,v)=>Side(g,h).sidewalk=v);
                }
                var streets = hits.GroupBy(h => graph.Span(h.id).run).Select(group => group.First()).ToArray();
                StreetlightRun Lights(RoadGraph g, RoadHit h) => g.Lights(g.Span(h.id).run);
                Field("Toggle streetlights", (g,h) => Lights(g,h)?.enabled == true,
                    v => EditorGUILayout.Toggle("Repeating streetlights", v), (g,h,v) => {
                        var style = Lights(g,h);
                        if (style == null)
                        {
                            style = new StreetlightRun { run = g.Span(h.id).run };
                            g.streetlights.Add(style);
                        }
                        if (v && !style.prefab)
                        {
                            style.prefab = AssetDatabase.FindAssets("Pizzeria_streetlight_001 t:Prefab")
                                .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)))
                                .FirstOrDefault(prefab => prefab && prefab.name == "Pizzeria_streetlight_001");
                            if (!style.prefab) throw new InvalidOperationException("The Pizzeria_streetlight_001 prefab is missing.");
                        }
                        style.enabled = v;
                    }, streets);
                var lit = streets.Where(h => Lights(graph,h)?.enabled == true).ToArray();
                if (lit.Length > 0)
                {
                    Field("Change streetlight side", (g,h) => Lights(g,h).side,
                        v => (StreetlightSide)EditorGUILayout.EnumPopup("Side", v),
                        (g,h,v) => Lights(g,h).side = v, lit);
                    Field("Change streetlight period", (g,h) => Lights(g,h).period,
                        v => EditorGUILayout.Slider("Period (m)", v, 2, 60), (g,h,v) => {
                            var style = Lights(g,h);
                            style.period = v;
                            style.offset = Mathf.Repeat(style.offset, v);
                        }, lit);
                    Field("Change streetlight offset", (g,h) => Lights(g,h).offset,
                        v => EditorGUILayout.Slider("Offset (m)", v, 0, lit.Min(h => Lights(graph,h).period)),
                        (g,h,v) => Lights(g,h).offset = v, lit);
                }
            }
            if(part==RoadPart.Path && !(road is ProceduralShelf || road is ProceduralMarking))
            {
                PavementDefaults(road,true);
                SidewalkRunStyle Run(RoadGraph g,RoadHit h)=>g.RunStyle(g.Span(h.id).run);
                SidewalkRunStyle Assign(RoadGraph g,RoadHit h)
                {
                    var style=Run(g,h);
                    if(style==null){style=new SidewalkRunStyle{run=g.Span(h.id).run};g.runStyles.Add(style);}
                    return style;
                }
                Field("Edit sidewalk runs",(g,h)=>Run(g,h)?.width??0,v=>Width("Run width (m)",v,road.PathWidth),(g,h,v)=>Assign(g,h).width=v);
                Field("Edit sidewalk runs",(g,h)=>Run(g,h)?.material,v=>Asset("Run material",v),(g,h,v)=>Assign(g,h).material=v);
                var span=graph.Span(hits[0].id);
                Field("Edit sidewalk widths",(g,h)=>g.Span(h.id).pathWidth,v=>Width("Segment width (m)",v,graph.RunWidth(span.run,road.PathWidth)),
                    (g,h,v)=>g.Span(h.id).pathWidth=v);
                Field("Edit sidewalk materials",(g,h)=>g.Span(h.id).pathMaterial,v=>Asset("Material override",v),(g,h,v)=>g.Span(h.id).pathMaterial=v);
            }
            if(part==RoadPart.Point || part==RoadPart.PathPoint)
            {
                var pivot=hits.Aggregate(Vector3.zero,(v,h)=>v+graph.Node(h.id).position)/hits.Length;
                EditorGUI.BeginChangeCheck();
                var location=EditorGUILayout.Vector3Field(hits.Length==1?"Location (m)":"Selection location (m)",pivot);
                if(paths && !(road is ProceduralPath elevated && elevated.Elevated))location.y=pivot.y;
                if(EditorGUI.EndChangeCheck()) Edit("Move points",(g,h)=>{
                    if(hits.Length==1) RoadEditing.Move(g,h.id,location);
                    else g.Node(h.id).position+=location-pivot;
                },hits);
                var junctions=paths?Array.Empty<RoadHit>():hits.Where(h=>RoadGeometry.Arms(graph,graph.Node(h.id),road.StreetFamily).Count>=3).ToArray();
                if(junctions.Length>0)
                {
                    Field("Change junction materials",(g,h)=>g.Node(h.id).junctionMaterial,v=>Asset("Junction override",v),(g,h,v)=>g.Node(h.id).junctionMaterial=v,junctions);
                    int armCount=RoadGeometry.Arms(graph,graph.Node(junctions[0].id),road.StreetFamily).Count;
                    string label=junctions.Length>1 ? "Cycle junction orientation" : armCount==3 ? "Cycle T branch" : "Rotate X texture 90°";
                    if(GUILayout.Button(label)) Edit("Orient junctions",(g,h)=>{
                        var n=g.Node(h.id);var arms=RoadGeometry.Arms(g,n,road.StreetFamily);
                        n.firstArm=arms[(RoadGeometry.Primary(arms)+1)%arms.Count].end.port;
                    },junctions);
                }
            }
            if(part==RoadPart.Approach)
            {
                RoadEnd End(RoadGraph g,RoadHit h){var s=g.Span(h.id);return s.start.port==h.arm?s.start:s.end;}
                Field("Change crosswalks",(g,h)=>End(g,h).crosswalk,v=>EditorGUILayout.Toggle("Crosswalk",v),(g,h,v)=>End(g,h).crosswalk=v);
                Field("Change stop lines",(g,h)=>End(g,h).stopLine,v=>EditorGUILayout.Toggle("Stop line",v),(g,h,v)=>End(g,h).stopLine=v);
                foreach(var group in hits.GroupBy(h=>graph.Span(h.id).Style(road.StreetFamily).markingSet))
                {
                    var set=group.Key;if(!set || !set.atlas)continue;
                    var subset=group.ToArray();
                    Field("Change crosswalk style",(g,h)=>End(g,h).crosswalkStyle,v=>MarkingPicker("Crosswalk style",set,v,true),
                        (g,h,v)=>End(g,h).crosswalkStyle=v,subset.Where(h=>End(graph,h).crosswalk).ToArray());
                    Field("Change lane marking",(g,h)=>End(g,h).symbol,v=>MarkingPicker("Lane marking",set,v,false),(g,h,v)=>End(g,h).symbol=v,subset);
                }
                if(GUILayout.Button("Select roads"))
                {
                    RoadScene.Pick(default);
                    foreach(int id in hits.Select(h=>h.id).Distinct())RoadScene.Pick(new RoadHit(RoadPart.Road,id),true);
                }
            }
            if(part==RoadPart.Sidewalk || part==RoadPart.Corner)
            {
                RoadCorner Corner(RoadGraph g,RoadHit h,bool create)
                {
                    var list=g.Node(h.id).corners;
                    var c=list.Find(i=>i.arm==h.arm && i.nextArm==h.next);
                    if(c==null){c=new RoadCorner{arm=h.arm,nextArm=h.next};if(create)list.Add(c);}
                    return c;
                }
                RoadSide Side(RoadGraph g,RoadHit h)=>h.left?g.Span(h.id).left:g.Span(h.id).right;
                bool corner=part==RoadPart.Corner;
                if(corner)Field("Change corners",(g,h)=>Corner(g,h,false).sidewalk,v=>EditorGUILayout.Toggle("Sidewalk",v),(g,h,v)=>Corner(g,h,true).sidewalk=v);
                var eligible=hits.Where(h=>!corner || Corner(graph,h,false).sidewalk).ToArray();
                Field("Change pavement finish",(g,h)=>corner?Corner(g,h,false).material:Side(g,h).material,v=>Asset("Sidewalk material",v),
                    (g,h,v)=>{if(corner)Corner(g,h,true).material=v;else Side(g,h).material=v;},eligible);
                RoadCurb Curb(RoadGraph g,RoadHit h,bool create)=>corner?Corner(g,h,create).curb:Side(g,h).curb;
                Field("Change curbs",(g,h)=>Curb(g,h,false).enabled,v=>(RoadOverride)EditorGUILayout.EnumPopup("Curbs",v),(g,h,v)=>Curb(g,h,true).enabled=v,eligible);
                if(eligible.Length>0)
                {
                    RoadFamily Family(RoadHit h)=>corner?RoadGeometry.Arms(graph,graph.Node(h.id),road.StreetFamily).First(a=>a.end.port==h.arm).family:graph.Span(h.id).Style(road.StreetFamily);
                    RoadCurb Parent(RoadHit h)=>corner?RoadGeometry.Arms(graph,graph.Node(h.id),road.StreetFamily).First(a=>a.end.port==h.arm).Side(true).curb:null;
                    eligible=eligible.Where(h=>{
                        var enabled=Curb(graph,h,false).enabled;
                        if(enabled==RoadOverride.Inherit)enabled=Parent(h)?.enabled??RoadOverride.Inherit;
                        return enabled==RoadOverride.On || enabled==RoadOverride.Inherit && Family(h).curbs;
                    }).ToArray();
                    if(eligible.Length==0)return;
                    var f=Family(eligible[0]);var parent=Parent(eligible[0]);
                    Field("Change curb width",(g,h)=>Curb(g,h,false).width,v=>Width("Curb width (m)",v,parent!=null && parent.width>0 ? parent.width : f.curbWidth,.01f),(g,h,v)=>Curb(g,h,true).width=v,eligible);
                    Field("Change curb height",(g,h)=>Curb(g,h,false).height,v=>Inherited("Curb height (m)",v,parent!=null && parent.height>=0 ? parent.height : f.curbHeight>=0?f.curbHeight:f.sidewalkHeight),(g,h,v)=>Curb(g,h,true).height=v,eligible);
                    Field("Change curb material",(g,h)=>Curb(g,h,false).material,v=>Asset("Curb material",v),(g,h,v)=>Curb(g,h,true).material=v,eligible);
                }
            }
            bool removable=part!=RoadPart.Sidewalk && part!=RoadPart.Corner && part!=RoadPart.Approach;
            if(removable)
            {
                bool points=part==RoadPart.Point || part==RoadPart.PathPoint;
                int Degree(RoadHit h)=>graph.spans.Count(s=>s.start.node==h.id || s.end.node==h.id);
                string action=points && hits.All(h=>Degree(h)==2) ? "Dissolve points" : "Delete selection";
                if ((!points || hits.All(h=>Degree(h)<=2)) && GUILayout.Button(action)) RoadScene.Delete();
            }
            if(!string.IsNullOrEmpty(RoadScene.Message))EditorGUILayout.HelpBox(RoadScene.Message,MessageType.Info);
        }
    }
}
