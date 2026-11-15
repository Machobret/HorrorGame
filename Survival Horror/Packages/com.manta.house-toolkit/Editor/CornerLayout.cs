using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // One record per physical wall end, in the floor plane. The same solver runs on
    // detached drawing previews and on the graph used to publish meshes.
    internal static class CornerLayout
    {
        internal sealed class End
        {
            internal readonly WallFamily family;
            internal readonly Vector2 axis;
            internal readonly float length, opening;
            internal float depth;
            float glassDepth;
            internal readonly int other;
            internal float cut, clear;
            internal float paneDepth => family.panel ? glassDepth : depth;

            internal End(WallFamily family, Vector2 delta, int other, IEnumerable<PlacedOpening> openings, bool start)
            {
                this.family=family; this.other=other;
                length=delta.magnitude; axis=delta/length;
                depth=family.thickness*.5f;
                if (family.panel)
                {
                    void Include(WallSegment segment)
                    {
                        if (!segment) return;
                        if (segment.mesh)
                        {
                            var m=segment.mesh;
                            depth=Mathf.Max(depth,m.bounds.extents.z);
                            if (m.subMeshCount==2)
                            {
                                var b=m.GetSubMesh(1).bounds;
                                glassDepth=Mathf.Max(glassDepth,Mathf.Abs(b.center.z-m.bounds.center.z)+b.extents.z);
                            }
                            return;
                        }
                        if (segment.parts==null) return;
                        foreach (var p in segment.parts)
                        {
                            float reach=Mathf.Abs(p.bounds.center.z)+p.bounds.extents.z;
                            depth=Mathf.Max(depth,reach);
                            if(p.glass) glassDepth=Mathf.Max(glassDepth,reach);
                        }
                    }
                    foreach (var panel in family.panels) Include(panel);
                    if (family.surrounds!=null)
                        foreach (var surround in family.surrounds) Include(surround);
                }
                opening=float.PositiveInfinity;
                foreach (var o in openings)
                {
                    var surround=family.panel ? family.FindSurround(o.insert) : null;
                    var fit=new FittedOpening(o,surround);
                    opening=Mathf.Min(opening,start ? fit.left : length-fit.Right);
                }
            }
        }

        internal static bool Solve(End[] ends,out End host,out float width,out string issue)
        {
            host=null; width=0; issue=null;
            if (ends.Length<2) return true;
            width=2*ends.Max(e=>e.depth);
            float half=width*.5f;
            var owner=ends.OrderByDescending(e=>(bool)e.family.panel).ThenByDescending(e=>e.depth).ThenBy(e=>e.other).First();
            bool through=ends.Any(a=>ends.Any(b=>a!=b && (a.axis+b.axis).sqrMagnitude<1e-10f &&
                (ends.Length==2 || !a.family.panel && !b.family.panel && a.depth>=half && b.depth>=half)));

            // An opening flush with a right-angle junction can keep its entire surround.
            // Butt the perpendicular branch against that wall's face instead.
            var flush=ends.Where(e=>e.opening<half-.00001f).ToArray();
            if (ends.Length==2 && flush.Length==1 && Mathf.Abs(Vector2.Dot(ends[0].axis,ends[1].axis))<.00001f)
            {
                var side=ends.First(e=>e!=flush[0]);
                side.cut=side.clear=flush[0].depth;
                return Available(side,side.clear,out issue);
            }
            if (!through) host=owner;
            foreach (var e in ends)
            {
                float openingLimit=0;
                float cos=Mathf.Abs(Vector2.Dot(e.axis,owner.axis));
                float sin=Mathf.Abs(Cross(e.axis,owner.axis));
                if (!through)
                {
                    // Last square cut whose whole cross section still touches the post.
                    e.cut=Mathf.Min(Exit(half-sin*e.depth,cos),Exit(half-cos*e.depth,sin));
                    openingLimit=half*(cos+sin);
                    e.clear=Mathf.Min(Exit(half+sin*e.paneDepth,cos),Exit(half+cos*e.paneDepth,sin));
                }
                foreach (var neighbor in ends)
                {
                    if (e==neighbor) continue;
                    float dot=Vector2.Dot(e.axis,neighbor.axis);
                    float cross=Mathf.Abs(Cross(e.axis,neighbor.axis));
                    if (cross<.00001f && dot<0) continue;
                    // Furthest possible overlap of the finite rectangular wall strips
                    // along this arm, including offset panes and their collider depth.
                    float endProjection=Mathf.Max(0,dot)*neighbor.length+cross*neighbor.depth;
                    float separation=Exit(neighbor.depth+Mathf.Abs(dot)*e.paneDepth,cross);
                    e.clear=Mathf.Max(e.clear,Mathf.Min(endProjection,separation));
                    float surroundSeparation=Exit(neighbor.depth+Mathf.Abs(dot)*e.depth,cross);
                    openingLimit=Mathf.Max(openingLimit,Mathf.Min(endProjection,surroundSeparation));
                }
                if (!Available(e,openingLimit,out issue)) return false;
            }
            return true;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float Exit(float distance,float speed)=>speed<.000001f ? float.PositiveInfinity : Mathf.Max(0,distance)/speed;
        static bool Available(End e,float openingLimit,out string issue)
        {
            issue=null;
            if (e.opening+.00001f>=openingLimit) return true;
            issue="This corner would overlap a door or window surround. Move the opening or widen the angle.";
            return false;
        }
    }
}
