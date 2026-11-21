using UnityEngine;
using System;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class InteractionStartMessage : BaseMessage
    {
    }
    public class InteractionEndMessage : BaseMessage
    {
    }

    [Serializable]
    public class OnInteractionEvent : UnityEvent<IInteractor> {}

    public interface IInteractionReach
    {
        bool IsReachableFrom(Vector3 position);
    }

    [RequireComponent(typeof(OnDisableNotifier))]
    public class Interactive : MonoBehaviour
    {
        public InteractionData Data;
        public bool ShowPrompt = true;
        public OnInteractionEvent OnInteract;
        public UnityEvent OnFinishInteract;
        public UnityEvent OnDataChanged;
        public Sprite PromptIconOverride { get; private set; }
        public Func<bool> PowerAvailable { get; set; }
        public Func<Vector3, bool> CanReachFrom { get; set; }
        public bool LastInteractionCompleted { get; private set; }
        public string PromptTextOverride { get; private set; }
        private IInteractionReach m_ParentReach;

        private void Awake() => m_ParentReach = GetComponentInParent<IInteractionReach>();

        public bool IsReachableFrom(Vector3 position) =>
            (CanReachFrom?.Invoke(position) ?? true) && (m_ParentReach?.IsReachableFrom(position) ?? true);

        public void SetPromptText(string text)
        {
            if (PromptTextOverride == text) return;
            PromptTextOverride = text;
            OnDataChanged?.Invoke();
        }

        public void SetPromptIcon(Sprite icon)
        {
            if (PromptIconOverride == icon) return;
            PromptIconOverride = icon;
            OnDataChanged?.Invoke();
        }

        // --------------------------------------------------------------------

        public void SetInteractable(bool active)
        {
            gameObject.SetActive(active);
        }

        // --------------------------------------------------------------------

        public void Interact(IInteractor interactor)
        {
            if (!isActiveAndEnabled)
                return;

            if ((CanReachFrom != null || m_ParentReach != null) &&
                (interactor is not Component source || !IsReachableFrom(source.transform.position)))
                return;

            if (interactor is PlayerInteractor)
                HorrorGame.UI.GameplayNotifications.DismissAll();
            if (PowerAvailable != null && !PowerAvailable())
            {
                HorrorGame.UI.PlayerMonologue.Show("No power");
                return;
            }

            OnInteract?.Invoke(interactor);
            MessageBuffer<InteractionStartMessage>.Dispatch();
        }

        // --------------------------------------------------------------------

        public void Finish() => Finish(true);

        internal void Finish(bool completed)
        {
            LastInteractionCompleted = completed;
            MessageBuffer<InteractionEndMessage>.Dispatch();
            OnFinishInteract?.Invoke();
        }


        // --------------------------------------------------------------------

        public void SetInteractionData(InteractionData data)
        {
            Data = data;
            OnDataChanged?.Invoke();
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (TryGetComponent(out Collider collider))
            {
                DebugUtils.DrawCollider(collider, new Color(0f, 1f, 0f, 0.5f), 0);
            }
            
        }
    }
}
