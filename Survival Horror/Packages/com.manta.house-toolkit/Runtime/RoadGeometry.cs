using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HouseToolkit
{
    internal static class RoadGeometry
    {
        internal sealed class Arm
        {
            internal RoadSpan span;
            internal RoadEnd end;
            internal RoadFamily family;
            internal RoadNode node;
            internal Vector2 dir, side;
            internal float length, half, trim, maxTrim;
            internal bool outgoing, capLeft, capRight, openEnd;
            internal Vector2 Mouth => Flat(node.position) + dir * trim;
            internal Vector2 Left => Mouth + side;
            internal Vector2 Right => Mouth - side;
            internal Vector2 Across => side / half;
            internal RoadSide Side(bool left) => left == outgoing ? span.left : span.right;
            internal bool Walk(bool left)
            {
                var choice = Side(left).sidewalk;
                return choice == RoadOverride.Inherit ? (left == outgoing ? family.leftSidewalk : family.rightSidewalk) : choice == RoadOverride.On;
            }
        }

        internal static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
        static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);
        internal static Vector2 LeftOf(Vector2 v) => new Vector2(-v.y, v.x);
        static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;

        internal static List<Arm> Arms(RoadGraph graph, RoadNode node, RoadFamily fallback, float pathWidth = 0)
        {
            var arms = new List<Arm>();
            foreach (var span in graph.spans)
            {
                bool start = span.start.node == node.id;
                if (!start && span.end.node != node.id) continue;
                var other = graph.Node(start ? span.end.node : span.start.node);
                var delta = Flat(other.position - node.position);
                var family = span.Style(fallback);
                float width = pathWidth > 0 ? (span.pathWidth > 0 ? span.pathWidth : graph.RunWidth(span.run,pathWidth)) : family.width;
                float half = Mathf.Max(.05f, width * .5f);
                arms.Add(new Arm { span=span, end=start?span.start:span.end, family=family, node=node, outgoing=start,
                    length=delta.magnitude, dir=delta.normalized, half=half, side=LeftOf(delta.normalized)*half });
            }
            arms.Sort((a,b) => Mathf.Atan2(a.dir.y,a.dir.x).CompareTo(Mathf.Atan2(b.dir.y,b.dir.x)));
            if (arms.Count == 1) arms[0].openEnd=arms[0].capLeft=arms[0].capRight=true;
            if (arms.Count == 2)
            {
                // Both spans share a cross-section through a bend. Subdividing a straight
                // leaves exactly the same textured ribbon on either side of the new point.
                var a = arms[0]; var b = arms[1];
                var normal = (a.side.normalized - b.side.normalized).normalized;
                float width = (a.half + b.half) * .5f;
                a.side = normal * (width / Mathf.Max(.5f, Vector2.Dot(normal,a.side.normalized)));
                b.side = -a.side;
                a.capLeft=!b.Walk(false); a.capRight=!b.Walk(true);
                b.capLeft=!a.Walk(false); b.capRight=!a.Walk(true);
            }
            if (arms.Count < 3) return arms;
            foreach (var arm in arms)
            {
                int far=arm.outgoing?arm.span.end.node:arm.span.start.node;
                bool shared=graph.spans.Any(s=>s!=arm.span && (s.start.node==far || s.end.node==far));
                arm.maxTrim=arm.length*(shared?.45f:.95f);
            }
            for (int i=0; i<arms.Count; i++)
            {
                var a=arms[i]; var b=arms[(i+1)%arms.Count];
                var corner=Corner(node,a,b);
                a.capLeft=b.capRight=corner?.sidewalk==false;
                float border=pathWidth > 0 ? 0 : Mathf.Max(.25f,a.family.sidewalkWidth,b.family.sidewalkWidth);
                var cross=Cross(a.dir,b.dir);
                float ta=0,tb=0;
                if (Mathf.Abs(cross)>.001f)
                {
                    var between=-b.side-a.side;
                    ta=Cross(between,b.dir)/cross; tb=Cross(between,a.dir)/cross;
                }
                a.trim=Mathf.Max(a.trim,Mathf.Min(a.maxTrim, Mathf.Max(a.half,ta)+border));
                b.trim=Mathf.Max(b.trim,Mathf.Min(b.maxTrim, Mathf.Max(b.half,tb)+border));
            }
            return arms;
        }

        internal static RoadCorner Corner(RoadNode n, Arm a, Arm b) =>
            n.corners.Find(c=>c.arm==a.end.port && c.nextArm==b.end.port);

        internal static int Primary(List<Arm> arms)
        {
            int saved=arms.FindIndex(a=>a.end.port==a.node.firstArm);
            if (saved>=0) return saved;
            if (arms.Count!=3) return arms.FindIndex(a=>a.end.port==arms.Min(b=>b.end.port));
            int branch=0; float best=2;
            for (int i=0;i<3;i++)
            {
                float dot=Vector2.Dot(arms[(i+1)%3].dir,arms[(i+2)%3].dir);
                if (dot<best) { best=dot; branch=i; }
            }
            return branch;
        }

        internal static RoadMesh Build(RoadGraph graph, RoadFamily fallback)
        {
            var mesh=new RoadMesh();
            var byPort=new Dictionary<int,Arm>();
            foreach (var n in graph.nodes)
            {
                var arms=Arms(graph,n,fallback);
                foreach (var a in arms) byPort.Add(a.end.port,a);
                if (arms.Count>=3) Junction(mesh,arms);
            }
            // Accumulate distance along an authored run, including subdivisions and grade.
            foreach (var run in graph.spans.GroupBy(s=>s.run))
            {
                var remaining=new HashSet<RoadSpan>(run);
                var starts=run.ToLookup(s=>s.start.node);
                var ends=new HashSet<int>(run.Select(s=>s.end.node));
                foreach (var root in run.OrderBy(s=>ends.Contains(s.start.node)).ThenBy(s=>s.id))
                {
                    var span=root; float station=0;
                    while (span!=null && remaining.Remove(span))
                    {
                        var a=byPort[span.start.port]; var b=byPort[span.end.port];
                        float length=Vector3.Distance(a.node.position,b.node.position);
                        Strip(mesh,a,b,station,length);
                        station+=length;
                        span=starts[span.end.node].FirstOrDefault(remaining.Contains);
                    }
                }
            }
            return mesh;
        }

        static Vector2 Template(int arm, int count)
        {
            if (arm==0) return Vector2.down;
            if (arm==1) return Vector2.right;
            return count==4 && arm==2 ? Vector2.up : Vector2.left;
        }

        // Project the same physical lane coordinates over every triangle of the junction.
        // Pair opposite arms at an X and the straightest pair at a T, independently of art rotation.
        internal sealed class JunctionWear
        {
            readonly Arm[] mouths;
            readonly Vector2 origin;
            internal readonly Vector4 lanes;
            internal readonly int turns;

            internal JunctionWear(List<Arm> arms)
            {
                origin=Flat(arms[0].node.position);
                int first=0;
                int opposite=arms.Count==4?2:1;
                if(arms.Count==3)
                {
                    for(int i=1;i<3;i++)
                        if(Vector2.Dot(arms[i].dir,arms[(i+1)%3].dir)<Vector2.Dot(arms[first].dir,arms[opposite].dir))
                        {first=i;opposite=(i+1)%3;}
                }
                if(arms.Count==4)
                {
                    // Give the straighter through route the broadest wear in the overlap.
                    first=Vector2.Dot(arms[1].dir,arms[3].dir)<Vector2.Dot(arms[0].dir,arms[2].dir)?1:0;
                    mouths=new[]{arms[first],arms[first+2],arms[first+1],arms[(first+3)%4]};
                }
                else mouths=new[]{arms[opposite],arms[first],arms[(opposite+1)%3]};
                var counts=Vector4.zero;
                for(int i=0;i<mouths.Length;i++)counts[i]=mouths[i].family.LaneCount;
                lanes=counts;
                bool CanTurn(int from,int to)
                {
                    var a=mouths[from];var b=mouths[to];
                    return (a.outgoing?a.family.reverseLanes:a.family.forwardLanes)>0 &&
                           (b.outgoing?b.family.forwardLanes:b.family.reverseLanes)>0;
                }
                turns=(CanTurn(0,2)?1:0) | (CanTurn(2,1)?2:0);
                if(mouths.Length==4)turns|=(CanTurn(3,0)?4:0) | (CanTurn(1,3)?8:0);
            }

            internal void Project(Vector3 point,out Vector4 across,out Vector4 inward)
            {
                var local=Flat(point)-origin;
                across=Vector4.zero;
                inward=Vector4.one;
                for(int i=0;i<mouths.Length;i++)
                {
                    var arm=mouths[i];
                    // Both ends of a through road count lanes from the same physical side.
                    float sign=(i&1)==0?1:-1;
                    across[i]=.5f+sign*Vector2.Dot(local,LeftOf(arm.dir))/(2*arm.half);
                    inward[i]=1-Vector2.Dot(local,arm.dir)/arm.trim;
                }
            }
        }

        static void Junction(RoadMesh mesh,List<Arm> arms)
        {
            int first=Primary(arms);
            // Rotating the texture must never change the material in a mixed-family junction.
            var family=arms.GroupBy(a=>a.family).OrderByDescending(g=>g.Count())
                .ThenBy(g=>g.Min(a=>a.end.port)).First().Key;
            var n=arms[0].node; float y=n.position.y;
            var material=n.junctionMaterial ? n.junctionMaterial : arms.Count==3 ? family.tJunction : family.xJunction;
            if (!material) material=family.bareAsphalt ? family.bareAsphalt : family.Asphalt;
            var hit=new RoadHit(RoadPart.Point,n.id);
            Vector2 middle=Vector2.one*.5f;
            var wear=new JunctionWear(arms);
            void Fan(Vector2 a,Vector2 b,Vector2 uvA,Vector2 uvB,RoadFamily atA,RoadFamily atB)
            {
                mesh.Face(material,hit,Vector3.up,new[]{n.position,At(a,y),At(b,y)},new[]{middle,uvA,uvB},
                    junctionArms:arms.Count,style:family,styleB:atA,styleC:atB,wear:wear);
            }

            for(int i=0;i<arms.Count;i++)
            {
                var a=arms[(first+i)%arms.Count]; var b=arms[(first+i+1)%arms.Count];
                var u=Template(i,arms.Count); var v=Template((i+1)%arms.Count,arms.Count);
                var mouth=middle+u*.5f; var right=mouth-LeftOf(u)*.25f; var left=mouth+LeftOf(u)*.25f;
                Fan(a.Right,a.Left,right,left,a.family,a.family);
                var other=middle+v*.5f-LeftOf(v)*.25f;
                var corner=Corner(n,a,b);
                var turn=CornerPoint(a,b);
                float turnCross=Cross(u,v);
                var halfUV=Mathf.Abs(turnCross)<.001f ? (left+other)*.5f :
                    left+u*(Cross(other-left,v)/turnCross);
                Fan(a.Left,turn,left,halfUV,a.family,family);
                Fan(turn,b.Right,halfUV,other,family,b.family);
                JunctionEdge(mesh, a, b, turn);

                if(corner?.sidewalk==false)continue;
                bool walkA=a.Walk(true), walkB=b.Walk(false);
                if(!walkA && !walkB)continue;
                var target=new RoadHit(RoadPart.Corner,n.id,a.end.port,b.end.port);
                var sa=new Section(a.family,a.Side(true),corner,walkA);
                var sb=new Section(b.family,b.Side(false),corner,walkB);
                var normalA=LeftOf(a.dir);var normalB=-LeftOf(b.dir);
                var vertex=At(turn,y);
                if(walkA && walkB)
                {
                    // Intersect both outer sidewalk edges. Each straight leg keeps its
                    // own width right up to the shared seam.
                    var outer=OffsetIntersection(normalA,sa.width,normalB,sb.width);
                    var joinA=outer/Mathf.Max(.001f,sa.width);
                    var joinB=outer/Mathf.Max(.001f,sb.width);
                    Walk(mesh,target,At(a.Left,y),vertex,a.Across,joinA,sa,sa);
                    Walk(mesh,target,vertex,At(b.Right,y),joinB,-b.Across,sb,sb);
                    if(Mathf.Abs(sa.height-sb.height)>.0001f)
                        mesh.Face(sa.height>sb.height?sa.pavement:sb.pavement,target,At(a.dir,0),
                            new[]{vertex+Vector3.up*sa.height,vertex+At(outer,sa.height),
                                  vertex+At(outer,sb.height),vertex+Vector3.up*sb.height});
                }
                else
                {
                    // One sidewalk continues straight and ends against the other
                    // road edge. Its outer edge never sweeps into the side street.
                    var arm=walkA?a:b;
                    var normal=walkA?normalA:normalB;
                    var across=walkA?a.Across:-b.Across;
                    var section=walkA?sa:sb;
                    var cap=OffsetIntersection(normal,1,walkA?normalB:normalA,0);
                    var walkStart=At(walkA?a.Left:b.Right,y);
                    Walk(mesh,target,walkStart,vertex,across,cap,section,section);
                    EndCap(mesh,target,vertex,cap,-arm.dir,section);
                }
            }
        }

        // Follow only the closed boundary between mouths; never draw across a junction entrance.
        static void JunctionEdge(RoadMesh mesh, Arm a, Arm b, Vector2 corner)
        {
            var normalA = LeftOf(a.dir);
            var normalB = -LeftOf(b.dir);
            var middleFamily = a.family.edgeLines ? a.family : b.family;
            Vector2 AtCorner(float inset) => corner - OffsetIntersection(normalA, inset, normalB, inset);
            void Half(Arm arm, bool first)
            {
                var f = arm.family;
                if (!f.edgeLines) return;
                float inset = Mathf.Clamp(f.edgeLineInset, 0, arm.half - .02f);
                float width = Mathf.Min(Mathf.Max(.02f, f.edgeLineWidth), arm.half - inset);
                float middleInset = Mathf.Clamp(middleFamily.edgeLineInset, 0, Mathf.Min(a.half, b.half) - .02f);
                float middleWidth = Mathf.Min(Mathf.Max(.02f, middleFamily.edgeLineWidth), Mathf.Min(a.half, b.half) - middleInset);
                var outer = first ? a.Left - a.Across * inset : b.Right + b.Across * inset;
                var inner = first ? a.Left - a.Across * (inset + width) : b.Right + b.Across * (inset + width);
                var midOuter = AtCorner(middleInset);
                var midInner = AtCorner(middleInset + middleWidth);
                var points = first ? new[] { outer, inner, midInner, midOuter } : new[] { midOuter, midInner, inner, outer };
                mesh.Face(f.EdgePaint, default, Vector3.up,
                    points.Select(p => At(p, a.node.position.y + .003f)).ToArray(), paint: true, style: f);
            }
            Half(a, true);
            Half(b, false);
        }

        internal static Vector2 CornerPoint(Arm a,Arm b)
        {
            float cross=Cross(a.dir,b.dir);
            var midpoint=(a.Left+b.Right)*.5f;
            if(Mathf.Abs(cross)<.001f)return midpoint;
            var delta=b.Right-a.Left;
            float back=-Cross(delta,b.dir)/cross, forward=-Cross(delta,a.dir)/cross;
            // A very short arm may not have enough room for a full miter.
            if(back<-.001f || forward<-.001f || back>a.trim+2*b.half || forward>b.trim+2*a.half)
                return midpoint;
            return a.Left-a.dir*back;
        }

        static Vector2 OffsetIntersection(Vector2 a,float widthA,Vector2 b,float widthB)
        {
            float cross=Cross(a,b);
            if(Mathf.Abs(cross)<.001f)return a*widthA;
            var offset=(LeftOf(a)*widthB-LeftOf(b)*widthA)/cross;
            return Vector2.ClampMagnitude(offset,4*Mathf.Max(widthA,widthB));
        }

        struct Section
        {
            internal float width,height,curbWidth,curbHeight;
            internal Material pavement,curb;
            internal Section(RoadFamily f,RoadSide side,RoadCorner corner,bool exists)
            {
                width=exists ? f.sidewalkWidth : 0; height=f.sidewalkHeight;
                var local=corner?.curb; var edge=side.curb;
                var mode=local!=null && local.enabled!=RoadOverride.Inherit ? local.enabled : edge.enabled;
                bool enabled=mode==RoadOverride.Inherit ? f.curbs : mode==RoadOverride.On;
                float w=local!=null && local.width>0 ? local.width : edge.width>0 ? edge.width : f.curbWidth;
                curbWidth=enabled ? Mathf.Clamp(w,0,width) : 0;
                curbHeight=local!=null && local.height>=0 ? local.height : edge.height>=0 ? edge.height :
                    f.curbHeight>=0 ? f.curbHeight : height;
                pavement=corner!=null && corner.material ? corner.material : side.material ? side.material : f.sidewalk;
                curb=local!=null && local.material ? local.material : edge.material ? edge.material : f.curbMaterial ? f.curbMaterial : pavement;
                if (!enabled) curbHeight=height;
            }
        }

        static void EndCap(RoadMesh mesh,RoadHit hit,Vector3 origin,Vector2 outward,Vector2 normal,Section section)
        {
            void Band(float fromWidth,float toWidth,float height,Material material)
            {
                if(toWidth<=fromWidth || height<=0)return;
                var p=origin+At(outward*fromWidth,0);var q=origin+At(outward*toWidth,0);
                mesh.Face(material,hit,At(normal,0),new[]{p,q,q+Vector3.up*height,p+Vector3.up*height});
            }
            Band(0,section.curbWidth,section.curbHeight,section.curb);
            Band(section.curbWidth,section.width,section.height,section.pavement);
        }

        static void Walk(RoadMesh mesh,RoadHit hit,Vector3 a,Vector3 b,Vector2 sideA,Vector2 sideB,Section s,Section t)
        {
            var outward=At(sideA,0); var other=At(sideB,0);
            Vector3 A(float w,float h)=>a+outward*w+Vector3.up*h;
            Vector3 B(float w,float h)=>b+other*w+Vector3.up*h;
            var along = Flat(b-a).normalized;
            // Use the same metre coordinates on the curb face and top, including split spans.
            if (along.x < -.0001f || (Mathf.Abs(along.x) <= .0001f && along.y < 0)) along = -along;
            Vector2 UV(Vector3 p, float crossSection) => new Vector2(Vector2.Dot(Flat(p), along), crossSection);
            void Quad(Material mat,Vector3 normal,float w0,float h0,float w1,float h1,float v0,float y0,float v1,float y1)
            {
                var points = new[]{A(w0,h0),B(v0,y0),B(v1,y1),A(w1,h1)};
                mesh.Face(mat,hit,normal,points,new[]{UV(points[0],w0+h0),UV(points[1],v0+y0),
                    UV(points[2],v1+y1),UV(points[3],w1+h1)});
            }
            Quad(s.curbWidth>0?s.curb:s.pavement,-outward,0,0,0,s.curbHeight,0,0,0,t.curbHeight);
            if(s.curbWidth>0 || t.curbWidth>0)
            {
                Quad(s.curb,Vector3.up,0,s.curbHeight,s.curbWidth,s.curbHeight,0,t.curbHeight,t.curbWidth,t.curbHeight);
                Quad(s.curb,outward*Mathf.Sign(s.curbHeight-s.height),s.curbWidth,s.curbHeight,s.curbWidth,s.height,
                    t.curbWidth,t.curbHeight,t.curbWidth,t.height);
            }
            Quad(s.pavement,Vector3.up,s.curbWidth,s.height,s.width,s.height,t.curbWidth,t.height,t.width,t.height);
            Quad(s.pavement,outward,s.width,s.height,s.width,0,t.width,t.height,t.width,0);
        }

        static void Strip(RoadMesh mesh,Arm a,Arm b,float station,float length)
        {
            var f=a.family;
            float from=a.trim/a.length*length, to=length-b.trim/b.length*length;
            float repeat=Mathf.Max(.1f,f.repeatLength);
            Vector3 P(float t,float lateral)
            {
                var p=Vector2.Lerp(a.Mouth,b.Mouth,t)+Vector2.Lerp(a.side,-b.side,t)*lateral;
                return At(p,Mathf.Lerp(a.node.position.y,b.node.position.y,t));
            }
            void Road(float s,float t,RoadHit hit)
            {
                float v0=(station+Mathf.Lerp(from,to,s))/repeat, v1=(station+Mathf.Lerp(from,to,t))/repeat;
                // Clamp the fade to this strip so even short terminal spans reach full wear at their join.
                float fade = Mathf.Min(f.wheelPathEndFade, (to-from)*.45f);
                Vector2 Distances(float at) => new Vector2(a.openEnd ? Mathf.Lerp(from,to,at)-from : fade,
                    b.openEnd ? to-Mathf.Lerp(from,to,at) : fade) * (f.wheelPathEndFade / Mathf.Max(.001f,fade));
                var near = Distances(s); var far = Distances(t);
                mesh.Face(f.Asphalt,hit,Vector3.up,new[]{P(s,1),P(s,-1),P(t,-1),P(t,1)},
                    new[]{new Vector2(0,v0),new Vector2(1,v0),new Vector2(1,v1),new Vector2(0,v1)},style:f,
                    stripFade: new Vector4(near.x,near.y,far.x,far.y));
            }
            int lanes=Mathf.Max(1,f.LaneCount);
            // Left is +1 in span space. All lanes share the family's total road width equally.
            float Boundary(int fromLeft)=>1f-2f*fromLeft/lanes;
            float divider=Boundary(f.reverseLanes);
            float zone=Mathf.Min(.24f,2/Mathf.Max(.01f,to-from));
            float start=a.trim>0 ? zone : 0, end=b.trim>0 ? 1-zone : 1;
            if(start>0) Road(0,start,new RoadHit(RoadPart.Approach,a.span.id,a.end.port));
            Road(start,end,new RoadHit(RoadPart.Road,a.span.id));
            if(end<1) Road(end,1,new RoadHit(RoadPart.Approach,a.span.id,b.end.port));
            foreach(bool left in new[]{true,false})
            {
                if(!a.Walk(left))continue;
                float sign=left?1:-1;
                var section=new Section(f,a.Side(left),null,true);
                var hit=new RoadHit(RoadPart.Sidewalk,a.span.id,left:left);
                if(left?a.capLeft:a.capRight)EndCap(mesh,hit,P(0,sign),a.Across*sign,-a.dir,section);
                if(left?b.capRight:b.capLeft)EndCap(mesh,hit,P(1,sign),-b.Across*sign,-b.dir,section);
                Walk(mesh,hit,P(0,sign),P(1,sign),
                    a.Across*sign,-b.Across*sign,section,section);
            }
            void Paint(Material material,float d0,float d1,float left,float right)
            {
                float t0=Mathf.InverseLerp(from,to,d0),t1=Mathf.InverseLerp(from,to,d1);
                if(t1<=t0)return;
                var lift=Vector3.up*.003f;
                // Tile paint wear in road-local metres so neighboring markings share the pattern.
                float u0=(1-left)*a.half, u1=(1-right)*a.half;
                float v0=station+Mathf.Lerp(from,to,t0), v1=station+Mathf.Lerp(from,to,t1);
                mesh.Face(material,default,Vector3.up,new[]{P(t0,left)+lift,P(t0,right)+lift,P(t1,right)+lift,P(t1,left)+lift},
                    new[]{new Vector2(u0,v0),new Vector2(u1,v0),new Vector2(u1,v1),new Vector2(u0,v1)},paint:true,style:f);
            }
            if (f.edgeLines)
            {
                float inset = Mathf.Clamp(f.edgeLineInset, 0, a.half - .02f);
                float width = Mathf.Min(Mathf.Max(.02f, f.edgeLineWidth), a.half - inset);
                float outer = 1-inset/a.half, inner = 1-(inset+width)/a.half;
                Paint(f.EdgePaint, from, to, inner, outer);
                Paint(f.EdgePaint, from, to, -outer, -inner);
            }
            float EndMarkings(Arm arm,bool first)
            {
                if(arm.trim<=0)return 0;
                void Band(float near,float far,float l,float r) =>
                    Paint(f.CrossingPaint,first?from+near:to-far,first?from+far:to-near,l,r);
                // Leave the edge lines clear, with equal margins on both sides.
                const float limit=.78f;
                var set = f.markingSet;
                var crossing = set && arm.end.crosswalk ? set.Find(arm.end.crosswalkStyle, true) : null;
                float availableLength = to - from;
                float crossingDepth = crossing != null ? Mathf.Min(crossing.size.y, availableLength * .45f) : 1.4f;
                float crossingEnd = .15f + crossingDepth;
                int incoming=first?f.reverseLanes:f.forwardLanes;
                float inner=Mathf.Clamp(divider,-limit,limit);
                float outer=first?limit:-limit;
                float stopOffset = arm.end.crosswalk ? crossingEnd + .3f : .4f;
                float travel = first ? 1 : -1;
                void Stamp(RoadMarkingSet.Marking mark, float near, float depth, float center, float width)
                {
                    float halfWidth = width / (2 * a.half);
                    float left = center - travel * halfWidth, right = center + travel * halfWidth;
                    float tNear = first ? near / availableLength : 1 - near / availableLength;
                    float tFar = tNear + travel * depth / availableLength;
                    var lift = Vector3.up * .003f;
                    var uv = mark.uv;
                    mesh.Face(set.atlas, default, Vector3.up,
                        new[]{P(tFar,left)+lift,P(tFar,right)+lift,P(tNear,right)+lift,P(tNear,left)+lift},
                        new[]{uv.min,new Vector2(uv.xMax,uv.yMin),uv.max,new Vector2(uv.xMin,uv.yMax)},
                        paint:true,style:f, atlasPaint:true);
                }
                if (crossing != null && crossingEnd <= availableLength)
                {
                    float width = 2 * a.half * limit;
                    int repeats = Mathf.Clamp(Mathf.RoundToInt(width / crossing.size.x), 1, 512);
                    for (int i = 0; i < repeats; i++)
                        Stamp(crossing, .15f, crossingDepth, -limit + (i + .5f)*2*limit/repeats, width/repeats);
                }
                else if(arm.end.crosswalk)
                {
                    float available=2*a.half*limit;
                    int count=Mathf.Max(1,Mathf.FloorToInt((available+.3f)/.7f));
                    float pitch=available/count;
                    for(int i=0;i<count;i++)
                    {
                        float center=-a.half*limit+(i+.5f)*pitch;
                        float halfBar=Mathf.Min(.2f,pitch*.3f);
                        Band(.15f,1.55f,(center-halfBar)/a.half,(center+halfBar)/a.half);
                    }
                }
                if(arm.end.stopLine && incoming>0)
                {
                    float d=stopOffset;
                    Band(d,d+.24f,Mathf.Min(inner,outer),Mathf.Max(inner,outer));
                }
                var symbol = set ? set.Find(arm.end.symbol, false) : null;
                if (symbol != null && incoming>0)
                {
                    float near = arm.end.stopLine ? stopOffset + .64f : arm.end.crosswalk ? crossingEnd + .4f : .4f;
                    float laneFraction=Mathf.Abs(outer-inner)/incoming;
                    float width = Mathf.Min(a.half * laneFraction * .9f, symbol.size.x);
                    float depth = symbol.size.y * width / symbol.size.x;
                    var opposite = first ? b : a;
                    float reserve = opposite.trim > 0 && opposite.end.crosswalk
                        ? .55f + (set.Find(opposite.end.crosswalkStyle,true)?.size.y ?? 1.4f) : 0;
                    if (near + depth <= availableLength - reserve)
                        Stamp(symbol, near, depth, inner+travel*laneFraction*.5f, width);
                }
                return arm.end.crosswalk || arm.end.stopLine ? Mathf.Max(2.15f, crossingEnd + .6f) : 0;
            }
            float startClear = EndMarkings(a,true), endClear = EndMarkings(b,false);
            float lineFrom=from+startClear;
            float lineTo=to-endClear;
            float halfLine=f.lineWidth/(2*a.half);
            for(int lane=1;lane<lanes;lane++)
            {
                float lateral=Boundary(lane);
                bool center=lane==f.reverseLanes;
                if(center && f.centerLine==RoadLines.None)continue;
                var material=center?f.CenterLinePaint:f.CrossingPaint;
                if(center && f.centerLine==RoadLines.Solid)
                    Paint(material,lineFrom,lineTo,lateral-halfLine,lateral+halfLine);
                else Dashes(material,lateral);
            }
            void Dashes(Material material,float lateral)
            {
                float period=Mathf.Max(.2f,f.dash+f.gap);
                for(float d=Mathf.Floor((station+lineFrom)/period)*period-station; d<lineTo; d+=period)
                {
                    float lo=Mathf.Max(lineFrom,d), hi=Mathf.Min(lineTo,d+f.dash);
                    bool clippedAtJunction=(a.trim>0 && lo==lineFrom) || (b.trim>0 && hi==lineTo);
                    if(clippedAtJunction && hi-lo<Mathf.Max(f.lineWidth,f.dash*.2f)) continue;
                    Paint(material,lo,hi,lateral-halfLine,lateral+halfLine);
                }
            }
        }
    }
}
