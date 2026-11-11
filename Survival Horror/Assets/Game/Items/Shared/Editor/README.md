# Item Icon Studio

Open **Horror Game > Items > Icon Studio**. Select a prefab/model asset in the Prefab field, or select one in the Project window and click **Use selected prefab**.

- Camera sliders: distance in metres, zoom, XYZ rotation. Drag the preview to orbit; scroll to change distance.
- **Frame item** centres/fits the current model; **Reset view** restores a useful initial angle.
- **Framing offset** pans the shot. **Rotate model** adjusts the model independently. Perspective and orthographic modes are supported.
- **Lighting** exposes key, fill and ambient intensity.
- Export sizes: 128, 256, 512, 1024 or 2048 square. Transparent background is enabled by default. The preview uses a checkerboard where pixels are transparent.
- **Export PNG** lets you choose an Assets location and imports the result as a single sprite, with alpha transparency and no compression/mipmaps. Pixel filtering is optional.
- To update the game item directly, select its **Item data** and enable **Assign exported icon** before exporting. Pickup prefabs prefill the Item data field when available. Assignment supports Undo and saves only that item asset.

The source prefab and current level are never instantiated as gameplay objects or modified. An isolated PreviewRenderUtility scene contains render-only copies of enabled MeshRenderer/SkinnedMeshRenderer geometry and skeletal transforms. Gameplay scripts, colliders, cameras, particles, audio, and lights from the prefab are not copied. Skinned previews use the prefab's saved skeleton pose; animation playback is not included. Camera/light/model choices survive script recompilation while the window remains open.

**Horror Game > Items > Validate Icon Studio** captures a register to ignored `Exports/Reviews/ItemIconStudio`, checks visible/transparent pixels and the source hierarchy, then exercises changed camera settings and preview cleanup.
