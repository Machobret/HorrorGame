using System;
using HorrorEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Owns the tablet shell and its window state. The workspace owns the pages inside it.</summary>
    internal sealed class TabletWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        const float TitleHeight = 34;
        const float Rim = 4;
        const float ScreenInset = .035f;
        static readonly Vector2 WindowedSize = new(.78f, .76f);
        static readonly Color TitleColor = new Color32(20, 34, 43, 255);
        static readonly Color TitleBorder = new Color32(65, 88, 99, 255);
        static readonly Color ButtonFace = new Color32(43, 58, 65, 255);
        static readonly Color Trim = new Color32(106, 130, 141, 255);
        static readonly Color Ink = new Color32(219, 233, 230, 255);

        RectTransform frame, viewport;
        RectTransform maximizeControl, restoreGlyph;
        TabletDisplayFilter filter;
        Vector2 windowedCenter = new(.5f, .5f);
        Vector2 dragOrigin, dragStart;
        bool maximized = true;
        bool dragReady;

        internal static void Create(RectTransform content, TMP_FontAsset font,
            TabletStartupScreen startup, TabletDisplayFilter filter, Action minimize, Action close)
        {
            var viewport = (RectTransform)content.parent;
            var frame = Rect("Tablet window", viewport, Vector2.zero, Vector2.one);
            var edge = Fill(frame, Trim);
            edge.raycastTarget = false;
            var backing = Rect("Window face", frame, Vector2.zero, Vector2.one);
            Inset(backing, new Vector2(1, 1), new Vector2(-2, -2));
            Fill(backing, new Color32(17, 26, 31, 255));

            content.SetParent(frame, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            Inset(content, new Vector2(Rim, Rim), new Vector2(-Rim, -TitleHeight - Rim));
            frame.SetAsLastSibling();

            var title = Rect("Title bar", frame, new Vector2(0, 1), Vector2.one);
            Inset(title, new Vector2(Rim, -TitleHeight - Rim), new Vector2(-Rim, -Rim));
            Fill(title, TitleColor);

            var drag = Rect("Move tablet window", title, Vector2.zero, Vector2.one);
            Inset(drag, Vector2.zero, new Vector2(-110, 0));
            Fill(drag, Color.clear);
            var window = drag.gameObject.AddComponent<TabletWindow>();
            window.frame = frame;
            window.viewport = viewport;
            window.filter = filter;

            var mark = Rect("Hero response badge", drag, new Vector2(0, .5f), new Vector2(0, .5f))
                .gameObject.AddComponent<TabletHeroBadge>();
            mark.rectTransform.anchoredPosition = new Vector2(21, 0);
            mark.rectTransform.sizeDelta = new Vector2(23, 23);
            var bootMark = startup && startup.Screen ? startup.Screen.GetComponentInChildren<TabletHeroBadge>(true) : null;
            mark.color = bootMark ? bootMark.color : Ink;
            mark.raycastTarget = false;

            var name = Rect("Operating system", drag, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
            Inset(name.rectTransform, new Vector2(42, 0), new Vector2(-4, 0));
            name.font = font;
            name.fontSize = 19;
            name.color = Ink;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.raycastTarget = false;
            GameText.SetLabel(name, TabletStartupScreen.OperatingSystemName);

            var tray = WindowButton("Minimize", title, 2, minimize);
            Stroke(tray, -4, 3, new Vector2(12, 2));
            var maximize = WindowButton("Restore Down", title, 1, window.ToggleMaximized);
            window.maximizeControl = maximize;
            window.restoreGlyph = Box("Overlapping window", maximize, new Vector2(2, 2), 10);
            Box("Window outline", maximize, new Vector2(-1, -1), 12);
            var exit = WindowButton("Close tablet", title, 0, close);
            Stroke(exit, 0, -45, new Vector2(14, 2));
            Stroke(exit, 0, 45, new Vector2(14, 2));

            var border = Rect("Title bar bottom border", title, Vector2.zero, Vector2.right);
            Inset(border, Vector2.zero, new Vector2(0, 1.5f));
            Fill(border, TitleBorder).raycastTarget = false;
            window.ApplyLayout();
        }

        void ToggleMaximized()
        {
            maximized = !maximized;
            ApplyLayout();
        }

        void ApplyLayout()
        {
            frame.anchorMin = maximized ? Vector2.one * ScreenInset : windowedCenter - WindowedSize / 2;
            frame.anchorMax = maximized ? Vector2.one * (1 - ScreenInset) : windowedCenter + WindowedSize / 2;
            maximizeControl.name = maximized ? "Restore Down" : "Maximize";
            restoreGlyph.gameObject.SetActive(maximized);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragOrigin = windowedCenter;
            dragReady = !maximized && eventData.button == PointerEventData.InputButton.Left &&
                TryGetPointer(eventData, out dragStart);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragReady || maximized || eventData.button != PointerEventData.InputButton.Left ||
                !TryGetPointer(eventData, out var current)) return;
            var size = viewport.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            Vector2 delta = current - dragStart;
            Vector2 center = dragOrigin + new Vector2(delta.x / size.x, delta.y / size.y);
            Vector2 limit = WindowedSize / 2 + Vector2.one * ScreenInset;
            windowedCenter = new Vector2(Mathf.Clamp(center.x, limit.x, 1 - limit.x),
                Mathf.Clamp(center.y, limit.y, 1 - limit.y));
            ApplyLayout();
        }

        bool TryGetPointer(PointerEventData eventData, out Vector2 local)
        {
            Vector2 position = filter ? filter.GlassToScreen(eventData.position) : eventData.position;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, position,
                eventData.pressEventCamera, out local);
        }

        static RectTransform WindowButton(string name, RectTransform title, int index, Action action)
        {
            var rect = Rect(name, title, new Vector2(1, .5f), new Vector2(1, .5f));
            rect.anchoredPosition = new Vector2(-19 - 36 * index, 0);
            rect.sizeDelta = new Vector2(34, 26);
            var face = Fill(rect, ButtonFace);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            return rect;
        }

        static RectTransform Box(string name, RectTransform parent, Vector2 position, float width)
        {
            var box = Rect(name, parent, Vector2.one / 2, Vector2.one / 2);
            box.anchoredPosition = position;
            box.sizeDelta = new Vector2(width, 10);
            Fill(box, Ink).raycastTarget = false;
            var face = Rect("Inset", box, Vector2.zero, Vector2.one);
            Inset(face, new Vector2(1, 1), new Vector2(-1, -2));
            Fill(face, ButtonFace).raycastTarget = false;
            return box;
        }

        static void Stroke(RectTransform parent, float y, float angle, Vector2 size)
        {
            var line = Rect("Glyph", parent, Vector2.one / 2, Vector2.one / 2);
            line.anchoredPosition = new Vector2(0, y);
            line.sizeDelta = size;
            line.localRotation = Quaternion.Euler(0, 0, angle);
            Fill(line, Ink).raycastTarget = false;
        }

        static RectTransform Rect(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            Inset(rect, Vector2.zero, Vector2.zero);
            return rect;
        }

        static void Inset(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        static Image Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
