using System.Collections;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Small authored cutscene used after lighting the cigarette signal.</summary>
    public sealed class Chapter1LadderSignalCutscene : MonoBehaviour
    {
        [Header("Staging")]
        public Transform HighlightedPoint;
        public Transform Ladder;
        [Tooltip("Move this scene placeholder to the exact final location for the ladder.")]
        public Transform LadderLoweredPosition;
        [Min(.1f)] public float Duration = 3f;
        [Min(.1f)] public float RunToPointSeconds = 1.25f;
        [Min(.05f)] public float BarFadeSeconds = .35f;
        [Min(.05f)] public float LadderLowerSeconds = 1.2f;
        [Header("Placeholder presentation")]
        public AudioClip AlarmSound;
        public string LookAroundAnimationState = "LookAround";
        public Color HighlightColor = new Color(1f, .68f, .08f, 1f);

        private bool m_Playing;
        private float m_BarAlpha;
        private Texture2D m_White;
        private AudioSource m_Audio;

        public void Play()
        {
            if (!m_Playing) StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            m_Playing = true;
            yield return FadeBars(1f);
            while (!GameManager.Exists || GameManager.Instance.Player == null) yield return null;

            PlayerActor player = GameManager.Instance.Player;
            player.Disable(this);
            EnsureAudio();
            if (m_Audio && AlarmSound) m_Audio.PlayOneShot(AlarmSound);
            Animator animator = player.MainAnimator;
            yield return RunPlayerToHighlightedPoint(player, animator);

            // The remaining beat is deliberately still. The placeholder animation can
            // provide the look-around motion without rotating the actor transform.
            if (animator && !string.IsNullOrWhiteSpace(LookAroundAnimationState))
                animator.CrossFade(LookAroundAnimationState, .1f, 0);
            float lookElapsed = 0f;
            while (lookElapsed < Mathf.Max(0f, Duration - RunToPointSeconds))
            {
                lookElapsed += Time.deltaTime;
                yield return null;
            }

            yield return FadeBars(0f);
            player.Enable(this);
            if (Ladder) yield return LowerLadder();
            m_Playing = false;
        }

        private IEnumerator RunPlayerToHighlightedPoint(PlayerActor player, Animator animator)
        {
            if (!HighlightedPoint) yield break;

            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (!movement) yield break;

            Vector3 destination = HighlightedPoint.position;
            destination.y = player.transform.position.y;
            float elapsed = 0f;
            while (elapsed < RunToPointSeconds)
            {
                elapsed += Time.deltaTime;
                Vector3 current = player.transform.position;
                Vector3 delta = destination - current;
                delta.y = 0f;
                if (delta.sqrMagnitude < .0025f) break;

                Vector3 direction = delta.normalized;
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12f);
                // Feed the exact same controller/intended-movement path as normal
                // locomotion, so the player's existing run blend responds naturally.
                float speed = Vector3.Distance(current, destination) / Mathf.Max(.01f, RunToPointSeconds - elapsed + Time.deltaTime);
                movement.MoveCinematic(direction * speed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
            movement.StopCinematicMovement();
        }

        private IEnumerator LowerLadder()
        {
            Vector3 from = Ladder.position;
            Vector3 to = LadderLoweredPosition ? LadderLoweredPosition.position : from;
            Quaternion fromRotation = Ladder.rotation;
            Quaternion toRotation = LadderLoweredPosition ? LadderLoweredPosition.rotation : fromRotation;
            float elapsed = 0f;
            while (elapsed < LadderLowerSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / LadderLowerSeconds);
                Ladder.SetPositionAndRotation(Vector3.Lerp(from, to, t), Quaternion.Slerp(fromRotation, toRotation, t));
                yield return null;
            }
            Ladder.SetPositionAndRotation(to, toRotation);
        }

        private void EnsureAudio()
        {
            if (m_Audio) return;
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.spatialBlend = 0f;
            m_Audio.playOnAwake = false;
            if (!AlarmSound) AlarmSound = CreatePlaceholderAlarm();
        }

        private AudioClip CreatePlaceholderAlarm()
        {
            const int sampleRate = 22050;
            const int samples = sampleRate / 2;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)sampleRate;
                float pulse = Mathf.Sin(time * Mathf.PI * 4f) > 0f ? 1f : .18f;
                data[i] = Mathf.Sin(time * Mathf.PI * 2f * 880f) * pulse * .22f;
            }
            AudioClip clip = AudioClip.Create("Chapter1 Placeholder Alarm", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private IEnumerator FadeBars(float goal)
        {
            float from = m_BarAlpha;
            float elapsed = 0f;
            while (elapsed < BarFadeSeconds)
            {
                elapsed += Time.deltaTime;
                m_BarAlpha = Mathf.Lerp(from, goal, elapsed / BarFadeSeconds);
                yield return null;
            }
            m_BarAlpha = goal;
        }

        private void OnGUI()
        {
            if (!m_Playing) return;
            if (!m_White)
            {
                m_White = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                m_White.SetPixel(0, 0, Color.white); m_White.Apply();
            }
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, m_BarAlpha);
            float height = Screen.height * .24f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, height), m_White);
            GUI.DrawTexture(new Rect(0, Screen.height - height, Screen.width, height), m_White);
            GUI.color = old;
        }
    }
}
