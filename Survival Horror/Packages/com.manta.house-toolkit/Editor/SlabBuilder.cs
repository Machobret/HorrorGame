using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class SlabBuilder
    {
        sealed class Region
        {
            internal Vector2[] loop;
            internal Material finish;
        }

        internal static void Rebuild(House house)
        {
            if(!house) return;
            house.InvalidateProjection();
            if (house.wallsOnly) return;
            var floors=HouseEditing.Floors(house);
            for(int i=0;i<floors.Length;i++)
            {
                var floor=floors[i];
                foreach(var room in floor.GetComponentsInChildren<HouseRoom>(true))
                {
                    // Earlier scenes stored generated floor geometry on the room itself.
                    var filter=room.GetComponent<MeshFilter>();
                    if(!filter)continue;
                    var collider=room.GetComponent<MeshCollider>();if(collider)Undo.DestroyObjectImmediate(collider);
                    var renderer=room.GetComponent<MeshRenderer>();if(renderer)Undo.DestroyObjectImmediate(renderer);
                    Undo.DestroyObjectImmediate(filter);
                }
                JoinWalls(floor,i+1<floors.Length?floors[i+1]:null);
                Build(floor,i>0?floors[i-1]:null,floor,false);
                var roof=floor.GetComponentsInChildren<HouseSlab>(true).FirstOrDefault(s=>s.overhead);
                if(i==floors.Length-1) Build(floor,floor,null,true);
                else if(roof) Undo.DestroyObjectImmediate(roof.gameObject);
            }
            if (house.booleanCuts.Count > 0)
            {
                foreach (var slab in house.GetComponentsInChildren<HouseSlab>(true)) BooleanOutput.Apply(house, slab);
                foreach (var joint in house.GetComponentsInChildren<CornerJoint>(true)) BooleanOutput.Apply(house, joint);
            }
            if(HouseScene.House==house) HouseScene.ApplyCutaway();
        }

        static void JoinWalls(HouseFloor floor,HouseFloor upper)
        {
            var loops=floor.GetComponentsInChildren<HouseRoom>(true).Where(r=>r.HasCeiling(floor)).Select(r=>r.polygon).ToList();
            if(upper)loops.AddRange(upper.GetComponentsInChildren<HouseRoom>(true).Select(r=>r.polygon.Select(p=>floor.Local(upper.World(p))).ToArray()));
            var coveredCorners=new HashSet<int>();
            foreach(var w in floor.GetComponentsInChildren<HouseWall>(true))
            {
                var spans=new List<Vector2>();
                if(!w.boundaryOnly && loops.Count>0)
                {
                    var a=floor.Position(w.startCorner);var b=floor.Position(w.endCorner);
                    var dir=(b-a).normalized; var side=new Vector2(-dir.y,dir.x)*.0001f;
                    var p=new List<Vector2>{a,b};var edges=new List<Vector2Int>{new Vector2Int(0,1)};
                    foreach(var loop in loops)
                    {
                        int n=p.Count;p.AddRange(loop);
                        for(int k=0;k<loop.Length;k++)edges.Add(new Vector2Int(n+k,n+(k+1)%loop.Length));
                    }
                    var graph=PlanarNoding.Split(p,edges);
                    foreach(var piece in graph.Pieces.Where(e=>e.Source==0))
                    {
                        var u=graph.Vertices[piece.Start];var v=graph.Vertices[piece.End];var mid=(u+v)*.5f;
                        if(!loops.Any(loop=>RoomGeometry.Inside(mid+side,loop)||RoomGeometry.Inside(mid-side,loop)))continue;
                        float first=Mathf.Max(0,Vector2.Dot(u-a,dir)),last=Mathf.Min(w.length,Vector2.Dot(v-a,dir));
                        if(first<.0002f)coveredCorners.Add(w.startCorner);
                        if(last>w.length-.0002f)coveredCorners.Add(w.endCorner);
                        if(spans.Count>0 && Mathf.Abs(spans[spans.Count-1].y-first)<.0002f)
                            spans[spans.Count-1]=new Vector2(spans[spans.Count-1].x,last);
                        else spans.Add(new Vector2(first,last));
                    }
                }
                if(w.slabSpans!=null && w.slabSpans.SequenceEqual(spans))continue;
                Undo.RecordObject(w,"Join walls across slab");w.slabSpans=spans.ToArray();
                if(!WallBuilder.Rebuild(w,out var reason))throw new System.InvalidOperationException(reason);
            }
            foreach(var joint in floor.GetComponentsInChildren<CornerJoint>(true))
            {
                float height=floor.height+(coveredCorners.Contains(joint.corner)?floor.House.slabThickness:0);
                if(Mathf.Approximately(joint.height,height))continue;
                Undo.RecordObjects(new Object[]{joint,joint.transform},"Join corner across slab");
                if(joint.post)
                {
                    var bounds=joint.post.bounds;
                    joint.transform.localPosition-=Vector3.up*(bounds.min.y*(height-joint.height)/bounds.size.y);
                }
                joint.height=height;joint.Refresh();
            }
        }

        static void Build(HouseFloor owner,HouseFloor below,HouseFloor above,bool roof)
        {
            var house=owner.House;
            var fallback=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.defaultMaterial ??
                AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            Material Default(HouseFloor f,bool top)
            {
                var m=f ? (top?f.floorFinish:f.ceilingFinish) : null;
                if(!m) m=top?house.floorFinish:house.ceilingFinish;
                return m?m:fallback;
            }
            Region[] Regions(HouseFloor f,bool top)
            {
                if(!f)return new Region[0];
                return f.GetComponentsInChildren<HouseRoom>(true).Where(r=>r.polygon!=null && r.polygon.Length>=3 && (top || r.HasCeiling(f)))
                    .Select(r=>new Region {loop=r.polygon.Select(p=>PlanarNoding.AtPrecision(owner.Local(f.World(p)))).ToArray(),
                        finish=(top?r.floorFinish:r.ceilingFinish) ? (top?r.floorFinish:r.ceilingFinish) : Default(f,top)}).ToArray();
            }
            var upper=Regions(above,true);var lower=Regions(below,false);
            var cuts=(roof?owner.roofOpenings:owner.floorOpenings).Select(o=>o.points).ToArray();
            var loops=upper.Concat(lower).Select(r=>r.loop).Concat(cuts);
            var points=new List<Vector2>();var lines=new List<Vector2Int>();
            foreach(var loop in loops)
            {
                int start=points.Count;points.AddRange(loop);
                for(int i=0;i<loop.Length;i++)lines.Add(new Vector2Int(start+i,start+(i+1)%loop.Length));
            }
            var slab=owner.GetComponentsInChildren<HouseSlab>(true).FirstOrDefault(s=>s.overhead==roof);
            if(upper.Length+lower.Length==0)
            {
                if(slab) Undo.DestroyObjectImmediate(slab.gameObject);
                return;
            }
            var graph=PlanarNoding.Split(points,lines);
            var edges=graph.Pieces.Select(e=>(a:Mathf.Min(e.Start,e.End),b:Mathf.Max(e.Start,e.End))).Distinct()
                .Select(e=>(a:graph.Vertices[e.a],b:graph.Vertices[e.b])).Where(e=>e.a.x!=e.b.x).ToArray();
            var xs=graph.Vertices.Select(p=>p.x).Distinct().OrderBy(x=>x).ToArray();
            var verts=new List<Vector3>();var indices=new List<int>();var slots=new List<int>();var materials=new List<Material>();
            float bottom=-house.slabThickness;
            Vector3 V(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material mat)
            {
                int slot=materials.IndexOf(mat);if(slot<0){slot=materials.Count;materials.Add(mat);}
                int start=verts.Count;verts.AddRange(new[]{a,b,c,d});
                void Triangle(int i,int j,int k)
                {
                    if(Vector3.Cross(verts[start+j]-verts[start+i],verts[start+k]-verts[start+i]).sqrMagnitude<1e-19f)return;
                    indices.AddRange(new[]{start+i,start+j,start+k});slots.Add(slot);
                }
                Triangle(0,1,2);Triangle(0,2,3);
            }
            void Side(Vector2 a,Vector2 b,Material mat)=>Quad(V(a,0),V(b,0),V(b,bottom),V(a,bottom),mat);
            // Decompose the union into vertical strips. All intersections are strip boundaries,
            // so each interval has one upper finish, one lower finish, and no internal side faces.
            var previous=new List<(float lo,float hi,Material mat)>();
            for(int i=0;i<xs.Length;i++)
            {
                float left=xs[i],right=i+1<xs.Length?xs[i+1]:left,mid=(left+right)*.5f;
                var next=new List<(float lo,float hi,Material mat)>();
                var far=new List<(float lo,float hi,Material mat)>();
                float At((Vector2 a,Vector2 b) edge,float x)=>Mathf.LerpUnclamped(edge.a.y,edge.b.y,(x-edge.a.x)/(edge.b.x-edge.a.x));
                var crossing=edges.Where(e=>mid>Mathf.Min(e.a.x,e.b.x) && mid<Mathf.Max(e.a.x,e.b.x)).OrderBy(e=>At(e,mid)).ToArray();
                Material previousSide=null;
                for(int j=1;j<=crossing.Length;j++)
                {
                    var low=crossing[j-1];var a=new Vector2(left,At(low,left));var b=new Vector2(right,At(low,right));
                    Material top=null,under=null;
                    if(j<crossing.Length)
                    {
                        var high=crossing[j];var q=new Vector2(mid,(At(low,mid)+At(high,mid))*.5f);
                        var u=upper.FirstOrDefault(r=>RoomGeometry.Inside(q,r.loop));
                        var l=lower.FirstOrDefault(r=>RoomGeometry.Inside(q,r.loop));
                        if((u!=null||l!=null) && !cuts.Any(loop=>RoomGeometry.Inside(q,loop)))
                        {
                            top=u?.finish??Default(above,true);under=l?.finish??Default(below,false);
                            var c=new Vector2(right,At(high,right));var d=new Vector2(left,At(high,left));
                            Quad(V(a,0),V(d,0),V(c,0),V(b,0),top);
                            Quad(V(a,bottom),V(b,bottom),V(c,bottom),V(d,bottom),under);
                            next.Add((a.y,d.y,under));far.Add((b.y,c.y,under));
                        }
                    }
                    if(under && !previousSide)Side(a,b,under);
                    if(!under && previousSide)Side(b,a,previousSide);
                    previousSide=under;
                }
                var ys=previous.Concat(next).SelectMany(s=>new[]{s.lo,s.hi}).Distinct().OrderBy(y=>y).ToArray();
                for(int j=1;j<ys.Length;j++)
                {
                    float y=(ys[j]+ys[j-1])*.5f;
                    var west=previous.FirstOrDefault(s=>s.lo<y&&y<s.hi).mat;
                    var east=next.FirstOrDefault(s=>s.lo<y&&y<s.hi).mat;
                    var a=new Vector2(left,ys[j-1]);var b=new Vector2(left,ys[j]);
                    if(west && !east)Side(a,b,west);
                    if(east && !west)Side(b,a,east);
                }
                previous=far;
            }
            if(!slab)
            {
                var go=new GameObject(roof?"Ceiling slab":"Floor slab");go.transform.SetParent(owner.transform,false);
                slab=go.AddComponent<HouseSlab>();slab.overhead=roof;Undo.RegisterCreatedObjectUndo(go,"Generate slab");
            }
            var position=roof?Vector3.up*(owner.height+house.slabThickness):Vector3.zero;
            var renderer=slab.GetComponent<MeshRenderer>();
            if(slab.vertices!=null && slab.vertices.SequenceEqual(verts) && slab.triangles.SequenceEqual(indices) &&
                slab.faceSlots.SequenceEqual(slots) && renderer.sharedMaterials.SequenceEqual(materials) && slab.transform.localPosition==position)return;
            Undo.RecordObjects(new Object[]{slab,slab.transform,renderer},"Build slab");
            slab.transform.localPosition=position;slab.vertices=verts.ToArray();slab.triangles=indices.ToArray();
            slab.faceSlots=slots.ToArray();slab.slots=materials.Count;renderer.sharedMaterials=materials.ToArray();
            slab.Refresh();EditorUtility.SetDirty(slab);
        }

        internal static bool SetOpening(HouseFloor owner,bool roof,int index,Vector2[] points,out string error)
        {
            var openings=roof?owner.roofOpenings:owner.floorOpenings;
            error="The selected opening no longer exists.";
            if(index<0 || index>openings.Count || (points==null && index==openings.Count))return false;
            if(points!=null)
            {
                if(points.Any(p=>!float.IsFinite(p.x)||!float.IsFinite(p.y)))
                {error="Opening coordinates must be finite.";return false;}
                points=points.Select(PlanarNoding.AtPrecision).ToArray();
                if(!ValidateOutline(points,out error))return false;
            }
            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Edit slab opening");
            Undo.RecordObject(owner,"Edit slab opening");
            if(points==null)openings.RemoveAt(index);
            else if(index==openings.Count)openings.Add(new SlabOpening(points));
            else openings[index].points=points;
            Rebuild(owner.House);EditorUtility.SetDirty(owner);
            Undo.CollapseUndoOperations(group);
            error=null;return true;
        }

        static bool ValidateOutline(Vector2[] outline,out string error)
        {
            error="Draw at least three distinct corners enclosing an area.";
            if(outline.Length<3 || outline.Distinct().Count()!=outline.Length || Mathf.Abs(RoomGeometry.Area(outline))<.0001f)return false;
            var edges = Enumerable.Range(0, outline.Length).Select(i => new Vector2Int(i, (i+1)%outline.Length)).ToList();
            var graph = PlanarNoding.Split(outline, edges);
            if(graph.Pieces.Count!=outline.Length || graph.Vertices.Count!=outline.Length)
            {error="The outline crosses or doubles back on itself.";return false;}

            error=null;return true;
        }
    }
}
