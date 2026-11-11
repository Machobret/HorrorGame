# URBAN environment library

The complete supplied pack is imported: 483 GLB/FBX model pairs map to 446 distinct
prefabs. Thirty-seven exact duplicate archive entries share their authoritative
prefab. The six previously imported apartment exteriors retain their paths and GUIDs;
the complete import adds 440 prefabs, 43 textures and 43 materials.

`FullPackIndex.csv` maps every archive model to its Unity prefab. `FullImportManifest.json`
records sources, exact aliases, native destinations, texture hashes, material slots,
scales and source geometry notes. `FullImportReceipt.json` records verified asset
GUIDs and hashes. Shared surfaces and the runtime navigation guide live in
`Assets/Game/Environment/UrbanSurfaces`. Search `Urban t:Prefab` in Unity.

Runtime families contain 58 building models (49 modular pieces and nine prebuilt
exteriors), 73 road-furniture models, 105 fence/wall variants, 72 bins/litter models,
24 pipe models, 104 road/bridge/sidewalk models and 10 vents. Exact duplicates are
shared; distinct embedded texture variants remain separate. All 40 loose textures
and four additional embedded versions are retained byte-for-byte in the runtime
texture collection, including the existing houses atlas.

Source display-grid translations are removed. Modular pieces retain their authored
origins; prebuilt buildings are ground-centred. Architecture uses the established
0.7 correction to match the existing 2.8 m storey pitch. Other props retain source
metres. Complete buildings have static mesh colliders; other new prefabs are visual
assets without gameplay scripts or colliders. Engine omissions of duplicate and
zero-area source triangles are independently counted in the manifest. No visible
surface is simplified. The complete import installs no editor menus or scripts.

Original canonical GLB/FBX pairs, five editable Blender masters and loose images
are preserved under `Original/Pack`; embedded-only image versions are under
`Original/EmbeddedTextures`. Review contact sheets and one-time conversion/import
tools remain in ignored `Exports/Reviews/UrbanFull`.

## Original six-apartment workflow

The following retained workflow applies only to the original six `_Exterior`
prefabs. It does not rebuild the full pack. `Selection.json` and `ImportReceipt.json`
retain this narrower contract; the `FullImport*` files describe the complete import.

Original GLB and FBX files for the selection, and the shared houses atlas, are kept
under `Original`. `Provenance.json` records the archive and source status. The archive
does not include license/attribution documentation; permission and attribution remain
unverified. No Classic64 licensing statement applies to this pack.

`Selection.json` records source SHA-256 hashes, runtime paths, original placement,
ground-pivot correction, metre bounds, source triangle counts and UV corner counts.
The GLBs provide the conversion input because their images are embedded and can be
matched byte-for-byte to the supplied atlas. Blender bakes only placement transforms;
source topology, UV coordinates and materials are preserved. The source's measured
4 m storey pitch is uniformly scaled by 0.7 to match the accepted 2.8 m ALEX pitch.
This normalization is baked into mesh coordinates while prefab roots remain unit scale.
Unity receives normalized
FBXs, without embedded textures or Blender dependencies.

From the project root, reproduce with Blender 5.0.1 and Unity 6000.3.11f1:

1. `python ArtSource/Environment/Urban/urban_library.py --archive <URBAN.zip>` preserves
   the selected sources, records scene hashes, and prepares the cached isolated project
   at `Exports/Reviews/Classic64/UnityValidation`. This project must already have the
   game's URP packages/settings; it is shared with the earlier asset validation workflow.
2. Run Blender with `--background --factory-startup --disable-autoexec --python-exit-code 1
   --python ArtSource/Environment/Urban/export_apartments.py` to export the selected FBXs.
3. In the isolated project run Unity with `-batchmode -executeMethod
   HorrorGame.Environment.Editor.UrbanApartmentLibraryBuilder.RunBatch -logFile <review-log>`.
   A graphics device is required for captures. The method exits after verification/rendering.
4. Inspect `Exports/Reviews/Urban` in the isolated project and the staged import receipt.
   Run `python ArtSource/Environment/Urban/urban_library.py --promote` only after validation.

Promotion copies only the fourteen checked model/prefab/material/texture assets and
their metadata, preserving existing GUIDs and scene files. Build code and README live
alongside the runtime family. Existing unrelated edits are never staged or committed by
this workflow. The normal rebuild menu regenerates prefab assets only and saves no scenes.

The existing Urban menu and batch entry points now delegate to the shared
`Assets/Game/Environment/Buildings/Editor/ExteriorBuildingLibraryBuilder.cs`, also used
by the ALEX building kit. Both scripts are copied when preparing the isolated project.
