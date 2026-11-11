# Railing wall family

In Edit House, select walls and assign **Sources/Wall_Railing.asset** in **Wall / fence family**.
This is a native WallFamily built from the supplied Blender object's evaluated Wireframe geometry.
It provides closed 0.5, 1 and 2 m modules, 100 mm connection thickness and the saved model's authored height.
Shorter variants crop and cap the source; the baluster spacing stays native on standard lengths.

Both sides use the existing shared **M_Metal_Rusty_04** material. The module's fixed material
slots preserve this finish instead of inheriting room plaster. Editing the shared material
updates all its consumers. **Keep Native Height** prevents vertical stacking to ceiling height;
**Cover Slab Edge** is disabled. Ordinary families keep their existing storey-height behavior.
Wall collision is a continuous barrier up to railing height, including the gaps between bars.
The family has no door/window openings and stays horizontal on the level plane.

Source, export recipe and provenance are in `ArtSource/Environment/Railings`. Run the exporter
with Blender in background mode, then **Tools > Environment > Railings > Import railing wall family**.
The import updates assets in place and retains their GUIDs. It does not assign scene walls or save scenes.
