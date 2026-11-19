using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class GroundEditing
    {
        internal static bool Change(ProceduralSurface road, string label, Action<GroundPlan> edit, out string issue,
            Action<FillerBuilding> updateFacade = null)
        {
            if (!road || road is ProceduralPath) { issue="Select a ground area."; return false; }
            var plan = JsonUtility.FromJson<GroundPlan>(JsonUtility.ToJson(road.Areas));
            try
            {
                edit(plan);
                Rebuild(plan);
                if(road is FillerBuilding && !SinglePerimeter(plan))
                    throw new InvalidOperationException("Draw one connected perimeter around the building.");
            }
            catch (InvalidOperationException e) { issue = e.Message; return false; }
            Undo.RegisterCompleteObjectUndo(road, label);
            bool firstPerimeter=road.Areas.faces.Count==0 && plan.faces.Count==1;
            road.Areas = plan;
            if(road is FillerBuilding building)
            {
                var ids=plan.edges.Select(e=>e.id).ToHashSet();
                building.finishes.RemoveAll(f=>f.wall!=0 && !ids.Contains(f.wall));
                building.entrances.RemoveAll(e=>!ids.Contains(e.wall));
                updateFacade?.Invoke(building);
                foreach(var fixture in building.GetComponentsInChildren<FixtureAttachment>(true))
                    if(fixture.transform.parent==building.transform && fixture.facadeEdge!=0 && !ids.Contains(fixture.facadeEdge))
                        Undo.DestroyObjectImmediate(fixture.gameObject);
                if(firstPerimeter)building.PlaceInitialEntrance();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(road);
            EditorUtility.SetDirty(road);
            road.Rebuild();
            issue = null;
            return true;
        }

        static bool SinglePerimeter(GroundPlan plan)
        {
            if(plan.edges.Count==0)return true;
            var neighbours=new Dictionary<Vector2,List<Vector2>>();
            void Join(Vector2 a,Vector2 b)
            {
                if(!neighbours.TryGetValue(a,out var list))neighbours[a]=list=new List<Vector2>();
                list.Add(b);
            }
            foreach(var e in plan.edges){Join(e.a,e.b);Join(e.b,e.a);}
            if(neighbours.Values.Any(n=>n.Count>2) || plan.faces.Count>1)return false;
            var visited=new HashSet<Vector2>();
            var pending=new Stack<Vector2>();pending.Push(plan.edges[0].a);
            while(pending.Count>0)
            {
                var p=pending.Pop();
                if(visited.Add(p))foreach(var n in neighbours[p])pending.Push(n);
            }
            return visited.Count==neighbours.Count;
        }

        internal static void Line(GroundPlan plan, Vector2 a, Vector2 b)
        {
            if ((a-b).sqrMagnitude < 1e-8f) throw new InvalidOperationException("Choose a different endpoint.");
            plan.edges.Add(new GroundEdge { id = plan.nextId++, a = a, b = b });
        }

        internal static bool InsertPoint(ProceduralSurface surface, int id, Vector2 position, out Vector2 point, out string issue)
        {
            var edge=surface.Areas.edges.Find(e=>e.id==id);
            point=edge==null ? position : PlanarNoding.AtPrecision(PlanSnapping.Project(position,edge.a,edge.b));
            if(edge==null || point==edge.a || point==edge.b)
            {
                issue="Choose a point inside the boundary edge.";
                return false;
            }
            var split=point;
            int newId=surface.Areas.nextId;
            var face=surface.Areas.faces.FirstOrDefault(f=>Borders(edge,f));
            int index=face==null ? -1 : Array.IndexOf(face.polygon,edge.a);
            bool forward=index>=0 && face.polygon[(index+1)%face.polygon.Length]==edge.b;
            return Change(surface,"Insert boundary point",plan=>{
                plan.edges.Find(e=>e.id==id).b=split;
                Line(plan,split,edge.b);
            },out issue,building=>{
                float splitDistance=Vector2.Distance(edge.a,split);
                foreach(var fixture in building.GetComponentsInChildren<FixtureAttachment>(true))
                {
                    if(fixture.transform.parent!=building.transform || fixture.facadeEdge!=id || fixture.edgeDistance<=splitDistance)continue;
                    Undo.RecordObject(fixture,"Insert boundary point");
                    fixture.facadeEdge=newId;
                    fixture.edgeDistance-=splitDistance;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
                }
                foreach(var finish in building.finishes.Where(f=>f.wall==id).ToArray())
                    if(finish.floor==0 && finish.groundStyle!=FillerBuilding.GroundStyle.Inherit)
                        building.SetGroundStyle(newId,finish.groundStyle,finish.material);
                    else building.SetFinish(newId,finish.floor,finish.material);
                int bay=building.EntranceBay(id);
                if(bay<0)return;
                // The rendered bay order follows the face perimeter, including reversed edges.
                int bays=building.Bays(edge);
                float along=(Mathf.Min(bay,bays-1)+.5f)/bays;
                if(!forward)along=1-along;
                float cut=Vector2.Distance(edge.a,split)/Vector2.Distance(edge.a,edge.b);
                bool first=along<cut;
                var host=building.footprint.edges.Find(e=>e.id==(first?id:newId));
                float fraction=first ? along/cut : (along-cut)/(1-cut);
                int count=building.Bays(host);
                building.SetEntrance(id,-1);
                building.SetEntrance(host.id,Mathf.Clamp(Mathf.FloorToInt((forward?fraction:1-fraction)*count),0,count-1));
            });
        }

        internal static void Move(GroundPlan plan, HashSet<Vector2> points, Vector2 delta)
        {
            Vector2 Moved(Vector2 point) => points.Contains(point) ? point+delta : point;
            foreach(var edge in plan.edges)
            {
                edge.a=Moved(edge.a);
                edge.b=Moved(edge.b);
                if((edge.a-edge.b).sqrMagnitude<1e-8f)
                    throw new InvalidOperationException("Keep the boundary endpoints apart.");
            }
            // Rebuild matches face finishes using their polygons in the edited layout.
            foreach(var face in plan.faces)
                for(int i=0;i<face.polygon.Length;i++) face.polygon[i]=Moved(face.polygon[i]);
        }

        internal static void RemovePoints(GroundPlan plan,IEnumerable<Vector2> points)
        {
            foreach(var point in points)
            {
                var edges=plan.edges.Where(e=>e.a==point || e.b==point).ToArray();
                if(edges.Length>2)throw new InvalidOperationException("Keep shared junctions or remove a boundary first.");
                foreach(var edge in edges)plan.edges.Remove(edge);
                if(edges.Length==2)
                {
                    Vector2 Other(GroundEdge e)=>e.a==point ? e.b : e.a;
                    Line(plan,Other(edges[0]),Other(edges[1]));
                }
            }
        }

        internal static bool Borders(GroundEdge edge, GroundFace face)
        {
            for (int i = 0; i < face.polygon.Length; i++)
            {
                var a = face.polygon[i];
                var b = face.polygon[(i+1) % face.polygon.Length];
                if (a == edge.a && b == edge.b || a == edge.b && b == edge.a) return true;
            }
            return false;
        }

        internal static void RemoveFace(GroundPlan plan, int id)
        {
            var face = plan.faces.Find(f => f.id == id);
            if (face == null) return;
            int removed = plan.edges.RemoveAll(e => Borders(e, face) && !plan.faces.Any(f => f != face && Borders(e,f)));
            if (removed == 0) throw new InvalidOperationException("Delete a boundary to merge this enclosed face with its neighbour.");
        }

        static void Rebuild(GroundPlan plan)
        {
            var points = plan.edges.SelectMany(e => new[]{e.a,e.b}).ToArray();
            var lines = Enumerable.Range(0, plan.edges.Count).Select(i => new Vector2Int(i*2,i*2+1)).ToArray();
            var split = PlanarNoding.Split(points,lines,true);
            var seen = new HashSet<(int,int)>();
            var usedIds = new HashSet<int>();
            var edges = new List<GroundEdge>();
            var indices = new List<Vector2Int>();
            foreach (var piece in split.Pieces)
            {
                int a = piece.Start, b = piece.End;
                if (!seen.Add((Math.Min(a,b),Math.Max(a,b)))) continue;
                int id = plan.edges[piece.Source].id;
                if (!usedIds.Add(id)) id = plan.nextId++;
                edges.Add(new GroundEdge { id=id, a=split.Vertices[a], b=split.Vertices[b] });
                indices.Add(new Vector2Int(a,b));
            }
            var faces = new List<GroundFace>();
            foreach (var walk in PlanarFaceWalk.Trace(split.Vertices, indices))
            {
                var polygon = walk.Select(i => split.Vertices[i%2==0 ? indices[i/2].x : indices[i/2].y]).ToArray();
                if (RoomGeometry.Area(polygon) <= 1e-6f) continue;
                // A disconnected loop inside another would create overlapping filled faces.
                if (faces.Any(f => RoomGeometry.Nested(polygon,f.polygon)))
                    throw new InvalidOperationException("Connect the inner perimeter to the outer boundary before subdividing.");
                var triangles = RoomGeometry.Triangles(polygon);
                if (triangles == null || triangles.Length == 0)
                    throw new InvalidOperationException("This perimeter cannot be filled. Check its boundary.");
                var sample = (polygon[triangles[0]] + polygon[triangles[1]] + polygon[triangles[2]]) / 3;
                var parent = plan.faces.Find(f => RoomGeometry.Inside(sample,f.polygon));
                faces.Add(new GroundFace { id = parent != null && usedIds.Add(parent.id) ? parent.id : plan.nextId++,
                    areaId=parent?.areaId??0, areaMaterial=parent?.areaMaterial, material=parent?.material, polygon=polygon, triangles=triangles });
            }
            // A paved area is the set of faces connected across full noded edges.
            // Meeting at a single corner keeps two independent surface defaults.
            var neighbours=faces.ToDictionary(f=>f,f=>new List<GroundFace>());
            var owners=new Dictionary<(Vector2,Vector2),GroundFace>();
            foreach(var face in faces)
                for(int i=0;i<face.polygon.Length;i++)
                {
                    var a=face.polygon[i];var b=face.polygon[(i+1)%face.polygon.Length];
                    if(owners.TryGetValue((b,a),out var other))
                    { neighbours[other].Add(face);neighbours[face].Add(other); }
                    owners[(a,b)]=face;
                }
            var visited=new HashSet<GroundFace>();
            var assigned=new HashSet<int>();
            foreach(var seed in faces)
            {
                if(!visited.Add(seed))continue;
                var group=new List<GroundFace>{seed};
                for(int i=0;i<group.Count;i++)
                    foreach(var next in neighbours[group[i]])
                        if(visited.Add(next))group.Add(next);
                var inherited=group.Where(f=>f.areaId!=0).OrderBy(f=>f.areaId).FirstOrDefault();
                int area=inherited!=null && assigned.Add(inherited.areaId) ? inherited.areaId : plan.nextId++;
                var finish=inherited?.areaMaterial;
                foreach(var face in group)
                {
                    if(face.areaId!=0 && face.areaMaterial && face.areaMaterial!=finish && !face.material)
                        face.material=face.areaMaterial;
                    face.areaId=area;face.areaMaterial=finish;
                }
            }
            plan.edges = edges;
            plan.faces = faces;
        }
    }
}
