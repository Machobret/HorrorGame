using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace Game.Editor.AssetLibrary
{
    internal sealed class LibraryThumbnails
    {
        const int MaxInFlight = 3;
        const double ScrollDelay = 0.08;
        const double PollInterval = 0.06;
        const double PreviewTimeout = 4;

        readonly Dictionary<string, (Texture2D image, bool fallback)> cache = new();
        readonly List<Work> active = new();
        IReadOnlyList<LibraryEntry> entries = Array.Empty<LibraryEntry>();
        LibraryEntry selected;
        int start, end, generation;
        double readyAt, nextPoll;

        sealed class Work
        {
            public string guid;
            public int generation;
            public AssetDatabaseLoadOperation load;
            public double previewStarted;
        }

        public Texture2D Get(string guid) => cache.TryGetValue(guid, out var result) ? result.image : null;

        public void SetViewport(IReadOnlyList<LibraryEntry> results, int first, int last, LibraryEntry selection)
        {
            if (ReferenceEquals(entries, results) && start == first && end == last && selected == selection) return;
            entries = results;
            start = first;
            end = last;
            selected = selection;
            readyAt = EditorApplication.timeSinceStartup + ScrollDelay;
        }

        public void Invalidate()
        {
            cache.Clear();
            generation++;
            SetViewport(Array.Empty<LibraryEntry>(), 0, 0, null);
            // Keep outstanding load handles until they finish so rapid refreshes still obey the cap.
        }

        public bool Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < readyAt || now < nextPoll) return false;
            nextPoll = now + PollInterval;

            bool changed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var work = active[i];
                if (!work.load.isDone) continue;
                if (work.generation == generation && IsWanted(work.guid))
                {
                    var prefab = work.load.LoadedObject as GameObject;
                    if (prefab != null)
                    {
                        if (work.previewStarted == 0) work.previewStarted = now;
                        var image = AssetPreview.GetAssetPreview(prefab);
                        if (image == null && now - work.previewStarted < PreviewTimeout) continue;
                        cache[work.guid] = (image != null ? image : AssetPreview.GetMiniThumbnail(prefab), image == null);
                    }
                    else cache[work.guid] = (null, true);
                    changed = true;
                }
                active.RemoveAt(i);
            }
            if (active.Count >= MaxInFlight) return changed;
            if (selected != null && Queue(selected)) return changed;
            for (int i = start; i < end; i++)
                if (Queue(entries[i])) break;
            return changed;
        }

        bool IsWanted(string guid)
        {
            if (selected?.guid == guid) return true;
            for (int i = start; i < end; i++)
                if (entries[i].guid == guid) return true;
            return false;
        }

        bool Queue(LibraryEntry entry)
        {
            if (cache.TryGetValue(entry.guid, out var result) && (result.image != null || result.fallback)) return false;
            foreach (var work in active)
                if (work.guid == entry.guid) return false;

            var load = LoadPrefabAsync(entry.guid);
            if (load == null)
                cache[entry.guid] = (null, true);
            else
                active.Add(new Work { guid = entry.guid, generation = generation, load = load });
            return true;
        }

        // Use the asset's actual file ID. Imported and nested prefabs need not use the same ID.
        internal static AssetDatabaseLoadOperation LoadPrefabAsync(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            EntityId entityId = SearchUtils.GetMainAssetInstanceID(path);
            if (!entityId.IsValid() || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entityId, out string assetGuid, out long localId) ||
                !string.Equals(assetGuid, guid, StringComparison.OrdinalIgnoreCase)) return null;
            return AssetDatabase.LoadObjectAsync(path, localId);
        }
    }
}
