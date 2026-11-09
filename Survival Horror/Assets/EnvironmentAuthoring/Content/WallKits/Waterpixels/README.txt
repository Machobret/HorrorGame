Waterpixels door and window plane models

32 pictures, 48 compatible model assets: 16 doors and 16 windows in each of the
two existing window apertures. Source copies are unchanged 256x256 PNG atlases
from C:/Work/Unity/HorrorGame/WaterpixelsExport. Each contains four columns and
two rows, numbered 01..08 left-to-right, top row first. The source filenames
were made descriptive here; the source bytes are unchanged.

Source SHA-256:
Doors_Exterior_01.png 1f94d521eff844760767beb3fd338aa66572822e9c7a73e60c619601800bae69
Doors_Interior_01.png f01d2d859f579847062fffba51cd48e1143f08d4b00fdde9c851c6718140c213
Windows_01_Broken.png 63c36d66668f049a09216891e9ee08a5be97c041e36845ac6325d847fe69a38d
Windows_01_Clean.png d1a29f9c56ee5d201d875e95447308cce0e758fe25f9d5b78ac0454d2b9c4342

Use: Edit House, select a door/window opening, click Add Waterpixels door/window
models for an existing level, then choose Model. New Houses include the library.
The same models fit both 1 m and 2 m segments in Wall_01 and Wall_02. Short
window choices use the existing smaller window aperture. Opening identities,
wall finishes and segment widths are retained by the normal editing commands.

The Models folder contains WallOpeningInsert assets. Prefabs are standalone
visual examples in the same segment coordinates. Meshes are rigid X/Y planes
at Z=0, with separate front/back winding and normals: 8 vertices, 4 triangles.
Materials use URP Lit with the original atlas, opaque rendering and authored
UVs. Interior/exterior door atlases share meshes, as do clean/broken windows.
The UV rectangle covers one picture with a half-texel inset. Point filtering and
disabled mipmaps avoid bleeding from the unpadded adjacent pictures; distant
pixel aliasing is possible. The original 1:2 pictures fit the existing .9x2.1 m,
.65x1.2 m and .5x.9 m apertures, with modest aspect-ratio adjustment.

These are static pictures, including glass, boards and damage. No transparent
holes, opening animation, game rules or model collision are added. Existing
House aperture collision remains independent of the selected visual model.

Reproduce: Assets > Environment > Rebuild Waterpixels opening models. The small
recipe is Editor/WaterpixelsOpeningModels.cs in com.manta.environment-authoring.
It reads only these local source copies and existing sample aperture metadata.
Rebuild preserves asset GUIDs, material edits/model material assignments and
existing prefab edits. It restores authored mesh/UV/interface metadata. Existing
asset edits use Undo; new files remain, matching the wall-family import contract.
