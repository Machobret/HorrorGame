# Drinks fridge

One original single-door stocked drinks fridge, continuing the neighborhood-shop set. Cream enamel, faded green trim, lower compressor vent, wire shelves, pull handle and lightly tinted glass. Each shelf has one row of nine bottles at the back, 36 bottles total, leaving the front empty. Single 256 x 128 original pixel atlas; 5,162 triangles across an opaque body/stock mesh and a separate glass mesh. Stock remains combined scenery with the existing shared material.

Place `Assets/Game/Environment/NeighborhoodStore/Prefabs/DrinksFridge.prefab` on the floor. Bottom-centre pivot, approx. 0.82 m wide, 2.05 m tall and 0.78 m deep including handle. Static mesh collision; door, stock and refrigeration have no gameplay behavior. Glass uses a dedicated transparent URP material and casts no opaque shadow.

Rebuild with `fridge_textures.py` in Python/Pillow (optional `--deps PATH`), then `blender --background --python ArtSource/Environment/NeighborhoodStore/build_fridge.py`, then **Horror Game > Environment > Build Drinks Fridge** in Unity. `DrinksFridge.blend` is the editable source; review floor/lights/camera are excluded from export. Previews and validation live in ignored `Exports/Reviews/NeighborhoodStore`.
