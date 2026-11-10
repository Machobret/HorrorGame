# Weathered brick

Assign **WeatheredBrick.mat** to a House/level's **Exterior wall finish**, a room finish, or an ordinary wall-side override. It is also labelled **Construction** for the House Properties material browser. The two supplied brick images are one material's clean and weathered layers, with identical texture coordinates.

The default **Wall projection** mapping preserves physical brick size across scaled, repeated and cropped wall pieces, using the engine's existing horizontal wall/corner mapping. Reveals and caps keep the source's authored UVs. **Box projection** is available for general masonry objects; **Triplanar** softens transitions between projection planes and may soften brick lines at corners. **Authored UV** retains the model's own texture coordinates.

- **Brick tile width/height (metres)** controls the physical size of the complete texture tile, initially 2 x 2.8 m. This does not set weathering size.
- **Weathering patch size (metres)** changes the scale of the broad noise field, initially 6 m. Larger values make broader patches.
- **Weathering amount** adjusts coverage: 0 is exactly clean; 1 is exactly weathered.
- **Blend softness** widens transitions, without changing the texture tile size.
- **Weathering pattern offset** chooses another region of the static noise field; the same material stays aligned across all its objects.
- **Tint**, separate clean/weathered **roughness**, and an optional shared **normal map/strength** control the finish. The supplied files contain base colour only; no normal map is inferred from their dirt or baked lighting.

Weathering uses absolute world positions and two frequencies of smooth 3D noise. It is static, appears at all heights, and crosses wall-module boundaries. Moving a building through the world changes which part of the stationary pattern it displays. Coverage is artistic rather than an exact percentage. The shader supports URP lighting, shadows, fog, depth/depth normals, lightmaps/probes and a matching material-baking Meta pass. It shares `Assets/EnvironmentAuthoring/Runtime/HorizontalWallMapping.hlsl` with the wall engine.

The original 256 x 256 PNGs and their provenance are in `ArtSource/Environment/WeatheredBrick`. Runtime textures retain their original pixels, with sRGB colour, Repeat wrapping, point filtering, mipmaps, no compression, and no downscaling. Import settings keep the project's pixel texture style; shader noise blends smoothly between those pixels. Review captures and verification belong in ignored `Exports/Reviews/WeatheredBrick`.

The supplied colour images contain darkening near a tile edge. That existing band repeats with the brick texture, including at Weathering amount 0; it is separate from the continuous procedural weathering mask. The shader does not remove or repaint features baked into the source images.
