using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.UI
{
    [AddComponentMenu("Horror Game/Player Monologue Trigger")]
    public sealed class PlayerMonologueTrigger : MonoBehaviour
    {
        [SerializeField, TextArea] private string text;
        [SerializeField, Min(.01f)] private float fadeIn = .8f;
        [SerializeField, Min(0f)] private float hold = 3f;
        [SerializeField, Min(.01f)] private float fadeOut = 1f;
        [SerializeField] private bool playOnce = true;
        [SerializeField] private bool holdWhileInside;
        private bool played;
        private readonly HashSet<Collider> occupants = new();

        public void Play()
        {
            if (!isActiveAndEnabled || (playOnce && played)) return;
            played = PlayerMonologue.Show(text, fadeIn, hold, fadeOut);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || !GameManager.Exists || !GameManager.Instance.Player ||
                other.GetComponentInParent<Actor>() != GameManager.Instance.Player) return;
            if (!holdWhileInside)
            {
                Play();
                return;
            }
            if (occupants.Add(other) && occupants.Count == 1 && !(playOnce && played))
                played = PlayerMonologue.ShowOwned(text, fadeIn, hold, fadeOut, this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (occupants.Remove(other) && occupants.Count == 0)
                PlayerMonologue.Release(this);
        }

        private void OnDisable()
        {
            if (occupants.Count == 0) return;
            occupants.Clear();
            PlayerMonologue.Release(this);
        }
    }
}
