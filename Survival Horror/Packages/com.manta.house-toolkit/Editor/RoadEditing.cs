using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class RoadEditing
    {
        internal static bool Change(RoadNetwork network, string action, Action<RoadGraph> apply, out string issue)
        {
            // Only publish a valid graph. A rejected drag, overlapping span or failed
            // dissolve leaves the scene and Undo stack untouched.
            var graph=JsonUtility.FromJson<RoadGraph>(JsonUtility.ToJson(network.graph));
            try
            {
                apply(graph);
                Normalize(graph,network.family);
            }
            catch (InvalidOperationException error) { issue=error.Message; return false; }
            Undo.RegisterCompleteObjectUndo(network,action);
            network.graph=graph;
            PrefabUtility.RecordPrefabInstancePropertyModifications(network);
            EditorUtility.SetDirty(network);
            network.Rebuild();
            issue=null;
            return true;
        }

        internal static RoadSpan Extend(RoadGraph graph,int a,int b,int run,RoadFamily family)
        {
            var incident=graph.spans.Where(s=>s.start.node==a || s.end.node==a).ToList();
            var continuation=run==0
                ? (incident.Count==1 && incident[0].family==family ? incident[0] : null)
                : incident.Find(s=>s.run==run);
            if(continuation==null)return graph.Connect(a,b,run,family);
            return continuation.end.node==a ? graph.Connect(a,b,continuation.run,family) :
                graph.Connect(b,a,continuation.run,family);
        }

        internal static RoadNode Point(RoadGraph graph,Vector3 position)
        {
            var existing=graph.nodes.Find(n=>(n.position-position).sqrMagnitude<.000001f);
            return existing??graph.AddNode(position);
        }

        internal static void Split(RoadGraph graph,RoadSpan span,RoadNode node)
        {
            if(node.id==span.start.node || node.id==span.end.node)return;
            var tail=JsonUtility.FromJson<RoadSpan>(JsonUtility.ToJson(span));
            tail.id=graph.nextId++;
            tail.start=graph.End(node.id);
            span.end=graph.End(node.id);
            graph.spans.Add(tail);
        }

        internal static void Remove(RoadGraph graph,RoadHit hit)
        {
            if(hit.part==RoadPart.Road || hit.part==RoadPart.Approach)
            {
                graph.spans.RemoveAll(s=>s.id==hit.id);
                return;
            }
            if(hit.part!=RoadPart.Point)throw new InvalidOperationException("Select a road or a point to delete.");
            var incident=graph.spans.Where(s=>s.start.node==hit.id || s.end.node==hit.id).ToList();
            if(incident.Count>2)throw new InvalidOperationException("Delete a connecting road first to keep the junction intact.");
            if(incident.Count==2)
            {
                var keep=incident[0]; var drop=incident[1];
                var far=drop.start.node==hit.id ? drop.end : drop.start;
                if(keep.start.node==hit.id)keep.start=far; else keep.end=far;
                graph.spans.Remove(drop);
            }
            else if(incident.Count==1)graph.spans.Remove(incident[0]);
            graph.nodes.RemoveAll(n=>n.id==hit.id);
        }

        internal static void Move(RoadGraph graph,int id,Vector3 position)
        {
            var existing=graph.nodes.Find(n=>n.id!=id && (n.position-position).sqrMagnitude<.000001f);
            if(existing==null) {graph.Node(id).position=position;return;}
            foreach(var span in graph.spans)
            {
                if(span.start.node==id) span.start.node=existing.id;
                if(span.end.node==id) span.end.node=existing.id;
            }
            graph.nodes.RemoveAll(n=>n.id==id);
        }

        static void Normalize(RoadGraph graph,RoadFamily family)
        {
            var spans=graph.spans.ToArray();
            var nodes=graph.nodes.ToArray();
            var vertex=nodes.Select((n,i)=>(n.id,i)).ToDictionary(p=>p.id,p=>p.i);
            var positions=nodes.Select(n=>RoadGeometry.Flat(n.position)).ToArray();
            var lines=spans.Select(s=>new Vector2Int(vertex[s.start.node],vertex[s.end.node])).ToArray();
            if(lines.Any(l=>(positions[l.x]-positions[l.y]).sqrMagnitude<1e-6f))
                throw new InvalidOperationException("Road endpoints are too close.");
            var split=PlanarNoding.Split(positions,lines,true);
            var incidence=new Dictionary<int,RoadNode>();
            for(int i=0;i<nodes.Length;i++)
            {
                if(incidence.ContainsKey(split.OriginalVertices[i]))
                    throw new InvalidOperationException("Two road points occupy the same plan position.");
                incidence.Add(split.OriginalVertices[i],nodes[i]);
            }
            RoadNode Resolve(int index,Vector3 a,Vector3 b)
            {
                var p=split.Vertices[index]; var direction=RoadGeometry.Flat(b-a);
                float t=Vector2.Dot(p-RoadGeometry.Flat(a),direction)/direction.sqrMagnitude;
                float y=Mathf.Lerp(a.y,b.y,t);
                if(!incidence.TryGetValue(index,out var n))
                    incidence.Add(index,n=graph.AddNode(new Vector3(p.x,y,p.y)));
                if(Mathf.Abs(n.position.y-y)>.025f)
                    throw new InvalidOperationException("Connected roads must meet at the same elevation.");
                return n;
            }
            var unique=new HashSet<(int,int)>();
            foreach(var group in split.Pieces.GroupBy(p=>p.Source))
            {
                var span=spans[group.Key];
                var a=graph.Node(span.start.node).position; var b=graph.Node(span.end.node).position;
                foreach(var piece in group)
                {
                    int from=piece.Start,to=piece.End;
                    if(!unique.Add((Math.Min(from,to),Math.Max(from,to))))
                        throw new InvalidOperationException("A road already occupies this stretch.");
                    Resolve(from,a,b);
                    var node=Resolve(to,a,b);
                    if(node.id==span.end.node)continue;
                    Split(graph,span,node);
                    span=graph.spans[graph.spans.Count-1];
                }
            }
            var ports=graph.spans.SelectMany(s=>new[]{s.start,s.end}).ToLookup(e=>e.node,e=>e.port);
            graph.nodes.RemoveAll(n=>!ports.Contains(n.id));
            foreach(var node in graph.nodes)
            {
                int count=ports[node.id].Count();
                if(count>4)throw new InvalidOperationException("A road junction supports up to four approaches.");
                if(count<3){node.corners.Clear();node.firstArm=0;continue;}
                var arms=RoadGeometry.Arms(graph,node,family);
                if(!ports[node.id].Contains(node.firstArm))node.firstArm=arms[RoadGeometry.Primary(arms)].end.port;
                node.corners.RemoveAll(c=>!ports[node.id].Contains(c.arm) || !ports[node.id].Contains(c.nextArm));
            }
        }

        internal static void RebuildLoaded(RoadFamily family = null)
        {
            foreach(var road in UnityEngine.Object.FindObjectsByType<RoadNetwork>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if(!family || road.family==family || road.graph.spans.Any(s=>s.family==family)) road.Rebuild();
        }
    }
}
