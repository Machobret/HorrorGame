# One older neighborhood-shop counter

Scope: a single worn counter, created after the user narrowed the requested store set to one prop. No additional store asset families or variants are generated.

Original geometry and laminate/sign texture art. The wood, metal and backing use the imported **PSX Bookshelves & Food Shelves** pack's dirty oak, dirty steel-grey and dirty backing textures to match its pixel density and palette. See `../PSXShelvesProps/Original_LICENSE.txt` for those shared textures' licence. They are referenced in place, not duplicated. Font: the project's existing ExaltFallUI-Bold.

Rebuild from project root:

```text
python ArtSource/Environment/NeighborhoodStore/build_textures.py
blender --background --python ArtSource/Environment/NeighborhoodStore/build_checkout.py
```

Python requires Pillow (`--deps PATH` is available for a local dependency directory). Blender 5.2 was used. Run **Horror Game > Environment > Build Checkout Counter** in Unity to assign URP materials and create the single placement prefab. Keep existing `.meta` files when rebuilding.

`CheckoutCounter.blend` is the editable source with review-only floor/lights/camera; the FBX exports only the actual counter mesh. Source part names document the panels, drawer, shelves and trim before joining. All parts of this static prop are combined into one four-material low-poly mesh (568 triangles). The front plaque and its mounting screws have been removed. The open storage geometry is preserved by the mesh collider. The recipe produces front and rear renders in ignored `Exports/Reviews/NeighborhoodStore`.
