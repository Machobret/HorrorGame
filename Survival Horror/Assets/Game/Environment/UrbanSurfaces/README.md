# URBAN complete pack

Search `Urban t:Prefab` in Unity's Project window to browse all 446 distinct models.
The complete import adds 440 prefabs; the six existing apartment exteriors retain
their paths and GUIDs. The archive's 483 GLB/FBX pairs include 37 exact duplicates
that map to the same prefab. Colour, shape and distinct texture variants remain.

| Family | Prefabs | Location under Assets/Game/Environment |
| --- | ---: | --- |
| Buildings and modular pieces | 58 | Buildings/Urban (49 pieces in Modular) |
| Bus stops, barriers, bollards, cones, traffic lights | 73 | RoadFurniture/Urban |
| Fences and walls | 105 | Fences/Urban |
| Dumpsters, trash cans, bags and litter | 72 | StorageContainers/Urban |
| Pipes, junctions and valves | 24 | Pipes/Urban |
| Roads, bridges and sidewalks | 104 | RoadSurfaces/Urban |
| Ducts and vents | 10 | InteriorFixtures/Urban/Vents |

Dumpsters specifically are in `StorageContainers/Urban/Dumpsters` (34 prefabs).
The source-to-prefab index is `ArtSource/Environment/Urban/FullPackIndex.csv`.

This folder holds 43 new shared textures and their URP Lit materials. The existing
houses atlas/material stay in Buildings/Urban, bringing the pack to 44 distinct
textures and surfaces. Original pixel bytes, UVs, source opacity, metallic factors,
roughness and double-sided settings are retained. Four embedded versions differ
from or are absent from the loose-image folder and remain distinct. These baked
UV atlases are model finishes, not general wall-projection materials.

Prefab roots have unit scale. Building pieces and prebuilt exteriors use the
established 0.7 architecture correction; other props retain source metres.
Source display-grid offsets are removed while authored modular origins remain.
Complete buildings have ground-centred pivots and static mesh colliders. Other
new prefabs are visual assets without colliders or gameplay behavior.

Original sources and full provenance are under `ArtSource/Environment/Urban`.
Duplicate and zero-area source triangle omissions are recorded in the full
manifest. Review images and validation logs are in ignored
`Exports/Reviews/UrbanFull`. No new game-project menus or scripts are installed.
