using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(AudioSource))]
    public class UIAudio : MonoBehaviour
    {
        private AudioSource m_AudioSource;

        private void Awake()
        {
            m_AudioSource = GetComponent<AudioSource>();
        }

        public void Play(AudioClip clip) => Play(clip,1f);

        public void Play(AudioClip clip, float volume)
        {
            if (clip)
                m_AudioSource.PlayOneShot(clip,Mathf.Clamp01(volume));
        }
    }
}
