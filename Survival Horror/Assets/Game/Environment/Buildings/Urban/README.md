# URBAN apartment exteriors

Six complete, static apartment exteriors from the supplied URBAN archive. Search
`Urban_Apartment` in Unity's Project window and place the `_Exterior` prefabs.

| Prefab subject | Source form | Width × height × depth |
| --- | --- | --- |
| LShapeMixed | Low L-shaped block with horizontal bands | 14.7 × 11.9 × 18.9 m |
| LShapeBrickLow | Low brick L-shaped block | 14.7 × 9.1 × 18.9 m |
| LShapeBrickTall | Tall brick L-shaped block | 14.7 × 20.3 × 18.9 m |
| LShapeStripedTall | Tall L-shaped block with vertical bands | 14.7 × 20.3 × 18.9 m |
| SquareBrick | Square brick block | 11.9 × 11.9 × 11.9 m |
| SquareConcrete | Square concrete block | 11.9 × 11.9 × 11.9 m |

Each root has unit scale and a ground-centred pivot. The prefabs retain source
topology and UVs, with a uniform 0.7 scale baked into the FBXs: the source has 4 m
window/storey spacing, now normalized to 2.8 m to match the ALEX exteriors. This is
independent of the House generator's ceiling/slab settings. Existing instance scale
overrides are preserved; unit-scale instances now use the corrected dimensions.
The prefabs retain source
geometry and UVs, reuse one opaque, double-sided URP Lit material and the original
houses atlas, and have static non-convex mesh colliders. Point filtering, mipmaps
and uncompressed sRGB colour match the imported retro environment assets. There
are no enterable interiors, teleport doors or generated apartment layouts; windows
and doors are the original exterior art. Source GLBs and FBXs remain in ArtSource.

`ArtSource/Environment/Urban` contains provenance, selection, conversion recipes
and a checked import receipt. The supplied archive has no license file; usage
terms and attribution remain unverified. No license from Classic64 is implied.

`Tools > Horror Game > Urban > Rebuild Apartment Prefabs` rebuilds these assets
from the normalized FBXs while retaining file GUIDs. It never opens or saves a
scene. Use the ArtSource workflow to reproduce conversion and isolated captures.
Its preserved entry points delegate to the shared `Buildings/Editor/ExteriorBuildingLibraryBuilder.cs`,
also used by the assembled ALEX library.
Review images and validation logs are under ignored `Exports/Reviews/Urban`.
