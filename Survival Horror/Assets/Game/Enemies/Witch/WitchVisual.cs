using UnityEngine;

namespace HorrorGame.Enemies
{
    [ExecuteAlways]
    public sealed class WitchVisual : MonoBehaviour
    {
        private GameObject m_Model;
        private Animation m_Animation;
        private string m_Clip = "Idle";
        private float m_Duration;

        private void OnEnable() { Build(); }
        private void Update()
        {
            if (!m_Model) Build();
            if (!Application.IsPlaying(gameObject) || !m_Animation) return;
            if (m_Animation[m_Clip] != null)
                m_Animation[m_Clip].speed = HorrorEngine.PauseController.Exists && HorrorEngine.PauseController.Instance.IsPaused ? 0f :
                    m_Duration > 0f ? m_Animation[m_Clip].length / m_Duration : 1f;
        }

        private void Build()
        {
            if (m_Model) return;
            var asset = Resources.Load<GameObject>("Witch/Witch_Model");
            if (!asset) return;
            m_Model = Instantiate(asset, transform, false);
            m_Model.name = "Witch model"; m_Model.hideFlags = HideFlags.DontSave;
            var animator = m_Model.GetComponent<Animator>();
            if (animator) animator.enabled = false;
            m_Animation = m_Model.GetComponent<Animation>();
            if (!m_Animation) m_Animation = m_Model.AddComponent<Animation>();
            m_Animation.playAutomatically = false;
            m_Animation.cullingType = AnimationCullingType.AlwaysAnimate;
            var material = Resources.Load<Material>("Witch/Witch_Skin");
            foreach (var renderer in m_Model.GetComponentsInChildren<Renderer>())
            {
                if (material) renderer.sharedMaterial = material;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            foreach (var clip in Resources.LoadAll<AnimationClip>("Witch/Witch_Model"))
            {
                if (clip.name.StartsWith("__preview__") || !clip.legacy) continue;
                m_Animation.AddClip(clip, clip.name);
                m_Animation[clip.name].wrapMode = clip.name == "Idle" || clip.name == "Chase" ? WrapMode.Loop : WrapMode.ClampForever;
            }
            Play(m_Clip, m_Duration);
        }

        public void Play(string clip, float duration = 0f)
        {
            m_Clip = clip; m_Duration = duration;
            if (!m_Animation || m_Animation[clip] == null) return;
            m_Animation[clip].speed = duration > 0f ? m_Animation[clip].length / duration : 1f;
            if (Application.IsPlaying(gameObject)) m_Animation.CrossFade(clip, .12f);
            else { m_Animation.Play(clip); m_Animation[clip].time = 0f; m_Animation.Sample(); }
        }

        private void OnDisable()
        {
            if (!m_Model) return;
            if (Application.IsPlaying(gameObject)) Destroy(m_Model); else DestroyImmediate(m_Model);
            m_Model = null; m_Animation = null;
        }
    }
}
