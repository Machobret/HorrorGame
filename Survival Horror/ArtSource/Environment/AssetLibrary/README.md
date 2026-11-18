# Kitchen, Grocery Store and Storage catalogue

The authoritative generated baseline is `Assets/Game/Environment/Editor/AssetLibrary/Catalog.json`. Library-local tags are independent of gameplay GameObject tags and Unity asset labels. Entries point to one existing prefab GUID, with a subject category, overlapping contexts, provenance and separate review status.

Run `python Tools/Environment/build_asset_library.py` from the project root. The generator scans owned environment prefabs across pack folders, uses the reviewed scale plan's semantic subjects for opaque FBX object names, and applies specific subject rules to named assets. It reads historical visual status from the downloaded-pack quality catalogue and does not infer readiness from the pack name. `ClassificationReceipt.json` records category/tag/status totals and unmatched named food/store subjects.

`ReviewedSubjects.json` contains individual silhouette decisions from existing native render captures. Crates were distinguished from compressed waste bales, and mugs, pitchers, tableware, pots, pans and lids were identified individually. A lampshade and waste bales were excluded from this pilot; their prefabs are retained. These decisions refine classification, not materials or geometry.

Other semantically named assets are rule-classified, not newly visually certified. They retain Unreviewed status. Existing Background-only retail facades remain Background even after scale/material repairs. The catalogue is an initial semantic slice, not a claim that the remaining environment library has been classified.

Search synonyms are stored once in the catalogue's synonym table; they are not competing tags. Primary categories are functional; context tags overlap. User refinements in the editor's `Overrides.json` are keyed by GUID and loaded over the generated baseline, so regeneration does not erase them. GUID resolution, rather than recorded paths, is used for previews and placement.

The implementation is under `Assets/Game/Environment/Editor/AssetLibrary`. To validate, create a current isolated snapshot with `python Tools/Environment/prefab_collision.py stage`, then run `python Tools/Environment/verify_asset_library.py stage`. Run Unity in batch mode on that snapshot with `-executeMethod AssetLibraryVerification.RunBatch`, then run `python Tools/Environment/verify_asset_library.py verify`. This checks GUIDs, shared tags, synonym equivalence, include and exclude filters, placement status, favourites and prefab instantiation with collision and Undo. Results go to `Exports/Reviews/AssetLibrary`.
