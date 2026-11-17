# Downloaded environment prefab libraries

Place assets from `Assets/Game/Environment/<Pack>/Prefabs`. The six packs are IndustrialExterior, MetroPSX, ModelsPackPSX, Office, Pizzeria and Sewer. `QualityCatalog.json` is the current decision ledger, including historical removed entries; `MaintenancePlan.json` records approved curation and exact replacement material assignments. `ImportReceipt.json` preserves the initial import accounting. `MaintenanceReceipt.json` records current verified prefab paths and material dependencies.

## Current usable library

| Decision | Prefabs | Unity Project search |
| --- | ---: | --- |
| Placement candidates | 925 | `t:Prefab l:DownloadedKeep` |
| Visual-quality review | 8 | `t:Prefab l:DownloadedReview` |
| Background photo proxies | 47 | `t:Prefab l:DownloadedBackgroundOnly` |
| Removed from prefab library | 161 | Recorded in the catalog; no runtime prefab |

There are 980 retained prefabs: IndustrialExterior 58, MetroPSX 37, ModelsPackPSX 338, Office 168, Pizzeria 264 and Sewer 115. Signs/posters (including US plates, Route 66/Hollywood graphics and vintage advertisements), billboard foliage and debris are retained by explicit direction. Generic building parts, loose furniture/fixture components, open-roof shells, incomplete assemblies and source sample-layout fragments were removed after dependency audits. Complete furniture and specialized sewer/metro modules remain. The raw source collections and original downloads remain preserved; removal applies to the placement prefab library.

Keep means visually screened placement candidate, not completed gameplay, traversal or interaction certification. All 338 retained ModelsPackPSX prefabs are now metre-calibrated by object/family: 335 resized, three plausible fixtures/trays unchanged. The blanket `DownloadedScaleReview` label is removed. `ArtSource/Environment/ScaleCalibration/ScalePlan.json` and `ScaleReceipt.json` record original/current dimensions and references; 54 corrections in other asset families are recorded there too. Forty-seven shop-display photo proxies are `DownloadedBackgroundOnly`; they do not supply close-up shelf geometry. Eight coarse foliage/debris assets retain visual-quality flags. Retention does not erase those limitations.

## Repairs

The initial audit found 61 prefabs using materials with absent source images, plus 23 unique prefabs using the importer's neutral gray `SourceDefault` for unassigned source slots. Sixteen texture-affected prefabs were removed with the unwanted components. The remaining 45 texture-affected prefabs and all 23 fallback users now use intentional replacement surfaces; no retained prefab uses `SourceDefault` or an unrepaired missing-source material slot.

Replacement wood, checked pillow fabric, brushed steel and paper textures come from the same supplied ModelsPackPSX archive. Ceramic bathroom fixtures, ceramic tableware, appliance enamel, plastic jugs/chair and aquarium glass have intentional URP Lit material properties. Plain ceramic, enamel and plastic are deliberately untextured materials, not fallback errors. Glass uses alpha blending without physical refraction. These are art repairs, not recovered original images.

Only affected material slots change. Replacement tiling textures receive projected metre-sized UVs on duplicated vertices in the affected submeshes; working original UVs and material slots are preserved. This avoids assigning an unrelated atlas over incompatible UV layouts. New repaired meshes are saved separately, while original source meshes remain intact. The Pizzeria cash register has 19 zero-area triangles removed (84 to 65); its collider uses the same repaired mesh.

The absent source filenames remain provenance facts: `Wood (4).jpg`, `Te1.jpg`, `Wood 17.jpg`, `Bur.png`, `Wood 6.jpg`, `Me4.jpg`, `Me.jpg`, `P10.jpg`, `Wood 7.jpg`, and `P12.jpg`. The catalog retains `source_missing_images` and `source_had_unassigned_material` separately from current material status.

## Sources and prefab structure

Each pack's `Original` folder preserves all downloaded formats and variants, including Blender sources and alternate DAE/GLB exports. `Source.json` records archive filename and SHA-256. No license document was supplied; the original download-page terms remain the provenance source.

The initial import accounted for 1,654 source objects: 1,141 unique prefabs, 511 exact duplicate instances, and two objects without renderable triangles. Current curation removes 161 unique prefabs and excludes their 224 original/duplicate source objects from future regeneration. The two initially empty objects are ModelsPackPSX `Cube.233` and Pizzeria scene `Decorative_Sign.035`.

Each prefab retains the source top-level object or multipart assembly, with source child relationships and transforms. Roots use identity transforms and bottom-centred placement pivots. Scale correction acts on prefab contents and their collision, preserving root identity and source pivots. Meshes are native Unity assets; prefab dependencies exclude combined FBX files and source scenes. Exact duplicate assemblies share prefabs and meshes. Static nonconvex MeshColliders use each part's rendering mesh; movable objects need suitable physics and gameplay setup. Source model collections are retained for traceability and are not placement entry points.

Available source textures use point filtering, mipmaps, appropriate alpha modes and supplied normal/emission maps. Matching supplied GLB PBR settings are authoritative over disconnected FBX socket defaults. No existing level, build configuration or gameplay system is changed by this import.

## Rebuild and verification

1. `prepare_import.py` copies byte-identical FBXs/images and extracts texture/PBR connections into `ImportManifest.json`. Original archives remain outside Assets.
2. `maintain_import.py stage` snapshots the six current pack roots and metadata into ignored `Exports/Reviews/DownloadedEnvironmentPacks/ImportProject`. Dependencies resolve from the project's local package cache. It writes `MaintenanceBaseline.json` for concurrent-change protection.
3. For a full rebuild, run the matching Unity editor in that isolated project with `-batchmode -nographics -executeMethod DownloadedEnvironmentPackImporter.RunBatch`. Approved excluded source objects are skipped, then `DownloadedPrefabMaintenance.Apply` reapplies curation, repairs and absolute target sizes from the shared scale plan. For maintenance of existing prefabs alone, use `DownloadedPrefabMaintenance.RunBatch`.
4. `DownloadedPrefabReview.RenderRepairsBatch` (without `-nographics`) verifies every retained GUID, bounds, expected triangle count, mesh, material, UV and collider, then renders all 69 repaired prefabs from opposing angles. `MaintenanceReviewInput.json` selects the repaired entries. Captures and metrics stay under ignored `Exports/Reviews/DownloadedEnvironmentPacks/MaintenanceReview`.
5. Rebuild `QualityCatalog.json` with `build_quality_catalog.py`, then run `DownloadedPrefabReview.LabelBatch`. Removed entries are omitted from native labels. The historical 1,141-entry `QualityReview/ReviewInput.json` and original render metrics are merged with repaired metrics; preserve those review inputs when reproducing the catalog.
6. `maintain_import.py promote` requires the verified maintenance receipt, rechecks saved-project references, checks every baseline file for concurrent changes, mirrors only Unity-authored modifications/deletions in the six roots and preserves all 980 retained prefab GUIDs. Deletions are performed through AssetDatabase in the isolated project before promotion. Shared/source dependencies are retained. No scene files are changed.

`BuildPrefabs.cs`, `MaintainPrefabs.cs` and `ReviewPrefabs.cs` are source recipes compiled only under the isolated project's Assets/Editor folder. No editor startup hooks, temporary runtime scripts or computer-use automation are needed. Review captures/logs stay in ignored Exports/Reviews; no website is required to use the native Unity labels.
