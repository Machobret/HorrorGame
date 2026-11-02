# Downloaded environment props

`build_atlases.py` is the Blender recipe for the approved November 2026 batch. It reads the packed review file in the current user's Downloads and uses the original extracted sources in the temporary `CodexDownloadedAssetsReview-20261103` working folder for the separately imported surface/shrub textures. Original download archives are unchanged.

The recipe keeps geometry separate and uses existing solid furniture variants. It preserves the authored UV islands and repacks them through the installed UV-Packer engine. Intentional point/strip texture mappings use texture-region proxies; the assets are not unwrapped anew. Temporary packing topology can weld duplicated FBX vertices only when face/loop counts and every source UV corner remain unchanged. Source geometry is preserved.

Page dimensions follow category surface area and are trimmed to content. Density targets are about 32 px/m for large props and 64 px/m for smaller readable props, with actual coverage and density recorded per page. Two-pixel chart spacing and one-pixel bake dilation suit the point-filtered retro assets. Base color, opacity, emission and metallic/smoothness are baked independently. Proven constant surface values use material properties instead of unnecessary mask textures. Unity uses mipmaps and high-quality compression.

Run in Blender background mode with factory startup and disabled auto-execution. The export manifest is written to the temporary batch's `UnityImport` folder. Pass it to `HorrorGame.Environment.Editor.EnvironmentAssetImportBuilder.RunBatch` using `-assetImportManifest`. The Unity builder imports models/materials and creates individual visual prefabs; its receipt verifies bounds, references and GUIDs. Existing GUIDs are retained when regenerating.

After a successful receipt, run the Blender recipe with `-- --finalize-review`. This updates the Downloads review and its adjacent import ledger. Imported representations move to X=300 m and original source geometry stays preserved but hidden. Deferred content remains in the original viewport area.

Use `-- --pack-only` to inspect packing metrics before baking. The rejected per-face packing implementation and its recovery flags have been removed. The current batch's imported/deferred records and GUIDs are in `ImportStatus.json` here and beside the review file in Downloads.

Packing review was accepted at approximately 60% mesh UV coverage. The current 21 sheets measure approximately 63.9% area-weighted mesh UV footprint (versus 72.3% reported packing coverage, which includes texture-region proxies). Review captures remain in ignored `Exports/Reviews/DownloadedAtlasReview`; no further packing changes are pending.
