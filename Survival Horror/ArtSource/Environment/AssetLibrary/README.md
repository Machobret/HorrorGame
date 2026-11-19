# Project prefab catalogue

The authoritative generated baseline is `Assets/Game/Environment/Editor/AssetLibrary/Catalog.json`. Library-local tags are independent of gameplay GameObject tags and Unity asset labels. Entries point to one existing prefab GUID, with a subject category, overlapping contexts, provenance and separate review status.

Run `python -B Tools/Environment/build_asset_library.py` from the project root. The generator derives the original pilot classifications, then selects reusable props and standalone building prefabs from the whole-project inventory. Building kit pieces remain in the Project window. `ClassificationReceipt.json` records broad category counts and exclusions. `python -B Tools/Environment/build_project_asset_inventory.py` regenerates the separate whole-project inventory and its decision and variant queues in `Exports/Reviews/AssetLibrary/WholeProject`.

The inventory command also audits the published catalogue without rewriting it. `ShelfCoverage.json` reports missing eligible prefabs, unexpected shelf entries, unresolved classifications, stale paths and invalid or duplicate GUIDs. `PrefabInventory.csv` gives each owned prefab a shelf outcome and reason. The command exits with an error when coverage differs from the current scope rules.

`ReviewedSubjects.json` contains individual silhouette decisions from existing native render captures. Crates were distinguished from compressed waste bales, and mugs, pitchers, tableware, pots, pans and lids were identified individually. A lampshade and waste bales were excluded from this pilot; their prefabs are retained. These decisions refine classification, not materials or geometry.

Quality flags come from the existing review records. New classifications based on a name or family retain Unreviewed status. Background-only retail facades and the eight foliage or debris review assets remain discoverable. Chapter-bound prefabs stay in the inventory only. Materials, audio, and other non-prefab asset types have a separate inventory.

`ModelsPackPSXSubjects.json` records names reviewed against both sides of the current Unity prefabs. The Asset Library uses these names ahead of the earlier scale-pass descriptions. Confirmed identity corrections can also replace the descriptive role in the scale ledger while retaining the measured dimensions.

`Tools/Environment/asset_library_variants.py` assigns family keys, names, and version labels. Different desk footprints and dumpster bodies stay separate. Confirmed colours, wear, and content states share a family. Retail shelf matches use the saved renders and calibrated sizes. Each version keeps its own GUID, status, tags and original search terms.

Search synonyms are stored once in the catalogue's synonym table; they are not competing tags. Primary categories are functional; context tags overlap. User refinements in the editor's `Overrides.json` are keyed by GUID and loaded over the generated baseline, so regeneration does not erase them. GUID resolution, rather than recorded paths, is used for previews and placement.

The implementation is under `Assets/Game/Environment/Editor/AssetLibrary`. To validate, create a current isolated snapshot with `python Tools/Environment/prefab_collision.py stage`, then run `python Tools/Environment/verify_asset_library.py stage`. Run Unity in batch mode on that snapshot with `-executeMethod AssetLibraryVerification.RunBatch`, then run `python Tools/Environment/verify_asset_library.py verify`. This checks GUIDs, shared tags, synonym equivalence, include and exclude filters, placement status, favourites and prefab instantiation with collision and Undo. Results go to `Exports/Reviews/AssetLibrary`.
