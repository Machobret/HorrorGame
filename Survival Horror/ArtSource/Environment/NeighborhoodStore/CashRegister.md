# Cash register

Original 412-triangle fictional HARTON Model 82 register: sloped keypad with numbered keys and total key, green numeric screen, printer cover and paper receipt, cash drawer front and rubber feet. The single point-filtered 256 x 128 atlas is original hand-authored pixel art in the existing pack's subdued cream/green palette.

Run `register_textures.py` with Python/Pillow (optional `--deps PATH`), then `blender --background --python ArtSource/Environment/NeighborhoodStore/build_register.py`, then **Horror Game > Environment > Build Cash Register** in Unity. Editable source is `CashRegister.blend`; the review-only floor/lights/camera are excluded from the FBX. Previews and validation go to ignored `Exports/Reviews/NeighborhoodStore`.

Place `Assets/Game/Environment/NeighborhoodStore/Prefabs/CashRegister.prefab` on the counter. Bottom-centre pivot, metre scale, approx. 42 cm wide, 45 cm deep and 37.5 cm tall. Front/customer controls face local +Z. This is a static scenery asset with mesh collision; it does not include money, interaction, drawer animation or puzzle behavior.
