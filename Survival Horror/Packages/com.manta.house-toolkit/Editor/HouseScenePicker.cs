using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // The editor picks the rendered object. Openings and boundary-only walls have no
    // surface of their own, so compare their drawing planes with that visible hit.
    internal struct HousePick
    {
        internal HouseFloor storey;
        internal GameObject prop;
        internal HouseWall wall;
        internal InsertAttachment insert;
        internal HouseRoom room;
        internal string segmentId, insertId, cutter;
        internal InsertAttachment ResolveInsert()
        {
            if(insert || !wall || insertId==null) return insert;
            string id=insertId;
            return Array.Find(wall.GetComponentsInChildren<InsertAttachment>(true),a=>a.openingId==id);
        }
        internal string OpeningId => insertId ?? (insert ? insert.openingId : segmentId);
        internal string Kind
        {
            get
            {
                if(cutter!=null)return "Boolean cut";
                if(OpeningId is string id && wall)
                {
                    var o=wall.openings.Find(item=>item.id==id);
                    return o?.insert ? o.insert.kind+(segmentId==null ? "" : " segment") : null;
                }
                if(cut!=null)return "Slab opening";
                if(room)return ceiling ? "Room ceiling" : "Room floor";
                if(corner!=0)return "Wall endpoint";
                if(wall)return "Wall";
                if(storey)return "Storey";
                if(prop)return "Prop";
                return null;
            }
        }
        internal int corner;
        internal HouseSlab slab;
        internal SlabOpening cut;
        internal HouseFloor cutOwner;
        internal bool roofCut;
        internal bool ceiling,underside;
        internal bool positiveFace;
        internal Vector2 point;

        internal bool Same(HousePick other) => storey==other.storey && prop==other.prop && wall==other.wall && OpeningId==other.OpeningId && room==other.room &&
            cutter==other.cutter && corner==other.corner && slab==other.slab && cut==other.cut && ceiling==other.ceiling && underside==other.underside && segmentId==other.segmentId;
    }

    internal sealed class HouseScenePicker
    {
        internal HousePick hover;
        internal SceneView hoverView;
        HouseFloor floor, ceilingOwner;
        bool ceilingIsRoof;
        HouseSlab floorSlab, ceilingSlab;
        readonly List<HouseWall> walls=new List<HouseWall>();
        readonly List<HouseRoom> rooms=new List<HouseRoom>();
        readonly List<HouseSlab> slabs=new List<HouseSlab>();
        readonly List<Collider> colliders=new List<Collider>();
        readonly List<AuthoredPanel> panels=new List<AuthoredPanel>();
        readonly List<HouseWall> emptyWalls=new List<HouseWall>();
        bool dirty=true;
        internal void Invalidate(){dirty=true;}

        void Refresh(HouseFloor active)
        {
            if(!dirty && floor==active)return;
            dirty=false;floor=active;
            floor.GetComponentsInChildren(false,walls);
            emptyWalls.Clear();
            foreach(var wall in walls)
                if(wall.openings.Exists(o=>o.insert && !o.HasPrefab))emptyWalls.Add(wall);
            floor.GetComponentsInChildren(false,rooms);
            floor.GetComponentsInChildren(false,panels);
            floor.GetComponentsInChildren(false,slabs);
            floorSlab=slabs.Find(s=>!s.overhead);
            ceilingOwner=SlabOpeningTool.Owner(floor,true,out ceilingIsRoof);
            if(!ceilingIsRoof)
                foreach(var slab in ceilingOwner.GetComponentsInChildren<HouseSlab>())
                    if(!slab.overhead)slabs.Add(slab);
            ceilingSlab=slabs.Find(s=>s.overhead==ceilingIsRoof && s.GetComponentInParent<HouseFloor>()==ceilingOwner);
        }

        // The builder emits a separate renderer for each frame or pane. Its centre
        // identifies the authored surround, including geometry in previously saved scenes.
        internal static string Surround(HouseWall wall, Renderer renderer, WallOutput output)
        {
            if (renderer && renderer.TryGetComponent<BooleanCutResult>(out _)) renderer = renderer.transform.parent.GetComponent<MeshRenderer>();
            if(!renderer || !output || !wall.Family || wall.openings.Count == 0 ||
                !(renderer is MeshRenderer mesh) || (!output.surfaces.Contains(mesh) && !output.panes.Contains(mesh) && !output.authored.Contains(mesh)))return null;
            var center=wall.transform.InverseTransformPoint(renderer.transform.TransformPoint(renderer.localBounds.center));
            foreach(var opening in wall.openings)
            {
                var segment=wall.Family.FindSurround(opening.insert);
                if(!segment)continue;
                var fit=new FittedOpening(opening,segment);
                if(center.x>=fit.left && center.x<=fit.Right && center.y>=0 && center.y<segment.size.y)
                    return opening.id;
            }
            return null;
        }

        internal static bool PickEmptyOpening(HouseWall wall, Ray ray, ref float depth, out HousePick pick)
        {
            pick=default;
            if(!wall || !wall.Family || wall.boundaryOnly || SceneVisibilityManager.instance.IsHidden(wall.gameObject) ||
                SceneVisibilityManager.instance.IsPickingDisabled(wall.gameObject)) return false;
            if(!new Plane(wall.transform.forward,wall.transform.position).Raycast(ray,out float distance) || distance>=depth) return false;
            var local=wall.transform.InverseTransformPoint(ray.GetPoint(distance));
            foreach(var opening in wall.openings)
            {
                if(!opening.insert || opening.HasPrefab)continue;
                var bounds=HouseSelection.OpeningBounds(wall,opening,false);
                if(local.x<bounds.min.x || local.x>bounds.max.x || local.y<bounds.min.y || local.y>bounds.max.y)continue;
                depth=distance;
                pick=new HousePick{wall=wall,insertId=opening.id};
                return true;
            }
            return false;
        }

        internal HousePick Pick(HouseFloor active,Vector2 mouse,Ray ray)
        {
            Refresh(active);
            var hit=HandleUtility.PickGameObject(mouse,false);
            float depth=float.PositiveInfinity;
            HousePick result=default;
            HouseSlab visibleSlab=null;
            if(hit)
            {
                var fixture = hit.GetComponentInParent<FixtureAttachment>();
                if (fixture) return new HousePick { prop = fixture.gameObject };
                var renderer=hit.GetComponent<Renderer>();
                if(renderer && renderer.bounds.IntersectRay(ray,out float entry))depth=entry;
                hit.GetComponents(colliders);
                float surface=float.PositiveInfinity;
                foreach(var collider in colliders)
                    if(collider.enabled && !collider.isTrigger && collider.Raycast(ray,out var info,Mathf.Infinity))
                        surface=Mathf.Min(surface,info.distance);
                if(float.IsFinite(surface))depth=surface;

            }
            // Unity's selection buffer can see the back wall through transparent glass.
            // Check the actual authored triangles from either side, including open surrounds.
            foreach (var panel in panels)
            {
                if (!panel || panel.gameObject == hit) continue;
                var collider = panel.GetComponent<MeshCollider>();
                if (collider && !collider.enabled)
                    collider = panel.GetComponentInChildren<BooleanCutResult>()?.GetComponent<MeshCollider>();
                if (!collider || !collider.enabled || !collider.bounds.IntersectRay(ray, out float entry) ||
                    entry >= depth || SceneVisibilityManager.instance.IsHidden(panel.gameObject) ||
                    SceneVisibilityManager.instance.IsPickingDisabled(panel.gameObject)) continue;
                float reach = entry + collider.bounds.size.magnitude + .01f;
                float distance = float.PositiveInfinity;
                if (collider.Raycast(ray, out var front, reach)) distance = front.distance;
                var reverse = new Ray(ray.GetPoint(reach), -ray.direction);
                if (collider.Raycast(reverse, out var back, reach)) distance = Mathf.Min(distance, reach - back.distance);
                if (distance < depth - .0001f) { depth = distance; hit = panel.gameObject; }
            }
            if (hit)
            {
                var renderer=hit.GetComponent<Renderer>();
                var wall=hit.GetComponentInParent<HouseWall>();
                if(wall && wall.Floor==floor)
                {
                    result.wall=wall;
                    result.insert=hit.GetComponentInParent<InsertAttachment>();
                    if(!result.insert)result.segmentId=Surround(wall,renderer,hit.GetComponentInParent<WallOutput>());
                    var p=wall.transform.InverseTransformPoint(ray.GetPoint(float.IsFinite(depth)?depth:0));
                    result.positiveFace = Mathf.Abs(p.z) > .0001f ? p.z > 0 : Vector3.Dot(ray.direction, wall.transform.forward) < 0;
                    result.point=floor.Local(wall.transform.TransformPoint(Vector3.right*Mathf.Clamp(p.x,0,wall.length)));
                }
                else if(hit.GetComponentInParent<CornerJoint>() is CornerJoint joint && joint.GetComponentInParent<HouseFloor>()==floor)
                    result.corner=joint.corner;
                else if((visibleSlab = hit.GetComponentInParent<HouseSlab>()) && slabs.Contains(visibleSlab) && float.IsFinite(depth))
                {
                    var p=floor.Local(ray.GetPoint(depth));
                    foreach(var room in rooms)
                        if(RoomGeometry.Inside(p,room.polygon))
                        {
                            result.room=room;result.slab=visibleSlab;
                            result.underside=Vector3.Dot(ray.direction,visibleSlab.transform.up)>0;
                            result.ceiling=visibleSlab.overhead || visibleSlab.GetComponentInParent<HouseFloor>()!=floor;
                            break;
                        }
                }
            }

            foreach(var wall in emptyWalls)
                if(PickEmptyOpening(wall,ray,ref depth,out var openingHit))
                {
                    result=openingHit;
                    hit=null;
                    visibleSlab=null;
                }

            if (hit && !hit.GetComponentInParent<HouseWall>() && !hit.GetComponentInParent<CornerJoint>() &&
                !hit.GetComponentInParent<HouseSlab>() && !hit.GetComponent<HouseRoom>() &&
                (hit.GetComponent<Renderer>() || hit.GetComponentInParent<RoomContents>() && !hit.GetComponent<RoomContents>()))
            {
                var prefab = PrefabUtility.GetNearestPrefabInstanceRoot(hit);
                result.prop = prefab && !prefab.GetComponentInChildren<House>(true) &&
                    !prefab.GetComponentInChildren<HouseFloor>(true) ? prefab : hit;
                return result;
            }

            // Keep small endpoint handles usable at the base of their own incident wall.
            float bestPixels=8;
            foreach(var node in floor.corners)
            {
                var world=floor.World(node.point);
                float along=Vector3.Dot(world-ray.origin,ray.direction);
                bool incident=result.wall && (result.wall.startCorner==node.id || result.wall.endCorner==node.id);
                if(along<=0 || along>depth+.03f && !incident)continue;
                float pixels=Vector2.Distance(HandleUtility.WorldToGUIPoint(world),mouse);
                if(pixels<bestPixels){bestPixels=pixels;result=new HousePick{corner=node.id};}
            }
            if(result.corner!=0)return result;

            PickCuts(floor, false, false, floorSlab);
            if (!HouseScene.Cutaway) PickCuts(ceilingOwner, ceilingIsRoof, true, ceilingSlab);

            void PickCuts(HouseFloor owner, bool roof, bool ceiling, HouseSlab slab)
            {
                var cuts=roof?owner.roofOpenings:owner.floorOpenings;
                if(cuts.Count==0 || SceneVisibilityManager.instance.IsHidden(slab ? slab.gameObject : owner.gameObject))return;
                var normal=owner.transform.up;
                var top=owner.transform.TransformPoint(Vector3.up*(roof ? owner.height+owner.House.slabThickness : 0));
                var face=Vector3.Dot(ray.direction,normal)<0?top:top-owner.transform.TransformVector(Vector3.up*owner.House.slabThickness);
                bool hitSlab=slab && slab==visibleSlab;
                if(!new Plane(normal,face).Raycast(ray,out var d) || d>depth+.002f && !hitSlab)return;
                var local=owner.Local(ray.GetPoint(d));
                foreach(var cut in cuts)
                {
                    bool inside=RoomGeometry.Inside(local,cut.points);
                    if(!inside && hitSlab)
                    {
                        var offset=face-owner.transform.position;
                        for(int i=0;i<cut.points.Length && !inside;i++)
                            inside=HandleUtility.DistancePointToLineSegment(mouse,
                                HandleUtility.WorldToGUIPoint(owner.World(cut.points[i])+offset),
                                HandleUtility.WorldToGUIPoint(owner.World(cut.points[(i+1)%cut.points.Length])+offset))<4;
                    }
                    if(!inside)continue;
                    result=new HousePick{slab=slab,cut=cut,cutOwner=owner,roofCut=roof,ceiling=ceiling};
                    depth=d;visibleSlab=null;break;
                }
            }
            if(result.cut!=null || result.wall)return result;

            // A boundary has no renderer. Only its visible line is a selection target.
            bestPixels=6;
            foreach(var wall in walls)
            {
                if(!wall || !wall.boundaryOnly || SceneVisibilityManager.instance.IsHidden(wall.gameObject))continue;
                var a=floor.World(floor.Position(wall.startCorner));var b=floor.World(floor.Position(wall.endCorner));
                float pixels=HandleUtility.DistancePointToLineSegment(mouse,HandleUtility.WorldToGUIPoint(a),HandleUtility.WorldToGUIPoint(b));
                if(pixels>=bestPixels || !new Plane(floor.transform.up,a).Raycast(ray,out var d) || d>depth+.03f)continue;
                result=new HousePick{wall=wall,point=floor.Local(ray.GetPoint(d))};bestPixels=pixels;
            }
            return result;
        }

        internal void DrawGuides(HouseFloor active)
        {
            Refresh(active);
            using(new Handles.DrawingScope(new Color(.45f,.85f,1,.8f)))
            {
                foreach(var wall in walls)
                    if(wall && wall.boundaryOnly && !SceneVisibilityManager.instance.IsHidden(wall.gameObject))
                        Handles.DrawLine(floor.World(floor.Position(wall.startCorner)),floor.World(floor.Position(wall.endCorner)));
                foreach(var c in floor.corners)
                {
                    var p=floor.World(c.point);
                    Handles.DotHandleCap(0,p,Quaternion.identity,HandleUtility.GetHandleSize(p)*.045f,EventType.Repaint);
                }
            }
        }
    }

    internal sealed class HouseHighlight
    {
        static Material surfaceTint;
        static Mesh posedMesh;
        static readonly int tintProperty=Shader.PropertyToID("_Color");
        HousePick cached;
        Renderer[] meshes=Array.Empty<Renderer>();
        readonly List<Vector3[]> roomFaces=new List<Vector3[]>();
        internal void Clear(){cached=default;meshes=Array.Empty<Renderer>();roomFaces.Clear();}

        void CacheRoomFaces(HousePick pick,HouseFloor floor)
        {
            // The slab builder splits faces at every room boundary and opening.
            // Reuse those triangles so the highlight leaves stair holes empty.
            var slab=pick.slab;
            if(!slab || slab.triangles==null)return;
            var cut = slab.GetComponentInChildren<BooleanCutResult>();
            var mesh = cut && cut.TryGetComponent<MeshFilter>(out var cutFilter) ? cutFilter.sharedMesh : null;
            var vertices = mesh ? mesh.vertices : slab.vertices;
            var indices = mesh ? mesh.triangles : slab.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];
                var b=vertices[indices[i+1]];
                var c=vertices[indices[i+2]];
                float normal=Vector3.Cross(b-a,c-a).y;
                if(pick.underside?normal>=0:normal<=0)continue;
                var center=floor.Local(slab.transform.TransformPoint((a+b+c)/3f));
                if(RoomGeometry.Inside(center,pick.room.polygon))roomFaces.Add(new[]{a,b,c});
            }
        }

        internal static Renderer[] SelectedRenderers(HousePick pick)
        {
            if (pick.insert || pick.insertId!=null)
            {
                var attachment=pick.ResolveInsert();
                if (attachment) return attachment.GetComponentsInChildren<Renderer>();
                // A selected gate leaf can be swapped for its bare frame.
                pick.segmentId = pick.insertId;
            }
            var output = pick.wall ? pick.wall.GetComponentInChildren<WallOutput>() : null;
            if (!output) return Array.Empty<Renderer>();
            var renderers = output.GetComponentsInChildren<Renderer>();
            if (pick.segmentId == null) return renderers;
            var segments = Array.FindAll(renderers, r => HouseScenePicker.Surround(pick.wall, r, output) == pick.segmentId);
            if (segments.Length != 0) return segments;
            // A frame can also be replaced with a self-contained prefab panel.
            pick.insertId = pick.segmentId;
            var leaf = pick.ResolveInsert();
            return leaf ? leaf.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
        }

        internal void Draw(HousePick pick,HouseFloor floor,Color color)
        {
            if(!cached.Same(pick))
            {
                Clear();cached=pick;
                if(pick.room)CacheRoomFaces(pick,floor);
                else meshes=SelectedRenderers(pick);
            }
            if(meshes.Length>0)DrawSurfaces(color);
            using(new Handles.DrawingScope(color))
            {
                if(pick.cut!=null && pick.cutOwner)
                {
                    var owner=pick.cutOwner;
                    var offset=owner.transform.TransformVector(Vector3.up*(pick.roofCut ? owner.height+owner.House.slabThickness : 0));
                    var thickness=owner.transform.TransformVector(Vector3.up*owner.House.slabThickness);
                    DrawLoop(owner,pick.cut.points,offset);DrawLoop(owner,pick.cut.points,offset-thickness);
                }
                else if(pick.room && pick.slab)
                {
                    var tint=color;tint.a=.18f;
                    var previousDepth=Handles.zTest;
                    Handles.zTest=UnityEngine.Rendering.CompareFunction.LessEqual;
                    try
                    {
                        var normal=pick.underside?Vector3.down:Vector3.up;
                        var matrix=pick.slab.transform.localToWorldMatrix*Matrix4x4.Translate(normal*.002f);
                        using(new Handles.DrawingScope(tint,matrix))
                            foreach(var triangle in roomFaces)Handles.DrawAAConvexPolygon(triangle);
                        var faceOffset=pick.slab.transform.position-floor.transform.position;
                        float lift=pick.underside?-floor.House.slabThickness-.002f:.002f;
                        DrawLoop(floor,pick.room.polygon,faceOffset+pick.slab.transform.TransformVector(Vector3.up*lift));
                    }
                    finally{Handles.zTest=previousDepth;}
                }
                else if(pick.wall && pick.OpeningId is string id && meshes.Length==0)
                {
                    var opening=pick.wall.openings.Find(o=>o.id==id);
                    if(opening!=null && opening.insert && pick.wall.Family)
                    {
                        var bounds=HouseSelection.OpeningBounds(pick.wall,opening,pick.segmentId!=null);
                        using(new Handles.DrawingScope(color,pick.wall.transform.localToWorldMatrix))
                            Handles.DrawWireCube(bounds.center,bounds.size);
                    }
                }
                else if(pick.wall && pick.wall.boundaryOnly)
                    Handles.DrawLine(floor.World(floor.Position(pick.wall.startCorner)),floor.World(floor.Position(pick.wall.endCorner)),4);
                else if(pick.corner!=0)
                {
                    var p=floor.World(floor.Position(pick.corner));
                    Handles.DotHandleCap(0,p,Quaternion.identity,HandleUtility.GetHandleSize(p)*.09f,EventType.Repaint);
                }
            }
        }

        void DrawSurfaces(Color color)
        {
            if(!surfaceTint)
            {
                var shader=Shader.Find("Hidden/HouseToolkit/SceneHighlight");
                if(!shader)return;
                surfaceTint=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                AssemblyReloadEvents.beforeAssemblyReload+=()=>{
                    UnityEngine.Object.DestroyImmediate(surfaceTint);
                    if(posedMesh)UnityEngine.Object.DestroyImmediate(posedMesh);
                };
            }
            color.a=.35f;
            surfaceTint.SetColor(tintProperty,color);
            if(!surfaceTint.SetPass(0))return;
            foreach(var renderer in meshes)
            {
                if(!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    SceneVisibilityManager.instance.IsHidden(renderer.gameObject))continue;
                if(renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                {
                    if(!posedMesh)posedMesh=new Mesh {hideFlags=HideFlags.HideAndDontSave};
                    skinned.BakeMesh(posedMesh);
                    Graphics.DrawMeshNow(posedMesh,skinned.localToWorldMatrix);
                }
                else if(renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                    Graphics.DrawMeshNow(filter.sharedMesh,renderer.localToWorldMatrix);
            }
        }

        static void DrawLoop(HouseFloor floor,Vector2[] poly,Vector3 offset)
        {
            for(int i=0;i<poly.Length;i++)
                Handles.DrawLine(floor.World(poly[i])+offset,floor.World(poly[(i+1)%poly.Length])+offset,3);
        }
    }
}
