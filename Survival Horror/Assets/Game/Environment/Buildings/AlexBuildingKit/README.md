# ALEX assembled building exteriors

Search `Alex_` and place the `_Exterior` prefabs. Six complete buildings are assembled
from the supplied modular kit: a red-brick walk-up, white-brick L-shaped corner,
grey-plaster courtyard, orange-plaster terrace, stepped red-plaster tower and a long
brick block. The terrace and long block have the kit's hipped metal roofs; the others
have tiled flat roofs and matching parapets. Gutters and AC units provide 3D details.

The original 2 m modules are uniformly scaled by 1.4 for 2.8 m storeys. Each completed
building has a ground-centred, unit-scale prefab root, shared opaque double-sided URP
Lit atlas materials, and static non-convex mesh colliders. Window/door art is baked into
the source wall panels. These are exterior shells, without apartment interiors or door
interaction. Window appearances vary deterministically by bay and storey, retaining
wide/narrow frame families and avoiding the same image on adjacent floors.
Original UVs and texture bytes are retained; compatible placed modules
are joined by material to reduce object and draw-call overhead. Loose modules are not
imported as runtime content.

`ArtSource/Environment/AlexBuildingKit` contains all nine original archive files,
the deterministic assembly recipe, selection, provenance and import receipt. The supplied
archive has no license/readme; usage rights and attribution requirements remain unverified.

`Tools > Horror Game > Alex Building Kit > Rebuild Building Prefabs` rebuilds the saved
prefabs from normalized FBXs, preserving GUIDs and saving no scenes. Conversion and
assembly use background Blender; Unity performs the native prefab/material import.
The shared importer is `Buildings/Editor/ExteriorBuildingLibraryBuilder.cs`.
Review captures and validation output are under ignored `Exports/Reviews/AlexBuildingKit`.
