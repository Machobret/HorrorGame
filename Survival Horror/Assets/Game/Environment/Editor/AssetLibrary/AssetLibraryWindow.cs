using HouseToolkit.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.AssetLibrary
{
    public sealed class AssetLibraryWindow : EditorWindow
    {
        enum Drawer { None, Filters, Details }
        [Serializable] sealed class SavedSearch { public string name; public LibraryFilter filter; }
        [Serializable] sealed class UserState
        {
            public List<string> favourites = new List<string>();
            public List<SavedSearch> searches = new List<SavedSearch>();
            public LibraryFilter last = new LibraryFilter();
            public float thumbnail = 80;
            public int layoutVersion;
            public Drawer drawer;
            public bool tagsExpanded, savedExpanded, statusExpanded, sortByCategory;
        }

        [SerializeField] LibraryFilter filter = new LibraryFilter();
        LibraryIndex index;
        readonly LibraryThumbnails thumbnails = new();
        UserState state;
        HashSet<string> favourites;
        List<LibraryEntry> visible = new List<LibraryEntry>();
        LibraryEntry[] byName, byCategory;
        string[] collections, categories, tags;
        readonly Dictionary<string, GUIContent> cardLabels = new();
        static readonly string[] Statuses = {LibraryFilter.DefaultStatus, "Placeable", "Candidate", "Unreviewed", "Review", "Background"};
        static readonly string[] TagModes = {"Match all", "Match any"};
        static readonly int[] ThumbnailSizes = {56, 80, 104, 128, 160};
        const string SearchControl = "AssetLibrarySearch";
        Vector2 gridScroll, filterScroll, detailScroll;
        string selectedGuid, error, savedName = "", tagSearch = "";
        string editTitle, editCategory, editNote, editTags, editStatus;
        bool editing;
        GUIContent filterButton = new GUIContent("Filters"), activeFilterSummary;
        double saveAt, projectChangeAt;
        (DateTime catalogue, DateTime overrides) metadataStamp;
        Vector2 dragOrigin;
        string dragGuid;
        int dragControl;
        UnityEditor.Editor previewEditor;
        GUIStyle titleStyle, smallStyle, cardNameStyle, summaryStyle;
        Texture prefabIcon;
        string PrefKey => "Game.AssetLibrary." + Application.dataPath;

        [MenuItem("Window/Game/Asset Library")]
        public static void Open()
        {
            var window = GetWindow<AssetLibraryWindow>();
            window.titleContent = new GUIContent("Asset Library");
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Asset Library");
            minSize = new Vector2(270, 225);
            state = JsonUtility.FromJson<UserState>(EditorPrefs.GetString(PrefKey, "{}")) ?? new UserState();
            if (state.layoutVersion == 0)
            {
                // The old default was sized for a wide, three-column window.
                if (Mathf.Approximately(state.thumbnail, 128) || state.thumbnail <= 0) state.thumbnail = 80;
            }
            favourites = new HashSet<string>(state.favourites);
            filter = state.last ?? new LibraryFilter();
            if (state.layoutVersion < 2)
            {
                // Replace the old browsing default. Named saved filters retain their original settings.
                if (filter.status == "Placeable") filter.status = LibraryFilter.DefaultStatus;
                state.layoutVersion = 2;
            }
            Reload();
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            SaveState();
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.update -= Tick;
            DestroyPreview();
        }

        void SaveState()
        {
            saveAt = 0;
            if (state == null) return;
            state.last = filter; state.favourites = favourites.OrderBy(s => s).ToList();
            EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(state));
        }

        void OnProjectChanged() => projectChangeAt = EditorApplication.timeSinceStartup + 0.2;

        static (DateTime, DateTime) GetMetadataStamp() => (System.IO.File.GetLastWriteTimeUtc(LibraryIndex.CatalogPath),
            System.IO.File.GetLastWriteTimeUtc(LibraryIndex.OverridesPath));

        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (saveAt > 0 && now >= saveAt) SaveState();
            if (projectChangeAt > 0 && now >= projectChangeAt)
            {
                projectChangeAt = 0;
                if (GetMetadataStamp() != metadataStamp) Reload();
                else
                {
                    thumbnails.Invalidate();
                    DestroyPreview();
                    Repaint();
                }
            }
            if (index != null && error == null && thumbnails.Update()) Repaint();
        }

        void OnBecameInvisible() => thumbnails.SetViewport(Array.Empty<LibraryEntry>(), 0, 0, null);
        void DestroyPreview() { if (previewEditor != null) DestroyImmediate(previewEditor); previewEditor = null; }
        void Reload()
        {
            try
            {
                metadataStamp = GetMetadataStamp();
                index = new LibraryIndex();
                var entries = index.catalog.entries;
                byName = entries.OrderBy(e => e.name).ToArray();
                byCategory = entries.OrderBy(e => e.category).ThenBy(e => e.name).ToArray();
                collections = new[] { "All collections" }.Concat(index.catalog.scope).ToArray();
                categories = new[] {"All categories"}.Concat(entries.Select(e => e.category.Split('/')[0]).Distinct().OrderBy(s => s))
                    .Concat(entries.Select(e => e.category).Where(c => c.Contains('/')).Distinct().OrderBy(s => s)).ToArray();
                tags = entries.SelectMany(e => e.tags).Distinct().OrderBy(s => s).ToArray();
                cardLabels.Clear();
                foreach (var entry in entries)
                    cardLabels.Add(entry.guid, new GUIContent(entry.name, $"{entry.name}\n{entry.category} · {entry.status}"));
                error = null;
                RefreshResults();
            }
            catch (Exception ex) { error = ex.Message; }
            thumbnails.Invalidate();
            DestroyPreview(); Repaint();
        }
        void RefreshResults()
        {
            if (index == null) return;
            visible = (state.sortByCategory ? byCategory : byName).Where(index.CreateMatcher(filter, favourites)).ToList();
            var activeFilters = DescribeActiveFilters(filter);
            string filterTooltip = "Active filters\n" + string.Join("\n", activeFilters);
            activeFilterSummary = activeFilters.Count == 0 ? null :
                new GUIContent("Active filters  ·  " + string.Join("  ·  ", activeFilters), filterTooltip);
            filterButton = new GUIContent(activeFilters.Count == 0 ? "Filters" : $"Filters ({activeFilters.Count})",
                activeFilters.Count == 0 ? "Show or hide filters" : filterTooltip);
            gridScroll = Vector2.zero;
            saveAt = EditorApplication.timeSinceStartup + 0.5;
            Repaint();
        }

        internal static List<string> DescribeActiveFilters(LibraryFilter filter)
        {
            var labels = new List<string>();
            if (!string.IsNullOrEmpty(filter.collection)) labels.Add(filter.collection);
            if (!string.IsNullOrEmpty(filter.category)) labels.Add("Category: " + filter.category);
            if (!string.IsNullOrEmpty(filter.status) && filter.status != LibraryFilter.DefaultStatus) labels.Add("Status: " + filter.status);
            if (filter.favouritesOnly) labels.Add("Favourites only");
            labels.AddRange(filter.include.Select(tag => "+ " + tag));
            labels.AddRange(filter.exclude.Select(tag => "− " + tag));
            return labels;
        }

        void OnGUI()
        {
            if (Event.current.rawType == EventType.MouseUp || Event.current.rawType == EventType.MouseDown)
                ReleaseDrag();
            if (index == null || error != null)
            {
                EditorGUILayout.HelpBox(error ?? "Loading catalogue…", MessageType.Error);
                if (GUILayout.Button("Reload catalogue")) Reload();
                return;
            }
            titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, wordWrap = true };
            smallStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            cardNameStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
            if (prefabIcon == null) prefabIcon = EditorGUIUtility.IconContent("Prefab Icon").image;
            var drawer = state.drawer;
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                if (drawer != Drawer.None) DrawLeftPanel(drawer);
                DrawGrid(drawer == Drawer.Details);
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(new GUIContent($"{visible.Count} / {index.catalog.entries.Length} assets",
                    "Drag a card into the Scene or Hierarchy. Double-click to reveal its prefab."), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (position.width >= 390) GUILayout.Label("Drag into Scene or Hierarchy", EditorStyles.miniLabel);
            }
        }

        void DrawToolbar()
        {
            bool narrow = position.width < 290;
            // Search can refresh the summary during this event. Keep the existing row for this layout pass.
            var summary = activeFilterSummary;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawSearch();
                if (!narrow) DrawToolbarControls();
            }
            if (summary != null)
            {
                summaryStyle ??= new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    fixedHeight = 0,
                    padding = new RectOffset(6, 6, 0, 0)
                };
                float height = EditorGUIUtility.singleLineHeight + 4;
                var row = GUILayoutUtility.GetRect(1, 100000, height, height, GUILayout.ExpandWidth(true));
                GUI.Box(row, GUIContent.none, EditorStyles.toolbar);
                GUI.Label(row, summary, summaryStyle);
            }
            if (narrow)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) DrawToolbarControls();
        }

        void DrawSearch()
        {
            var evt = Event.current;
            bool findCommand = evt.commandName == "Find" && (evt.type == EventType.ValidateCommand || evt.type == EventType.ExecuteCommand);
            bool focus = (findCommand && evt.type == EventType.ExecuteCommand) ||
                (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.F && (evt.control || evt.command));
            if (findCommand || focus) evt.Use();
            GUI.SetNextControlName(SearchControl);
            EditorGUI.BeginChangeCheck();
            filter.query = GUILayout.TextField(filter.query, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true), GUILayout.MinWidth(42));
            if (EditorGUI.EndChangeCheck()) RefreshResults();
            if (focus) EditorGUI.FocusTextInControl(SearchControl);
            if (filter.query.Length == 0 && GUI.GetNameOfFocusedControl() != SearchControl && evt.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                rect.xMin += 18;
                GUI.Label(rect, "Search assets", EditorStyles.miniLabel);
            }
            using (new EditorGUI.DisabledScope(filter.query.Length == 0))
                if (GUILayout.Button(new GUIContent("×", "Clear search"), EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    filter.query = "";
                    EditorGUI.FocusTextInControl(SearchControl);
                    RefreshResults();
                }
        }

        void DrawToolbarControls()
        {
            DrawerButton(Drawer.Filters, filterButton, 72);
            DrawerButton(Drawer.Details, new GUIContent("Details", "Show or hide details for the selected asset"), 50);
            var viewRect = GUILayoutUtility.GetRect(new GUIContent("View"), EditorStyles.toolbarDropDown, GUILayout.Width(49));
            if (!EditorGUI.DropdownButton(viewRect, new GUIContent("View", "Thumbnail size, sort and reload"), FocusType.Passive, EditorStyles.toolbarDropDown)) return;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Sort/Name"), !state.sortByCategory, () => SetSort(false));
            menu.AddItem(new GUIContent("Sort/Category"), state.sortByCategory, () => SetSort(true));
            foreach (int size in ThumbnailSizes)
                menu.AddItem(new GUIContent($"Thumbnail size/{size} px"), Mathf.Approximately(state.thumbnail, size), () =>
                {
                    state.thumbnail = size;
                    SaveState();
                    Repaint();
                });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Reload catalogue"), false, Reload);
            menu.DropDown(viewRect);
        }

        void DrawerButton(Drawer drawer, GUIContent label, float width)
        {
            bool open = state.drawer == drawer;
            if (GUILayout.Toggle(open, label, EditorStyles.toolbarButton, GUILayout.Width(width)) == open) return;
            state.drawer = open ? Drawer.None : drawer;
            if (state.drawer != Drawer.Details) DestroyPreview();
            SaveState();
            Repaint();
        }

        void SetSort(bool sortByCategory)
        {
            state.sortByCategory = sortByCategory;
            RefreshResults();
        }

        void DrawLeftPanel(Drawer drawer)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox,
                GUILayout.Width(LeftPanelWidth(position.width)), GUILayout.ExpandHeight(true)))
            {
                float oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 65;
                try
                {
                    if (drawer == Drawer.Filters) DrawFilters();
                    else DrawDetails();
                }
                finally { EditorGUIUtility.labelWidth = oldLabelWidth; }
            }
        }

        void DrawFilters()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(filterScroll, GUILayout.ExpandHeight(true)))
            {
                filterScroll = scroll.scrollPosition;
                EditorGUI.BeginChangeCheck();
                filter.collection = DrawFilterPopup("Collection", filter.collection, collections);
                filter.category = DrawFilterPopup("Category", filter.category, categories);
                filter.favouritesOnly = EditorGUILayout.ToggleLeft("Favourites only", filter.favouritesOnly);
                if (EditorGUI.EndChangeCheck()) RefreshResults();

                if (GUILayout.Button("Reset filters", EditorStyles.miniButton))
                {
                    filter = new LibraryFilter { query = filter.query };
                    tagSearch = "";
                    RefreshResults();
                }

                bool showTags = state.tagsExpanded, showSaved = state.savedExpanded, showStatus = state.statusExpanded;
                int tagCount = filter.include.Count + filter.exclude.Count;
                state.tagsExpanded = DrawFoldout(showTags, tagCount == 0 ? "Tags" : $"Tags ({tagCount})");
                if (showTags) DrawTagFilters();
                state.savedExpanded = DrawFoldout(showSaved, "Saved filters");
                if (showSaved) DrawSavedFilters();
                string statusLabel = filter.status == LibraryFilter.DefaultStatus ? "Review status" : $"Review status · {filter.status}";
                state.statusExpanded = DrawFoldout(showStatus, statusLabel);
                if (showStatus)
                {
                    EditorGUI.BeginChangeCheck();
                    int selected = Mathf.Max(0, Array.IndexOf(Statuses, filter.status));
                    int choice = EditorGUILayout.Popup(selected, Statuses);
                    if (EditorGUI.EndChangeCheck())
                    {
                        filter.status = Statuses[choice];
                        RefreshResults();
                    }
                }
                if (state.tagsExpanded != showTags || state.savedExpanded != showSaved || state.statusExpanded != showStatus) SaveState();
            }
        }

        static bool DrawFoldout(bool expanded, string label)
        {
            var rect = EditorGUILayout.GetControlRect(false);
            rect.xMin += 12;
            return EditorGUI.Foldout(rect, expanded, label, true);
        }

        static string DrawFilterPopup(string label, string selected, string[] options)
        {
            int current = Mathf.Max(0, Array.IndexOf(options, selected));
            int choice = EditorGUILayout.Popup(label, current, options);
            return choice == 0 ? "" : options[choice];
        }

        void DrawTagFilters()
        {
            tagSearch = EditorGUILayout.TextField("Find tag", tagSearch);
            bool changed = false;
            EditorGUI.BeginChangeCheck();
            filter.requireAll = GUILayout.Toolbar(filter.requireAll ? 0 : 1, TagModes) == 0;
            changed |= EditorGUI.EndChangeCheck();
            GUILayout.Label("+ Include    − Exclude", EditorStyles.miniLabel);
            foreach (string tag in tags)
            {
                if (tag.IndexOf(tagSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool include = filter.include.Contains(tag), exclude = filter.exclude.Contains(tag);
                    bool nextInclude = GUILayout.Toggle(include, "+", EditorStyles.miniButtonLeft, GUILayout.Width(24));
                    bool nextExclude = GUILayout.Toggle(exclude, "−", EditorStyles.miniButtonRight, GUILayout.Width(24));
                    if (nextInclude != include)
                    {
                        filter.include.Remove(tag);
                        if (nextInclude) { filter.include.Add(tag); filter.exclude.Remove(tag); }
                        changed = true;
                    }
                    if (nextExclude != exclude)
                    {
                        filter.exclude.Remove(tag);
                        if (nextExclude) { filter.exclude.Add(tag); filter.include.Remove(tag); }
                        changed = true;
                    }
                    GUILayout.Label(tag, EditorStyles.miniLabel);
                }
            }
            if (changed) RefreshResults();
        }

        void DrawSavedFilters()
        {
            foreach (var saved in state.searches.ToArray())
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(saved.name, EditorStyles.miniButton)) { filter = saved.filter.Copy(); RefreshResults(); }
                    if (GUILayout.Button(new GUIContent("×", "Remove saved filter"), EditorStyles.miniButton, GUILayout.Width(22)))
                    { state.searches.Remove(saved); SaveState(); }
                }
            using (new EditorGUILayout.HorizontalScope())
            {
                savedName = EditorGUILayout.TextField(savedName, GUILayout.MinWidth(28));
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(savedName)))
                    if (GUILayout.Button("Save filter", EditorStyles.miniButton, GUILayout.Width(68)))
                    {
                        state.searches.RemoveAll(s => s.name == savedName.Trim());
                        state.searches.Add(new SavedSearch { name = savedName.Trim(), filter = filter.Copy() });
                        savedName = "";
                        SaveState();
                    }
            }
        }

        internal static float LeftPanelWidth(float windowWidth) =>
            Mathf.Min(Mathf.Clamp(windowWidth * 0.34f, 150, 255), Mathf.Max(1, windowWidth - 108));

        internal static (int columns, float cellWidth, float imageSize, float rowHeight) GridMetrics(float viewportWidth, float thumbnail, float scrollbar)
        {
            const float gap = 4;
            float available = Mathf.Max(1, viewportWidth - scrollbar - gap);
            int columns = Mathf.Max(1, Mathf.FloorToInt(available / (thumbnail + 12)));
            float cellWidth = available / columns;
            float imageSize = Mathf.Max(1, Mathf.Min(thumbnail, cellWidth - 12));
            return (columns, cellWidth, imageSize, imageSize + 34);
        }

        void DrawGrid(bool showDetails)
        {
            var viewport = GUILayoutUtility.GetRect(1, 100000, 48, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Layout || viewport.width <= 1 || viewport.height <= 1) return;
            float scrollbar = Mathf.Max(14, GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left);
            var (columns, width, imageSize, height) = GridMetrics(viewport.width, state.thumbnail, scrollbar);
            int rows = Mathf.CeilToInt(visible.Count / (float)columns);
            float total = rows * height;
            gridScroll.y = Mathf.Clamp(gridScroll.y, 0, Mathf.Max(0, total - viewport.height));
            float contentWidth = columns * width;
            gridScroll = GUI.BeginScrollView(viewport, gridScroll, new Rect(0, 0, contentWidth, total));
            int firstRow = Mathf.FloorToInt(gridScroll.y / height);
            int lastRow = Mathf.Min(rows, Mathf.CeilToInt((gridScroll.y + viewport.height) / height));
            if (Event.current.type == EventType.Repaint)
                thumbnails.SetViewport(visible, firstRow * columns, Mathf.Min(visible.Count, lastRow * columns), showDetails ? index.Find(selectedGuid) : null);
            for (int row = firstRow; row < lastRow; row++)
                for (int col = 0; col < columns; col++)
                {
                    int i = row * columns + col;
                    if (i >= visible.Count) break;
                    DrawCard(new Rect(4 + col * width, row * height + 2, width - 4, height - 4), imageSize, visible[i]);
                }
            if (visible.Count == 0) GUI.Label(new Rect(8, 10, contentWidth - 16, 64),
                "No matching assets. Clear search or adjust filters.", EditorStyles.wordWrappedLabel);
            GUI.EndScrollView();
        }

        void DrawCard(Rect rect, float imageSize, LibraryEntry entry)
        {
            int control = GUIUtility.GetControlID(entry.guid.GetHashCode(), FocusType.Passive, rect);
            bool selected = selectedGuid == entry.guid;
            GUI.Box(rect, GUIContent.none, selected ? "SelectionRect" : "HelpBox");
            var imageRect = new Rect(rect.center.x - imageSize / 2, rect.y + 3, imageSize, imageSize);
            if (Event.current.type == EventType.Repaint)
            {
                var image = thumbnails.Get(entry.guid);
                if (image != null) GUI.DrawTexture(imageRect, image, ScaleMode.ScaleToFit);
                else DrawPrefabIcon(imageRect);
            }
            var star = new Rect(rect.xMax - 23, rect.y + 5, 20, 19);
            if (GUI.Button(star, favourites.Contains(entry.guid) ? "★" : "☆", EditorStyles.miniButton))
            { if (!favourites.Add(entry.guid)) favourites.Remove(entry.guid); RefreshResults(); }
            GUI.Label(new Rect(rect.x + 4, imageRect.yMax + 1, rect.width - 8, 25), cardLabels[entry.guid], cardNameStyle);
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition) && !star.Contains(evt.mousePosition))
            {
                selectedGuid = entry.guid; editing = false; detailScroll = Vector2.zero; DestroyPreview();
                dragOrigin = evt.mousePosition; dragGuid = entry.guid;
                dragControl = control; GUIUtility.hotControl = control;
                if (evt.clickCount == 2) { ReleaseDrag(); Reveal(entry); }
                evt.Use(); Repaint();
            }
            if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == control && dragGuid == entry.guid &&
                (evt.mousePosition - dragOrigin).sqrMagnitude > 16)
            {
                // Standard prefab drag: Unity owns surface placement, prefab links, hierarchy targets and Undo.
                ReleaseDrag();
                var prefab = ResolveForAction(entry);
                if (prefab == null) { evt.Use(); return; }
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] {prefab};
                DragAndDrop.paths = new[] {AssetDatabase.GetAssetPath(prefab)};
                DragAndDrop.StartDrag(entry.name); evt.Use();
            }
        }

        void DrawPrefabIcon(Rect rect)
        {
            if (prefabIcon != null)
                GUI.DrawTexture(new Rect(rect.center.x - 16, rect.center.y - 16, 32, 32), prefabIcon, ScaleMode.ScaleToFit);
        }

        GameObject ResolveForAction(LibraryEntry entry)
        {
            var prefab = index.Resolve(entry);
            if (prefab == null) ShowNotification(new GUIContent("Prefab is missing from the project."));
            return prefab;
        }

        void Reveal(LibraryEntry entry)
        {
            var prefab = ResolveForAction(entry);
            if (prefab == null) return;
            ProjectAssetNavigation.Reveal(prefab);
        }

        void ReleaseDrag()
        {
            if (dragControl != 0 && GUIUtility.hotControl == dragControl)
                GUIUtility.hotControl = 0;
            dragControl = 0;
            dragGuid = null;
        }

        void DrawDetails()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(detailScroll, GUILayout.ExpandHeight(true)))
            {
                detailScroll = scroll.scrollPosition;
                var entry = index.Find(selectedGuid);
                if (entry == null)
                {
                    GUILayout.Label("Select an asset", titleStyle);
                    return;
                }
                GUILayout.Label(entry.name, titleStyle);
                string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrEmpty(path))
                {
                    var previewRect = GUILayoutUtility.GetRect(1, 124, GUILayout.ExpandWidth(true));
                    if (ProjectAssetNavigation.IsDoubleClick(previewRect)) { Event.current.Use(); Reveal(entry); }
                    if (previewEditor != null) previewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.helpBox);
                    else if (Event.current.type == EventType.Repaint)
                    {
                        var texture = thumbnails.Get(entry.guid);
                        if (texture != null) GUI.DrawTexture(previewRect, texture, ScaleMode.ScaleToFit);
                        else DrawPrefabIcon(previewRect);
                    }
                    if (GUILayout.Button(previewEditor != null ? "Close 3D preview" : "Interactive 3D preview"))
                    {
                        if (previewEditor != null) DestroyPreview();
                        else if (ResolveForAction(entry) is GameObject prefab)
                        {
                            previewEditor = UnityEditor.Editor.CreateEditor(prefab);
                            if (previewEditor == null || !previewEditor.HasPreviewGUI())
                            {
                                DestroyPreview();
                                ShowNotification(new GUIContent("Interactive preview is unavailable."));
                            }
                        }
                    }
                }
                GUILayout.Label(entry.category, EditorStyles.wordWrappedLabel);
                GUILayout.Label("Status: " + entry.status);
                if (entry.status == "Unreviewed") EditorGUILayout.HelpBox("Classified from its named subject. Full visual quality review has not been recorded.", MessageType.Info);
                if (entry.note.Length > 0) EditorGUILayout.HelpBox(entry.note, MessageType.Info);
                GUILayout.Label("TAGS", EditorStyles.boldLabel);
                foreach (string tag in entry.tags)
                    if (GUILayout.Button(tag, EditorStyles.miniButton)) { if (!filter.include.Contains(tag)) filter.include.Add(tag); filter.exclude.Remove(tag); RefreshResults(); }
                if (entry.size.Length == 3) GUILayout.Label($"Recorded size: {entry.size[0]:0.##} × {entry.size[1]:0.##} × {entry.size[2]:0.##} m", smallStyle);
                if (!string.IsNullOrEmpty(path))
                {
                    if (GUILayout.Button("Reveal in Project")) Reveal(entry);
                    if (GUILayout.Button("Open prefab") && ResolveForAction(entry) is GameObject prefab) AssetDatabase.OpenAsset(prefab);
                }
                else EditorGUILayout.HelpBox("The catalogue GUID does not resolve to a prefab. Reload after restoring the asset.", MessageType.Error);
                GUILayout.Space(8);
                GUILayout.Label("PROVENANCE", EditorStyles.boldLabel);
                GUILayout.Label(entry.source + " · " + entry.original, smallStyle);
                GUILayout.Label(path, smallStyle);
                GUILayout.Label(entry.classification, smallStyle);
                GUILayout.Space(8);
                if (GUILayout.Button(editing ? "Cancel editing" : "Edit classification"))
                {
                    editing = !editing; editTitle = entry.name; editCategory = entry.category; editStatus = entry.status;
                    editNote = entry.note; editTags = string.Join(", ", entry.tags);
                }
                if (editing) DrawEdit(entry);
            }
        }

        void DrawEdit(LibraryEntry entry)
        {
            editTitle = EditorGUILayout.TextField("Name", editTitle);
            editCategory = EditorGUILayout.TextField("Category", editCategory);
            editStatus = index.catalog.statuses[EditorGUILayout.Popup("Status", Mathf.Max(0, Array.IndexOf(index.catalog.statuses, editStatus)), index.catalog.statuses)];
            GUILayout.Label("Tags (comma separated)", smallStyle);
            editTags = EditorGUILayout.TextArea(editTags, GUILayout.MinHeight(45));
            GUILayout.Label("Review note", smallStyle);
            editNote = EditorGUILayout.TextArea(editNote, GUILayout.MinHeight(45));
            if (GUILayout.Button("Save classification"))
            {
                try
                {
                    // Resolve spelling/case against existing canonical tags; search aliases stay in the search system.
                    var known = index.catalog.entries.SelectMany(e => e.tags).Distinct().ToArray();
                    var tags = editTags.Split(',').Select(t => known.FirstOrDefault(k => k.Equals(t.Trim(), StringComparison.OrdinalIgnoreCase)) ?? t.Trim());
                    index.SaveOverride(entry, editTitle, editCategory, editStatus, editNote, tags);
                    editing = false; Reload();
                }
                catch (Exception ex) { ShowNotification(new GUIContent(ex.Message)); }
            }
        }
    }
}
