# Gravel glass windows

Select **GravelGlass_WindowFamily → Clean** in the window insert selector for **Wall_01** or **Wall_02**, then choose either model. Their existing window choices remain available. The separate **GravelGlass_WallFamily** also retains this catalog and shares Wall_01 geometry; switching the wall family is not required. Explicit gravel glass reimport reconnects the catalog to all three families without replacing their other choices or editing existing levels.

The two presets use the revised Models1.blend objects, each with one shared-vertex quad for glass. Shape 01 fits a 0.65 × 1.20 m opening; shape 02 fits a 0.50 × 0.90 m opening. Both use the 1 m segment datum and also fit the engine's compatible 2 m segments. Frame and pane remain submeshes 0 and 1 of one mesh. Frames reference the existing **M_Window_Clean_01** atlas material; their UVs are inset into the upper-left atlas tile. Pane UVs cover 0..1 separately. Existing FBX and preset GUIDs are retained.

Edit **GravelGlass.mat**:

- **Glass tint** changes transmitted colour. Its alpha is unused; use Opacity instead.
- **Opacity** controls the glass effect's alpha coverage: 0 hides the glass effect; 1 fully applies its tinted, refracted view. It remains transmissive at 1 because the shader samples the background.
- **Gravel normal strength** changes surface relief, reflections and the irregular refraction. Zero removes the warp.
- **Gravel scale (repeats / metre)** sets a uniform world-space grain scale. Increase it for smaller bumps. The 16-pebble normal texture repeats once per metre at 1, giving roughly 6 cm pebbles. World projections keep the same grain size across windows, storefronts and their glass door, regardless of UVs, rotation or object scaling; adjacent panes align. Texture tiling/offset is replaced by this single control. The pattern is anchored to the world, so moving a pane changes the portion of the pattern it displays.
- **Refraction distance** sets the world displacement in metres. **Minimum refraction scale** supplies a static normal-driven scale floor at the **reference image height** (for example, 4 pixels at 180 pixels high). It does not animate the glass or displace a flat normal. Set the floor to zero for the original world-distance-only behavior. Normal strength still scales the effect, including reducing it to zero. **Smoothness** controls reflected highlights independently.

The material samples URP's opaque colour and depth textures. Depth must be available before transparents render; the current game renderer supplies it with Force Prepass. It cannot refract later transparent objects or off-screen content. Foreground depth rejection limits frame smearing. The single pane renders from both sides, with back-face normals reversed for coherent lighting/refraction. Glass does not write depth or cast shadows. Its effect is composited before the game's retro presentation; very fine patterns can disappear at that resolution. Lower alpha coverage blends warped and unwarped images.

The windows are visual inserts. They do not add a pane collider, breakability or interaction.

To rebuild: run the Blender recipe in `ArtSource/Environment/Windows/GravelGlass/export_windows.py` with Models1.blend as its source, then **Tools → Environment → Windows → Import gravel glass family**. Explicit reimport reconnects the two presets/catalog; it preserves existing material edits and asset GUIDs. Keep each FBX's two material names (`Frame_Painted`, `Glass_Clear`) and exact `OpeningMin`/`OpeningMax` markers. The source is read only; an editable normalized source snapshot and provenance are kept under ArtSource. Existing normal textures are not regenerated over artist edits. Review logs and render checks belong in `Exports/Reviews/GravelGlass`.
