# Rolled glass windows

On a plain wall, the **Window** palette offers **TimberWindowWide** (0.65 × 1.20 m), **TimberWindowNarrow** (0.50 × 1.20 m), and **TimberWindowWithSill** (1.20 × 0.5427 m, including the sill). Dimensions are the full frame width and height. A complex family needs a surround with the matching aperture. The shared material also supplies the storefront and its glass door.

Edit **RolledGlass.mat**:

- **Glass tint** changes transmitted colour. Its alpha is unused; use Opacity instead.
- **Opacity** controls the glass effect's alpha coverage: 0 hides the glass effect; 1 fully applies its tinted, refracted view. It remains transmissive at 1 because the shader samples the background.
- **Glass normal strength** changes surface relief, reflections and the irregular refraction. Zero removes the warp.
- **Glass pattern scale (repeats / metre)** sets a uniform world-space grain scale. Increase it for smaller bumps. At 1, the normal texture repeats once per metre and the surface pattern is about 6 cm across. World projections keep the same grain size across windows, storefronts and their glass door, regardless of UVs, rotation or object scaling; adjacent panes align. Texture tiling/offset is replaced by this single control. The pattern is anchored to the world, so moving a pane changes the portion of the pattern it displays.
- **Refraction distance** sets the world displacement in metres. **Minimum refraction scale** supplies a static normal-driven scale floor at the **reference image height** (for example, 4 pixels at 180 pixels high). It does not animate the glass or displace a flat normal. Set the floor to zero for the original world-distance-only behavior. Normal strength still scales the effect, including reducing it to zero. **Smoothness** controls reflected highlights independently.

The material samples URP's opaque colour and depth textures. Depth must be available before transparents render; the current game renderer supplies it with Force Prepass. It cannot refract later transparent objects or off-screen content. Foreground depth rejection limits frame smearing. The single pane renders from both sides, with back-face normals reversed for coherent lighting/refraction. Glass does not write depth or cast shadows. Its effect is composited before the game's retro presentation; very fine patterns can disappear at that resolution. Lower alpha coverage blends warped and unwarped images.

Edit the three timber-framed models in the **Windows** collection of ArtSource/Environment/Storefront/House Family Templates.blend. Export each from its bottom-centre origin. The three prefabs use the authored timber UVs and share **TimberFrame** and **RolledGlass**. Earlier segment-coordinate FBXs are still referenced by baked level geometry.
