using HorrorEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Enemies
{
    [RequireComponent(typeof(Health))]
    public sealed class BruteAcidDeath : MonoBehaviour, IResetable
    {
        [SerializeField] private BruteAcidPool m_AcidPrefab;
        [SerializeField] private LayerMask m_GroundMask = 1;
        [SerializeField] private float m_BurstHeight = 1.5f;

        private Health m_Health;
        private Renderer[] m_Renderers;
        private bool[] m_RendererEnabled;
        private Collider[] m_Colliders;
        private bool[] m_ColliderEnabled;
        private bool m_Exploded;
        private BruteAcidPool m_ActivePool;

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_RendererEnabled = new bool[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++)
                m_RendererEnabled[i] = m_Renderers[i].enabled;
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_ColliderEnabled = new bool[m_Colliders.Length];
            for (int i = 0; i < m_Colliders.Length; i++)
                m_ColliderEnabled[i] = m_Colliders[i].enabled;
            m_Health.OnDeath.AddListener(OnDeath);
        }

        private void OnDeath(Health health)
        {
            if (m_Exploded || !m_AcidPrefab)
                return;
            m_Exploded = true;

            Vector3 position = transform.position;
            Vector3 normal = Vector3.up;
            float nearest = float.PositiveInfinity;
            // Ignore the Brute's own collider when projecting the pool onto the floor.
            foreach (var hit in Physics.RaycastAll(position + Vector3.up, Vector3.down,
                         4f, m_GroundMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > 0.5f && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    position = hit.point;
                    normal = hit.normal;
                }
            }

            var acid = Instantiate(m_AcidPrefab, position, Quaternion.FromToRotation(Vector3.up, normal));
            m_ActivePool = acid;
            SceneManager.MoveGameObjectToScene(acid.gameObject, gameObject.scene);
            var combatant = GetComponent<Combatant>();
            acid.Initialize(transform.position + Vector3.up * m_BurstHeight,
                combatant ? combatant.Faction : null, transform);

            foreach (var renderer in m_Renderers)
                if (renderer) renderer.enabled = false;
            // The body has dissolved; it must not remain as an invisible obstacle in the mist.
            foreach (var collider in m_Colliders)
                if (collider) collider.enabled = false;
        }

        public void OnReset()
        {
            m_Exploded = false;
            if (m_ActivePool) Destroy(m_ActivePool.gameObject);
            m_ActivePool = null;
            if (m_Renderers == null) return;
            for (int i = 0; i < m_Renderers.Length; i++)
                if (m_Renderers[i]) m_Renderers[i].enabled = m_RendererEnabled[i];
            for (int i = 0; i < m_Colliders.Length; i++)
                if (m_Colliders[i]) m_Colliders[i].enabled = m_ColliderEnabled[i];
        }

        private void OnDestroy()
        {
            if (m_Health) m_Health.OnDeath.RemoveListener(OnDeath);
        }
    }
}
