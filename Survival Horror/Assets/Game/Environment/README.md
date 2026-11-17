# Environment asset library

The Pizzeria, Sewer, IndustrialExterior, MetroPSX, ModelsPackPSX and Office packs
provide native placement prefabs in each pack's `Prefabs` folder. These contain
individual props, modular pieces and authored multipart assemblies, with URP
materials, bottom-centred pivots and static mesh collision. Their saved meshes
do not depend on the combined source FBXs. The source-object mapping, duplicates,
quality exclusions and texture caveats are recorded in
`ArtSource/Environment/DownloadedEnvironmentPacks/ImportReceipt.json`; the adjacent
README documents sources and the isolated batch rebuild workflow.

Use Unity's Project search `t:Prefab l:DownloadedKeep` for the 642 placement
candidates from these six packs. Another 291 retain scale/visual review and 47
are `DownloadedBackgroundOnly` photo proxies. The approved curation removed 161
generic building parts/incomplete assemblies and repaired 69 retained prefabs;
signs/posters, billboard foliage and debris remain. The current decisions and
material-repair receipt are in `ArtSource/Environment/DownloadedEnvironmentPacks`.
All 338 retained ModelsPackPSX prefabs still require scale calibration by family.

The earlier downloaded-prop batches are grouped by functional family. Each family keeps its FBX mesh library, category atlases, materials and individual visual prefabs together. Placeable art prefabs include static collision; gameplay behavior remains assigned by their consumers. `Tools/Environment/prefab_collision.py` audits resolved prefab hierarchies and repairs missing solid collision without replacing existing setups.

The source review is `Downloads/DownloadedAssets_Review.blend`. Its adjacent `DownloadedAssets_ImportStatus.json` records verified imports, GUIDs and deferred content. The Blender file also embeds the receipt and preserves original meshes, UVs and materials. Imported representations appear in a separate viewport area under `UnityImported_Atlased`.

Current scope uses existing solid furniture variants. Characters, construction, wall/fence families and their materials remain deferred. Foliage contributes textures/materials for custom shrub meshes; its downloaded meshes are excluded. Generic mine props use functional categories, while specialized equipment remains under `MineEquipment`.

The Blender recipe is `ArtSource/Environment/DownloadedProps/build_atlases.py`. It and Unity `EnvironmentAssetImportBuilder.RunBatch` use an export manifest and produce a verified import receipt. Read the handoff record before importing another batch; an object present in the source catalog is not necessarily imported into Unity.

Classic64 is fully imported using the `Classic64_` prefix in these same functional
families: 109 finished environment visual prefabs, including the 11 retained pilot prefabs.
Its two source example assemblies live under `Assets/Sandbox/Classic64/Examples`.
All 130 Blender sources (110 represented, 20 construction or quality exclusions) and
732 supplied PNG/JPEG images are reconciled in
`ArtSource/Environment/Classic64/Catalog.json`; manifests and receipts there record
runtime paths, GUIDs, repairs, scale choices and excluded source content.
The earlier downloaded-prop batch's deferred scope and ledger are unchanged. Open
`Assets/Sandbox/Classic64/Classic64_LibraryReview.unity` for the historical model and
material gallery, or retain `Classic64_PilotReview.unity` for the original comparison.
Gallery instances are scaled for inspection; use runtime prefabs for placement.

Classic64 standalone wall, door and window geometry is excluded. Only its complete
textured `Building01` and finished props are retained; surface textures remain available.
The pilot door was removed under the earlier geometry revision. The subsequent cleanup
removed seven unfinished/unpainted models and 21 untextured materials. Every scene file
was preserved, so the historical library review retains missing references to those items.
