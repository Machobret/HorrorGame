# URBAN selected apartment buildings

The runtime library is `Assets/Game/Environment/Buildings/Urban`. Six prebuilt
exteriors were selected from the supplied URBAN.zip: all four L-shaped apartment
blocks, one square brick block and one square concrete block. Other apartment
alternates, the hangar, modular building pieces and street furniture are not imported.

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
