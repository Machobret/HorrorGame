# Cigarette pack art source

Original fictional packaging and carton mesh created for this project; no downloaded or real-brand artwork. Cream paper, burgundy ink, folded-edge wear, six-face UV atlas. Model dimensions: 60 x 25 x 90 mm. No smoking implementation.

1. Run `python ArtSource/Items/CigarettePack/build_texture.py` with Pillow installed. Optional `--deps PATH` adds a local Python dependency directory.
2. Run Blender in background with `--python ArtSource/Items/CigarettePack/render_prop.py` to generate the FBX and transparent 512px pickup render. Requires Blender 5.2; the `.blend` is also included as an editable master.
3. In Unity run **Horror Game > Items > Build Cigarette Pack** to configure point filtering, material, pickup/examination prefabs, item data and database registration.

The atlas uses six 128px panels arranged in three columns: front, right, back; left, top, bottom. The pickup icon and inventory icon share the same sprite, matching the ammunition pickup convention. The Unity builder's native interaction reference comes from the current hunting-rifle ammo pickup; it copies only interaction timing/animation and pickup audio.

Generated validation output is stored in ignored `Exports/Reviews/CigarettePack`.
