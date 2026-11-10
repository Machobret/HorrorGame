# Classic64 environment library

The complete supplied Classic64 0.6 pack is preserved under `Original`: 872 files, including 130 Blender files and 732 PNG/JPEG images. `Catalog.json` records SHA-256 hashes, authoritative runtime paths, import status and source limitations. Text documents, four `.blend1` backups and two layered art masters remain source-only.

The supplied `Original/Readme.txt` declares CC0 and credits Craig Snedeker. Its internal version label is 0.2; the folder and `Changelog v0.6.txt` identify 0.6. The author's request against reselling the library is retained in the original.

## Runtime organization

The finished library lives in the existing functional families under `Assets/Game/Environment`, using the `Classic64_` prefix. It contains 109 environment visual prefabs, including the 11 retained pilot prefabs, plus two source example assemblies under `Assets/Sandbox/Classic64/Examples`. Standalone walls, doors and windows are excluded, as are unfinished and untextured models. Complete textured `Building01` and finished props remain. All source images remain available as surface textures. Source assemblies retain separate mesh parts within their owning prefab.

All 732 supplied PNG/JPEG files are represented by runtime textures/materials. Exact-byte matches reuse authoritative pilot or library paths. Embedded Blender images can differ from loose pack images, so both versions are retained where needed. The manifest contains 733 runtime texture records and 739 textured materials, including preserved pilot materials.

Original pixel dimensions, point filtering, mipmaps and uncompressed texture settings match the pilot. Source alpha uses clipping and coverage-preserving mipmaps. No atlas or new unwrap is introduced. Thin prop materials render double-sided. These are art-only prefabs; consumers assign collision, pickups, doors and navigation behavior. Existing downloaded-prop assets and their import ledger are unchanged. Retained pilot models, materials and metadata are preserved. The pilot door was explicitly removed and its review scenes updated under the later geometry scope.

## Source conversion and limitations

`LibraryManifest.json` records every source, mesh part, material/texture dependency, bounds, pivot, baked modifier, repair and scale adjustment. The exporter evaluates authored Mirror, Solidify, Array and Curve modifiers before FBX export, retaining evaluated UVs. No authored geometry is simplified or redrawn. Transforms are baked; runtime visual prefabs use unit scale and ground-level origins.

Legacy materials are rebuilt only in memory for export. Exact unique filename recovery and six explicit rename repairs reconnect available images. A material with an obsolete missing image node and a valid packed replacement uses that packed replacement. Original Blender files are never saved or changed.

Four sources are explicitly unfinished: `generator_02`, `walkin_cooler_display`, `bike_vintage` and `car_parts_brakes`. These and the unpainted shed are excluded from runtime content. Their seven model variants and all 21 untextured materials were removed through Unity AssetDatabase. Originals remain archived. `FinishedAssetsScope.json` and its receipt record the exact exclusion; exporters reject unfinished sources and untextured materials on subsequent imports.

Recorded metre-scale choices: pilot shovel height 1.25 m; guardrail height 1 m; Volvo length 4.6 m. The excluded shed's historical export used 3 m height. The curved guardrail example uses the reusable rail's scale. Other assets retain source dimensions. Source masters retain original dimensions and transforms.

## Browsing and review

Use `Tools > Horror Game > Classic64 > Open Complete Library Review`, or open `Assets/Sandbox/Classic64/Classic64_LibraryReview.unity`. The hierarchy groups model families, pilot assets, examples and material swatches. Gallery copies are scaled for inspection; labels show runtime metres. Use runtime prefabs for placement rather than copying gallery transforms.

The library review scene is historical: at the user's request, every scene file was left unchanged during the finished-assets cleanup. It therefore retains missing prefab/material references for removed unfinished models. The pilot scene is unchanged and has no new missing references. Do not rebuild or save either scene as part of this cleanup. Runtime prefabs and the catalog are the current selection.

`Classic64_PilotReview.unity` and its menu remain available. Neither review replaces a gameplay scene. The gallery and example assemblies are outside Resources and are not enabled build scenes. Library captures temporarily use plain URP for clear inspection; saved scenes use the project pipeline. Pilot horror captures retain the game's 320x180 retro renderer.

## Reproduction and verification

Run from the project root with Blender 5.0.1 and Unity 6000.3.11f1.

1. Keep `Original`, pilot assets and receipts. `prepare_library.py` is for initial archival; rerunning it resets catalog status.
2. `prepare_validation.py` prepares `Exports/Reviews/Classic64/UnityValidation`, copying installed packages and the game's rendering dependencies. Copy the current pilot receipt and all its referenced files/metadata into this project before full validation.
3. Run Blender with `--background --factory-startup --disable-autoexec --python-exit-code 1 --python ArtSource/Environment/Classic64/export_library.py -- --audit` for diagnostics, then omit `-- --audit` for full staged export.
4. Copy both Classic64 editor builders and the pilot receipt into the isolated project. Run Unity with `-batchmode -projectPath <isolated-project> -executeMethod HorrorGame.Environment.Editor.Classic64FullLibraryBuilder.RunBatch -logFile <log>`. Omit `-nographics`: screenshots need a graphics device.
5. After `CLASSIC64_LIBRARY_VERIFIED` and visual inspection, run `python ArtSource/Environment/Classic64/promote_library.py`. It checks receipt hashes/GUIDs, preserves the pilot, copies only named Classic64 files and reconciles 842 imported source records and 20 excluded model sources. Do not promote regenerated review scenes without separate authorization to change scene files.

`LibraryImportReceipt.json` records source coverage, hashes/GUIDs, counts, limitations and checks. Validation checks source bytes, mesh parts/UVs, dimensions, material slots, dependencies, pilot preservation, regeneration GUIDs and build-scene exclusion. Rebuild menu commands retain GUIDs and offer Unity's normal save prompt before changing the active scene.

Pilot selection, manifest, receipt and recipes remain for historical reproduction. Running pilot promotion after the full import resets catalog summary text; reconcile with full promotion afterward. Logs, audits, captures and validation projects remain under ignored `Exports/Reviews/Classic64`, outside runtime folders.

## Geometry scope revision

`GeometryScope.json` and `GeometryScopeReceipt.json` record the earlier exclusion of 18 standalone geometry prefabs from 15 source files (eight doors, six windows/shutters, four wall variants). Unity AssetDatabase removed their 36 prefab/model files after dependency checks and updated both review scenes at that time. The later finished-assets scope removes `Shed01`; complete textured `Building01` remains. Surface textures and source masters remain available. Recipes reject these source categories on subsequent imports. Original gallery/pilot captures preceding these revisions are historical.
