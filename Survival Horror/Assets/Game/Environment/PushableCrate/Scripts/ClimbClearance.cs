using UnityEngine;

namespace HorrorGame.Environment
{
    /// <summary>Queries the real player capsule while the native climb animation disables it.</summary>
    public static class ClimbClearance
    {
        const float Margin=.025f;
        static void Capsule(CharacterController controller,Vector3 position,out Vector3 a,out Vector3 b,out float radius)
        {
            var scale=controller.transform.lossyScale;
            radius=controller.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float halfHeight=Mathf.Max(radius,controller.height*Mathf.Abs(scale.y)*.5f);
            var center=position+controller.transform.TransformVector(controller.center);
            // Small inset avoids treating the supporting floor as penetration.
            a=center+Vector3.up*(halfHeight-radius);b=center-Vector3.up*(halfHeight-radius);
            radius=Mathf.Max(.01f,radius-Margin);
        }
        static bool Blocks(CharacterController controller,Collider collider,Transform ignored)
        {
            return collider && !collider.isTrigger && !collider.transform.IsChildOf(controller.transform) &&
                (!ignored || !collider.transform.IsChildOf(ignored)) &&
                !Physics.GetIgnoreLayerCollision(controller.gameObject.layer,collider.gameObject.layer) &&
                !Physics.GetIgnoreCollision(controller,collider);
        }
        public static bool IsClear(CharacterController controller,Vector3 position,Transform ignored)
        {
            Capsule(controller,position,out var a,out var b,out float radius);
            foreach(var collider in Physics.OverlapCapsule(a,b,radius,~0,QueryTriggerInteraction.Ignore))
                if(Blocks(controller,collider,ignored))return false;
            return true;
        }
        public static bool CanMove(CharacterController controller,Vector3 from,Vector3 to,Transform ignored)
        {
            if(!IsClear(controller,from,ignored)||!IsClear(controller,to,ignored))return false;
            var delta=to-from;float distance=delta.magnitude;
            if(distance<.0001f)return true;
            Capsule(controller,from,out var a,out var b,out float radius);
            foreach(var hit in Physics.CapsuleCastAll(a,b,radius,delta/distance,distance,~0,QueryTriggerInteraction.Ignore))
                if(Blocks(controller,hit.collider,ignored))return false;
            return true;
        }
        public static bool HasSupport(CharacterController controller,Vector3 position,Transform ignored=null,
            bool checkFootprint=false)
        {
            Capsule(controller,position,out _,out var bottom,out float radius);
            var foot=bottom-Vector3.up*radius;
            if(!HasGround(controller,foot,ignored))return false;
            float inset=radius*.65f;
            return !checkFootprint ||
                (HasGround(controller,foot+Vector3.right*inset,ignored) &&
                 HasGround(controller,foot-Vector3.right*inset,ignored) &&
                 HasGround(controller,foot+Vector3.forward*inset,ignored) &&
                 HasGround(controller,foot-Vector3.forward*inset,ignored));
        }

        static bool HasGround(CharacterController controller,Vector3 foot,Transform ignored)
        {
            foreach(var hit in Physics.RaycastAll(foot+Vector3.up*.15f,Vector3.down,.4f,~0,QueryTriggerInteraction.Ignore))
                if(Blocks(controller,hit.collider,ignored)&&Vector3.Angle(hit.normal,Vector3.up)<=controller.slopeLimit)return true;
            return false;
        }
    }
}
