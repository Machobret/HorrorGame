# Filler buildings

Open `Assets/Sandbox/FillerBuildingStreet.unity` to see the five town families at street scale. Each town family folder contains one editable prefab you can drag into a level. The brick corner apartments and ochre shop include ground-floor storefront overrides.

Choose **GameObject â†’ Environment â†’ Filler Building** or use the Environment Tools palette. Click around a perimeter and click the starting point to close it. In Select, drag a point or an edge to reshape it. Shift constrains movement in local 45-degree increments.

New footprints pick a building type at random. Change it in **Building → Building type**. The five town types share one façade render material. The mesh supplies the atlas row and bay style, including for compatible storefront and blank presets. The storey slider covers 1–10. The adjacent number accepts 1–64.

**Building** sets the storey count, preferred bay width, faÃ§ade and roof. Storeys use the house clear height (2.8 m by default) plus a 0.18 m slab allowance. Reroll changes the saved seed. Moving the building or reopening the scene preserves it.

**Floors** sets each levelâ€™s default faÃ§ade. In **Selection**, click a wall or Shift-click several, then choose **All floors** or a specific floor. A wall on a specific floor takes priority, followed by the floor default, the all-floors wall override and the building default. Clear a material field to inherit. Entrance bay 1 is the leftmost bay seen from outside. Set 0 to remove the painted entrance. These buildings have an exterior collider and decorative entrances.

Each of the first five atlas rows contains a blank wall, four glazing treatments in a single family frame, and a door. The families are brick, ochre plaster, concrete, industrial brick and sage clapboard. Regular bays pick a blank wall or window from the saved building seed. Wide storefront overrides use the square 3 × 3 storefront atlas.

The sixth row contains the five matching roofline trims and a plain metal alternative. The top 40 pixels of each tile wrap the top edge of the building. The default height is 0.32 m. The material on the top storey controls the trim, including an optional plain metal coping.

The source images and packing recipe are in ArtSource/Environment/FillerBuildings. Each cell is 256 pixels square. Door artwork retains its 181-pixel height.

Industrial Depot uses a square 1024 px atlas. Its bays are 6 m wide, with a 5 m ground-floor pitch for painted loading doors and a 2.98 m default upper-floor pitch. The depot starts with a painted loading door on the longest wall that fits a full bay. Select a wall to change or clear its entrance bay. Create a **Procedural Environment → Filler Building Type** asset and assign its façade material to add another choice to the dropdown and random creation pool.
