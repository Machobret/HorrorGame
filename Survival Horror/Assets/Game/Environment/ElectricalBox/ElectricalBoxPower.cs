using System;
using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace Game.Environment
{
    [DisallowMultipleComponent, RequireComponent(typeof(ElectricalBoxIndicator))]
    public sealed class ElectricalBoxPower : MonoBehaviour, ISavableObjectStateExtra
    {
        [SerializeField] private bool startsOn = true;
        [SerializeField] private bool requiresTabletAuthorization;
        [SerializeField] private Transform leverPivot;
        [SerializeField] private Interactive leverInteraction;
        private bool? on;
        private bool tabletAuthorized;
        private bool requestedPower;
        private Coroutine leverMotion;

        private bool LeverOn => on ?? startsOn;
        public bool RequiresTabletAuthorization => requiresTabletAuthorization;
        public bool IsAuthorized => !requiresTabletAuthorization || tabletAuthorized;
        public bool IsOn => IsAuthorized && LeverOn;
        public event Action<bool> PowerChanged;
        public event Action AuthorizationChanged;

        private void Awake() => ApplyPose();

        private void OnEnable()
        {
            if (!leverInteraction) return;
            leverInteraction.PowerAvailable = CanUseLever;
            leverInteraction.OnInteract.AddListener(UseLever);
            leverInteraction.OnFinishInteract.AddListener(FinishLever);
        }

        private void OnDisable()
        {
            if (leverInteraction)
            {
                if (leverInteraction.PowerAvailable == CanUseLever) leverInteraction.PowerAvailable = null;
                leverInteraction.OnInteract.RemoveListener(UseLever);
                leverInteraction.OnFinishInteract.RemoveListener(FinishLever);
            }
            CancelLeverMotion();
        }

        private bool CanUseLever() => IsAuthorized;

        public bool SetTabletAuthorization(bool authorized)
        {
            if (!requiresTabletAuthorization || tabletAuthorized == authorized) return false;
            bool wasPowered = IsOn;
            CancelLeverMotion();
            tabletAuthorized = authorized;
            ApplyPowerState(wasPowered);
            AuthorizationChanged?.Invoke();
            if (ObjectStateManager.Exists && TryGetComponent(out SavableObjectState state)) state.SaveState();
            return true;
        }

        private void UseLever(IInteractor interactor)
        {
            var interaction = leverInteraction.Data;
            if (!IsAuthorized || leverMotion != null || !leverPivot || !interaction) return;
            requestedPower = !LeverOn;
            float travelTime = Mathf.Max(.01f, interaction.InteractionDuration - interaction.InteractionDelay);
            leverMotion = StartCoroutine(MoveLever(travelTime));
        }

        private IEnumerator MoveLever(float duration)
        {
            Quaternion start = leverPivot.localRotation;
            Quaternion end = GetLeverRotation(requestedPower);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                leverPivot.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            leverPivot.localRotation = end;
        }

        private void FinishLever()
        {
            if (leverMotion == null) return;
            StopCoroutine(leverMotion);
            leverMotion = null;
            if (!leverInteraction.LastInteractionCompleted || !IsAuthorized)
            {
                ApplyPose();
                return;
            }
            bool wasPowered = IsOn;
            on = requestedPower;
            ApplyPowerState(wasPowered);
            if (ObjectStateManager.Exists && TryGetComponent(out SavableObjectState state)) state.SaveState();
        }

        private void CancelLeverMotion()
        {
            if (leverMotion == null) return;
            StopCoroutine(leverMotion);
            leverMotion = null;
            ApplyPose();
        }

        private void ApplyPowerState(bool wasPowered)
        {
            ApplyPose();
            if (wasPowered != IsOn) PowerChanged?.Invoke(IsOn);
        }

        private void ApplyPose()
        {
            if (leverPivot) leverPivot.localRotation = GetLeverRotation(LeverOn);
            GetComponent<ElectricalBoxIndicator>().SetBreakerState(IsAuthorized, LeverOn);
        }

        private static Quaternion GetLeverRotation(bool powered) => Quaternion.AngleAxis(powered ? -45f : 45f, Vector3.right);

        public string GetSavableData()
        {
            if (!requiresTabletAuthorization) return LeverOn ? "on" : "off";
            if (tabletAuthorized) return LeverOn ? "authorized:on" : "authorized:off";
            return LeverOn ? "disconnected:on" : "disconnected:off";
        }

        public void SetFromSavedData(string data)
        {
            if (data is not ("on" or "off" or "authorized:on" or "authorized:off" or
                "disconnected:on" or "disconnected:off")) return;
            bool wasPowered = IsOn;
            bool wasAuthorized = IsAuthorized;
            CancelLeverMotion();
            tabletAuthorized = data is "on" or "authorized:on" or "authorized:off";
            on = data is "on" or "authorized:on" or "disconnected:on";
            ApplyPowerState(wasPowered);
            if (wasAuthorized != IsAuthorized) AuthorizationChanged?.Invoke();
        }
    }
}
