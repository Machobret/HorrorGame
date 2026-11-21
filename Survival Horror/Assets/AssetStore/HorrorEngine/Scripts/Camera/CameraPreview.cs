using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class CameraPreview : MonoBehaviour
    {
        [Range(0f, .45f)] public float LetterboxHeight;

        public static GameObject CreateLetterbox(CameraPOV camera)
        {
            var preview = camera ? camera.GetComponentInChildren<CameraPreview>(true) : null;
            float height = preview ? preview.LetterboxHeight : .2f;
            if (height <= 0f) return null;

            var overlay = new GameObject("Cutscene letterbox", typeof(Canvas));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            AddBar(overlay.transform, "Top", 1f - height, 1f);
            AddBar(overlay.transform, "Bottom", 0f, height);
            return overlay;
        }

        private static void AddBar(Transform parent, string barName, float bottom, float top)
        {
            var bar = new GameObject(barName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bar.transform.SetParent(parent, false);
            bar.rectTransform.anchorMin = new Vector2(0f, bottom);
            bar.rectTransform.anchorMax = new Vector2(1f, top);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            bar.color = Color.black;
            bar.raycastTarget = false;
        }
    }
}