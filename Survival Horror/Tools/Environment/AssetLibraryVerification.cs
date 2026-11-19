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
            var samples = new[] { "Food & kitchenware/Packaged", "Furniture/Desks", "Storage/Retail Displays" }
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
            Require(entries.Length > 1000, "expected cross-project prop coverage");
            Require(entries.Select(e => e.guid).Distinct().Count() == entries.Length, "one entry per GUID");
            Require(entries.All(e => e.tags.Distinct().Count() == e.tags.Length), "no duplicate tags");
            Require(!entries.SelectMany(e => e.tags).Any(t => new[] {"Shop", "Store", "Retail", "Supermarket"}.Contains(t)), "aliases must not become competing tags");
            Func<LibraryFilter, HashSet<string>> search = f => new HashSet<string>(entries.Where(index.CreateMatcher(f)).Select(e => e.guid));
            var desks = entries.Where(index.CreateMatcher(new LibraryFilter {query = "desk", category = "Furniture/Desks"})).ToArray();
            Require(desks.Length >= 30 && desks.Select(e => e.source).Distinct().Count() >= 2 && desks.All(e => e.CanPlace),
                "desks are searchable and placeable across both source packs");
            var conflicting = new LibraryFilter {category = "Furniture", collection = "Grocery Store", query = "table"};
            Require(!desks.Any(index.CreateMatcher(conflicting)), "a retained grocery collection and table search can hide desks");
            var categoryOnly = AssetLibraryWindow.ClearNarrowing(conflicting);
            Require(categoryOnly.category == "Furniture" && categoryOnly.query == "" && desks.All(index.CreateMatcher(categoryOnly)),
                "clearing the other filters restores every desk while staying in Furniture");
            var groceryWithSearch = new LibraryFilter {collection = "Grocery Store", query = "table"};
            Require(search(groceryWithSearch).Count == 0, "reproduce the empty Grocery Store view from the saved search");
            var grocery = AssetLibraryWindow.ClearNarrowing(groceryWithSearch);
            var groceryStart = AssetLibraryWindow.StartCollection("Grocery Store");
            Require(grocery.collection == "Grocery Store" && search(grocery).SetEquals(search(groceryStart)) && search(groceryStart).Count >= 200,
                "clearing search or choosing a collection exposes the full Grocery Store kit");
            Require(index.catalog.scope.Length <= 5 && index.catalog.scope.All(name => entries.Count(e => e.tags.Contains(name)) >= 100) &&
                !index.catalog.scope.Contains("Pharmacy") && entries.Any(e => e.tags.Contains("Pharmacy")),
                "collection picker only shows substantial kits and smaller contexts remain tagged");
            var families = entries.Where(e => !string.IsNullOrEmpty(e.family)).GroupBy(e => e.family).ToArray();
            Require(families.Length > 25 && families.All(f => f.Count() >= 2 && f.Select(e => (e.category, e.action)).Distinct().Count() == 1),
                "grouped variants share category and placement role");
            LibraryEntry Named(string original) => entries.Single(e => e.original == original);
            var wornDesk = Named("desk1_alt4_weathered");
            Require(wornDesk.family == Named("desk1_alt4").family && wornDesk.family != Named("desk1").family,
                "office desks pair wear variants and preserve their different footprints");
            Require(Named("Urban_DumpsterDiagonalClosed").family == Named("Urban_DumpsterDiagonalClosedBlue").family &&
                Named("Urban_DumpsterDiagonalClosed").family != Named("Urban_DumpsterLidClosedBlue").family,
                "dumpster finishes retain body shape");
            var family = families.First(f => f.Key == wornDesk.family).ToArray();
            var expansion = new Dictionary<string, bool>();
            Require(LibraryGridCard.Build(family, expansion, false).Single().VariantCount == family.Length,
                "a family collapses to one card in browsing");
            expansion[wornDesk.family] = true;
            var secondFamily = families.First(f => f.Key == Named("desk1").family).ToArray();
            expansion[secondFamily[0].family] = true;
            var afterSections = entries.First(e => string.IsNullOrEmpty(e.family));
            var expanded = LibraryGridCard.Build(family.Concat(secondFamily).Append(afterSections).ToArray(), expansion, false);
            var layout = new LibraryGridLayout(expanded, 2, 90);
            Require(expanded.Count == 3 && expanded.Take(2).All(card => card.IsFamily && card.Expanded) &&
                layout.Sections.Count == 2 && layout.Sections[0].Bottom < layout.Sections[1].Top,
                "two groups in the same grid row get separate, non-overlapping sections");
            var variantRows = layout.Rows.Where(row => row.InSection).ToArray();
            foreach (var section in layout.Sections)
            {
                var cards = variantRows.Where(row => row.Top >= section.Top && row.Top < section.Bottom)
                    .SelectMany(row => layout.Cells.Skip(row.First).Take(row.Count)).ToArray();
                Require(cards.All(card => !card.IsFamily) &&
                    cards.Select(card => card.Entry.guid).SequenceEqual(section.Family.Variants.Select(e => e.guid)),
                    "each section contains only the draggable variants of its own group");
            }
            Require(layout.Rows.Last().Top > layout.Sections.Last().Bottom && layout.Cells.Last().Entry == afterSections &&
                layout.FirstRowAfter(layout.Sections[0].Top) == 1,
                "regular results resume outside the sections and viewport lookup skips panel headers");
            var exact = desks.Where(index.CreateMatcher(new LibraryFilter {query = "desk1 alt4 weathered"})).ToArray();
            Require(exact.Length == 1 && LibraryGridCard.Build(exact, expansion, true).Single().Entry == wornDesk &&
                !LibraryGridCard.Build(exact, expansion, true).Single().IsFamily, "exact search returns a directly placeable individual variant");
            var searchGroup = LibraryGridCard.Build(family.Take(2).ToArray(), new Dictionary<string, bool>(), true).Single();
            Require(searchGroup.Expanded && searchGroup.VariantCount == 2,
                "a search with multiple hits expands only the matching variants");
            var roots = index.catalog.browseCategories;
            Require(roots.Length <= 12 && roots.All(root => !root.Contains('/') && search(new LibraryFilter {category = root}).Count > 0),
                "filter exposes only populated broad categories");
            Require(index.BrowseCategory("Furniture/Desks") == "Furniture" && index.BrowseCategory("Doors & access") == "" &&
                index.BrowseCategory("Signs & graphics") == "Decor & signage", "saved filters map to broad prop categories or clear when out of scope");
            Require(entries.All(e => e.view == "Environment" && e.role == "Scene prop" && e.CanPlace),
                "the shelf contains only placeable environment prefabs");
            var interactables = search(new LibraryFilter {category = "Interactables"});
            Require(roots.Contains("Interactables") &&
                new[] { "ElectricityBox", "ControlButton", "IntercomSpeaker", "KeycardReader", "SecurityScreen" }
                    .All(name => interactables.Contains(Named(name).guid) && index.Resolve(Named(name))),
                "electrical interactables have their own category and resolve to prefabs");
            Require(interactables.SetEquals(search(new LibraryFilter {category = "Interactables", query = "interactibles"})),
                "alternate spelling finds the interactables");
            Require(!entries.Any(e => new[] { "/Doors/", "/GarageDoors/", "/Modular/", "/WallFamilies/", "/Windows/", "/RoadSurfaces/", "/Weapons/", "/UI/", "/DisplayGeometry/" }
                    .Any(folder => e.path.Contains(folder))), "loose building parts, doors and gameplay assets stay in Unity's Project browser");
            var buildings = entries.Where(index.CreateMatcher(new LibraryFilter { category = "Buildings" })).ToArray();
            Require(buildings.Length >= 90 && buildings.All(e => e.tags.Contains("Exterior") && e.CanPlace &&
                    index.Resolve(e)?.GetComponentInChildren<MeshRenderer>(true) != null),
                "the Buildings category contains placeable exterior prefabs with render geometry");
            Require(new[] { "Buildings", "FillerBuildings", "Pizzeria", "IndustrialExterior", "ModelsPackPSX", "InteriorFixtures" }
                    .All(source => buildings.Any(e => e.source == source)),
                "authored and imported building families are available together");
            var block = Named("building_04.012");
            Require(!string.IsNullOrEmpty(block.family) && block.family == Named("building_04").family &&
                LibraryGridCard.Build(new[] { Named("building_01"), Named("building_01.001") },
                    new Dictionary<string, bool>(), false).Count == 2 &&
                index.Matches(block, new LibraryFilter { category = "Buildings", query = "building 04 012" }),
                "rotated and matching-footprint versions are grouped and searchable; wider versions stay separate");
            Require(entries.Where(e => e.path.Contains("/Fences/")).Single().original == "Urban_WoodenFencePile",
                "loose fence debris belongs on the shelf while fence modules stay out");
            var workstations = desks.Where(e => e.name.StartsWith("Cubicle desk")).ToArray();
            Require(workstations.Length == 6 && workstations.GroupBy(e => e.family).Count() == 2 &&
                index.Matches(Named("divider_full_h_red_desk"), new LibraryFilter {query = "cubicle desk red", collection = "Office"}),
                "six complete cubicle desks appear under Furniture and remain searchable by finish");
            var screens = entries.Where(e => e.category == "Furniture/Office dividers").ToArray();
            Require(screens.Length == 24 && screens.GroupBy(e => e.family).Count() == 4 &&
                !entries.Any(e => e.original.StartsWith("divider_black") || e.original == "divider_full_h_red"),
                "freestanding dividers retain distinct shapes while bare partition pieces stay out");
            var lights = entries.Where(e => e.path.Contains("/PSXShelvesProps/") && e.path.Contains("/Lighting/")).ToArray();
            Require(lights.Length == 12 && lights.GroupBy(e => e.family).Count() == 6 &&
                lights.All(e => index.Resolve(e).GetComponentInChildren<MeshRenderer>(true) != null &&
                    index.Resolve(e).GetComponentInChildren<Light>(true) != null), "the twelve showroom light prefabs contain fixtures and light sources");
            Require(entries.Count(e => e.category == "Fixtures & utilities/Ventilation fixtures") == 3 &&
                entries.Any(e => e.category == "Fixtures & utilities/Access ladders" && e.name == "Sewer access ladder"),
                "standalone industrial and sewer fixtures survive the construction filter");
            Require(entries.Any(e => e.name == "Pharmacy Shelves"), "reusable chapter props retained");
            Require(entries.Where(e => e.status == "Review" && e.category.StartsWith("Nature & debris/")).Count(e => e.CanPlace) >= 8,
                "review foliage and debris remain available for hand placement");
            Require(search(new LibraryFilter {category = "Food/Packaged"}).SetEquals(search(new LibraryFilter {category = "Food & kitchenware/Packaged"})),
                "legacy categories used in saved filters resolve to the new taxonomy");
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
            Require(entries.Any(e => e.path.Contains("/LightingElectrical/")), "reusable lighting props remain available");
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
            Require(search(new LibraryFilter()).Count == entries.Length, "default prop search includes every status");
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
            foreach (var entry in new[] {canned[0], wornDesk, entries.First(e => e.category == "Food & kitchenware/Cookware"),
                entries.First(e => e.category == "Storage/Containers"), Named("Urban_ApartmentSquareBrick_Exterior"), block})
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
