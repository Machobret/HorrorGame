using System;
using System.Collections.Generic;
using System.Linq;
using HorrorEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    internal sealed class ReporterMenuView
    {
        sealed class ScrollArea
        {
            public ScrollRect Scroll;
            public RectTransform Content;
        }

        sealed class TargetView
        {
            public Image Backdrop;
            public Color Idle;
            public Color Focus;
            public Sprite IdleSprite;
            public Sprite FocusSprite;
            public TMP_Text Title;
            public Color IdleTitleColor = TextColor;
            public Graphic Pin;
            public Graphic FocusMarker;
            public bool Pinned;
            public ScrollArea ScrollArea;
            ReporterItemVisual item;

            public ReporterItemVisual AttachIcon(Image icon, Material outline)
            {
                item = Backdrop.gameObject.AddComponent<ReporterItemVisual>();
                item.Initialize(icon, outline);
                return item;
            }

            public void ShowSelection(bool selected)
            {
                if (item) item.SetSelected(selected);
                if (FocusMarker) FocusMarker.enabled = selected;
                if (Title) Title.color = selected ? TextColor : IdleTitleColor;
                Backdrop.color = selected ? Focus : Idle;
                Backdrop.sprite = selected ? FocusSprite : IdleSprite;
            }
        }

        static readonly Color PanelColor = new(0.14f, 0.13f, 0.12f, 0.97f);
        static readonly Color LedgerColor = new(0.21f, 0.19f, 0.16f, 0.98f);
        static readonly Color ShadowColor = new(0.035f, 0.030f, 0.026f, 0.78f);
        static readonly Color FocusColor = new(0.51f, 0.39f, 0.23f, 0.95f);
        static readonly Color EquippedShadowColor = new(0, 0, 0, 0.70f);
        static readonly Color HealthyFill = new Color32(42, 88, 53, 255);
        static readonly Color WoundedFill = new Color32(131, 89, 15, 255);
        static readonly Color CriticalFill = new Color32(138, 41, 34, 255);
        static readonly Color PaperInk = new Color32(46, 41, 32, 255);
        static readonly Color StampInk = new Color32(123, 44, 38, 235);
        internal static readonly Color TextColor = new(0.98f, 0.94f, 0.87f);
        const int InventoryColumns = 3;
        const int VisibleInventoryRows = 2;
        const float InventoryGap = 6;
        const float EquipmentReadoutHeight = 18;
        const float LeftPanelGap = 9f / ReferenceHeight;
        const float PanelBottom = 0.064f;
        const float PanelTop = 0.985f;
        const float LedgerBodyTop = 0.966f;
        const float LedgerPageInset = 0.055f;
        const float LedgerTitleInset = 0.135f;
        const int ItemActionReferenceRows = 4;
        const float ItemActionsBottom = 0.03f;
        const float ItemActionsTop = 0.34f;
        const float ReferenceHeight = 720;
        const float MinimumAspect = 4f / 3f;
        const float MaximumAspect = 16f / 9f;

        readonly Dictionary<string, TargetView> targets = new();
        readonly Dictionary<string, TargetView> equipmentMirrors = new();
        readonly Dictionary<ReporterPanel, TMP_Text> legends = new();
        readonly List<ReporterFocus.Target> navigation = new();
        readonly List<(TMP_Text Label, int Size)> persistentText = new();
        readonly List<RectTransform> inventoryTiles = new();
        readonly List<RectTransform> equipmentTiles = new();
        readonly TMP_Text[] indexLabels = new TMP_Text[2];
        readonly ReporterMenuArt art;
        readonly Sprite tabletIcon;
        readonly RectTransform viewport;
        readonly RectTransform layout;
        readonly RectTransform profile;
        readonly RectTransform inventory;
        readonly RectTransform ledger;
        readonly RectTransform ledgerPage;
        readonly RectTransform display;
        readonly ScrollArea inventoryScroll;
        readonly ScrollArea ledgerScroll;
        readonly RectTransform displayContent;
        readonly RectTransform actionList;
        readonly ScrollArea readerScroll;
        readonly RectTransform readerDim;
        readonly TMP_Text readerText;
        readonly TMP_Text pageNumber;
        readonly TMP_Text displayTitle;
        readonly TMP_Text displayText;
        readonly TMP_Text feedback;
        readonly Image displayImage;
        readonly RectTransform previousPage;
        readonly RectTransform nextPage;
        readonly RectTransform exitPage;
        readonly TMP_Text exitLabel;
        readonly Button readerExit;
        readonly CanvasGroup group;
        bool largeText = MenuReadability.LargeText;
        Vector2 viewportSize;
        Vector2 inventoryPanelSize;
        Vector2 equipmentAreaSize;
        int inventoryRows;
        string selectedId;
        string hoveredEquipmentId;
        RectTransform tablet;
        RectTransform equipmentArea;
        Image portraitImage;
        Sprite portraitSource;
        Image conditionBadge;
        TMP_Text conditionLabel;
        ReporterCondition? shownCondition;

        public IReadOnlyList<ReporterFocus.Target> FocusTargets => navigation;
        public float InventoryScroll => inventoryScroll.Scroll.verticalNormalizedPosition;
        public float LedgerScroll => ledgerScroll.Scroll.verticalNormalizedPosition;
        public float ReaderScroll => readerScroll.Scroll.verticalNormalizedPosition;

        public ReporterMenuView(Transform mount, Sprite tabletIcon, ReporterMenuArt art, Action previous, Action next, Action exit)
        {
            this.art = art;
            this.tabletIcon = Resolve(tabletIcon);
            viewport = Region(mount, "Reporter menu", 0, 0, 1, 1);
            group = viewport.gameObject.AddComponent<CanvasGroup>();
            var dim = viewport.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.73f);
            layout = Region(viewport, "Landscape layout", 0.5f, 0.5f, 0.5f, 0.5f);
            RefreshLandscapeLayout();
            var root = layout;
            float tabHeight = art ? (PanelTop - PanelBottom) * (1 - LedgerBodyTop) : 0;
            float profileBottom = 0.72f - tabHeight;
            profile = Panel(root, ReporterPanel.Profile, 0.01f, profileBottom, 0.297f, PanelTop - tabHeight, PanelColor);
            inventory = Panel(root, ReporterPanel.Inventory, 0.01f, PanelBottom, 0.297f, profileBottom - LeftPanelGap, PanelColor);
            ledger = Panel(root, ReporterPanel.Ledger, 0.307f, PanelBottom, 0.598f, PanelTop, LedgerColor);
            display = Panel(root, ReporterPanel.Display, 0.608f, PanelBottom, 0.99f, PanelTop, Color.clear);

            if (art)
            {
                SetSurface(profile, art.PressCard);
                SetSurface(inventory, art.Satchel);
                Paint(ledger, Color.clear);
                ledgerPage = Region(ledger, "Ledger page", 0, 0, 1, LedgerBodyTop);
                SetSurface(ledgerPage, art.LedgerPage);
            }
            inventoryScroll = Scroll(inventory, "Inventory slots", 0, 1, 1, 1);
            ledgerScroll = art
                ? Scroll(ledger, "Ledger entries", LedgerPageInset, 0.015f, 1 - LedgerPageInset, 0.949f)
                : Scroll(ledger, "Ledger entries", 0.02f, 0.015f, 0.98f, 0.914f);
            displayContent = Region(display, "Preview", 0.02f, 0.025f, 0.98f, 0.98f);
            displayTitle = Label(Region(displayContent, "Title", 0, 0.922f, 1, 1), "", 17, TextAlignmentOptions.Center, persistent: true);
            displayImage = Art(Region(displayContent, "Image", 0.07f, 0.29f, 0.93f, 0.88f), null);
            displayText = Label(Region(displayContent, "Details", 0.03f, 0.05f, 0.97f, 0.28f), "", 13,
                TextAlignmentOptions.TopLeft, persistent: true);
            actionList = Region(display, "Item actions", 0.12f, ItemActionsBottom, 0.88f, ItemActionsTop);

            readerDim = Region(display, "Readout dim", 0, 0, 1, 1);
            Paint(readerDim, new Color(0.027f, 0.025f, 0.022f, 0.72f));
            readerScroll = Scroll(display, "Reading", 0, 0.09f, 1, 0.98f);
            readerText = Label(Region(readerScroll.Content, "Transcript", 0, 0, 1, 1), "", 18,
                TextAlignmentOptions.TopLeft, persistent: true);
            readerText.margin = new Vector4(14, 11, 14, 11);
            readerText.textWrappingMode = TextWrappingModes.Normal;
            readerText.overflowMode = TextOverflowModes.Overflow;
            pageNumber = Label(Region(display, "Page number", 0.36f, 0.01f, 0.64f, 0.078f), "", 12, TextAlignmentOptions.Center, persistent: true);
            previousPage = Control(display, "Previous page", 0, 0.01f, 0.20f, 0.078f, ShadowColor, previous);
            nextPage = Control(display, "Next page", 0.80f, 0.01f, 1, 0.078f, ShadowColor, next);
            Chevron(previousPage, 180);
            Chevron(nextPage, 0);
            exitPage = Control(display, "Exit reader", 0.80f, 0.01f, 1, 0.078f, ShadowColor, exit);
            Label(exitPage, GameText.Translate("Exit"), 13, TextAlignmentOptions.Center, persistent: true);
            exitLabel = legends[ReporterPanel.Display];
            readerExit = exitLabel.gameObject.AddComponent<Button>();
            readerExit.navigation = new Navigation { mode = Navigation.Mode.None };
            readerExit.onClick.AddListener(() => exit());
            feedback = Label(Region(root, "Command feedback", 0.307f, 0.003f, 0.99f, 0.03f), "", 12, TextAlignmentOptions.Right, persistent: true);
        }

        RectTransform Panel(RectTransform root, ReporterPanel panel, float left, float bottom, float right, float top, Color color)
        {
            RectTransform frame = Region(root, panel.ToString(), left, bottom, right, top);
            if (color.a > 0) Paint(frame, color);
            if (panel != ReporterPanel.Profile)
                legends.Add(panel, Label(Region(root, $"{panel} controls", left, bottom - 0.034f, right, bottom - 0.001f),
                    "", 12, TextAlignmentOptions.Left, persistent: true));
            return frame;
        }

        public void Build(ReporterMenuSnapshot data, bool archive, ISet<string> collapsed,
            Action<string> select, Action<string> pin, float inventoryPosition, float ledgerPosition)
        {
            navigation.Clear();
            targets.Clear();
            largeText = MenuReadability.LargeText;
            foreach (var (label, baseSize) in persistentText) label.fontSize = TextSize(baseSize);
            hoveredEquipmentId = null;
            equipmentMirrors.Clear();
            Clear(profile);
            equipmentTiles.Clear();
            inventoryTiles.Clear();
            Clear(inventoryScroll.Content);
            Clear(ledgerScroll.Content);
            foreach (var tab in ledger.Cast<Transform>().Where(child => child != ledgerScroll.Scroll.transform && child != ledgerPage).ToArray())
                Discard(tab.gameObject);

            const float pressMarkLeft = 0.81f;
            var name = Label(Region(profile, "Name", 0.03f, 0.87f, art ? pressMarkLeft : 0.975f, 0.99f),
                data.CharacterName, 16, TextAlignmentOptions.Left);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            if (art)
            {
                name.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                var pressMark = Label(Region(profile, "Press mark", pressMarkLeft, 0.87f, 0.975f, 0.99f),
                    GameText.Translate("PRESS"), 10, TextAlignmentOptions.Center);
                pressMark.fontStyle = FontStyles.Bold;
                pressMark.textWrappingMode = TextWrappingModes.NoWrap;
            }
            portraitSource = data.Portrait;
            portraitImage = BuildPortrait(portraitSource);
            var badgeRect = Region(profile, "Condition badge", 0.51f, 0.105f, 0.93f, 0.305f);
            conditionBadge = Paint(badgeRect, Color.clear);
            conditionBadge.sprite = art ? art.ConditionBadge : null;
            conditionBadge.type = Image.Type.Sliced;
            conditionLabel = Label(Region(badgeRect, "Condition", 0.06f, 0, 0.94f, 1), "", 18, TextAlignmentOptions.Center);
            conditionLabel.fontStyle = FontStyles.Bold;
            conditionLabel.color = Color.white;
            shownCondition = null;
            SetCondition(data.Condition);
            equipmentArea = Region(profile, "Equipment", 0.465f, 0.385f, 0.975f, 0.86f);
            for (int i = 0; i < data.Equipment.Count; i++)
            {
                var node = data.Equipment[i];
                var cell = Region(equipmentArea, node.Title, 0.5f, 0.5f, 0.5f, 0.5f);
                equipmentTiles.Add(cell);
                EquipmentMirror(cell, node);
            }

            if (tablet) Discard(tablet.gameObject);
            tablet = null;
            int cellCount = data.Items.Count + Mathf.Max(0, data.Capacity - data.UsedSlots);
            inventoryRows = Mathf.Max(VisibleInventoryRows, Mathf.CeilToInt(cellCount / (float)InventoryColumns));
            for (int i = 0; i < data.Items.Count; i++)
            {
                var node = data.Items[i];
                int col = i % InventoryColumns;
                int row = i / InventoryColumns;
                var cell = Region(inventoryScroll.Content, node.Title, 0.5f, 1, 0.5f, 1);
                cell.pivot = new Vector2(0.5f, 1);
                inventoryTiles.Add(cell);
                ItemCell(cell, node, new Vector2(0.065f + 0.086f * col, 0.49f - row * 0.14f),
                    select, inventoryScroll);
            }
            if (data.Tablet != null)
            {
                tablet = Region(inventory, "Tablet", 0, 0, 1, 1);
                var tabletTarget = Register(tablet, data.Tablet.Id, ReporterPanel.Inventory,
                    new Vector2(0.15f, 0.125f - 0.14f * (inventoryRows - VisibleInventoryRows)),
                    Color.clear, select, isTablet: true, focus: Color.clear);
                var tabletArt = Art(Region(tablet, "Tablet icon", 0.11f, 0.11f, 0.89f, 0.89f), tabletIcon);
                tabletTarget.AttachIcon(tabletArt, art ? art.ItemOutline : null);
            }

            float rowHeight = largeText ? 37 : 30;
            int ledgerRow = 0;
            foreach (var section in data.Documents.Where(doc => doc.Archived == archive).GroupBy(doc => doc.Section))
            {
                string sectionId = GroupId(archive, section.Key);
                bool expanded = !collapsed.Contains(sectionId);
                var header = LedgerRow(section.Key, ledgerRow, rowHeight);
                Color rowColor = art ? new Color(0.84f, 0.84f, 0.84f) : ShadowColor;
                var category = Register(header, sectionId, ReporterPanel.Ledger, LedgerPosition(ledgerRow++),
                    rowColor, select, ledgerScroll, idleSprite: art ? art.Category : null, focus: rowColor);
                var marker = Region(header, "Category focus", 0, 0, 1, 1);
                marker.offsetMin = new Vector2(4, 3);
                marker.offsetMax = new Vector2(-4, -3);
                var badge = Paint(marker, art && art.Selection ? Color.white : ReporterMenuArt.Oxblood);
                badge.sprite = art ? art.Selection : null;
                badge.type = Image.Type.Sliced;
                badge.enabled = false;
                category.FocusMarker = badge;
                var gutter = Region(header, "Category chevron", 0, 0, 0.12f, 1);
                Chevron(gutter, expanded ? -90 : 0);
                LedgerTitle(header, "Category label", GameText.Translate(section.Key));
                if (!expanded) continue;
                foreach (var doc in section)
                {
                    var row = LedgerRow(doc.Node.Title, ledgerRow, rowHeight);
                    var view = Register(row, doc.Node.Id, ReporterPanel.Ledger, LedgerPosition(ledgerRow++),
                        art ? Color.clear : new Color(0.29f, 0.26f, 0.22f, 0.95f), select, ledgerScroll, entryPriority: 1,
                        focusSprite: art ? art.Selection : null, focus: art ? Color.white : FocusColor);
                    view.Title = LedgerTitle(row, "Document title", doc.Node.Title);
                    view.IdleTitleColor = art ? PaperInk : TextColor;
                    if (doc.Archived) continue;
                    var pinArea = Control(row, "Pin document", 0, 0, 0.12f, 1, Color.clear, () => pin(doc.Node.Id));
                    if (art && art.Pin)
                        view.Pin = Art(Region(pinArea, "Pin icon", 0.20f, 0.19f, 0.72f, 0.81f), art.Pin);
                    else
                        view.Pin = Label(pinArea, "◆", 12, TextAlignmentOptions.Center);
                    view.Pinned = doc.Pinned;
                }
            }
            if (ledgerRow == 0)
            {
                var emptyLabel = Label(LedgerRow("Empty ledger", 0, rowHeight),
                    GameText.Translate(archive ? "No archived documents" : "No collected documents"), 13, TextAlignmentOptions.Center);
                if (art) emptyLabel.color = PaperInk;
            }
            ledgerScroll.Content.sizeDelta = new Vector2(0, Mathf.Max(1, ledgerRow) * rowHeight);
            Tab(false, archive, select);
            Tab(true, archive, select);

            Canvas.ForceUpdateCanvases();
            RefreshInventoryLayout(force: true);
            inventoryScroll.Scroll.verticalNormalizedPosition = inventoryPosition;
            ledgerScroll.Scroll.verticalNormalizedPosition = ledgerPosition;
        }

        public static string GroupId(bool archive, string section) => $"group:{archive}:{section}";
        static Vector2 LedgerPosition(int row) => new(0.45f, 0.81f - row * 0.055f);

        public void RefreshInventoryLayout(bool force = false)
        {
            bool layoutResized = RefreshLandscapeLayout();
            if (layoutResized && readerScroll.Scroll.gameObject.activeSelf)
                FitReader(ReaderScroll);

            var inventorySize = inventory.rect.size;
            var equipmentSize = equipmentArea.rect.size;
            if (!force && inventorySize == inventoryPanelSize && equipmentSize == equipmentAreaSize) return;
            inventoryPanelSize = inventorySize;
            equipmentAreaSize = equipmentSize;
            if (ledgerPage) FitLedgerTabs();

            float scrollPosition = inventoryScroll.Scroll.verticalNormalizedPosition;
            float edge = InventoryGap + (art ? inventorySize.x * 0.047f : 0);
            float side = Mathf.Max(1, (inventorySize.x - 2 * edge - (InventoryColumns - 1) * InventoryGap) / InventoryColumns);
            float pitch = side + InventoryGap;
            float visibleGridHeight = VisibleInventoryRows * pitch - InventoryGap;
            var grid = (RectTransform)inventoryScroll.Scroll.transform;
            grid.offsetMin = new Vector2(edge, -edge - visibleGridHeight);
            grid.offsetMax = new Vector2(-edge, -edge);
            if (tablet)
            {
                tablet.offsetMin = new Vector2(edge, edge);
                tablet.offsetMax = new Vector2(-edge, -visibleGridHeight - edge - InventoryGap);
            }
            inventoryScroll.Content.sizeDelta = new Vector2(0, inventoryRows * pitch - InventoryGap);
            for (int i = 0; i < inventoryTiles.Count; i++)
            {
                var tile = inventoryTiles[i];
                int column = i % InventoryColumns;
                int row = i / InventoryColumns;
                float x = (column - (InventoryColumns - 1) * 0.5f) * pitch;
                float y = -row * pitch;
                tile.sizeDelta = new Vector2(side, side);
                tile.anchoredPosition = new Vector2(x, y);
            }
            if (equipmentTiles.Count > 0)
            {
                float maxWidth = (equipmentSize.x - (equipmentTiles.Count - 1) * InventoryGap) / equipmentTiles.Count;
                float equipmentSide = Mathf.Max(0, Mathf.Min(side * 0.7f, Mathf.Min(maxWidth, equipmentSize.y - EquipmentReadoutHeight)));
                float equipmentPitch = equipmentSide + InventoryGap;
                for (int i = 0; i < equipmentTiles.Count; i++)
                {
                    var tile = equipmentTiles[i];
                    tile.sizeDelta = new Vector2(equipmentSide, equipmentSide);
                    tile.anchoredPosition = new Vector2((i - (equipmentTiles.Count - 1) * 0.5f) * equipmentPitch, EquipmentReadoutHeight * 0.5f);
                }
            }
            inventoryScroll.Scroll.verticalNormalizedPosition = scrollPosition;
        }

        bool RefreshLandscapeLayout()
        {
            Vector2 available = viewport.rect.size;
            if (available == viewportSize || available.x <= 0 || available.y <= 0) return false;
            viewportSize = available;

            // Keep the common landscape ratios flexible. Outside that range, fit the nearest
            // supported frame as one unit so the panels, glyphs, and hit targets scale together.
            float aspect = Mathf.Clamp(available.x / available.y, MinimumAspect, MaximumAspect);
            var logicalSize = new Vector2(ReferenceHeight * aspect, ReferenceHeight);
            bool resized = layout.sizeDelta != logicalSize;
            layout.sizeDelta = logicalSize;
            layout.localScale = Vector3.one * Mathf.Min(available.x / logicalSize.x, available.y / logicalSize.y);
            return resized;
        }

        Image BuildPortrait(Sprite sprite)
        {
            var area = Region(profile, "Portrait", 0.026f, 0.025f, 0.454f, 0.845f);
            if (!sprite)
            {
                if (!art) Paint(area, ShadowColor);
                return null;
            }

            var photo = Region(area, "White photo border", 0, 0, 1, 1);
            var fit = photo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectRatio = 0.72f;
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            photo.localRotation = Quaternion.Euler(0, 0, 3.5f);
            Paint(photo, new Color32(251, 249, 242, 255));
            var print = Region(photo, "Photo print", 0.04f, 0.082f, 0.96f, 0.986f);
            Paint(print, new Color32(68, 63, 57, 255));
            var image = Art(Region(print, "Portrait image", 0, 0, 1, 1), sprite);

            var tape = Region(photo, "Tape", 0.27f, 0.97f, 0.73f, 1.045f);
            tape.localRotation = Quaternion.Euler(0, 0, -10);
            Paint(tape, new Color32(212, 196, 160, 235));
            return image;
        }

        public void SetCondition(ReporterCondition condition)
        {
            if (condition == shownCondition) return;
            shownCondition = condition;
            if (portraitImage)
                portraitImage.sprite = art ? art.ResolvePortrait(portraitSource, condition) : portraitSource;
            var (label, fill) = condition switch
            {
                ReporterCondition.Healthy => ("Healthy", HealthyFill),
                ReporterCondition.Wounded => ("Wounded", WoundedFill),
                ReporterCondition.Critical => ("Critical", CriticalFill),
                _ => ("", Color.clear)
            };
            conditionLabel.text = GameText.Translate(label);
            conditionBadge.color = fill;
            conditionBadge.gameObject.SetActive(condition != ReporterCondition.Unknown);
        }

        RectTransform LedgerRow(string title, int row, float height)
        {
            var line = Region(ledgerScroll.Content, title, 0, 0, 1, 0);
            OffsetRow(line, row * height, height - 2);
            return line;
        }

        TMP_Text LedgerTitle(RectTransform row, string labelName, string title)
        {
            var label = Label(Region(row, labelName, art ? 0.104f : LedgerTitleInset, 0, 0.98f, 1),
                title, 13, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        void Tab(bool archivedTab, bool currentArchive, Action<string> select)
        {
            string name = archivedTab ? "Archive" : "Documents";
            string id = archivedTab ? "tab:archive" : "tab:documents";
            bool open = currentArchive == archivedTab;
            if (art)
            {
                var indexTab = Region(ledger, name, 0, LedgerBodyTop - 0.017f, 0, 1);
                indexTab.SetSiblingIndex(ledgerPage.GetSiblingIndex());
                var surface = open ? art.Selection : art.ActionPaper;
                var idle = surface ? Color.white : open ? ReporterMenuArt.Oxblood : ReporterMenuArt.Paper;
                var tabTarget = Register(indexTab, id, ReporterPanel.Ledger, new Vector2(archivedTab ? 0.52f : 0.38f, 0.94f),
                    idle, select, idleSprite: surface, focus: idle);
                var indexLabel = Label(Region(indexTab, "Label", 0.02f, 0.33f, 0.98f, 1),
                    GameText.Translate(name), 12, TextAlignmentOptions.Center);
                indexLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                indexLabel.color = open ? TextColor : PaperInk;
                var focusLine = Paint(Region(indexTab, "Tab focus", 0.18f, 0.41f, 0.82f, 0.46f), indexLabel.color);
                focusLine.enabled = false;
                tabTarget.FocusMarker = focusLine;
                indexLabels[archivedTab ? 1 : 0] = indexLabel;
                return;
            }
            float left = archivedTab ? 0.505f : 0.02f;
            var tab = Region(ledger, name, left, 0.927f, left + 0.475f, 1);
            Register(tab, id, ReporterPanel.Ledger, new Vector2(archivedTab ? 0.52f : 0.38f, 0.94f),
                open ? new Color(0.36f, 0.30f, 0.23f) : ShadowColor, select, focus: ReporterMenuArt.Oxblood);
            Label(tab, GameText.Translate(name), 15, TextAlignmentOptions.Center);
        }

        void FitLedgerTabs()
        {
            if (ledger.rect.width <= 0) return;
            const float edge = LedgerPageInset;
            const float gap = 0.015f;
            float required = 0;
            foreach (var label in indexLabels)
                required = Mathf.Max(required, label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x);
            float width = Mathf.Clamp((required + 14) / ledger.rect.width, 0.268f, (1 - 2 * edge - gap) / 2);
            for (int i = 0; i < indexLabels.Length; i++)
            {
                var tab = (RectTransform)indexLabels[i].transform.parent;
                float left = edge + i * (width + gap);
                tab.anchorMin = new Vector2(left, tab.anchorMin.y);
                tab.anchorMax = new Vector2(left + width, tab.anchorMax.y);
            }
        }

        void ItemCell(RectTransform cell, ReporterNode node, Vector2 position, Action<string> select, ScrollArea scroll)
        {
            var target = Register(cell, node.Id, ReporterPanel.Inventory, position, Color.clear, select, scroll,
                focus: Color.clear);
            string value = ItemReadout(node);
            bool equipped = equipmentMirrors.ContainsKey(node.Id);
            bool hasFooter = value.Length > 0;
            if (equipped && art && art.EquippedShadow)
            {
                float bottom = hasFooter ? 0.08f : 0.04f;
                Art(Region(cell, "Equipped shadow", 0.04f, bottom, 0.96f, bottom + 0.92f), art.EquippedShadow)
                    .color = EquippedShadowColor;
            }

            var icon = Art(Region(cell, "Icon", 0.11f, hasFooter ? 0.30f : 0.11f, 0.89f, hasFooter ? 0.96f : 0.89f),
                Resolve(node.Image));
            var motion = target.AttachIcon(icon, art ? art.ItemOutline : null);
            if (equipped)
                motion.HoverChanged += hovered => MirrorHover(node.Id, hovered);
            DrawItemReadout(cell, value, onIdCard: false);
        }

        void EquipmentMirror(RectTransform cell, ReporterNode node)
        {
            if (node.Id == null)
            {
                Label(cell, node.Title, 11, TextAlignmentOptions.Center).color = art ? PaperInk : TextColor;
                return;
            }

            var mirror = new TargetView
            {
                Backdrop = Paint(cell, Color.clear),
                Idle = Color.clear,
                Focus = Color.clear
            };
            var icon = Art(Region(cell, "Icon", 0.11f, 0.11f, 0.89f, 0.89f), Resolve(node.Image));
            mirror.AttachIcon(icon, art ? art.ItemOutline : null);
            equipmentMirrors.Add(node.Id, mirror);

            DrawItemReadout(cell, ItemReadout(node), onIdCard: true);
        }

        void DrawItemReadout(RectTransform cell, string value, bool onIdCard)
        {
            if (value.Length == 0) return;
            var readout = Region(cell, "Item readout", 0.05f, 0.035f, 0.95f, 0.26f);
            if (onIdCard)
            {
                readout.anchorMin = new Vector2(0.05f, 0);
                readout.anchorMax = new Vector2(0.95f, 0);
                readout.offsetMin = new Vector2(0, -EquipmentReadoutHeight);
            }
            var label = Label(readout, value, 12, onIdCard ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomRight);
            label.color = onIdCard && art ? PaperInk : TextColor;
        }

        static string ItemReadout(ReporterNode node) =>
            string.Join("  ", new[] { node.LoadedAmmo?.ToString("0"), node.Charge?.ToString("P0"), node.StackCount?.ToString("0") }
                .Where(text => !string.IsNullOrEmpty(text)));

        void MirrorHover(string id, bool hovered)
        {
            if (hovered) hoveredEquipmentId = id;
            else if (hoveredEquipmentId == id) hoveredEquipmentId = null;
            RefreshEquipmentMirrors();
        }

        void RefreshEquipmentMirrors()
        {
            foreach (var (id, mirror) in equipmentMirrors)
                mirror.ShowSelection(id == (hoveredEquipmentId ?? selectedId));
        }

        TargetView Register(RectTransform rect, string id, ReporterPanel panel, Vector2 position, Color idle,
            Action<string> select, ScrollArea scroll = null, int entryPriority = 0, bool isTablet = false,
            Sprite idleSprite = null, Sprite focusSprite = null, Color? focus = null)
        {
            navigation.Add(new ReporterFocus.Target(id, panel, position, entryPriority, isTablet));
            var image = MakeControl(rect, idle, () => select(id));
            image.sprite = idleSprite;
            if ((idleSprite && idleSprite.border.sqrMagnitude > 0) || (focusSprite && focusSprite.border.sqrMagnitude > 0))
                image.type = Image.Type.Sliced;
            var result = new TargetView
            {
                Backdrop = image, Idle = idle, Focus = focus ?? FocusColor, IdleSprite = idleSprite,
                FocusSprite = focusSprite ? focusSprite : idleSprite, ScrollArea = scroll
            };
            targets.Add(id, result);
            return result;
        }

        public void ShowFocus(string id, ReporterPanel panel, string legend, bool reveal)
        {
            selectedId = id;
            RefreshEquipmentMirrors();
            foreach (var (target, visual) in targets)
            {
                bool selected = id == target;
                visual.ShowSelection(selected);
                if (visual.Pin)
                    visual.Pin.color = visual.Pinned ? art && !selected ? StampInk : TextColor
                        : selected ? new Color(0.95f, 0.9f, 0.78f, 0.46f) : Color.clear;
            }
            foreach (var (owner, label) in legends)
                label.text = owner == panel ? legend : "";
            if (reveal && id != null && targets.TryGetValue(id, out var view) && view.ScrollArea != null)
                Reveal(view.ScrollArea, view.Backdrop.rectTransform);
        }

        public void Preview(ReporterNode node, bool document, IReadOnlyList<ReporterAction> actions, int activeAction, Action<ReporterVerb> choose)
        {
            readerDim.gameObject.SetActive(false);
            readerScroll.Scroll.gameObject.SetActive(false);
            previousPage.gameObject.SetActive(false);
            nextPage.gameObject.SetActive(false);
            exitPage.gameObject.SetActive(false);
            pageNumber.text = "";
            exitLabel.alignment = TextAlignmentOptions.Left;
            exitLabel.raycastTarget = readerExit.interactable = false;
            int actionRows = Mathf.Max(ItemActionReferenceRows, actions.Count);
            float rowHeight = 1f / actionRows;
            float rowGap = 0.014f * (ItemActionReferenceRows * rowHeight);
            float extraActionHeight = (ItemActionsTop - ItemActionsBottom) * (actionRows - ItemActionReferenceRows) / ItemActionReferenceRows;
            actionList.anchorMax = new Vector2(actionList.anchorMax.x, ItemActionsTop + extraActionHeight);
            ShowPreviewContent(node, document, actions.Count > 0, extraActionHeight);
            Clear(actionList);
            for (int i = 0; i < actions.Count; i++)
            {
                ReporterAction action = actions[i];
                bool selected = i == activeAction;
                float bottom = (actions.Count - 1 - i) * rowHeight;
                var line = Control(actionList, action.Label, 0, bottom, 1, bottom + rowHeight - rowGap,
                    Color.white, () => choose(action.Verb));
                var text = Label(line, GameText.Translate(action.Label), ReporterActionAppearance.BaseFontSize, TextAlignmentOptions.Center);
                var appearance = line.gameObject.AddComponent<ReporterActionAppearance>();
                appearance.Initialize(line.GetComponent<Button>(), text, art);
                appearance.SetSelected(selected);
            }
        }

        void ShowPreviewContent(ReporterNode node, bool document, bool hasActions, float extraActionHeight = 0)
        {
            displayContent.gameObject.SetActive(node != null);
            displayTitle.text = node?.Title ?? "";
            displayText.text = node?.Detail ?? "";
            displayImage.sprite = Resolve(node?.Image);
            displayImage.enabled = displayImage.sprite;
            displayImage.rectTransform.anchorMin = document ? new Vector2(0, 0) : new Vector2(0.07f, hasActions ? 0.54f + extraActionHeight :
                string.IsNullOrEmpty(node?.Detail) ? 0.07f : 0.30f);
            displayImage.rectTransform.anchorMax = document ? new Vector2(1, 0.936f) : new Vector2(0.93f, 0.88f);
            displayText.rectTransform.anchorMin = new Vector2(0.03f, hasActions ? ItemActionsTop + extraActionHeight : 0.05f);
            displayText.rectTransform.anchorMax = new Vector2(0.97f, hasActions ? 0.53f + extraActionHeight : 0.28f);
        }

        public void Read(ReporterNode document, string transcript, int page, int count, float scrollPosition)
        {
            ShowPreviewContent(document, document: true, hasActions: false);
            Clear(actionList);
            readerDim.gameObject.SetActive(true);
            readerScroll.Scroll.gameObject.SetActive(true);
            readerText.text = transcript;
            Canvas.ForceUpdateCanvases();
            FitReader(scrollPosition);
            previousPage.gameObject.SetActive(page > 0 && count > 1);
            nextPage.gameObject.SetActive(page + 1 < count);
            exitPage.gameObject.SetActive(count > 1 && page + 1 == count);
            pageNumber.text = count > 1 ? $"{page + 1} / {count}" : "";
            exitLabel.alignment = count == 1 ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            exitLabel.raycastTarget = readerExit.interactable = count == 1;
        }

        void FitReader(float scrollPosition)
        {
            var readerSize = readerScroll.Scroll.viewport.rect.size;
            float width = Mathf.Max(1, readerSize.x - readerText.margin.x - readerText.margin.z);
            float height = readerText.GetPreferredValues(readerText.text, width, float.PositiveInfinity).y + 4;
            readerScroll.Content.sizeDelta = new Vector2(0, Mathf.Max(readerSize.y, height));
            readerScroll.Scroll.StopMovement();
            readerScroll.Scroll.verticalNormalizedPosition = scrollPosition;
        }

        public void Feedback(string message) => feedback.text = GameText.Translate(message);

        public void Block(bool blocked, bool hidden = false)
        {
            group.alpha = hidden ? 0f : 1f;
            group.interactable = !blocked;
            group.blocksRaycasts = !blocked;
        }

        static void Reveal(ScrollArea area, RectTransform target)
        {
            float viewport = area.Scroll.viewport.rect.height;
            float scrollable = area.Content.rect.height - viewport;
            if (scrollable <= 0) return;
            float top = -target.anchoredPosition.y;
            float bottom = top + target.rect.height;
            float offset = area.Content.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (bottom > offset + viewport) offset = bottom - viewport;
            else return;
            area.Scroll.StopMovement();
            area.Scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(offset / scrollable);
        }

        static ScrollArea Scroll(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var frame = Region(parent, name, left, bottom, right, top);
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            var viewport = Region(frame, "Viewport", 0, 0, 1, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            Paint(viewport, Color.clear).raycastTarget = true;
            var content = Region(viewport, "Scroll content", 0, 1, 1, 1);
            content.pivot = new Vector2(0.5f, 1);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 32;
            return new ScrollArea { Scroll = scroll, Content = content };
        }

        static void OffsetRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(rect.anchorMin.x, 1);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(0, height);
        }

        static RectTransform Region(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        int TextSize(int size) => largeText ? size + 3 : size;

        TMP_Text Label(RectTransform region, string text, int size, TextAlignmentOptions alignment, bool persistent = false)
        {
            if (region.GetComponent<Graphic>()) region = Region(region, "Label", 0, 0, 1, 1);
            var label = region.gameObject.AddComponent<TextMeshProUGUI>();
            if (art && art.InterfaceFont) label.font = art.InterfaceFont;
            label.enableAutoSizing = false;
            label.fontSize = TextSize(size);
            label.color = TextColor;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            label.raycastTarget = false;
            label.text = text;
            if (persistent) persistentText.Add((label, size));
            return label;
        }

        internal static void Chevron(RectTransform parent, float angle)
        {
            var icon = Region(parent, "Chevron", 0.5f, 0.5f, 0.5f, 0.5f);
            icon.localRotation = Quaternion.Euler(0, 0, angle);
            ChevronStroke(icon, 2.5f, -43);
            ChevronStroke(icon, -2.5f, 43);
        }

        static void ChevronStroke(RectTransform parent, float y, float angle)
        {
            var stroke = Region(parent, "Stroke", 0.5f, 0.5f, 0.5f, 0.5f);
            stroke.sizeDelta = new Vector2(8, 1.8f);
            stroke.anchoredPosition = new Vector2(0, y);
            stroke.localRotation = Quaternion.Euler(0, 0, angle);
            Paint(stroke, TextColor);
        }

        static Image Paint(RectTransform region, Color color)
        {
            var image = region.GetComponent<Image>() ?? region.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image Art(RectTransform region, Sprite sprite)
        {
            var image = Paint(region, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite;
            return image;
        }

        Sprite Resolve(Sprite sprite) => art ? art.Resolve(sprite) : sprite;

        static void SetSurface(RectTransform region, Sprite sprite)
        {
            if (sprite) Paint(region, Color.white).sprite = sprite;
        }

        static RectTransform Control(Transform parent, string name, float left, float bottom, float right, float top,
            Color color, Action click)
        {
            var rect = Region(parent, name, left, bottom, right, top);
            MakeControl(rect, color, click);
            return rect;
        }

        static Image MakeControl(RectTransform rect, Color color, Action click)
        {
            var image = Paint(rect, color);
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => click());
            return image;
        }

        static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Discard(parent.GetChild(i).gameObject);
        }

        static void Discard(GameObject child)
        {
            child.SetActive(false);
            // Detach immediately so subsequent refreshes in this frame cannot find the old layout.
            child.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(child);
        }
    }
}
