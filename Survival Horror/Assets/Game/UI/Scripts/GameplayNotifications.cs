using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.Chapter1;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    public sealed class GameplayNotifications : MonoBehaviour
    {
        private const float Duration = 4f;
        private GameObject surface;
        private CanvasGroup documentPopup, circuitPopup, controlsPopup, activePopup;
        private TMP_Text documentName, circuitText, controlsText;
        private readonly Queue<DocumentData> pendingDocuments = new();
        private UIMap map;
        private float started, expires, countChangedAt;
        private int circuitCount;
        private static bool TabletOwned => Chapter1MapTutorial.Instance && Chapter1MapTutorial.Instance.MapUnlocked;

        public static void DocumentReceived(DocumentData document)
        {
            if (document && UIManager.Exists) Get().pendingDocuments.Enqueue(document);
        }

        public static void CircuitsDiscovered(int count)
        {
            if (!TabletOwned || count <= 0 || !UIManager.Exists) return;
            var notifications = Get();
            notifications.ShowCircuits(count);
        }

        public static void ShowControls(string hint)
        {
            if (!TabletOwned || !UIManager.Exists || string.IsNullOrWhiteSpace(hint)) return;
            var notifications = Get();
            if (!notifications.controlsPopup)
                notifications.controlsPopup = notifications.CreatePopup("CONTROLS", string.Empty, false, out notifications.controlsText);
            notifications.ClearPopup();
            notifications.controlsText.text = hint;
            notifications.Activate(notifications.controlsPopup);
        }

        public static void DismissAll()
        {
            if (UIManager.Exists && UIManager.Instance.TryGetComponent(out GameplayNotifications notifications))
                notifications.ClearPopup();
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
                surface = new GameObject("Gameplay notifications", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                var canvas = surface.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = surface.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = .5f;
            }

            var workspace = map ? map.GetComponent<TabletWorkspace>() : null;
            var font = workspace && workspace.Footer ? workspace.Footer.font : null;
            return TabletWorkspace.CreatePopup(surface.transform, new Vector2(.66f, .04f),
                new Vector2(.975f, .158f), font, title, circuit, out body, subtitle, exclamation: !circuit, large: true);
        }

        private void ShowDocument()
        {
            if (!documentPopup)
                documentPopup = CreatePopup("RECEIVED DOCUMENT", "Tab · Inventory → Documents", false, out documentName);
            documentName.text = pendingDocuments.Dequeue().Name;
            Activate(documentPopup);
        }

        private void ShowCircuits(int count)
        {
            if (activePopup && activePopup != circuitPopup && Time.unscaledTime < expires) return;
            if (!circuitPopup)
                circuitPopup = CreatePopup("TABLET UPDATE", "Press M", true, out circuitText);
            if (activePopup != circuitPopup || Time.unscaledTime >= expires)
            {
                ClearPopup();
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
            TabletWorkspace.AnimatePopup(popup, 0f, Duration, fromBottom: true);
            popup.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (map && map.isActiveAndEnabled)
            {
                ClearPopup();
                return;
            }

            if (pendingDocuments.Count > 0 && (!activePopup || activePopup != documentPopup) &&
                !IsOpen<UIDialog>() && !IsOpen<UIChoices>() && !IsOpen<UIDocument>() && !IsOpen<UIInventory>() &&
                (!PauseController.Exists || !PauseController.Instance.IsPaused))
                ShowDocument();

            if (!activePopup) return;
            float now = Time.unscaledTime;
            if (now >= expires)
            {
                ClearPopup();
                return;
            }
            if (activePopup == circuitPopup)
            {
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - countChangedAt - .28f) / .42f));
                var color = Color.Lerp(new Color(1f, .30f, .26f), Color.white, fade);
                string noun = circuitCount == 1 ? "circuit" : "circuits";
                circuitText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{circuitCount}</color> new {noun} discovered";
            }
            TabletWorkspace.AnimatePopup(activePopup, now - started, expires - now, fromBottom: true);
        }

        private static bool IsOpen<T>() where T : MonoBehaviour
        {
            var screen = UIManager.Get<T>(true);
            return screen && screen.isActiveAndEnabled;
        }

        private void ClearPopup()
        {
            circuitCount = 0;
            if (activePopup) activePopup.gameObject.SetActive(false);
            activePopup = null;
        }

        private void OnDisable()
        {
            pendingDocuments.Clear();
            ClearPopup();
        }

        private void OnDestroy()
        {
            if (surface) Destroy(surface);
        }
    }
}
