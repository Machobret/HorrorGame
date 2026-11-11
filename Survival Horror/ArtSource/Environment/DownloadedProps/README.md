# Downloaded environment props

`build_atlases.py` is the Blender recipe for the approved November 2026 batch. It reads the packed review file in the current user's Downloads and uses the original extracted sources in the temporary `CodexDownloadedAssetsReview-20261103` working folder for the separately imported surface/shrub textures. Original download archives are unchanged.

The recipe keeps geometry separate and uses existing solid furniture variants. It preserves the authored UV islands and repacks them through the installed UV-Packer engine. Intentional point/strip texture mappings use texture-region proxies; the assets are not unwrapped anew. Temporary packing topology can weld duplicated FBX vertices only when face/loop counts and every source UV corner remain unchanged. Source geometry is preserved.

### Vehicle dimensions

The GGBot `Cars_*` pack uses a uniform **0.7** correction baked into mesh coordinates by `make_copy`, after ground-centering and before atlas packing/export. It includes the standalone `Cars_Wheel`; Classic64's Volvo and car ramp use their own recipe and are unaffected. Prefab roots and the FBX importer stay at scale 1. The correction preserves proportions, pivots, UVs and topology; it does not stretch individual axes. `source_dimensions_m` and `scale_correction` record the conversion in prefab import metadata, while `dimensions_m` contains corrected dimensions (Blender X/Y/Z; Unity bounds swap Y/Z).

At unit scale, `Cars_Car` is now approximately 4.31 m long, 1.79 m wide and 1.52 m high; `Cars_Car5` is 5.16 x 1.95 x 1.43 m; `Cars_Car8` is 4.82 x 2.22 x 2.26 m. Police/taxi variants retain the same pack correction. The existing `Cars_Car8` placements in Chapter1's `APT` and `Electricity_Puzzle` have no parent or scale override and inherit the corrected size without scene edits. These visual prefabs contain no colliders.

The November 11 correction retained the existing packed atlas, increasing its effective vehicle density from about 30.43 to 43.48 px/m. A complete atlas rebuild may choose different sheet dimensions for the corrected surface areas. The external Downloads review and old import receipts are historical snapshots; the repository `ImportStatus.json` records current Unity dimensions. Do not rescale those already corrected meshes again. The comparison, geometry-preservation checks, Unity receipt and pre-change copies are under ignored `Exports/Reviews/VehiclesScale`.

Page dimensions follow category surface area and are trimmed to content. Density targets are about 32 px/m for large props and 64 px/m for smaller readable props, with actual coverage and density recorded per page. Two-pixel chart spacing and one-pixel bake dilation suit the point-filtered retro assets. Base color, opacity, emission and metallic/smoothness are baked independently. Proven constant surface values use material properties instead of unnecessary mask textures. Unity uses mipmaps and high-quality compression.

Run in Blender background mode with factory startup and disabled auto-execution. The export manifest is written to the temporary batch's `UnityImport` folder. Pass it to `HorrorGame.Environment.Editor.EnvironmentAssetImportBuilder.RunBatch` using `-assetImportManifest`. The Unity builder imports models/materials and creates individual visual prefabs; its receipt verifies bounds, references and GUIDs. Existing GUIDs are retained when regenerating.

After a successful receipt, run the Blender recipe with `-- --finalize-review`. This updates the Downloads review and its adjacent import ledger. Imported representations move to X=300 m and original source geometry stays preserved but hidden. Deferred content remains in the original viewport area.

Use `-- --pack-only` to inspect packing metrics before baking. The rejected per-face packing implementation and its recovery flags have been removed. The current batch's imported/deferred records and GUIDs are in `ImportStatus.json` here and beside the review file in Downloads.

Packing review was accepted at approximately 60% mesh UV coverage. The current 21 sheets measure approximately 63.9% area-weighted mesh UV footprint (versus 72.3% reported packing coverage, which includes texture-region proxies). Review captures remain in ignored `Exports/Reviews/DownloadedAtlasReview`; no further packing changes are pending.
