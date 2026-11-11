# PSX shelves and store props

Drag assets from **Prefabs/Normal** or **Prefabs/Horror** into a scene. Each shelf/prop is independent; the original showroom is not required.

| Folder | Contents per version |
| --- | --- |
| Shelving | Tall, short and empty bookcases; library bay; wall shelf; pantry shelf; store shelf; wire rack; bread rack; spice rack (10) |
| Books | Book pile (1) |
| Lighting | Six ceiling fixtures with editable spotlights (6) |
| DisplayGeometry | Original showroom floor, ceiling, walls, trim, pads, labels and backing panels (31) |

There are **96 individual prefabs**: 48 Normal and 48 Horror. The 34 shelving/book/light prefabs are the main level-design assets; the 62 showroom pieces are retained separately for optional use. Stock and books on each shelf remain part of that authored shelf assembly.

- Metre scale, identity prefab transforms, useful origins at the base centre. WallShelf uses back centre, ceiling fixtures top centre. Normal/Horror counterparts share their exact origin even when the Horror version has scattered contents.
- URP Lit materials with original low-resolution textures and nearest/point filtering.
- Static non-convex mesh colliders follow the actual shelf/prop geometry. Use these as scenery, not dynamic Rigidbody objects. Showroom text labels have no collider.
- Lights have editable intensity/range and no realtime shadows by default. Horror fixtures 02, 04 and 06 have their light disabled; flicker behavior is not added.
- No pickup scripts, save IDs, item-database entries or scene placements are added.

`Models` contains individual FBXs; `Textures` contains the 127 used original images; `Materials` contains shared material assets. Prefer placing **prefabs**, which include the configured materials and collision.

To rebuild/validate, use **Horror Game > Environment > Build PSX Shelves Props** or **Validate PSX Shelves Props**. Keep existing `.meta` files when regenerating. Source/provenance and the original licence are in `ArtSource/Environment/PSXShelvesProps`. The pack's licence permits use/modification in games and prohibits standalone asset redistribution.
