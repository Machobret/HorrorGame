# Cigarette pack

Drag `CigarettePack_Pickup.prefab` into a scene and place its root on the floor or a shelf. The pack rests face-up; its larger, invisible interaction trigger uses the native InteractionSystem layer.

The native pickup shows `CigarettePack_PickupIcon.png` on collection and in inventory. The item can be examined and dropped. It has no use action, smoking animation, lighter requirement or puzzle effect yet. The item data can later be referenced by a puzzle.

Original fictional ASHLINE packaging: six separately textured faces, point-filtered paper atlas and URP Lit material. The icon is an independently lit render of the same model.

Rebuild through **Horror Game > Items > Build Cigarette Pack**; validate through the adjacent menu. Rebuilding preserves the item and prefab IDs. The builder appends only this item to the existing ItemDB and SpawnableSavableDB, required for native inventory persistence and dropped-item spawning. It does not change scenes.

Editable sources and generation instructions: `ArtSource/Items/CigarettePack`.
