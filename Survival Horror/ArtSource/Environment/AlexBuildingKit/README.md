# ALEX modular building assembly

All nine files supplied in `modular_building_kit_by_ALEX.zip` are archived unchanged
under `Original`. Provenance records source hashes and the filename credit. The archive
has no license/readme; usage rights and attribution requirements remain unverified.

The GLB contains 188 authored module objects using six embedded atlases. Each embedded
atlas is checked byte-for-byte against the supplied PNG. `AssemblyBlueprints.json`
records all six deterministic floor footprints, storey counts, roof choices, seeds and
module usage. The kit's 2 m grid is uniformly scaled by 1.4 to 2.8 m storeys. No wall
textures are repainted or UVs regenerated. Original source placement offsets are discarded.

Only complete assembled exterior buildings are runtime assets, under
`Assets/Game/Environment/Buildings/AlexBuildingKit`. Source modules remain source-only.
Placed geometry is joined by material, preserving UV corners and source triangle counts.
Each native prefab has static matching mesh colliders and a ground-centred unit root.
The shared Unity builder also serves the existing URBAN library, through preserved entry points.

Rebuild from the project root with Blender 5.0.1 and Unity 6000.3.11f1:

1. `python ArtSource/Environment/AlexBuildingKit/building_library.py --archive <kit.zip>`
   prepares sources and the existing cached isolated Unity validation project.
2. Run Blender with `--background --factory-startup --disable-autoexec --python-exit-code 1
   --python ArtSource/Environment/AlexBuildingKit/assemble_buildings.py`.
3. Run isolated Unity with `-batchmode -executeMethod
   HorrorGame.Environment.Editor.AlexBuildingLibraryBuilder.RunBatch -logFile <review-log>`.
   A graphics device is required for captures; this method exits when done.
4. Inspect the seven captures and checked receipt in the isolated project, then run
   `python ArtSource/Environment/AlexBuildingKit/building_library.py --promote`.

The isolated project is `Exports/Reviews/Classic64/UnityValidation`, reusing its installed
URP packages. Its temporary capture renderer is never promoted. Promotion checks native
asset hashes, GUIDs and dependencies; preserves all existing URBAN runtime bytes and current
scene files; and copies only the selected completed buildings, six materials and six atlases.
Review output belongs in ignored `Exports/Reviews/AlexBuildingKit`. No scene is saved,
opened or regenerated in the main project.
