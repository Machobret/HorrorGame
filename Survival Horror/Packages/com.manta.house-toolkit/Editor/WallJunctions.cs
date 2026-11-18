using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class WallJunctions
    {
        internal static bool CheckOpening(HouseWall wall,List<PlacedOpening> openings,out string issue)
        {
            issue=null;
            var floor=wall.Floor;
            if (!floor || !floor.House) return true;
            var physical=floor.GetComponentsInChildren<HouseWall>(true).Where(w=>!w.boundaryOnly && w.Family).ToArray();
            foreach (int id in new[]{wall.startCorner,wall.endCorner})
            {
                var ends=physical.Where(w=>w.startCorner==id || w.endCorner==id)
                    .Select(w=>Describe(w,id,w==wall ? openings : w.openings)).ToArray();
                if (!CornerLayout.Solve(ends,out _,out _,out issue)) return false;
            }
            return true;
        }

        static CornerLayout.End Describe(HouseWall w,int id,List<PlacedOpening> openings)
        {
            bool start=w.startCorner==id;
            int other=start ? w.endCorner : w.startCorner;
            return new CornerLayout.End(w.Family,w.Floor.Position(other)-w.Floor.Position(id),other,openings,start,w.Thickness);
        }

        internal static bool Refresh(HouseFloor floor,out string issue)
        {
            issue=null;
            if(!floor.House)return true;
            var walls=floor.GetComponentsInChildren<HouseWall>(true);
            var incident=walls.Where(w=>!w.boundaryOnly && w.Family)
                .SelectMany(w=>new[]{(id:w.startCorner,wall:w),(id:w.endCorner,wall:w)}).ToLookup(e=>e.id,e=>e.wall);
            var ends=new Dictionary<(HouseWall,int),CornerLayout.End>();
            var posts=new List<(FloorCorner corner,HouseWall host,Vector2 axis,float width)>();
            foreach(var c in floor.corners)
            {
                var branches=incident[c.id].ToArray();
                var arms=branches.Select(w=>Describe(w,c.id,w.openings)).ToArray();
                if (!CornerLayout.Solve(arms,out var owner,out var width,out issue)) return false;
                for(int i=0;i<branches.Length;i++)
                {
                    ends[(branches[i],c.id)]=arms[i];
                    if (arms[i]==owner) posts.Add((c,branches[i],owner.axis,width));
                }
            }

            var old=floor.GetComponentsInChildren<CornerJoint>(true).ToDictionary(j=>j.corner);
            foreach (var item in posts)
            {
                var host=item.host;
                var mesh=host.Family.cornerPost;
                if (!mesh || mesh.bounds.size.x<=0 || mesh.bounds.size.y<=0 || mesh.bounds.size.z<=0)
                    mesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (!old.Remove(item.corner.id,out var joint))
                {
                    var go=new GameObject("Wall joint");go.transform.SetParent(floor.transform,false);
                    joint=go.AddComponent<CornerJoint>();joint.corner=item.corner.id;
                    Undo.RegisterCreatedObjectUndo(go,"Fit corner post");
                }
                float height=floor.height;
                if(incident[item.corner.id].Any(w=>w.slabSpans!=null && w.slabSpans.Any(span=>
                    w.startCorner==item.corner.id ? span.x<.0002f : span.y>w.length-.0002f)))height+=floor.House.slabThickness;
                var size=mesh.bounds.size;
                var offset=Vector3.Scale(new Vector3(mesh.bounds.center.x,mesh.bounds.min.y,mesh.bounds.center.z),
                    new Vector3(item.width/size.x,height/size.y,item.width/size.z));
                var rotation=Quaternion.Euler(0,-Mathf.Atan2(item.axis.y,item.axis.x)*Mathf.Rad2Deg,0);
                var position=new Vector3(item.corner.point.x,0,item.corner.point.y)-rotation*offset;
                if(joint.post!=mesh || joint.postWidth!=item.width || joint.postDirection!=item.axis ||
                    joint.height!=height || joint.transform.localPosition!=position)
                {
                    Undo.RecordObjects(new Object[]{joint,joint.transform},"Fit corner post");
                    joint.post=mesh;joint.postWidth=item.width;joint.postDirection=item.axis;joint.height=height;
                    joint.profile=null;joint.transform.localPosition=position;joint.Refresh();
                }
                var material=WallFaces.StructuralFinish(host);
                var renderer=joint.GetComponent<MeshRenderer>();
                if(renderer.sharedMaterial!=material){Undo.RecordObject(renderer,"Finish wall joint");renderer.sharedMaterial=material;}
            }
            if (floor.House.booleanCuts.Count > 0)
                foreach (var joint in floor.GetComponentsInChildren<CornerJoint>(true)) BooleanOutput.Apply(floor.House, joint);
            foreach(var joint in old.Values)HouseOwnership.DestroyGeometry(joint,floor.transform,"Rebuild corner");
            foreach(var w in walls)
            {
                ends.TryGetValue((w,w.startCorner),out var a); ends.TryGetValue((w,w.endCorner),out var b);
                float start=a?.cut??0,end=b?.cut??0;
                float front=a?.clear??0,back=b?.clear??0;
                if(w.startPostInset==start && w.endPostInset==end && w.startCornerClearance==front && w.endCornerClearance==back)continue;
                Undo.RecordObject(w,"Fit wall ends");
                w.startPostInset=start;w.endPostInset=end;w.startCornerClearance=front;w.endCornerClearance=back;
                if (!WallBuilder.Rebuild(w,out issue)) return false;
            }
            return true;
        }
    }
}
