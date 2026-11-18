using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Editor.AssetLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in the isolated review project; no scenes or prefabs are saved by these checks.
public static class AssetLibraryVerification
{
    static int checks;
    static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Asset library check failed: " + message);
    }
    public static void RunBatch()
    {
        try
        {
            var index = new LibraryIndex();
            var samples = new[] { "Food/Packaged", "Retail/Checkout", "Storage/RetailDisplays" }
                .Select(category => index.catalog.entries.First(entry => entry.category == category)).ToArray();
            Require(LibraryThumbnails.LoadPrefabAsync(new string('0', 32)) == null, "missing GUID does not start a load");
            var requests = samples.Select(entry => LibraryThumbnails.LoadPrefabAsync(entry.guid)).ToArray();
            Require(requests.All(request => request != null), "asynchronous requests use valid prefab file IDs");
            double deadline = EditorApplication.timeSinceStartup + 30;

            void Poll()
            {
                bool done = requests.All(request => request.isDone);
                if (!done && EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                try
                {
                    Require(done, "async prefab loads completed while the editor was pumping events");
                    for (int i = 0; i < requests.Length; i++)
                    {
                        var prefab = requests[i].LoadedObject as GameObject;
                        Require(prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab), "async load returns the prefab GameObject");
                        Require(AssetDatabase.GetAssetPath(prefab) == AssetDatabase.GUIDToAssetPath(samples[i].guid), "async load retains prefab identity");
                    }
                    VerifyCatalogue(index);
                }
                catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
            }

            EditorApplication.update += Poll;
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    static void VerifyCatalogue(LibraryIndex index)
    {
        try
        {
            var entries = index.catalog.entries;
            Require(AssetLibraryWindow.DescribeActiveFilters(new LibraryFilter {query = "fridge"}).Count == 0,
                "search text and the default status leave the active filter row hidden");
            var activeFilters = new LibraryFilter {collection = "Grocery Store", category = "Storage", status = "Review", favouritesOnly = true};
            activeFilters.include.Add("Stockroom");
            activeFilters.exclude.Add("Horror");
            var filterLabels = AssetLibraryWindow.DescribeActiveFilters(activeFilters);
            Require(filterLabels.Count == 6 && filterLabels.Contains("Status: Review") && filterLabels.Contains("+ Stockroom") && filterLabels.Contains("− Horror"),
                "the active filter row reports status overrides and both tag directions");
            var narrow = AssetLibraryWindow.GridMetrics(300, 80, 15);
            var medium = AssetLibraryWindow.GridMetrics(610, 80, 15);
            var compact = AssetLibraryWindow.GridMetrics(610, 56, 15);
            Require(narrow.columns == 3 && medium.columns == 6, "compact grid uses the full measured dock width");
            Require(compact.columns > medium.columns && compact.rowHeight < medium.rowHeight, "smaller thumbnails fit more assets on both axes");
            float narrowGridWidth = 270 - AssetLibraryWindow.LeftPanelWidth(270);
            float mediumGridWidth = 610 - AssetLibraryWindow.LeftPanelWidth(610);
            Require(medium.cellWidth * medium.columns + 15 < 610 && AssetLibraryWindow.GridMetrics(narrowGridWidth, 80, 15).columns == 1 &&
                AssetLibraryWindow.GridMetrics(mediumGridWidth, 80, 15).columns >= 4,
                "the left panel leaves room for results in narrow and medium docks");
            Require(entries.Length > 300, "expected cross-pack catalogue");
            Require(entries.Select(e => e.guid).Distinct().Count() == entries.Length, "one entry per GUID");
            Require(entries.All(e => e.tags.Distinct().Count() == e.tags.Length), "no duplicate tags");
            Require(!entries.SelectMany(e => e.tags).Any(t => new[] {"Shop", "Store", "Retail", "Supermarket"}.Contains(t)), "aliases must not become competing tags");
            Func<LibraryFilter, HashSet<string>> search = f => new HashSet<string>(entries.Where(index.CreateMatcher(f)).Select(e => e.guid));
            var stores = search(new LibraryFilter {query = "store", status = "All"});
            Require(stores.Count > 80, "store search has substantial results");
            foreach (string alias in new[] {"shop", "retail", "supermarket", "\"grocery store\"", "grocery store"})
                Require(stores.SetEquals(search(new LibraryFilter {query = alias, status = "All"})), "equivalent alias: " + alias);
            var canned = entries.Where(e => e.original.Contains("FoodCannedSoup")).ToArray();
            Require(canned.Length >= 9, "canned soup family found");
            Require(canned.All(e => e.tags.Contains("Kitchen") && e.tags.Contains("Grocery Store")), "canned food crosses contexts");
            Require(canned.All(e => index.Matches(e, new LibraryFilter {query = "canned food", collection = "Grocery Store"})), "synonym search finds canned food in store");
            Require(entries.Any(e => e.name.StartsWith("Shopping cart") && e.tags.Contains("Grocery Store")), "shopping equipment present");
            Require(entries.Any(e => index.Matches(e, new LibraryFilter {collection = "Grocery Store", category = "Storage", include = new List<string> {"Stockroom"}})), "grocery stockroom combines filters");
            Require(entries.All(e => !e.path.Contains("/GarageDoors/") && !e.path.Contains("/LightingElectrical/")), "unrelated architecture and electrical parts excluded");
            Require(entries.Any(e => index.Matches(e, new LibraryFilter {query = "fridge", collection = "Kitchen"})), "fridge alias finds kitchen refrigeration");
            var allFilter = new LibraryFilter {include = new List<string> {"Kitchen", "Grocery Store"}, requireAll = true, status = "All"};
            var anyFilter = allFilter.Copy(); anyFilter.requireAll = false;
            var all = search(allFilter); var any = search(anyFilter);
            Require(all.Count > 0 && all.IsProperSubsetOf(any), "all/any have distinct results");
            var exclude = anyFilter.Copy(); exclude.exclude.Add("Kitchen");
            Require(entries.Where(e => index.Matches(e, exclude)).All(e => !e.tags.Contains("Kitchen")), "exclusion wins over inclusion");
            Require(entries.Where(e => index.Matches(e, new LibraryFilter {query = "store -storage", status = "All"})).All(e => !e.tags.Contains("Storage")), "negative search terms");
            Require(search(new LibraryFilter {query = "\"cold storage\"", status = "All"}).SetEquals(search(new LibraryFilter {query = "fridge", status = "All"})), "multiword alias");
            Require(entries.Where(e => index.Matches(e, new LibraryFilter {category = "Storage", status = "All"})).All(e => e.category.StartsWith("Storage/")), "parent category filter");
            Require(search(new LibraryFilter()).Count == entries.Length, "default search includes every status");
            var placeable = new LibraryFilter {status = "Placeable"};
            Require(!entries.Where(index.CreateMatcher(placeable)).Any(e => e.status == "Review" || e.status == "Background"),
                "Placeable still excludes review/background when explicitly selected");
            var favourite = new HashSet<string> {canned[0].guid};
            Require(entries.Count(e => index.Matches(e, new LibraryFilter {favouritesOnly = true}, favourite)) == 1, "favourites filter");
            var query = new LibraryFilter {query = "supermarket -\"cold storage\"", status = "All"};
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var expected = entries.Count(entry => index.Matches(entry, query));
            double perEntryQueryMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            const int iterations = 100;
            for (int i = 0; i < iterations; i++)
                Require(entries.Count(index.CreateMatcher(query)) == expected, "one compiled query matches the per-entry result");
            double perSearchMs = timer.Elapsed.TotalMilliseconds / iterations;
            Debug.Log($"Asset library search on {entries.Length} entries: {perSearchMs:F2} ms per query, {perEntryQueryMs:F2} ms parsing for every entry.");
            int meshParts = 0, colliders = 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var entry in entries)
            {
                var prefab = index.Resolve(entry);
                Require(prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab), "resolvable prefab: " + entry.path);
                Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)) == entry.guid, "stable GUID: " + entry.name);
                Require(!string.IsNullOrEmpty(entry.category) && !string.IsNullOrEmpty(entry.name), "metadata: " + entry.guid);
                meshParts += prefab.GetComponentsInChildren<MeshFilter>(true).Length;
                colliders += prefab.GetComponentsInChildren<Collider>(true).Count(c => c.enabled && !c.isTrigger);
            }
            foreach (var entry in new[] {canned[0], entries.First(e => e.category == "Retail/Checkout"), entries.First(e => e.category == "Kitchenware/Cookware"), entries.First(e => e.category == "Storage/Containers")})
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(index.Resolve(entry), scene);
                Undo.RegisterCreatedObjectUndo(instance, "Place library asset");
                Require(PrefabUtility.IsPartOfPrefabInstance(instance), "placement retains prefab link");
                Require(instance.GetComponentsInChildren<Renderer>(true).Length > 0, "placement has geometry");
                Require(instance.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger), "placement retains solid collision");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Require(instance == null, "placement is undoable");
            }
            Directory.CreateDirectory("Library/AssetLibraryReview");
            File.WriteAllText("Library/AssetLibraryReview/Verification.json", JsonUtility.ToJson(new Receipt {verified = true, entries = entries.Length, checks = checks, meshParts = meshParts, colliders = colliders, searchMs = perSearchMs, perEntryQueryMs = perEntryQueryMs}, true));
            Debug.Log($"Asset library verification passed: {entries.Length} entries, {checks} checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    [Serializable] sealed class Receipt { public bool verified; public int entries, checks, meshParts, colliders; public double searchMs, perEntryQueryMs; }
}
