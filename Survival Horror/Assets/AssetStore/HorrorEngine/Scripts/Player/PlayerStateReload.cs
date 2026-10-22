using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [DefaultExecutionOrder(240)]
    public class PlayerStateReload : ActorStateWithDuration
    {
        private static readonly string k_EventAmmoAttachToSocket = "AmmoAttachToSocket";
        private static readonly string k_EventAmmoAttachToWeapon = "AmmoAttachToWeapon";

        private AudioSource m_AudioSource;
        private InventoryEntry m_WeaponEntry;
        private ReloadableWeaponData m_Weapon;
        private UnityAction<AnimationEvent> m_OnAnimatorEvent;
        private PlayerEquipment m_Equipment;
        private WeaponAmmoVisuals m_WeaponAmmoVisuals;
        private SocketController m_SocketController;
        private PlayerMovement m_Movement;
        private IPlayerInput m_Input;
        private PlayerStateMotion m_MotionState;
        private PlayerStateAiming m_AimingState;
        private bool m_AimReleasedDuringReload, m_ReturningToAim;
        private bool ShouldReturnToAim => StartedAimed && !m_AimReleasedDuringReload && m_Input.IsAimingHeld() && m_AimingState;
        private IWeaponReloadPresentation[] m_ReloadPresentations;
        private bool m_ReloadCompleted;
        public bool StartedAimed { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress => Mathf.Clamp01(m_TimeInState / Mathf.Max(.1f, m_Duration));
        private bool m_ReturnBlendStarted;
        private static readonly int k_PlaybackSpeed = Animator.StringToHash("ReloadPlaybackSpeed");
        private bool m_PlaybackConfigured, m_HasPlaybackParameter;
        public float AimReturnBlend => IsReloading && m_ReturningToAim && ShouldReturnToAim ?
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.86f, .99f, m_TimeInState / Mathf.Max(.1f, m_Duration))) : 0;
        private Transform m_RightShoulder, m_LeftShoulder;
        private Transform m_RightUpper, m_RightLower, m_RightHand, m_LeftUpper, m_LeftLower, m_LeftHand;
        public AnimatorStateHandle MobileReloadAnimation;
        private AnimatorStateHandle m_DefaultReloadAnimation;
        private bool m_UsingMobileAnimation;
        private readonly Dictionary<AnimatorLayerBlend, int> m_LayerOverrides = new Dictionary<AnimatorLayerBlend, int>();

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_OnAnimatorEvent = OnAnimatorEvent;
            m_AudioSource = GetComponentInParent<AudioSource>();
            m_Equipment = GetComponentInParent<PlayerEquipment>();
            m_SocketController = GetComponentInParent<SocketController>();
            m_Movement = GetComponentInParent<PlayerMovement>();
            m_DefaultReloadAnimation = m_AnimationState;
            m_Input = GetComponentInParent<IPlayerInput>();
            m_MotionState = Actor.GetComponentInChildren<PlayerStateMotion>(true);
            m_AimingState = Actor.GetComponentInChildren<PlayerStateAiming>(true);
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            var weaponInstance = m_Equipment.GetWeaponInstance(GameManager.Instance.Inventory.WeaponSlot).GetComponent<Weapon>();
            m_WeaponAmmoVisuals = weaponInstance ? weaponInstance.GetComponent<WeaponAmmoVisuals>() : null;
            m_ReloadPresentations = weaponInstance ? weaponInstance.GetComponents<IWeaponReloadPresentation>() : null;
            m_ReloadCompleted = false;
            StartedAimed = fromState is PlayerStateAiming || (fromState is PlayerStateAttack && m_Input.IsAimingHeld());
            IsReloading = true;
            m_AimReleasedDuringReload = !m_Input.IsAimingHeld();
            m_ReturningToAim = false;
            m_ReturnBlendStarted = false;
            var animator = Actor.MainAnimator;
            m_PlaybackConfigured = false;
            m_HasPlaybackParameter = false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == k_PlaybackSpeed) { m_HasPlaybackParameter = true; break; }
            if (m_HasPlaybackParameter) animator.SetFloat(k_PlaybackSpeed, 1);
            if (animator.isHuman)
            {
                m_RightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                m_LeftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                m_RightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                m_RightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                m_RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                m_LeftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                m_LeftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                m_LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }

            m_WeaponEntry = GameManager.Instance.Inventory.GetEquippedWeapon();
            m_Weapon = m_WeaponEntry.Item as ReloadableWeaponData;
            Debug.Assert(m_Weapon, "ReloadableWeapon not equipped, it is assumed it'll be equipped in the primary equipment slot");

            m_Duration = m_Weapon.ReloadDuration;
            m_UsingMobileAnimation = m_Weapon.AllowMovementDuringReload && MobileReloadAnimation &&
                Actor.MainAnimator.HasState(MobileReloadAnimation.Layer, MobileReloadAnimation.Hash);
            m_AnimationState = m_UsingMobileAnimation ? MobileReloadAnimation : m_DefaultReloadAnimation;
            if (m_UsingMobileAnimation)
            {
                RedirectLayerBlends(OnStateEnter);
                RedirectLayerBlends(OnStateExit);
            }

            base.StateEnter(fromState);
            // Attack/aim states disable movement on exit. Opt-in reloads must
            // explicitly enable it while the reload timer and animation run.
            if (m_Movement)
            {
                m_Movement.AddConstrain(PlayerMovement.MovementConstrain.Running);
                m_Movement.enabled = m_Weapon.AllowMovementDuringReload;
            }

            if (m_AudioSource && m_Weapon.ReloadSound) m_AudioSource.PlayOneShot(m_Weapon.ReloadSound);

            UIManager.Get<UIInputListener>().AddBlockingContext(this);

            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.AddListener(m_OnAnimatorEvent);

            if (m_ReloadPresentations != null)
            {
                var reserves = new List<InventoryEntry>();
                GameManager.Instance.Inventory.GetAll(m_Weapon.AmmoItem, reserves);
                float available = 0;
                foreach (var reserve in reserves) available += Mathf.Max(0, reserve.Count);
                int rounds = Mathf.FloorToInt(Mathf.Min(available, Mathf.Max(0, m_Weapon.MaxAmmo - m_WeaponEntry.SecondaryCount)));
                foreach (var presentation in m_ReloadPresentations)
                    presentation.BeginReload(m_Duration, m_WeaponEntry.SecondaryCount, rounds);
            }
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            IsReloading = false;
            if (m_ReturningToAim && !(intoState is PlayerStateAiming))
                m_AimingState.BlendReloadReturn(false, .1f);
            m_ReturningToAim = false;
            if (m_HasPlaybackParameter) Actor.MainAnimator.SetFloat(k_PlaybackSpeed, 1);
            if (m_ReloadPresentations != null)
            {
                foreach (var presentation in m_ReloadPresentations)
                    if (presentation is Component component && component)
                        presentation.EndReload(m_ReloadCompleted);
                m_ReloadPresentations = null;
            }
            if (m_WeaponAmmoVisuals)
            {
                m_WeaponAmmoVisuals.AttachAmmoToWeapon();
                if (GameManager.Exists && GameManager.Instance.Inventory.GetEquippedWeapon() != null)
                    m_WeaponAmmoVisuals.UpdateVisibility();
            }
            // Interrupted reloads must not load ammunition.
            if (m_Movement)
            {
                m_Movement.enabled = false;
                m_Movement.RemoveConstrain(PlayerMovement.MovementConstrain.Running);
            }

            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);
            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.RemoveListener(m_OnAnimatorEvent);

            base.StateExit(intoState);
            if (m_UsingMobileAnimation)
                Actor.MainAnimator.Play("Empty", MobileReloadAnimation.Layer, 0);
            foreach (var entry in m_LayerOverrides) entry.Key.RuntimeLayerOverride = entry.Value;
            m_LayerOverrides.Clear();
            m_AnimationState = m_DefaultReloadAnimation;
            m_UsingMobileAnimation = false;
        }

        public override void StateUpdate()
        {
            m_AimReleasedDuringReload |= !m_Input.IsAimingHeld();
            base.StateUpdate();
            ConfigurePlaybackSpeed();
            float progress = Mathf.Clamp01(m_TimeInState / Mathf.Max(.1f, m_Duration));
            if (!m_ReturnBlendStarted && progress >= .82f && m_AnimationState)
            {
                m_ReturnBlendStarted = true;
                var blends = Actor.MainAnimator.GetComponent<AnimatorLayerBlendHandler>();
                float remaining = Mathf.Max(.01f, m_Duration - m_TimeInState);
                if (blends) blends.StartBlend(m_AnimationState.Layer, 0, remaining);
                m_ReturningToAim = ShouldReturnToAim;
                if (m_ReturningToAim) m_AimingState.BlendReloadReturn(true, remaining);
            }
            else if (m_ReturningToAim && !ShouldReturnToAim)
            {
                // Releasing aim even during the final blend cancels the return.
                m_ReturningToAim = false;
                m_AimingState.BlendReloadReturn(false, Mathf.Max(.01f, m_Duration - m_TimeInState));
            }
        }

        private void ConfigurePlaybackSpeed()
        {
            if (m_PlaybackConfigured || !m_HasPlaybackParameter || !m_AnimationState) return;
            var animator = Actor.MainAnimator;
            int layer = m_AnimationState.Layer;
            bool next = animator.IsInTransition(layer);
            var state = next ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
            if (state.shortNameHash != Animator.StringToHash("WeaponReload")) return;
            var clips = next ? animator.GetNextAnimatorClipInfo(layer) : animator.GetCurrentAnimatorClipInfo(layer);
            if (clips.Length == 0 || !clips[0].clip) return;
            float clipLeft = clips[0].clip.length * (1 - Mathf.Clamp01(state.normalizedTime));
            float timeLeft = Mathf.Max(.05f, m_Duration - m_TimeInState);
            // Only reload states use this parameter: walking and aiming retain
            // their own speed, and clip events advance normally without seeking.
            animator.SetFloat(k_PlaybackSpeed, Mathf.Max(.01f, clipLeft / (timeLeft * Mathf.Max(.01f, state.speed))));
            m_PlaybackConfigured = true;
        }

        private void LateUpdate()
        {
            if (!IsReloading || StartedAimed || !m_RightHand || !m_LeftHand ||
                (m_ReloadPresentations != null && m_ReloadPresentations.Length > 0)) return;
            // The template's pistol, shotgun and rifle reload clips are performed
            // at aim height. Their relaxed variants keep the same mechanism action
            // but work lower and nearer the body. Dedicated flare/revolver IK owns
            // its own grip positions, so it must not be solved twice here.
            float t = Mathf.Clamp01(m_TimeInState / Mathf.Max(.1f, m_Duration));
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .15f)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.80f, 1, t)));
            var actor = Actor.transform;
            Quaternion turn = Quaternion.AngleAxis(28, actor.right);
            Vector3 pivot = (m_RightUpper.position + m_LeftUpper.position) * .5f - actor.up * .15f;
            Vector3 shift = -actor.forward * .045f - actor.up * .025f;
            Vector3 right = pivot + turn * (m_RightHand.position - pivot) + shift;
            Vector3 left = pivot + turn * (m_LeftHand.position - pivot) + shift;
            Quaternion rightRotation = turn * m_RightHand.rotation, leftRotation = turn * m_LeftHand.rotation;
            // Only lower the gun-handling part of the clip. The pocket reach must
            // stay beside the hip instead of rotating down/back into the body.
            Vector3 pocket = actor.InverseTransformPoint(m_LeftHand.position);
            float atPocket = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.94f, 1.16f, pocket.y));
            bool raisedPocket = m_Weapon.name == "Gun" || m_Weapon.name == "Shotgun";
            pocket.x = Mathf.Min(-.18f, pocket.x);
            pocket.y = Mathf.Max(.94f, pocket.y);
            pocket.z = Mathf.Max(.015f, pocket.z);
            // Lift the retrieving arm away from the hanging pose on these two
            // clips. The higher/outward pole raises the elbow; the hand stays
            // near the waist so the forearm visibly folds toward the pocket.
            if (raisedPocket)
            {
                pocket.y = Mathf.Max(1.08f, pocket.y);
                pocket.z = Mathf.Max(.12f, pocket.z);
            }
            Vector3 elbowPole = raisedPocket
                ? new Vector3(-.65f, 1.65f, .12f)
                : new Vector3(-.38f, 1.10f, .06f);
            left = Vector3.Lerp(left, actor.TransformPoint(pocket), atPocket);
            if (raisedPocket)
            {
                Vector3 raisedHand = actor.InverseTransformPoint(left);
                raisedHand.y = Mathf.Max(1.13f, raisedHand.y);
                raisedHand.z = Mathf.Max(.10f, raisedHand.z);
                left = actor.TransformPoint(raisedHand);
            }
            leftRotation = Quaternion.Slerp(leftRotation, m_LeftHand.rotation, atPocket);
            ReloadArmIK.Solve(m_RightUpper, m_RightLower, m_RightHand, actor, right, rightRotation,
                new Vector3(.32f, 1.06f, .04f), weight, m_RightShoulder, .4f);
            ReloadArmIK.Solve(m_LeftUpper, m_LeftLower, m_LeftHand, actor, left, leftRotation,
                elbowPole, weight, m_LeftShoulder, .7f + .3f * atPocket, atPocket,
                raisedPocket ? 40f : 0);
        }

        private void RedirectLayerBlends(UnityEvent callbacks)
        {
            for (int i = 0; i < callbacks.GetPersistentEventCount(); ++i)
            {
                var blend = callbacks.GetPersistentTarget(i) as AnimatorLayerBlend;
                if (!blend || m_LayerOverrides.ContainsKey(blend)) continue;
                m_LayerOverrides.Add(blend, blend.RuntimeLayerOverride);
                blend.RuntimeLayerOverride = MobileReloadAnimation.Layer;
            }
        }

        // --------------------------------------------------------------------

        public void OnAnimatorEvent(AnimationEvent e)
        {
            if (m_WeaponAmmoVisuals != null)
            {
                if (e.stringParameter == k_EventAmmoAttachToSocket)
                {
                    m_WeaponAmmoVisuals.AttachAmmoToSocket(m_SocketController);
                }
                else if (e.stringParameter == k_EventAmmoAttachToWeapon)
                {
                    m_WeaponAmmoVisuals.AttachAmmoToWeapon();
                }
            }
        }

        // --------------------------------------------------------------------

        protected override void OnStateDurationEnd()
        {
            Reload();
            m_ReloadCompleted = true;
            if (ShouldReturnToAim)
                SetState(m_AimingState);
            else
            {
                // Reloads begun lowered, or interrupted by an aim release, stay
                // lowered until a fresh aim press after completion.
                m_Input.RequireAimRelease();
                SetState(m_MotionState ? m_MotionState : m_ExitState);
            }
        }

        public virtual void Reload()
        {
            Debug.Assert(m_Weapon.AmmoItem != null, "Weapon can not reload, AmmoItem is null in the WeaponData");

            GameManager.Instance.Inventory.ReloadWeapon(m_WeaponEntry);
        }

    }
}
