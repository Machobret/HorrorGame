using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Weapons
{
    // A fired shot must outlive attack interruption, reloading or unequipping.
    public sealed class WeaponShotAudio : MonoBehaviour, IWeaponShotAudio
    {
        [Range(0, 2)] public float Volume = 1;

        public void Play(AudioClip clip)
        {
            if (!clip) return;
            var settings = GetComponent<AudioSource>();
            var voice = new GameObject("Weapon shot audio");
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.outputAudioMixerGroup = settings ? settings.outputAudioMixerGroup : null;
            source.volume = Mathf.Clamp01(settings ? settings.volume : 1);
            source.pitch = settings ? Mathf.Max(.1f, settings.pitch) : 1;
            source.clip = clip;
            source.PlayOneShot(clip, Volume);
            Destroy(voice, clip.length / source.pitch + .1f);
        }
    }
}
