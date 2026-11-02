using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter0
{
    [RequireComponent(typeof(Collider))]
    public sealed class Chapter0TutorialTrigger : MonoBehaviour
    {
        public enum TriggerKind { Movement, Turn, TurnAround, Encounter }

        [SerializeField] private Chapter0TutorialSequence m_Sequence;
        [SerializeField] private TriggerKind m_Kind;
        private bool m_Used;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        public void SetAvailable(bool available)
        {
            m_Used = false;
            GetComponent<Collider>().enabled = available;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Used || other.GetComponentInParent<PlayerActor>() == null)
                return;

            m_Used = true;
            GetComponent<Collider>().enabled = false;
            if (m_Kind == TriggerKind.Movement) m_Sequence.TriggerMovement();
            else if (m_Kind == TriggerKind.Turn) m_Sequence.TriggerTurn();
            else if (m_Kind == TriggerKind.TurnAround) m_Sequence.TriggerTurnAround();
            else m_Sequence.TriggerEncounter();
        }
    }
}
