using System;
using System.IO;
using System.Collections.Generic;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
namespace HorrorGame.Weapons.Editor
{
    [InitializeOnLoad]
    public static class MP5ClearanceReview
    {
        const string Key="MP5ClearanceReview";
        static int frames,hits;static string output;
        static readonly HumanBodyBones[] Bones={HumanBodyBones.LeftHand,HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftIndexIntermediate,HumanBodyBones.LeftIndexDistal,HumanBodyBones.LeftMiddleProximal,HumanBodyBones.LeftMiddleIntermediate,HumanBodyBones.LeftMiddleDistal,HumanBodyBones.LeftThumbProximal,HumanBodyBones.LeftThumbDistal};
        static MP5ClearanceReview(){EditorApplication.playModeStateChanged+=OnPlay;}
        public static void RunBatch(){SessionState.SetBool(Key,true);MP5AnimationValidation.RunBatch();}
        static void OnPlay(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;frames=hits=0;output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"MP5PoseRevisionReview/ReloadClearance.csv");File.WriteAllText(output,"time,progress,bone,insideReceiver,x,y,z\n");EditorApplication.update+=Tick;}
        static void Tick(){if(!GameManager.Exists||!GameManager.Instance.Player)return;var p=GameManager.Instance.Player;var reload=p.GetComponentInChildren<MP5ReloadVisual>();if(!reload||!reload.IsReloading)return;var body=Array.Find(reload.ModelFrame.GetComponentsInChildren<MeshFilter>(),m=>m.name=="MP5BodyFull");if(!body)return;++frames;foreach(var which in Bones){var bone=p.MainAnimator.GetBoneTransform(which);if(!bone)continue;var local=body.transform.InverseTransformPoint(bone.position);bool inside=Inside(body.sharedMesh,local);if(inside)++hits;File.AppendAllText(output,string.Join(",",Time.time,reload.ReloadProgress,which,inside,local.x,local.y,local.z)+"\n");}File.WriteAllText(Path.ChangeExtension(output,"json"),"{\"sampledFrames\":"+frames+",\"boneSamplesInsideReceiver\":"+hits+"}");}
        static bool Inside(Mesh mesh,Vector3 origin){if(!mesh.bounds.Contains(origin))return false;var vertices=mesh.vertices;var triangles=mesh.triangles;Vector3 direction=new Vector3(1,.127f,.083f).normalized;var distances=new List<float>();for(int n=0;n<triangles.Length;n+=3){Vector3 a=vertices[triangles[n]],e1=vertices[triangles[n+1]]-a,e2=vertices[triangles[n+2]]-a;Vector3 h=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-7)continue;float inv=1/det;Vector3 s=origin-a;float u=Vector3.Dot(s,h)*inv;if(u<0||u>1)continue;Vector3 q=Vector3.Cross(s,e1);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)continue;float t=Vector3.Dot(e2,q)*inv;if(t>.0005f&&!distances.Exists(x=>Mathf.Abs(x-t)<.0001f))distances.Add(t);}return distances.Count%2==1;}
    }
}