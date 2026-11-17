using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Unscaled, presentation-only boot sequence. Native UIMap still owns input, map data and pause.</summary>
    public sealed class TabletStartupScreen : MonoBehaviour
    {
        public CanvasGroup Screen;
        public Image Progress;
        public TMP_Text Status;
        [Min(.5f)] public float Duration = 2.6f;
        public bool OncePerSession = true;
        static bool shown;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => shown = false;
        public void Present()
        {
            StopAllCoroutines();
            if (!Screen) return;
            Screen.gameObject.SetActive(!OncePerSession || !shown);
            if (OncePerSession && shown) return;
            StartCoroutine(Boot());
        }
        void OnDisable() => StopAllCoroutines();
        IEnumerator Boot()
        {
            double start=Time.realtimeSinceStartupAsDouble;
            float elapsed=0; Screen.alpha=1;Progress.rectTransform.anchorMax=new Vector2(0,1);
            while(elapsed<Duration)
            {
                float t=elapsed/Duration;
                Progress.rectTransform.anchorMax=new Vector2(Mathf.Floor(t*20)/20,1);
                Status.text=t<.3f?"CHECKING FIELD HARDWARE...":t<.65f?"LOADING LOCAL MAP ARCHIVE...":"LOCAL MODE READY";
                Screen.alpha=t>.9f?Mathf.Clamp01((1-t)/.1f):1;
                yield return null;elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);
            }
            Progress.rectTransform.anchorMax=Vector2.one;shown=true;Screen.gameObject.SetActive(false);
        }
    }
}
