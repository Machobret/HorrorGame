using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interactive), typeof(TwoSidedDoor))]
    public sealed class DoorDocumentPickup : MonoBehaviour
    {
        [SerializeField] private PickupDocument document;
        [SerializeField] private InteractionData pickupInteraction;

        private Interactive interaction;
        private TwoSidedDoor door;
        private InteractionData openInteraction;
        private bool collectingDocument;

        private bool HasDocument => document && document.isActiveAndEnabled;

        private void Awake()
        {
            interaction = GetComponent<Interactive>();
            door = GetComponent<TwoSidedDoor>();
            openInteraction = interaction.Data;
        }

        private void OnEnable()
        {
            interaction.OnFinishInteract.AddListener(OnInteractionFinished);
            RefreshInteraction();
        }

        private void OnDisable()
        {
            interaction.OnFinishInteract.RemoveListener(OnInteractionFinished);
            collectingDocument = false;
        }

        private void LateUpdate()
        {
            // The pickup's saved active state can be applied after the door is enabled.
            if (!collectingDocument)
                RefreshInteraction();
        }

        public void Interact(IInteractor interactor)
        {
            if (collectingDocument)
                return;

            if (HasDocument)
            {
                collectingDocument = true;
                document.Take();
                return;
            }

            door.Use(interactor);
        }

        private void OnInteractionFinished()
        {
            collectingDocument = false;
            RefreshInteraction();
        }

        private void RefreshInteraction()
        {
            InteractionData data = HasDocument ? pickupInteraction : openInteraction;
            if (interaction.Data != data)
                interaction.SetInteractionData(data);
        }
    }
}
