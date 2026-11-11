using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Environment
{
    /// <summary>Moves the native climb markers to the approached crate edge and plants hands on that edge.</summary>
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(Climbable))]
    public sealed class CrateClimbRig : MonoBehaviour
    {
        public Rigidbody CrateBody;
        public Transform LeftHandContact;
        public Transform RightHandContact;
        [Range(0, 1)] public float HandPlantWeight = .6f;
        [Min(.01f)] public float EdgeHoldDuration = .25f;
        [Min(0)] public float EdgeInset = .08f;
        Actor actor;
        Animator animator;
        RigidbodyConstraints previousConstraints;
        bool previousKinematic, entered, dropping;
        float started;
        Vector3 restPosition;
        Quaternion restRotation;

        void Awake()
        {
            restPosition=transform.localPosition; restRotation=transform.localRotation;
        }

        // Persistent OnInteract listener runs before Climbable's runtime listener starts the native state.
        public void PrepareClimb(IInteractor interactor)
        {
            if (interactor is PlayerInteractor player)
                PrepareForActor(player.GetComponentInParent<Actor>(),null);
        }

        public bool IsPlayerOnTop(Actor player)
        {
            if (!player || !CrateBody) return false;
            var root=CrateBody.transform;
            Vector3 local=root.InverseTransformPoint(player.transform.position);
            float top=root.TransformPoint(new Vector3(0,2,0)).y;
            return Mathf.Abs(player.transform.position.y-top)<.18f && Mathf.Abs(local.x)<=1.15f && Mathf.Abs(local.z)<=1.15f;
        }

        public void DropFromEdge(Actor player,Vector3 outward)
        {
            if (actor || !IsPlayerOnTop(player)) return;
            var state=player.GetComponentInChildren<PlayerStateClimbing>();
            if (!state) return;
            PrepareForActor(player,outward);
            if (actor) state.StartClimb(GetComponent<Climbable>());
        }

        void PrepareForActor(Actor player,Vector3? requestedOutward)
        {
            if (actor || !CrateBody || !player) return;
            actor=player;
            var guard=actor.GetComponent<CrateEdgeClimbGuard>();
            if (guard) guard.Detach();
            animator=actor.MainAnimator;
            var root=CrateBody.transform;
            dropping=actor.transform.position.y-root.position.y>.55f;
            Vector3 approach=root.InverseTransformDirection(requestedOutward ?? (dropping ? actor.transform.forward : actor.transform.position-root.position));
            approach.y=0;
            Vector3 outward=Mathf.Abs(approach.x)>Mathf.Abs(approach.z)
                ? Vector3.right*(approach.x>=0 ? 1 : -1) : Vector3.forward*(approach.z>=0 ? 1 : -1);
            // The sample mesh is a two-unit cube, uniformly scaled to one metre on this prefab.
            transform.localPosition=outward;
            transform.localRotation=Quaternion.LookRotation(-outward);
            previousConstraints=CrateBody.constraints; previousKinematic=CrateBody.isKinematic;
            if (!previousKinematic) { CrateBody.linearVelocity=Vector3.zero; CrateBody.angularVelocity=Vector3.zero; }
            CrateBody.isKinematic=true;
            CrateBody.constraints=RigidbodyConstraints.FreezeAll;
            started=Time.time; entered=false;
        }

        void LateUpdate()
        {
            if (!actor) return;
            bool climbing=actor.StateController.CurrentState is PlayerStateClimbing;
            if (climbing) entered=true;
            else if (entered || Time.time-started>1f) { Release(); return; }
            if (!climbing || dropping || !animator || !animator.isHuman) return;
            float weight=Mathf.Clamp01(animator.GetFloat("ClimbAttachment"))*HandPlantWeight;
            if (weight<=.001f) return;
            Plant(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,LeftHandContact,new Vector3(-.5f,.8f,.15f),weight);
            Plant(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,RightHandContact,new Vector3(.5f,.8f,.15f),weight);
        }

        void Plant(HumanBodyBones upper,HumanBodyBones lower,HumanBodyBones hand,Transform contact,Vector3 pole,float weight)
        {
            var wrist=animator.GetBoneTransform(hand);
            if (!contact || !wrist) return;
            ReloadArmIK.Solve(animator.GetBoneTransform(upper),animator.GetBoneTransform(lower),wrist,actor.transform,
                contact.position,wrist.rotation,pole,weight);
        }

        void Release()
        {
            if (!actor) return;
            if (CrateBody) { CrateBody.constraints=previousConstraints; CrateBody.isKinematic=previousKinematic; }
            transform.localPosition=restPosition; transform.localRotation=restRotation;
            var player=actor;
            bool guardTop=entered && !dropping && isActiveAndEnabled && IsPlayerOnTop(player);
            actor=null;animator=null;entered=false;
            if (guardTop)
            {
                var guard=player.GetComponent<CrateEdgeClimbGuard>();
                if (!guard) guard=player.gameObject.AddComponent<CrateEdgeClimbGuard>();
                guard.Bind(this);
            }
        }
        void OnDisable() { Release(); }
    }
}
