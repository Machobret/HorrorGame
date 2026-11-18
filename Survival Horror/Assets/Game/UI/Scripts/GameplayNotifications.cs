using HorrorEngine;
using TMPro;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class GameplayNotifications : MonoBehaviour
    {
        private const float Duration = 4f;
        private GameObject surface;
        private CanvasGroup documentPopup, circuitPopup, activePopup;
        private TMP_Text documentName, circuitText;
        private DocumentData pendingDocument;
        private UIMap map;
        private float started, expires, countChangedAt;
        private int circuitCount;

        public static void DocumentReceived(DocumentData document)
        {
            if (!document || !UIManager.Exists) return;
            var notifications = Get();
            notifications.Clear();
            notifications.pendingDocument = document;
        }

        public static void CircuitsDiscovered(int count)
        {
            if (count <= 0 || !UIManager.Exists) return;
            var notifications = Get();
            notifications.ShowCircuits(count);
        }

        public static void DismissAll()
        {
            if (UIManager.Exists && UIManager.Instance.TryGetComponent(out GameplayNotifications notifications))
                notifications.Clear();
        }

        private static GameplayNotifications Get()
        {
            var manager = UIManager.Instance;
            return manager.TryGetComponent(out GameplayNotifications notifications)
                ? notifications : manager.gameObject.AddComponent<GameplayNotifications>();
        }

        private void Awake() => map = UIManager.Get<UIMap>(true);

        private CanvasGroup CreatePopup(string title, string subtitle, bool circuit, out TMP_Text body)
        {
            if (!surface)
            {
                surface = new GameObject("Gameplay notifications", typeof(RectTransform), typeof(Canvas));
                var canvas = surface.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
            }

            var workspace = map ? map.GetComponent<TabletWorkspace>() : null;
            var font = workspace && workspace.Footer ? workspace.Footer.font : null;
            return TabletWorkspace.CreatePopup(surface.transform, new Vector2(.025f, .89f),
                new Vector2(.29f, .965f), font, title, circuit, out body, subtitle, exclamation: !circuit);
        }

        private void ShowDocument()
        {
            if (!documentPopup)
                documentPopup = CreatePopup("RECEIVED DOCUMENT", "Press I to read", false, out documentName);
            documentName.text = pendingDocument.Name;
            pendingDocument = null;
            Activate(documentPopup);
        }

        private void ShowCircuits(int count)
        {
            if (!circuitPopup)
                circuitPopup = CreatePopup("TABLET UPDATE", "Press M", true, out circuitText);
            if (activePopup != circuitPopup || Time.unscaledTime >= expires)
            {
                Clear();
                Activate(circuitPopup);
            }
            circuitCount += count;
            countChangedAt = Time.unscaledTime;
            expires = countChangedAt + Duration;
        }

        private void Activate(CanvasGroup popup)
        {
            if (activePopup) activePopup.gameObject.SetActive(false);
            activePopup = popup;
            started = Time.unscaledTime;
            expires = started + Duration;
            TabletWorkspace.AnimatePopup(popup, 0f, Duration);
            popup.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (map && map.isActiveAndEnabled)
            {
                Clear();
                return;
            }

            if (pendingDocument && !IsOpen<UIDialog>() && !IsOpen<UIChoices>() && !IsOpen<UIDocument>())
                ShowDocument();

            if (!activePopup) return;
            float now = Time.unscaledTime;
            if (now >= expires)
            {
                Clear();
                return;
            }
            if (activePopup == circuitPopup)
            {
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - countChangedAt - .28f) / .42f));
                var color = Color.Lerp(new Color(1f, .30f, .26f), Color.white, fade);
                string noun = circuitCount == 1 ? "circuit" : "circuits";
                circuitText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{circuitCount}</color> new {noun} discovered";
            }
            TabletWorkspace.AnimatePopup(activePopup, now - started, expires - now);
        }

        private static bool IsOpen<T>() where T : MonoBehaviour
        {
            var screen = UIManager.Get<T>(true);
            return screen && screen.isActiveAndEnabled;
        }

        private void Clear()
        {
            pendingDocument = null;
            circuitCount = 0;
            if (activePopup) activePopup.gameObject.SetActive(false);
            activePopup = null;
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            if (surface) Destroy(surface);
        }
    }
}
