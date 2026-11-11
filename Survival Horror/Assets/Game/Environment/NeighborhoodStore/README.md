# Neighborhood store: checkout counter

`Prefabs/DrinksFridge.prefab` adds one stocked glass-door drinks fridge with four wire shelves, cream/green pixel textures and static collision. Place its bottom-centre pivot on the floor. It is approximately 0.82 x 2.05 x 0.78 m including the handle. Build through **Horror Game > Environment > Build Drinks Fridge**. The glass is transparent; the door/stock are scenery without interaction. Source details: `ArtSource/Environment/NeighborhoodStore/DrinksFridge.md`.

The next prop, `Prefabs/CashRegister.prefab`, is available separately for placement on the counter. It has a worn cream casing, numbered keys, receipt and green numeric display. Its bottom-centre pivot sits on the countertop; it is static scenery with collision. Build through **Horror Game > Environment > Build Cash Register**. Source/provenance: `ArtSource/Environment/NeighborhoodStore/CashRegister.md`.

This first batch contains **one asset**, `Prefabs/CheckoutCounter.prefab`. Drag it onto the floor in a scene; its root has identity rotation/scale and a bottom-centre pivot. Front faces local +Z after FBX conversion; the rear has open storage and a drawer. Approximate dimensions: 1.85 m wide, 0.95 m high, 0.72 m deep.

The worn older counter has green laminate, wooden recessed front panels, metal edging/kick plate, a cashier drawer and storage shelves. The front is unmarked. It is a static scenery prop with precise non-convex mesh collision. The register is a separate prefab; the counter itself has no interactions.

The original low-resolution wood, backing and metal textures are shared with `PSXShelvesProps`; keep that pack installed. Laminate wear and sign art were authored for this prop. All surfaces use point filtering and URP Lit materials.

Use **Horror Game > Environment > Build Checkout Counter** to rebuild, or **Validate Checkout Counter** to check references, UVs, collision and scale. Neither command places anything in existing scenes. Editable Blender source and recipes are in `ArtSource/Environment/NeighborhoodStore`; reproducible previews/validation are kept outside Assets in `Exports/Reviews/NeighborhoodStore`.
