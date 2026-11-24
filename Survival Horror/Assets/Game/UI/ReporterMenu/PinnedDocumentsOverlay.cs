using System;
using System.Linq;
using HorrorEngine;
using HorrorGame.Documents;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    internal sealed class PinnedDocumentsOverlay : MonoBehaviour
    {
        const float SlideDuration = 0.24f;
        const float BottomMargin = 0.064f;
        const float HorizontalInset = 0.018f;
        const float Height = 0.87f;
        const float Travel = Height + BottomMargin + 0.035f;
        const float CardGap = 0.012f;

        readonly Image[] cards = new Image[DocumentLedgerState.PinLimit];
        DocumentData[] documents = Array.Empty<DocumentData>();
        DocumentData selected;
        RectTransform surface, strip, selectionMarker;
        TMP_Text legend;
        Inventory inventory;
        UIInventory originInventory;
        UIDocument activeReader;
        int sessionVersion, revision;
        float progress, layoutAspect;
        bool expanded;
        static float ScreenAspect => (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
        internal bool IsVisible => surface && surface.gameObject.activeSelf;

        internal void Show(Inventory source, ReporterMenuArt art, UIInventory origin)
        {
            if (source != inventory || source.MenuSessionVersion != sessionVersion)
                selected = null;
            inventory = source;
            sessionVersion = source.MenuSessionVersion;
            expanded = RefreshCards(art);
            if (!expanded) return;

            // Keep the inventory open underneath this pause context to preserve its current view.
            originInventory = origin;
            if (originInventory) originInventory.SetPinnedDocumentsCover(true);
            PauseController.Instance.Pause(this);
        }

        internal void Collapse() => expanded = false;

        internal void MoveSelection(int direction)
        {
            if (!expanded || documents.Length == 0) return;

            int index = Array.IndexOf(documents, selected);
            if (index < 0) index = direction < 0 ? documents.Length - 1 : 0;
            else index = (index + direction + documents.Length) % documents.Length;
            selected = documents[index];
            UpdateSelection();
        }

        internal void ReadSelected(Inventory current)
        {
            if (!expanded || !selected || current != inventory || current.MenuSessionVersion != sessionVersion ||
                !current.DocumentLedger.IsPinned(selected.UniqueId) || !current.Documents.Contains(selected)) return;

            var reader = UIManager.Get<UIDocument>(true);
            if (!reader) return;

            reader.Show(selected);
            if (!reader.isActiveAndEnabled) return;

            UIManager.PushAction(new UIStackedAction { Name = "Pinned document preview", StopProcessingActions = true });
            activeReader = reader;
            // Leave the strip's pause context beneath the reader so it regains focus when reading ends.
            surface.gameObject.SetActive(false);
        }

        internal void Tick(Inventory current)
        {
            if (activeReader && activeReader.isActiveAndEnabled) return;
            if (!expanded && !IsVisible) return;
            activeReader = null;

            if (current == null || current != inventory || current.MenuSessionVersion != sessionVersion)
            {
                Dismiss();
                return;
            }

            if (expanded && current.DocumentLedger.Revision != revision)
                expanded = RefreshCards();
            if (documents.Length > 0 && layoutAspect != ScreenAspect)
                LayoutCards();

            progress = Mathf.MoveTowards(progress, expanded ? 1f : 0f, Time.unscaledDeltaTime / SlideDuration);
            PlaceStrip();
            if (!expanded && progress == 0) Dismiss();
            else if (!IsVisible) surface.gameObject.SetActive(true);
        }

        bool RefreshCards(ReporterMenuArt art = null)
        {
            revision = inventory.DocumentLedger.Revision;
            var previews = inventory.DocumentLedger.CopyPins()
                .Select(id => inventory.Documents.FirstOrDefault(document => document && document.UniqueId == id))
                .Where(document => document)
                .Select(document => (Document: document, Artwork: ReporterMenuBridge.DocumentArtwork(document)))
                .Where(preview => preview.Artwork).ToArray();
            documents = previews.Select(preview => preview.Document).ToArray();
            if (!documents.Contains(selected)) selected = documents.FirstOrDefault();
            if (documents.Length == 0) return false;

            if (!surface) Build(art);
            for (int i = 0; i < cards.Length; i++)
            {
                Image card = cards[i];
                card.gameObject.SetActive(i < documents.Length);
                if (i >= documents.Length) continue;

                card.sprite = previews[i].Artwork;
            }

            legend.fontSize = MenuReadability.LargeText ? 15f : 12f;
            LayoutCards();
            PlaceStrip();
            surface.gameObject.SetActive(true);
            return true;
        }

        void LayoutCards()
        {
            layoutAspect = ScreenAspect;
            float stripAspect = layoutAspect * (1f - 2f * HorizontalInset) / Height;
            float aspectSum = 0f;
            for (int i = 0; i < documents.Length; i++)
                aspectSum += ArtworkAspect(cards[i]);

            float gaps = (documents.Length - 1) * CardGap;
            float height = Mathf.Min(1f, (1f - gaps) * stripAspect / aspectSum);
            float start = (1f - height * aspectSum / stripAspect - gaps) * 0.5f;
            float bottom = (1f - height) * 0.5f;
            float left = start;
            for (int i = 0; i < documents.Length; i++)
            {
                float right = left + height * ArtworkAspect(cards[i]) / stripAspect;
                Anchor(cards[i].rectTransform, left, bottom, right, bottom + height);
                left = right + CardGap;
            }

            Anchor(legend.rectTransform, start, -0.054f, 1f - start, -0.01f);
            UpdateSelection();
        }

        static float ArtworkAspect(Image card) => card.sprite.rect.width / card.sprite.rect.height;

        void UpdateSelection()
        {
            int index = Array.IndexOf(documents, selected);
            selectionMarker.gameObject.SetActive(index >= 0);
            if (index < 0) return;

            var card = cards[index].rectTransform;
            float center = (card.anchorMin.x + card.anchorMax.x) * 0.5f;
            selectionMarker.anchorMin = selectionMarker.anchorMax = new Vector2(center, card.anchorMax.y);
            selectionMarker.anchoredPosition = new Vector2(0, 9f);
        }

        void Build(ReporterMenuArt art)
        {
            // An independent canvas stays separate from the inventory and tablet canvases.
            var root = new GameObject("Pinned documents", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            surface = (RectTransform)root.transform;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            strip = Region(surface, "Pinned document strip");
            for (int i = 0; i < cards.Length; i++)
            {
                var card = Region(strip, "Document artwork").gameObject.AddComponent<Image>();
                card.preserveAspect = true;
                card.raycastTarget = false;
                cards[i] = card;
            }

            selectionMarker = Region(strip, "Selected document");
            selectionMarker.localScale = new Vector3(1.2f, 1.2f, 1f);
            ReporterMenuView.Chevron(selectionMarker, -90f);
            legend = Region(strip, "Document controls").gameObject.AddComponent<TextMeshProUGUI>();
            if (art && art.InterfaceFont) legend.font = art.InterfaceFont;
            legend.color = ReporterMenuView.TextColor;
            legend.alignment = TextAlignmentOptions.MidlineLeft;
            legend.textWrappingMode = TextWrappingModes.NoWrap;
            legend.raycastTarget = false;
            GameText.SetLabel(legend, "E Read");
        }

        void PlaceStrip()
        {
            float bottom = BottomMargin - Travel * (1 - Mathf.SmoothStep(0, 1, progress));
            Anchor(strip, HorizontalInset, bottom, 1f - HorizontalInset, bottom + Height);
        }

        internal void Dismiss()
        {
            documents = Array.Empty<DocumentData>();
            expanded = false;
            progress = 0;
            if (surface) surface.gameObject.SetActive(false);
            if (PauseController.Exists) PauseController.Instance.Resume(this);
            if (originInventory) originInventory.SetPinnedDocumentsCover(false);
            originInventory = null;
            activeReader = null;
        }

        void OnDisable() => Dismiss();

        void OnDestroy()
        {
            if (surface) Destroy(surface.gameObject);
        }

        static RectTransform Region(Transform parent, string name)
        {
            var region = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            region.SetParent(parent, false);
            return region;
        }

        static void Anchor(RectTransform region, float left, float bottom, float right, float top)
        {
            region.anchorMin = new Vector2(left, bottom);
            region.anchorMax = new Vector2(right, top);
            region.offsetMin = region.offsetMax = Vector2.zero;
        }
    }
}
