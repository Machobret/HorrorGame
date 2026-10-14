# Approved weapon icon style

All five weapon icons use the selected horror lighting: low three-quarter view, amber key, cool blue rim, darker exposure and point-filtered 128px texture atlases. Transparent PNGs are 1024×1024. Handguns use a 30-degree tilt; long guns use 45 degrees.

Rebuild with Blender `--background --python ArtSource/Weapons/render_horror_icons.py`, then Python `ArtSource/Weapons/assemble_horror_icons.py --apply`. Run this final icon pass after any weapon/model rebuild that generates older icons. Existing PNG paths and .meta files retain inventory/pickup references. Previous PNGs are backed up here as `*_Before.png`.

The comparison sheet includes 64px previews on a dark inventory background. These renders change only UI icon images; lighting and geometry of the actual in-game guns are unchanged.
