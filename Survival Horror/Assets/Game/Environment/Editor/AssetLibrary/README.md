# Asset Library

Open **Window > Game > Asset Library**. This is a dockable editor tool; its catalogue and code are excluded from player builds.

The catalogue is a shelf for reusable, placeable scene props. Each entry uses its existing prefab GUID and has one broad category, with optional context tags. Doors, buildings, construction kits, gameplay, and technical assets remain in Unity’s Project browser. Select **Furniture** and search for “desk” to find the desk family.

## Browsing and placement

- Search is pinned at the top. Use **Ctrl+F** or **Cmd+F** to focus it, and **×** to clear the query.
- The **Active filters** row appears below search when a filter is applied. It includes any active search text. **Clear others** keeps the selected category, or the collection when there is no category, and clears the rest.
- **Filters** and **Details** share the left panel. Click the active button again to return to the full-width grid. **Tags** and **Saved filters** fold away independently. **Reset search & filters** returns to the full shelf.
- Use **View** to change thumbnail size or sort order. The grid fits as many columns as the current dock width allows.
- Click a family card with a **+** count to show its individual versions. Drag or favourite the specific version you want. Search matches the family, original prefab name, and version label. One matching version appears directly, and families with several matches expand to show just those results.
- Select a collection to open its full scene kit, then use search or a broad category to narrow it. The collection menu shows each kit's unfiltered prefab count. Small contexts such as Pharmacy and Metro remain in Tags. The detailed subject appears in Details and is searchable. Expand **Review status** when you need to browse by quality status.
- Use **+** to include a tag or **−** to exclude it. **Match all / Match any** controls included tags; exclusions always win. Collection and category filters are additional requirements.
- Search resolves synonyms centrally: shop/store/retail/supermarket find Grocery Store, fridge/freezer find Refrigeration, pans find Cookware. Aliases are not attached to every asset. Positive search terms are combined with AND, quotes keep a phrase together, and `-word` or `-"quoted phrase"` excludes text. Multiword aliases work without quotes, too.
- Drag a thumbnail into the Scene or Hierarchy using Unity's standard prefab placement. Placement retains prefab links and the prefab's collision/materials, and uses the normal Unity Undo workflow.
- Select an entry for its thumbnail, tags, recorded dimensions, review notes and provenance. Choose **Interactive 3D preview** to rotate the asset. Double-click reveals the prefab. **Open prefab** opens Prefab Mode.
- Stars and saved filters are personal, stored in EditorPrefs per project. Filters and thumbnail size survive reopening the window.

## Status meaning

**All** is the browsing default and includes **Review** and **Background** entries. The optional **Placeable** filter limits results to **Candidate** entries marked Keep in the visual review and **Unreviewed** entries classified by subject. Quality status is also visible in Details.

Dimensions are recorded audit values when available, not live measurements of edited prefabs. Thumbnails/previews use the current GUID-resolved asset. Missing GUIDs appear explicitly as missing prefabs.

## Refining classifications

Use **Edit classification** in the details panel to change a display name, category, tags, status or review note. Changes are saved to `Overrides.json` here, keyed by prefab GUID, and should be committed with its `.meta` file. Synonymous tags resolve to existing canonical tags when saving. The generated catalogue remains reproducible, and these edits survive catalogue regeneration. Names are display names; this does not rename the prefab file.

Source classification rules, evidence and rebuild instructions: `ArtSource/Environment/AssetLibrary/README.md`. Existing pack builders do not overwrite this editor catalogue or its overrides. Rebuild the catalogue after import/curation changes; **Reload** only reloads current metadata and resolves moved GUIDs.
