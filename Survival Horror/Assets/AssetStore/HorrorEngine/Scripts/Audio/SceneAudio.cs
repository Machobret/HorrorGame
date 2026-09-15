using UnityEngine;

namespace HorrorEngine
{
    public abstract class SceneAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip m_Audio;
        [SerializeField] private float m_Volume = 1f;
        [SerializeField] private bool m_PlayOnStart;

        private float m_Time;

        protected abstract AudioStack GetStack();

        private void Start()
        {
            if (m_PlayOnStart)
            {
                Play();
            }
        }

        public void Play()
        {
            GetStack().Play(m_Audio, m_Volume);
        }

        public void Stop()
        {
            GetStack().Stop();
        }

        public void FadeIn(float duration = 1f)
        {
            GetStack().Play(m_Audio, m_Volume);
            GetStack().FadeIn(duration);
        }

        public void FadeOut(float duration = 1f)
        {
            GetStack().FadeOut(duration);
        }

        public void Push(float duration = 1f)
        {
            GetStack().Push(m_Audio, m_Volume, duration, m_Time);
        }

        public void Pop(float duration = 1f)
        {
            GetStack().Pop(m_Audio, duration, out m_Time);
        }
    }
}
