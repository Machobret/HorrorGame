using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class DepleteEquipment : MonoBehaviour
    {
        [SerializeField] private EquipableItemData m_Item;
        [SerializeField] private float m_Depletion = 0.01f;
        [SerializeField] private float m_DepletionFrequency = 1f;

        public UnityEvent<float> OnDepleted;

        private float? m_LastReportedStatus;

        private void Awake()
        {
            Debug.Assert(m_Depletion > 0, "Equipment m_Depletion amount can't be less than 0", gameObject);
            Debug.Assert(m_DepletionFrequency > 0, "Equipment m_DepletionFrequency can't be less than 0", gameObject);
        }

        private void OnEnable()
        {
            m_LastReportedStatus = null;
            UpdateCharge(false);
            InvokeRepeating(nameof(Deplete), m_DepletionFrequency, m_DepletionFrequency);
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

        private void Deplete()
        {
            UpdateCharge(true);
        }

        private void UpdateCharge(bool drain)
        {
            var game = GameManager.Instance;
            if (!game || game.CharacterState == null)
                return;

            var equipped = game.Inventory.GetEquipped(m_Item.Slot);
            if (equipped == null || equipped.Item != m_Item)
                return;

            if (drain)
                equipped.Status = Mathf.Clamp01(equipped.Status - m_Depletion);

            if (m_LastReportedStatus == equipped.Status)
                return;

            m_LastReportedStatus = equipped.Status;
            OnDepleted?.Invoke(equipped.Status);
        }
    }
}
