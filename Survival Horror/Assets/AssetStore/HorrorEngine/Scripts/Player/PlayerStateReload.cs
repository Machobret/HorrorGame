using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
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
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            var weaponInstance = m_Equipment.GetWeaponInstance(GameManager.Instance.Inventory.WeaponSlot).GetComponent<Weapon>();
            if (weaponInstance)
                m_WeaponAmmoVisuals = weaponInstance.GetComponent<WeaponAmmoVisuals>();

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
            if (m_Movement) m_Movement.enabled = m_Weapon.AllowMovementDuringReload;

            m_AudioSource.PlayOneShot(m_Weapon.ReloadSound);

            UIManager.Get<UIInputListener>().AddBlockingContext(this);

            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.AddListener(m_OnAnimatorEvent);
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            Reload();
            if (m_Movement) m_Movement.enabled = false;

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

        public virtual void Reload()
        {
            Debug.Assert(m_Weapon.AmmoItem != null, "Weapon can not reload, AmmoItem is null in the WeaponData");

            Inventory inventory = GameManager.Instance.Inventory;
            if (inventory.TryGet(m_Weapon.AmmoItem, out var ammoEntry))
                inventory.Combine(m_WeaponEntry, ammoEntry);
        }

    }
}
