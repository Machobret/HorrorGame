using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // All pointer edits use a detached plan. No native scene objects change until validation succeeds.
    public sealed class FloorEdit
    {
        sealed class Edge
        {
            public HouseWall source;
            public WallFamily familyOverride;
            public int a,b;
            public List<PlacedOpening> openings = new List<PlacedOpening>();
            public bool interior;
        }

        readonly HouseFloor floor;
        readonly Dictionary<int,Vector2> points;
        readonly List<Edge> edges = new List<Edge>();
        readonly List<HouseWall> originals;
        readonly List<int[]> rooms = new List<int[]>();
        readonly Dictionary<int,int> insertionHints = new Dictionary<int,int>();
        int next;
        List<Edge> preparedEdges;
        public FloorEdit(HouseFloor floor)
        {
            this.floor = floor;
            points = floor.corners.ToDictionary(c=>c.id,c=>c.point);
            next = Mathf.Max(floor.nextCorner, points.Keys.DefaultIfEmpty().Max()+1);
            originals = floor.GetComponentsInChildren<HouseWall>(true).Where(w=>w.Floor==floor).ToList();
            foreach (var wall in originals)
            {
                int a = wall.startCorner, b = wall.endCorner;
                if (!points.ContainsKey(a) || !points.ContainsKey(b))
                {
                    a = Corner(floor.Local(wall.transform.position));
                    b = Corner(floor.Local(wall.transform.TransformPoint(Vector3.right * wall.length)));
                }
                edges.Add(new Edge {a=a,b=b,source=wall,familyOverride=wall.familyOverride,openings=wall.openings.Select(Clone).ToList()});
            }
        }
        static PlacedOpening Clone(PlacedOpening o) => new PlacedOpening {id=o.id,insert=o.insert,distance=o.distance,sill=o.sill,reversed=o.reversed};
        int Corner(Vector2 p)
        {
            p=PlanarNoding.AtPrecision(p);
            foreach (var pair in points) if ((pair.Value-p).sqrMagnitude < 0.00000001f) return pair.Key;
            int id=next++; points.Add(id,p); return id;
        }
        public void Draw(Vector2 start, Vector2 end) => Draw(start, end, null);

        public void Draw(Vector2 start, Vector2 end, WallFamily familyOverride)
        {
            preparedEdges = null;
            edges.Add(new Edge { a = Corner(start), b = Corner(end), familyOverride = familyOverride });
        }
        public void Split(HouseWall wall, Vector2 at)
        {
            var e=edges.Find(x=>x.source==wall);
            if (e == null) return;
            preparedEdges = null;
            Vector2 delta=points[e.b]-points[e.a];
            var p=points[e.a]+delta*Mathf.Clamp01(Vector2.Dot(at-points[e.a],delta)/delta.sqrMagnitude);
            insertionHints[Corner(p)]=edges.IndexOf(e);
        }
        public void Remove(HouseWall wall)
        {
            preparedEdges = null;
            edges.RemoveAll(e=>e.source==wall);
        }
        public void Move(IEnumerable<int> ids, Vector2 offset)
        {
            preparedEdges = null;
            var moved = new HashSet<int>(ids);
            var old = edges.Select(e=>Vector2.Distance(points[e.a],points[e.b])).ToArray();
            foreach (int id in moved) points[id]=PlanarNoding.AtPrecision(points[id]+offset);
            for (int i=0;i<edges.Count;i++)
            {
                var e=edges[i];
                if (moved.Contains(e.a) && !moved.Contains(e.b))
                    foreach(var o in e.openings) o.distance += Vector2.Distance(points[e.a],points[e.b])-old[i];
            }
        }
        const string JunctionRequired = "Dissolve requires exactly two walls at the endpoint.";
        static readonly List<HouseWall> floorWalls = new List<HouseWall>();

        internal static string DissolveIssue(HouseFloor floor, int endpoint)
        {
            int count = 0;
            floor.GetComponentsInChildren(true, floorWalls);
            foreach (var wall in floorWalls)
            {
                if ((wall.startCorner == endpoint || wall.endCorner == endpoint) && wall.Floor == floor && ++count > 2) break;
            }
            floorWalls.Clear();
            return count == 2 ? null : JunctionRequired;
        }

        public bool Dissolve(int id, out string reason) => Dissolve(id, null, out _, out reason);

        internal bool Dissolve(int id, HouseWall preferred, out HouseWall merged, out string reason)
        {
            merged = null;
            var adjacent = edges.Where(edge => edge.a == id || edge.b == id).ToArray();
            if (adjacent.Length != 2) { reason = JunctionRequired; return false; }

            var keep = adjacent[0];
            var remove = adjacent[1];
            if (preferred && remove.source == preferred) (keep, remove) = (remove, keep);

            // Orient the new chord in the retained wall's direction. Measure each opening
            // along the original two legs so their order is preserved even at acute bends.
            bool keepFirst = keep.b == id;
            var first = keepFirst ? keep : remove;
            var second = keepFirst ? remove : keep;
            int start = first.a == id ? first.b : first.a;
            int end = second.a == id ? second.b : second.a;
            float firstLength = Vector2.Distance(points[start], points[id]);
            float secondLength = Vector2.Distance(points[id], points[end]);
            float length = Vector2.Distance(points[start], points[end]);
            if (length < .001f) { reason = "The outer endpoints are too close to form a wall."; return false; }
            float scale = length / (firstLength + secondLength);
            void Remap(Edge leg, int from, float offset, float legLength)
            {
                bool backwards = leg.a != from;
                foreach (var opening in leg.openings)
                {
                    float distance = backwards ? legLength - opening.distance : opening.distance;
                    opening.distance = (offset + distance) * scale;
                    opening.reversed ^= backwards;
                }
            }
            preparedEdges = null;
            Remap(first, start, 0, firstLength);
            Remap(second, id, firstLength, secondLength);

            keep.a = start;
            keep.b = end;
            keep.openings.AddRange(remove.openings);
            edges.Remove(remove);
            merged = keep.source;
            reason = null;
            return true;
        }
        List<Vector2Int> IndexedLines(List<int> ids, List<Edge> source)
        {
            var index = new Dictionary<int,int>(ids.Count);
            for (int i = 0; i < ids.Count; i++) index.Add(ids[i],i);
            return source.Select(e => new Vector2Int(index[e.a],index[e.b])).ToList();
        }
        public bool Prepare(out string reason)
        {
            if(preparedEdges != null) {reason=null;return true;}
            reason="A wall must be at least 1 mm long.";
            if(edges.Any(e=>Vector2.Distance(points[e.a],points[e.b])<.001f))return false;
            rooms.Clear();
            var ids=points.Keys.ToList();
            var vertices=ids.Select(i=>points[i]).ToList();
            var lines=IndexedLines(ids,edges);
            // A zero-length helper vertex alone is not an intersection. Split its chosen host explicitly.
            var graph=PlanarNoding.Split(vertices,lines,true);
            var graphCorners=graph.Vertices.Select(Corner).ToArray();
            var pieces=new List<Edge>();
            var unique=new HashSet<(int,int)>();
            foreach(var group in graph.Pieces.GroupBy(p=>p.Source))
            {
                var origin=edges[group.Key];
                var axis=(points[origin.b]-points[origin.a]).normalized;
                var cuts=group.SelectMany(p=>new[]{graphCorners[p.Start],graphCorners[p.End]})
                    .Concat(insertionHints.Where(h=>h.Value==group.Key).Select(h=>h.Key)).Distinct()
                    .OrderBy(id=>Vector2.Dot(points[id]-points[origin.a],axis)).ToList();
                for(int i=1;i<cuts.Count;i++)
                {
                    int a=cuts[i-1],b=cuts[i];
                    if(a==b||Vector2.Distance(points[a],points[b])<.001f){reason="That edit creates a junction smaller than 1 mm.";return false;}
                    if(!unique.Add((Math.Min(a,b),Math.Max(a,b)))) {reason="A wall already occupies that span.";return false;}
                    float from=Vector2.Dot(points[a]-points[origin.a],axis), to=Vector2.Dot(points[b]-points[origin.a],axis);
                    var piece=new Edge {source=origin.source,familyOverride=origin.familyOverride,a=a,b=b};
                    foreach(var opening in origin.openings.Where(o=>(i==1||o.distance>from) && (i==cuts.Count-1||o.distance<=to)))
                    {
                        var o=Clone(opening); o.distance-=from;piece.openings.Add(o);
                    }
                    pieces.Add(piece);
                }
            }
            int[] use=new int[pieces.Count];
            if (!floor.WallsOnly)
            {
                ids=pieces.SelectMany(e=>new[]{e.a,e.b}).Distinct().ToList();
                vertices=ids.Select(i=>points[i]).ToList();
                lines=IndexedLines(ids,pieces);
                foreach(var ring in PlanarFaceWalk.Trace(vertices,lines))
                {
                    var loop=ring.Select(k=>(k%2==0 ? pieces[k/2].a : pieces[k/2].b)).ToArray();
                    if(RoomGeometry.Area(loop.Select(id=>points[id]).ToArray())<=.000001f)continue;
                    if(loop.Distinct().Count()!=loop.Length){reason="This room boundary touches itself.";return false;}
                    var poly=loop.Select(id=>points[id]).ToArray();
                    if(RoomGeometry.Triangles(poly)==null){reason="This room cannot be triangulated.";return false;}
                    if(rooms.Any(other=>RoomGeometry.Nested(poly,other.Select(id=>points[id]).ToArray()))) {reason="Nested room loops need a connecting wall.";return false;}
                    rooms.Add(loop);
                    foreach(int k in ring)use[k/2]++;
                }
            }
            var ends=new List<(int id,CornerLayout.End arm)>();
            for(int i=0;i<pieces.Count;i++)
            {
                var e=pieces[i]; e.interior=use[i]==2 || (use[i]==0 && rooms.Any(r=>RoomGeometry.Inside((points[e.a]+points[e.b])*.5f,r.Select(id=>points[id]).ToArray())));
                if(e.source && e.source.boundaryOnly){if(e.openings.Count==0) continue;reason="Remove openings before hiding the wall.";return false;}
                var family=e.familyOverride ? e.familyOverride : floor.FamilyFor(e.interior);
                float depth=HouseWall.ResolveThickness(family,floor,e.interior,e.source ? e.source.thicknessOverride : 0);
                if(!HouseWall.Validate(family,Vector2.Distance(points[e.a],points[e.b]),floor.height,e.openings,new List<FittedOpening>(),out reason,resolvedThickness:depth))return false;
                var delta=points[e.b]-points[e.a];
                ends.Add((e.a,new CornerLayout.End(family,delta,e.b,e.openings,true,depth)));
                ends.Add((e.b,new CornerLayout.End(family,-delta,e.a,e.openings,false,depth)));
            }
            foreach(var junction in ends.GroupBy(end=>end.id))
                if (!CornerLayout.Solve(junction.Select(end=>end.arm).ToArray(),out _,out _,out reason)) return false;
            preparedEdges=pieces;reason=null;return true;
        }
        public bool Commit(string action,out string reason)
        {
            if(!Prepare(out reason))return false;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(action);
            try
            {
                Undo.RecordObject(floor,action);
                floor.corners=preparedEdges.SelectMany(e=>new[]{e.a,e.b}).Distinct().Select(i=>new FloorCorner(i,points[i])).ToList();
                floor.nextCorner=next;
                var kept=new HashSet<HouseWall>();
                var outputs=new List<HouseWall>();
                var bindings=originals.SelectMany(w=>w.GetComponentsInChildren<InsertAttachment>(true)).ToLookup(a=>a.openingId);
                foreach(var e in preparedEdges)
                {
                    var w=e.source;
                    bool fresh=!w || kept.Contains(w);
                    if(fresh)
                    {
                        var go=new GameObject(w ? w.name+" segment" : "Wall");
                        go.transform.SetParent(floor.transform,false);
                        w=go.AddComponent<HouseWall>();
                        w.familyOverride = e.familyOverride;
                        if(e.source){w.thicknessOverride=e.source.thicknessOverride;w.panelLayoutOverride=e.source.panelLayoutOverride; w.positiveFinish=e.source.positiveFinish;w.negativeFinish=e.source.negativeFinish;w.surfaceOverride=e.source.surfaceOverride;w.glassOverride=e.source.glassOverride;
                            w.boundaryOnly=e.source.boundaryOnly;w.trimOverride=e.source.trimOverride;w.suppressTrim=e.source.suppressTrim;}
                        Undo.RegisterCreatedObjectUndo(go,action);
                    }
                    kept.Add(w);
                    var delta=points[e.b]-points[e.a];
                    var pos=new Vector3(points[e.a].x,0,points[e.a].y);
                    var rot=Quaternion.Euler(0,-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,0);
                    bool changed=fresh || w.startCorner!=e.a || w.endCorner!=e.b || w.interior!=e.interior ||
                        (w.transform.localPosition-pos).sqrMagnitude>1e-10f || Quaternion.Angle(w.transform.localRotation,rot)>.001f ||
                        Mathf.Abs(w.length-delta.magnitude)>.00005f || w.openings.Count!=e.openings.Count ||
                        !w.openings.Zip(e.openings,(a,b)=>a.id==b.id && a.reversed==b.reversed && Mathf.Abs(a.distance-b.distance)<.00005f).All(x=>x);
                    if(!changed)continue;
                    Undo.RecordObject(w,action);
                    Undo.RecordObject(w.transform,action);
                    w.startCorner=e.a;w.endCorner=e.b;w.interior=e.interior;w.length=delta.magnitude;w.openings=e.openings;
                    w.transform.SetLocalPositionAndRotation(pos,rot);
                    // Record the pose delta before separate Undo operations move children and replace geometry.
                    Undo.FlushUndoRecordObjects();
                    foreach(var o in w.openings)
                        foreach(var attachment in bindings[o.id])
                            if(attachment.transform.parent!=w.transform) Undo.SetTransformParent(attachment.transform,w.transform,action);
                    outputs.Add(w);
                }
                foreach(var w in originals.Where(w=>!kept.Contains(w)))Undo.DestroyObjectImmediate(w.gameObject);
                foreach(var w in outputs)if(!WallBuilder.Rebuild(w,out reason))throw new InvalidOperationException(reason);
                if (!WallJunctions.Refresh(floor,out reason)) throw new InvalidOperationException(reason);
                if (!floor.WallsOnly) RoomGeometry.Publish(floor,rooms,points,action);
                foreach (var wall in kept) WallBuilder.ApplyMaterials(wall);
                EditorUtility.SetDirty(floor);
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
                return true;
            }
            catch(Exception ex){Undo.RevertAllDownToGroup(group);reason=ex.Message;Debug.LogException(ex);return false;}
        }
    }
}
