# Cigarette pack

Drag `CigarettePack_Pickup.prefab` into a scene and place its root on the floor or a shelf. The pack rests face-up; its larger, invisible interaction trigger uses the native InteractionSystem layer.

The native pickup shows `CigarettePack_PickupIcon.png` on collection and in inventory. The item can be examined and dropped. It has no use action, smoking animation, lighter requirement or puzzle effect yet. The item data can later be referenced by a puzzle.

Original fictional ASHLINE packaging: six separately textured faces, point-filtered paper atlas and URP Lit material. The icon is an independently lit render of the same model.

Rebuild through **Horror Game > Items > Build Cigarette Pack**; validate through the adjacent menu. Rebuilding preserves the item and prefab IDs. The builder appends only this item to the existing ItemDB and SpawnableSavableDB, required for native inventory persistence and dropped-item spawning. It does not change scenes.

Editable sources and generation instructions: `ArtSource/Items/CigarettePack`.

## Cigarette carton

`CigaretteCarton_Pickup.prefab` reuses the pack mesh and point-filtered ASHLINE atlas, sized to a 31 x 10 x 6 cm carton with a separate, redder paper material. Place its root on a shelf or floor. Interacting awards **one regular cigarette pack** with its normal inventory icon, examination model, and drop prefab; the carton disappears after successful collection. It stays available if inventory is full. It is a single-use pickup, not an unlimited dispenser. The carton has its own save/spawn ID. The original pack material is unchanged.

Build through **Horror Game > Items > Build Cigarette Carton**; validate through **Validate Cigarette Carton**. Rebuilds preserve the carton prefab ID. No scene is changed by the builder.

Sizing: the current pack mesh measures 6 x 9 x 2.5 cm. Five packs across and two deep require 30 x 9 x 5 cm. Adding 1 cm total allowance on each axis gives 31 x 10 x 6 cm. The builder derives this from the pack renderer bounds rather than assuming FBX units. Prefab root scale stays 1; a scaled scene parent will also scale the carton.
