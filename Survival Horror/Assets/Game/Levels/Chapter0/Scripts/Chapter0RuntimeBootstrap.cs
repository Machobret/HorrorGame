using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter0
{
    /// <summary>
    /// Guarantees the persistent Horror Engine services exist before the menu
    /// transition asks GameManager to place the player in Chapter 0.
    /// </summary>
    public sealed class Chapter0RuntimeBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject m_GameManagerPrefab;
        [SerializeField] private GameObject m_CorePrefab;

        private void Awake()
        {
            if (!GameManager.Exists)
            {
                var manager = Instantiate(m_GameManagerPrefab);
                DontDestroyOnLoad(manager);
            }

            if (FindFirstObjectByType<HECore>() == null)
            {
                Instantiate(m_CorePrefab);
            }
        }
    }
}
