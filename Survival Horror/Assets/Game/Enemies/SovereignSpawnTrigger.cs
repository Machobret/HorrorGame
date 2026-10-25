using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Enemies
{
    /// <summary>Drop this on a trigger volume, or invoke TriggerSovereign from any scripted event.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SovereignSpawnTrigger : MonoBehaviour
    {
        [SerializeField] private SovereignController m_SovereignPrefab;
        [SerializeField] private bool m_AutoTriggerOnStart;
        [SerializeField] private bool m_OneShot = true;
        private SovereignController m_Instance;
        private bool m_Used;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void Start()
        {
            if (m_AutoTriggerOnStart) TriggerSovereign();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerActor>()) TriggerSovereign();
        }

        [ContextMenu("Test Trigger Sovereign")]
        public void TriggerSovereign()
        {
            if (m_Used && m_OneShot) return;
            Transform player = GameManager.Exists && GameManager.Instance.Player ? GameManager.Instance.Player.transform : null;
            if (!player || !m_SovereignPrefab)
            {
                Debug.LogWarning("SovereignSpawnTrigger needs a player and Sovereign prefab.", this);
                return;
            }
            if (!m_Instance) m_Instance = Instantiate(m_SovereignPrefab);
            if (m_Instance.Trigger(player)) m_Used = true;
        }
    }
}
