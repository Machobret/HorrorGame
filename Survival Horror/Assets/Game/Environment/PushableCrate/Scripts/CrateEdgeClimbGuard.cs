using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Environment
{
    /// <summary>Resists walking off the small crate; sustained outward input requests a native drop.</summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class CrateEdgeClimbGuard : MonoBehaviour
    {
        CrateClimbRig rig;
        PlayerMovement movement;
        Actor actor;
        Vector3 edge;
        bool pending;
        public float HeldTime { get; private set; }
        public bool IsGuarding => rig;

        public void Bind(CrateClimbRig owner)
        {
            Detach();
            rig=owner;actor=GetComponent<Actor>();movement=GetComponent<PlayerMovement>();
            if (!rig || !actor || !movement) { Detach(); return; }
            movement.FilterMovement+=Filter;
        }

        public void Detach()
        {
            if (movement) movement.FilterMovement-=Filter;
            rig=null;movement=null;HeldTime=0;edge=Vector3.zero;pending=false;
        }

        void Filter(ref Vector3 displacement)
        {
            if (!rig || !rig.IsPlayerOnTop(actor)) { Detach(); return; }
            var root=rig.CrateBody.transform;
            Vector3 current=root.InverseTransformPoint(actor.transform.position);
            Vector3 proposed=root.InverseTransformPoint(actor.transform.position+displacement);
            float xLimit=Mathf.Max(.1f,1f-rig.EdgeInset/Mathf.Abs(root.lossyScale.x));
            float zLimit=Mathf.Max(.1f,1f-rig.EdgeInset/Mathf.Abs(root.lossyScale.z));
            Vector3 safe=proposed;
            safe.x=Mathf.Clamp(proposed.x,-xLimit,xLimit);safe.z=Mathf.Clamp(proposed.z,-zLimit,zLimit);
            Vector3 blocked=proposed-safe;blocked.y=0;
            bool outward=blocked.sqrMagnitude>.000001f && Vector3.Dot(blocked,proposed-current)>.000001f;
            if (!outward)
            {
                HeldTime=0;edge=Vector3.zero;pending=false;return;
            }
            Vector3 allowed=root.TransformPoint(safe)-actor.transform.position;
            allowed.y=displacement.y;displacement=allowed;
            if (!(actor.StateController.CurrentState is PlayerStateMotion))
            { HeldTime=0;edge=Vector3.zero;pending=false;return; }
            Vector3 direction=Mathf.Abs(blocked.x)>Mathf.Abs(blocked.z)
                ? Vector3.right*Mathf.Sign(blocked.x) : Vector3.forward*Mathf.Sign(blocked.z);
            // Keep one exit selected while diagonal input continues outward at a corner.
            if (edge!=Vector3.zero && Vector3.Dot(edge,proposed-current)>.00001f) direction=edge;
            if (direction!=edge) { edge=direction;HeldTime=0; }
            HeldTime+=Time.fixedDeltaTime;
            if (HeldTime>=rig.EdgeHoldDuration) pending=true;
        }

        void Update()
        {
            if (!rig) { Detach();return; }
            if (!rig.IsPlayerOnTop(actor)) { Detach();return; }
            if (!(actor.StateController.CurrentState is PlayerStateMotion) ||
                (PauseController.Exists && PauseController.Instance.IsPaused))
            { HeldTime=0;edge=Vector3.zero;pending=false;return; }
            if (!pending) return;
            var owner=rig;Vector3 outward=owner.CrateBody.transform.TransformDirection(edge);
            pending=false;
            owner.DropFromEdge(actor,outward);
        }
        void OnDisable() { Detach(); }
        void OnDestroy() { Detach(); }
    }
}
